using System.Net;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Lidgren.Network;
using SFS.UI;
using SFS.World;
using SFS.WorldBase;
using static SFS.Base;
using MultiplayerSFS.Mod.GUI;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Mod.Networking
{
    public static class NetworkingManager
    {
        public static NetClient client;
        public static Thread listenerThread;
        public static ClientStateManager stateManager;

        public static async Task<(bool, string)> TryConnect(JoinInfo joinInfo, CancellationToken cancel)
        {
            NetPeerConfiguration config = new NetPeerConfiguration("MultiplayerSFS");
            config.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            // config.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
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

            Task<(bool, string)> responseTask = Task.Run(() =>
            {
                try
                {
                    System.Diagnostics.Stopwatch timeout = new System.Diagnostics.Stopwatch();
                    timeout.Start();

                    while (true)
                    {
                        cancel.ThrowIfCancellationRequested();

                        NetIncomingMessage responseMessage;
                        if ((responseMessage = client.ReadMessage()) != null)
                        {
                            if (responseMessage.MessageType == NetIncomingMessageType.StatusChanged)
                            {
                                NetConnectionStatus status = (NetConnectionStatus)responseMessage.ReadByte();
                                if (status == NetConnectionStatus.Disconnected)
                                {
                                    if (!responseMessage.ReadString(out string reason))
                                        reason = "Blocked by server...";
                                    client.Disconnect("");
                                    return (false, reason);
                                }
                                else if (status == NetConnectionStatus.Connected)
                                {
                                    client.Recycle(responseMessage);
                                    return (true, "Connection approved!");
                                }
                            }
                            client.Recycle(responseMessage);
                        }

                        if (timeout.ElapsedMilliseconds >= 5000)
                        {
                            throw new System.Exception("Client timed out.");
                        }
                    }
                }
                catch (System.OperationCanceledException e)
                {
                    client.Disconnect("");
                    throw e;
                }
                catch (System.Exception e)
                {
                    client.Disconnect("");
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

        public static async Task LoadWorld()
        {
            Menu.loading.Open("Waiting for world data...");

            // * World save is sent immediately after connection approval.
            Task<LoadWorldPacket> responseTask = Task.Run(() =>
            {
                System.Diagnostics.Stopwatch timeout = new System.Diagnostics.Stopwatch();
                timeout.Start();
                try
                {
                    while (true)
                    {
                        NetIncomingMessage msg;
                        if ((msg = client.ReadMessage()) != null)
                        {
                            if (msg.MessageType == NetIncomingMessageType.Data)
                            {
                                if (msg.DeserializeMessageToPacket() is LoadWorldPacket packet)
                                {
                                    return packet;
                                }
                            }
                            else if (msg.MessageType == NetIncomingMessageType.StatusChanged)
                            {
                                NetConnectionStatus status = (NetConnectionStatus)msg.ReadByte();
                                Debug.Log(status);
                                if (status == NetConnectionStatus.Disconnected)
                                {
                                    throw new System.Exception("Client was disconnected whilst waiting for world data.");
                                }
                            }
                            else
                            {
                                Debug.Log(msg.MessageType);
                            }
                        }

                        if (timeout.ElapsedMilliseconds >= 5000)
                        {
                            throw new System.Exception("Client timed out.");
                        }
                    }
                }
                catch (System.Exception e)
                {
                    throw new System.Exception("NetworkingManager.WaitForWorldData(): Encountered an exception!", e);
                }
            });

            while (!responseTask.IsCompleted)
            {
                await Task.Yield();
            }
            Menu.loading.Close();

            if (responseTask.Exception is System.AggregateException ae)
            {
                MsgDrawer.main.Log("An error occured... (Check console)");
                client.Disconnect("RecvWorldError");
                Debug.Log(ae.InnerException);
            }
            else
            {
                try
                {
                    LoadWorldPacket packet = responseTask.Result;

                    WorldSettings settings = new WorldSettings(
                        new SolarSystemReference(""),
                        new Difficulty() { difficulty = packet.difficulty },
                        new WorldMode(WorldMode.Mode.Sandbox) { allowQuicksaves = false },
                        new WorldPlaytime(),
                        new SandboxSettings.Data()
                    );

                    typeof(WorldBaseManager).GetMethod("EnterWorld", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(
                        worldBase,
                        new object[]
                        {
                            null,
                            settings,
                            (System.Action) sceneLoader.LoadHubScene
                        }
                    );

                    stateManager = new ClientStateManager(packet);

                    listenerThread = new Thread(Listen);
                    listenerThread.Start();
                }
                catch (System.Exception e)
                {
                    MsgDrawer.main.Log("An error occured... (Check console)");
                    client.Disconnect("LoadWorldError");
                    Debug.Log(e);
                }
            }
        }

        public static void Listen()
        {
            // TODO
        }

        public static void Shutdown()
        {
            stateManager = null;
            listenerThread?.Abort();
            client.Disconnect("PlayerDisconnect");
        }
    }
}