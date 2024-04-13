using System;
using System.Collections.Generic;
using System.Reflection;
using Lidgren.Network;

namespace MultiplayerSFS.Common.Packets
{
    public static class PacketUtils
    {
        static readonly Dictionary<string, DeserializerReflectionInfo> deserializerLookup = new Dictionary<string, DeserializerReflectionInfo>();

        private class DeserializerReflectionInfo
        {
            readonly Type packetType;
            readonly MethodInfo deserializerMethod;

            public DeserializerReflectionInfo(Type type)
            {
                packetType = type;
                deserializerMethod = type.GetMethod("Deserialize", BindingFlags.Public);
            }

            public IPacket Deserialize(NetIncomingMessage msg)
            {
                IPacket packet = (IPacket) Activator.CreateInstance(packetType);
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
                {
                    deserializerLookup.Add(packetType, new DeserializerReflectionInfo(Type.GetType(packetType)));
                }
                return deserializerLookup[packetType].Deserialize(msg);
            }
            catch (Exception e)
            {
                throw new Exception("PacketUtils.DeserializePacket(): Reflection error! ", e);
            }
        }
    }
}