using System;
using System.Reflection;
using System.Collections.Generic;
using Lidgren.Network;

namespace MultiplayerSFS.Common.Packets
{
    public static class PacketUtils
    {
        static readonly Dictionary<string, DeserializerReflectionInfo> deserializerLookup = new Dictionary<string, DeserializerReflectionInfo>();

        private class DeserializerReflectionInfo
        {
            readonly ConstructorInfo constructorMethod;
            readonly MethodInfo deserializerMethod;

            public DeserializerReflectionInfo(Type type)
            {
                constructorMethod = type.GetConstructor(Type.EmptyTypes);
                deserializerMethod = type.GetMethod("Deserialize");
            }

            public IPacket Deserialize(NetIncomingMessage msg)
            {
                IPacket packet = (IPacket) constructorMethod.Invoke(null);
                deserializerMethod.Invoke(packet, new object[] { msg });
                return packet;
            }
        }

        public static void SerializePacketToMessage(this NetOutgoingMessage msg, IPacket packet)
        {
            msg.Write(packet.GetType().FullName);
            packet.Serialize(msg);
        }

        public static IPacket DeserializeMessageToPacket(this NetIncomingMessage msg)
        {
            string packetType = msg.ReadString();
            try
            {
                if (!deserializerLookup.ContainsKey(packetType))
                    deserializerLookup.Add(packetType, new DeserializerReflectionInfo(Type.GetType(packetType)));
                return deserializerLookup[packetType].Deserialize(msg);
            }
            catch (Exception e)
            {
                throw new Exception("PacketUtils.DeserializeMessageToPacket(): Reflection on '" + packetType + "' encountered an error! ", e);
            }
        }
    }
}