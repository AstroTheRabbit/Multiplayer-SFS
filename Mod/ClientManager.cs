using System;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Lidgren.Network;
using SFS;
using SFS.UI;
using SFS.World;
using SFS.WorldBase;
using MultiplayerSFS.Common;
using SFS.Variables;

namespace MultiplayerSFS.Mod
{
    public static class ClientManager
    {
        public static Bool_Local multiplayerEnabled = new Bool_Local() { Value = false };
        public static NetClient client;
        public static WorldState world;
        public static int playerId;

        public static async Task TryConnect(JoinInfo info)
        {
            if (client != null && client.Status != NetPeerStatus.NotRunning)
                client.Shutdown("Re-attempting join request");

            NetPeerConfiguration npc = new NetPeerConfiguration("multiplayersfs")
            {
                ConnectionTimeout = 10,
            };
            npc.EnableMessageType(NetIncomingMessageType.StatusChanged);
			npc.EnableMessageType(NetIncomingMessageType.UnconnectedData);
			npc.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);
			// npc.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
            
            client = new NetClient(npc);
            client.Start();

            NetOutgoingMessage hail = client.CreateMessage();
            hail.Write
            (
                new Packet_JoinRequest()
                {
                    Username = info.username,
                    Password = info.password
                }
            );
            client.Connect(new IPEndPoint(info.address, info.port), hail);

            Menu.loading.Open("Waiting for server response...");
            string denialReason = "Unable to connect to server...";
            while (true)
            {
                NetIncomingMessage msg;
                while ((msg = client.ReadMessage()) == null)
                {
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

            LocalManager.Initialize();
            
            Packet_JoinResponse response = client.ServerConnection.RemoteHailMessage.Read<Packet_JoinResponse>();
            playerId = response.PlayerId;
            
            world = new WorldState
            {
                worldTime = response.WorldTime,
                difficulty = response.Difficulty,
            };

            WorldSettings settings = new WorldSettings
            (
                new SolarSystemReference(""),
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
                // Debug.Log("Recieved message...");
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
                    case NetIncomingMessageType.StatusChanged:
                        NetConnectionStatus status = (NetConnectionStatus) msg.ReadByte();
                        if (status == NetConnectionStatus.Disconnected)
                        {
                            SceneLoader.ExitToMainMenu();
                            client.Shutdown("Disconnected by server.");
                        }
                        break;
                    default:
                        Debug.LogWarning($"Unhandled message type ({msg.MessageType})!");
                        break;
                }
                client.Recycle(msg);
            }
        }

        public static void HandlePacket(NetIncomingMessage msg)
        {
            PacketType packetType = (PacketType) msg.ReadByte();
            Debug.Log($"Recieved packet of type {packetType}.");
            switch (packetType)
            {
                // * Player/server Info Packets
                case PacketType.PlayerConnected:
                    OnPacket_PlayerConnected(msg);
                    break;
                case PacketType.PlayerDisconnected:
                    OnPacket_PlayerDisconnected(msg);
                    break;
                case PacketType.UpdatePlayerControl:
                    OnPacket_UpdatePlayerControl(msg);
                    break;
                case PacketType.UpdatePlayerAuthority:
                    OnPacket_UpdatePlayerAuthority(msg);
                    break;

                // * Rocket Packets
                case PacketType.CreateRocket:
                    OnPacket_CreateRocket(msg);
                    break;
                case PacketType.DestroyRocket:
                    OnPacket_DestroyRocket(msg);
                    break;
                case PacketType.UpdateRocket:
                    OnPacket_UpdateRocket(msg);
                    break;

                // * Part packets
                case PacketType.UpdatePart:
                    OnPacket_UpdatePart(msg);
                    break;
                case PacketType.DestroyPart:
                    OnPacket_DestroyPart(msg);
                    break;
                case PacketType.UpdateStaging:
                    // ! TODO
                    OnPacket_UpdateStaging(msg);
                    break;
                
                // * Invalid Packets
                case PacketType.JoinResponse:
                    Debug.LogWarning($"Recieved server info packet outside of connection attempt.");
                    break;
                case PacketType.JoinRequest:
                    Debug.LogWarning($"Recieved packet (of type {packetType}) intended for the server.");
                    break;
                default:
                    Debug.LogWarning($"Unhandled packet type ({packetType})!");
                    break;
            }
        }

