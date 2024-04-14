using System.Net;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
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

        public static async Task<JoinResponsePacket> TryConnect(JoinInfo joinInfo, CancellationToken cancel)
        {
            NetPeerConfiguration config = new NetPeerConfiguration("MultiplayerSFS");
            Debug.Log(config.Port);
            config.EnableMessageType(NetIncomingMessageType.Error);
            config.EnableMessageType(NetIncomingMessageType.StatusChanged);
            config.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            config.EnableMessageType(NetIncomingMessageType.Data);
            config.EnableMessageType(NetIncomingMessageType.Receipt);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            config.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);
            config.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);
            config.EnableMessageType(NetIncomingMessageType.DebugMessage);
            config.EnableMessageType(NetIncomingMessageType.WarningMessage);
            config.EnableMessageType(NetIncomingMessageType.ErrorMessage);
            config.EnableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            client = new NetClient(config);
            client.Start();

            NetOutgoingMessage hailMessage = client.CreateMessage();
            hailMessage.SerializePacketToMessage(
                new JoinRequestPacket
                {
                    username = joinInfo.username,
                    password = joinInfo.password,
                }
            );
            client.Connect(new IPEndPoint(joinInfo.ipAddress, joinInfo.port), hailMessage);

            Task<JoinResponsePacket> responseTask = Task.Run(() =>
            {
                try
                {
                    while (true)
                    {
                        Debug.Log(client.ConnectionStatus);
                        cancel.ThrowIfCancellationRequested();

                        NetIncomingMessage responseMessage;
                        if ((responseMessage = client.ReadMessage()) != null)
                        {
                            if (responseMessage.MessageType == NetIncomingMessageType.Data)
                            {
                                if (responseMessage.DeserializeMessageToPacket() is JoinResponsePacket joinResponse)
                                {
                                    client.Recycle(responseMessage);
                                    return joinResponse;
                                }
                                else
                                {
                                    throw new System.Exception("Recieved packet was not a JoinResponsePacket.");
                                }
                            }
                            else
                            {
                                if (responseMessage.MessageType == NetIncomingMessageType.StatusChanged)
                                {
                                    if ((NetConnectionStatus) responseMessage.ReadByte() == NetConnectionStatus.Disconnected)
                                    {
                                        throw new System.Exception("Client was disconnected from the server.");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (System.OperationCanceledException e)
                {
                    throw e;
                }
                catch (System.Exception e)
                {
                    throw new System.Exception("NetworkingManager.TryConnect(): Encountered an exception whilst waiting for a server response!", e);
                }
            });

            do
            {
                MsgDrawer.main.Log("Waiting for server response...");
                await Task.Yield();
            }
            while (!responseTask.IsCompleted);

            return responseTask.Result;
        }

        public static void Listen()
        {
            // TODO
        }
    }
}