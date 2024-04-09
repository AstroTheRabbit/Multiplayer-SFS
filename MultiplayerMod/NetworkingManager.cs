using System.Net.Sockets;
using System.Threading.Tasks;
using MultiplayerSFS.Mod.GUI;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Mod.Networking
{
    public static class NetworkingManager
    {
        public static TcpClient client;

        public static async Task<JoinResponsePacket> TryConnect(JoinInfo joinInfo)
        {
            client = new TcpClient();
            client.Connect(joinInfo.ipAddress, joinInfo.port);
            JoinRequestPacket joinPacket = new JoinRequestPacket
            {
                Username = joinInfo.username,
                Password = joinInfo.password,
            };
            await client.GetStream().SendPacketAsync(joinPacket);
            if (await client.GetStream().RecievePacketAsync() is JoinResponsePacket joinResponse)
            {
                return joinResponse;
            }
            else
            {
                throw new System.Exception("TryConnect: Recieved packet was not a JoinResponsePacket");
            }
        }
    }
}