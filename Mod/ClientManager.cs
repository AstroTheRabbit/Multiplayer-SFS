using System;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine; 
using Lidgren.Network;
using SFS;
using SFS.UI;
using SFS.Parts;
using SFS.World;
using SFS.WorldBase;
using MultiplayerSFS.Common;
using MultiplayerSFS.Mod.GUI;
using MultiplayerSFS.Common.Networking;

namespace MultiplayerSFS.Mod.Networking
{
    public static class ClientManager
    {
        public static Thread thread;
        public static NetClient client;

        public static WorldState world;
        public static Dictionary<int, Rocket> localRockets;
        public static Dictionary<int, Part> localParts;

        public static async Task TryConnect(JoinInfo info, CancellationToken token)
        {
            if (client != null && client.Status != NetPeerStatus.NotRunning)
                client.Shutdown("Reattempting join request.");

            NetPeerConfiguration npc = new NetPeerConfiguration("multiplayersfs");
			npc.EnableMessageType(NetIncomingMessageType.StatusChanged);
			npc.EnableMessageType(NetIncomingMessageType.UnconnectedData);
			npc.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            
            client = new NetClient(npc);
            client.Start();

            NetOutgoingMessage hail = client.CreateMessage();
            hail.Write
            (
                new Client_JoinRequest()
                {
                    Username = info.username,
                    Password = info.password
                }
            );
            client.Connect(new IPEndPoint(info.address, info.port), hail);

            Menu.loading.Open("Waiting for server response...");
            string denialReason = "Unknown disconnect error...";
            while (true)
            {
                NetIncomingMessage msg;
                while ((msg = client.ReadMessage()) == null)
                {
                    token.ThrowIfCancellationRequested();
                    await Task.Yield();
                }

                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.Error:
                        Debug.LogError("Lidgren Error: Corrupted packet!");
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        Debug.LogError($"Lidgren Error: \"{msg.ReadString()}\".");
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        Debug.LogWarning($"Lidgren Warning: \"{msg.ReadString()}\".");
                        break;
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.VerboseDebugMessage:
                        Debug.Log($"Lidgren Debug: \"{msg.ReadString()}\".");
                        break;
                    case NetIncomingMessageType.StatusChanged:
                        NetConnectionStatus status = (NetConnectionStatus) msg.ReadByte();
                        if (status == NetConnectionStatus.Connected)
                            goto ConnectionApproved;
                        else if (status == NetConnectionStatus.Disconnected)
                            goto ConnectionDenied;
                        break;
                    default:
                        Debug.LogWarning($"Recieved unhandled message type ({msg.MessageType}) when attempting to connect to server.");
                        break;
                }
            }

            ConnectionApproved:
                Menu.loading.Close();
                client.RegisterReceivedCallback(new SendOrPostCallback(Listen));
                LoadWorld();
                return;
            
            ConnectionDenied:
                Menu.loading.Close();
                MsgDrawer.main.Log(denialReason);
                return;
        }

        public static void LoadWorld()
        {
            Menu.loading.Open("Loading multiplayer world...");

            ClearLocals();
            Server_ServerInfo serverInfo = client.ServerConnection.RemoteHailMessage.Read<Server_ServerInfo>();
            world = new WorldState
            {
                worldTime = serverInfo.WorldTime,
                difficulty = serverInfo.Difficulty,
                rockets = serverInfo.Rockets,
            };

            WorldSettings settings = new WorldSettings
            (
                new SolarSystemReference(null),
                new Difficulty() { difficulty = world.difficulty },
                new WorldMode(WorldMode.Mode.Sandbox) { allowQuicksaves = false },
                new WorldPlaytime(),
                new SandboxSettings.Data()
            );

            typeof(WorldBaseManager).GetMethod("EnterWorld", BindingFlags.NonPublic | BindingFlags.Instance).Invoke
            (
                Base.worldBase,
                new object[]
                {
                    null,
                    settings,
                    (Action) Base.sceneLoader.LoadHubScene
                }
            );
        }

        public static void Listen(object peer)
        {
            NetIncomingMessage msg;
            while ((msg = client.ReadMessage()) != null)
            {
                Debug.Log($"Recieved a message!!! Type is {msg.MessageType}.");
                switch (msg.MessageType)
                {
                    case NetIncomingMessageType.Error:
                        Debug.LogError("Lidgren Error: Corrupted packet!");
                        break;
                    case NetIncomingMessageType.ErrorMessage:
                        Debug.LogError($"Lidgren Error: \"{msg.ReadString()}\".");
                        break;
                    case NetIncomingMessageType.WarningMessage:
                        Debug.LogWarning($"Lidgren Warning: \"{msg.ReadString()}\".");
                        break;
                    case NetIncomingMessageType.DebugMessage:
                    case NetIncomingMessageType.VerboseDebugMessage:
                        Debug.Log($"Lidgren Debug: \"{msg.ReadString()}\".");
                        break;
                    case NetIncomingMessageType.Data:
                        HandlePacket(msg);
                        break;
                    default:
                        Debug.LogWarning($"Unhandled message type ({msg.MessageType})!");
                        break;
                }
            }
        }

        public static void HandlePacket(NetIncomingMessage msg)
        {
            // ! TODO
            PacketType packetType = (PacketType) msg.ReadByte();
            switch (packetType)
            {
                default:
                    Debug.LogWarning($"Unhandled message type ({packetType})!");
                    break;
            }
        }

        public static void ClearLocals()
        {
            localRockets = new Dictionary<int, Rocket>();
        }

        public static void Disconnect()
        {
            client.Shutdown("Intentional disconnect.");
        }

        public static void SpawnRockets()
        {
            throw new NotImplementedException();
        }
    }
}