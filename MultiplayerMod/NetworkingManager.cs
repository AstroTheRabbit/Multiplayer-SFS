using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Lidgren.Network;
using SFS.UI;
using MultiplayerSFS.Mod.GUI;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Mod.Networking
{
    public static class NetworkingManager
    {
        public static NetClient client;
        public static Thread listenerThread;

        public static async Task<JoinResponsePacket> TryConnect(JoinInfo joinInfo)
        {
            client = new NetClient(new NetPeerConfiguration("MultiplayerSFS.Mod"));
            NetOutgoingMessage hailMessage = client.CreateMessage();
            hailMessage.SerializePacketToMessage(
                new JoinRequestPacket
                {
                    Username = joinInfo.username,
                    Password = joinInfo.password,
                }
            );
            client.Connect(new IPEndPoint(joinInfo.ipAddress, joinInfo.port), hailMessage);

            NetIncomingMessage responseMessage;
            while ((responseMessage = client.ReadMessage()) == null)
            {
                MsgDrawer.main.Log("Waiting for server response...");
                await Task.Yield();
            }

            if (responseMessage.DeserializeMessageToPacket() is JoinResponsePacket joinResponse)
            {
                if (joinResponse.Response != JoinResponsePacket.JoinResponse.AccessGranted)
                {
                    client.Disconnect("");
                }

                return joinResponse;
            }
            else
            {
                throw new System.Exception("NetworkingManager.TryConnect(): Recieved packet was not a JoinResponsePacket.");
            }
        }

        public static void Listen()
        {
            // TODO
        }
    }
}