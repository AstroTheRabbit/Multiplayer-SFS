using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using Google.Protobuf;
using System.Reflection;
using Google.Protobuf.Reflection;
using System;

namespace MultiplayerSFS.Common.Packets
{
    public static class PacketUtils
    {
        public static async Task SendPacketAsync(this NetworkStream stream, IMessage packet)
        {
            byte[] packetType = Encoding.UTF8.GetBytes(packet.GetType().AssemblyQualifiedName);
            int packetTypeSize = packetType.Length;
            int packetSize = packet.CalculateSize();

            await stream.WriteAsync(BitConverter.GetBytes(packetTypeSize), 0, 4);
            await stream.WriteAsync(BitConverter.GetBytes(packetSize), 0, 4);

            await stream.WriteAsync(packetType, 0, packetTypeSize);
            await stream.WriteAsync(packet.ToByteArray(), 0, packetSize);

            await stream.FlushAsync();
        }

        public static async Task<IMessage> RecievePacketAsync(this NetworkStream stream)
        {
            // TODO: May have to add while loops to ensure all bytes are read.

            byte[] packetSizeBuffer = new byte[8];
            if (await stream.ReadAsync(packetSizeBuffer, 0, 8) < 8)
            {
                throw new Exception("RecievePacketAsync: Missing bytes for packet size!");
            }

            int packetTypeSize = BitConverter.ToInt32(packetSizeBuffer, 0);
            int packetSize = BitConverter.ToInt32(packetSizeBuffer, 4);
            int totalPacketSize = packetTypeSize + packetSize;

            byte[] packetBuffer = new byte[totalPacketSize];
            if (await stream.ReadAsync(packetBuffer, 0, totalPacketSize) < totalPacketSize)
            {
                throw new Exception("RecievePacketAsync: Missing bytes for packet!");
            }

            try
            {
                Type packetType = Type.GetType(Encoding.UTF8.GetString(packetBuffer, 0, packetTypeSize));
                MessageDescriptor descriptor = (MessageDescriptor) packetType.GetProperty("Descriptor", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                return descriptor.Parser.ParseFrom(packetBuffer, packetTypeSize, packetSize);
            }
            catch (Exception e)
            {
                throw new Exception("RecievePacketAsync: Reflection error! ", e);
            }
        }
    }
}