        public static void SendPacket(Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
        {
            // Debug.Log($"Sending packet of type {packet.Type}.");
            NetOutgoingMessage msg = client.CreateMessage();
            msg.Write((byte) packet.Type);
            msg.Write(packet);
            client.SendMessage(msg, method);
        }

        static void OnPacket_PlayerConnected(NetIncomingMessage msg)
        {
            Packet_PlayerConnected packet = msg.Read<Packet_PlayerConnected>();
            LocalManager.players.Add(packet.Id, new LocalPlayer(packet.Username));
            if (packet.PrintMessage)
            {
                MsgDrawer.main.Log($"'{packet.Username}' connected");
            }
        }

        static void OnPacket_PlayerDisconnected(NetIncomingMessage msg)
        {
            Packet_PlayerDisconnected packet = msg.Read<Packet_PlayerDisconnected>();
            if (LocalManager.players.TryGetValue(packet.Id, out LocalPlayer player))
            {
                MsgDrawer.main.Log($"'{player.username}' disconnected");
                LocalManager.players.Remove(packet.Id);
            }
        }

        static void OnPacket_UpdatePlayerControl(NetIncomingMessage msg)
        {
            Packet_UpdatePlayerControl packet = msg.Read<Packet_UpdatePlayerControl>();
            if (LocalManager.players.TryGetValue(packet.PlayerId, out LocalPlayer player))
            {
                player.currentRocket.Value = packet.RocketId;
            }
            else
            {
                Debug.LogError("Missing player while trying to update controlled rocket!");
            }

        }
        static void OnPacket_UpdatePlayerAuthority(NetIncomingMessage msg)
        {
            Packet_UpdatePlayerAuthority packet = msg.Read<Packet_UpdatePlayerAuthority>();
            LocalManager.updateAuthority = packet.RocketIds;
        }

        static void OnPacket_CreateRocket(NetIncomingMessage msg)
        {
            Packet_CreateRocket packet = msg.Read<Packet_CreateRocket>();
            world.rockets[packet.GlobalId] = packet.Rocket;
            LocalManager.CreateRocket(packet);
        }

        static void OnPacket_DestroyRocket(NetIncomingMessage msg)
        {
            Packet_DestroyRocket packet = msg.Read<Packet_DestroyRocket>();
            LocalManager.DestroyRocket(packet.Id);
        }

        static void OnPacket_UpdateRocket(NetIncomingMessage msg)
        {
            Packet_UpdateRocket packet = msg.Read<Packet_UpdateRocket>();
            if (world.rockets.TryGetValue(packet.Id, out RocketState state))
            {
                state.UpdateRocket(packet);
                LocalManager.UpdateLocalRocket(packet);
            }
            else
            {
                Debug.LogError($"Missing rocket from world state!");
            }
        }

        static void OnPacket_UpdatePart(NetIncomingMessage msg)
        {
            Packet_UpdatePart packet = msg.Read<Packet_UpdatePart>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
            {
                state.UpdatePart(packet.PartId, packet.NewPart);
                LocalManager.UpdateLocalPart(packet);
            }
            else
            {
                Debug.LogError($"Missing rocket from world state!");
            }
        }

        static void OnPacket_DestroyPart(NetIncomingMessage msg)
        {
            Packet_DestroyPart packet = msg.Read<Packet_DestroyPart>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
            {
                state.RemovePart(packet.PartId);
                LocalManager.DestroyLocalPart(packet);
            }
        }

        static void OnPacket_UpdateStaging(NetIncomingMessage msg)
        {
            Packet_UpdateStaging packet = msg.Read<Packet_UpdateStaging>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
            {
                state.stages = packet.Stages;
                LocalManager.UpdateLocalStaging(packet);
            }
        }
    }
}