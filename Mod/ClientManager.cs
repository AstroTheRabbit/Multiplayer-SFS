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
using SFS.Variables;
using SFS.WorldBase;
using MultiplayerSFS.Common;

namespace MultiplayerSFS.Mod
{
    public static class ClientManager
    {
        public static Bool_Local multiplayerEnabled = new Bool_Local() { Value = false };
        public static NetClient client;
        public static WorldState world;
        /// <summary>
        /// Id of the local player.
        /// </summary>
        public static int playerId;

        public static async Task TryConnect(JoinInfo info)
        {
            if (client != null && client.Status != NetPeerStatus.NotRunning)
                client.Shutdown("Re-attempting join request");

            NetPeerConfiguration npc = new NetPeerConfiguration("multiplayersfs")
            {
                ConnectionTimeout = 5,
            };
            npc.EnableMessageType(NetIncomingMessageType.StatusChanged);
			npc.EnableMessageType(NetIncomingMessageType.UnconnectedData);
			npc.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);
            // TODO: ping readout UI?
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
            
            Packet_JoinResponse response = client.ServerConnection.RemoteHailMessage.Read<Packet_JoinResponse>();
            playerId = response.PlayerId;
            
            LocalManager.updateRocketsPeriod = response.UpdateRocketsPeriod;
            LocalManager.Initialize();

            ChatWindow.CreateCooldownTimer(response.ChatMessageCooldown);
            
            world = new WorldState()
            {
                initWorldTime = response.WorldTime,
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
            if (Packet.ShouldDebug(packetType))
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
                case PacketType.UpdateWorldTime:
                    OnPacket_UpdateWorldTime(msg);
                    break;
                case PacketType.UpdatePlayerColor:
                    OnPacket_UpdatePlayerColor(msg);
                    break;
                case PacketType.SendChatMessage:
                    OnPacket_SendChatMessage(msg);
                    break;

                // * Rocket Packets
                case PacketType.CreateRocket:
                    OnPacket_CreateRocket(msg);
                    break;
                case PacketType.DestroyRocket:
                    OnPacket_DestroyRocket(msg);
                    break;
                case PacketType.UpdateRocketPrimary:
                    OnPacket_UpdateRocketPrimary(msg);
                    break;
                case PacketType.UpdateRocketSecondary:
                    OnPacket_UpdateRocketSecondary(msg);
                    break;

                // * Part & Staging Packets
                case PacketType.DestroyPart:
                    OnPacket_DestroyPart(msg);
                    break;
                case PacketType.UpdateStaging:
                    OnPacket_UpdateStaging(msg);
                    break;
                case PacketType.UpdatePart_EngineModule:
                    OnPacket_UpdatePart_EngineModule(msg);
                    break;
                case PacketType.UpdatePart_WheelModule:
                    OnPacket_UpdatePart_WheelModule(msg);
                    break;
                case PacketType.UpdatePart_BoosterModule:
                    OnPacket_UpdatePart_BoosterModule(msg);
                    break;
                case PacketType.UpdatePart_ParachuteModule:
                    OnPacket_UpdatePart_ParachuteModule(msg);
                    break;
                case PacketType.UpdatePart_MoveModule:
                    OnPacket_UpdatePart_MoveModule(msg);
                    break;
                case PacketType.UpdatePart_ResourceModule:
                    OnPacket_UpdatePart_ResourceModule(msg);
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
            if (Packet.ShouldDebug(packet.Type))
                Debug.Log($"Sending packet of type {packet.Type}.");

            NetOutgoingMessage msg = client.CreateMessage();
            msg.Write((byte) packet.Type);
            msg.Write(packet);
            client.SendMessage(msg, method);
        }

        static void OnPacket_PlayerConnected(NetIncomingMessage msg)
        {
            Packet_PlayerConnected packet = msg.Read<Packet_PlayerConnected>();
            LocalManager.players.Add(packet.PlayerId, new LocalPlayer(packet.Username, packet.IconColor));
            if (packet.PrintMessage)
            {
                string message = $"{packet.Username} connected";
                MsgDrawer.main.Log(message);
                ChatWindow.AddMessage(new ChatMessage(message));
            }
        }

        static void OnPacket_PlayerDisconnected(NetIncomingMessage msg)
        {
            Packet_PlayerDisconnected packet = msg.Read<Packet_PlayerDisconnected>();
            if (LocalManager.players.TryGetValue(packet.PlayerId, out LocalPlayer player))
            {
                string message = $"{player.username} disconnected";
                MsgDrawer.main.Log(message);
                ChatWindow.AddMessage(new ChatMessage(message));
                LocalManager.players.Remove(packet.PlayerId);
            }
        }

        static void OnPacket_UpdatePlayerControl(NetIncomingMessage msg)
        {
            Packet_UpdatePlayerControl packet = msg.Read<Packet_UpdatePlayerControl>();
            if (LocalManager.players.TryGetValue(packet.PlayerId, out LocalPlayer player))
            {
                player.controlledRocket.Value = packet.RocketId;
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

            foreach (int id in LocalManager.updateAuthority)
            {
                if (LocalManager.syncedRockets.TryGetValue(id, out LocalRocket rocket))
                {

                }
            }
        }

        static void OnPacket_UpdateWorldTime(NetIncomingMessage msg)
        {
            Packet_UpdateWorldTime packet = msg.Read<Packet_UpdateWorldTime>();
            if (WorldTime.main != null)
            {
                WorldTime.main.worldTime = world.WorldTime = packet.WorldTime;
            }
        }
        
        static void OnPacket_UpdatePlayerColor(NetIncomingMessage msg)
        {
            Packet_UpdatePlayerColor packet = msg.Read<Packet_UpdatePlayerColor>();
            if (LocalManager.players.TryGetValue(packet.PlayerId, out LocalPlayer player))
            {
                player.iconColor = packet.Color;
                ChatWindow.OnPlayerColorChange(packet.PlayerId, packet.Color);
            }
        }

        static void OnPacket_SendChatMessage(NetIncomingMessage msg)
        {
            Packet_SendChatMessage packet = msg.Read<Packet_SendChatMessage>();
            ChatWindow.AddMessage(new ChatMessage(packet.Message, packet.SenderId));
        }

        static void OnPacket_CreateRocket(NetIncomingMessage msg)
        {
            Packet_CreateRocket packet = msg.Read<Packet_CreateRocket>();
            world.rockets[packet.GlobalId] = packet.Rocket;
            LocalManager.OnPacket_CreateRocket(packet);
        }

        static void OnPacket_DestroyRocket(NetIncomingMessage msg)
        {
            Packet_DestroyRocket packet = msg.Read<Packet_DestroyRocket>();
            world.rockets.Remove(packet.RocketId);
            LocalManager.TrueDestructionReason = packet.Reason;
            LocalManager.DestroyLocalRocket(packet.RocketId);
        }

        static void OnPacket_UpdateRocketPrimary(NetIncomingMessage msg)
        {
            Packet_UpdateRocketPrimary packet = msg.Read<Packet_UpdateRocketPrimary>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
            {
                state.UpdateRocketPrimary(packet);
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
                // Debug.Log($"Update rocket!!! {} => {}");
            }
            else
            {
                Debug.Log("Missing rocket from world state!!!");
            }
        }

        static void OnPacket_UpdateRocketSecondary(NetIncomingMessage msg)
        {
            Packet_UpdateRocketSecondary packet = msg.Read<Packet_UpdateRocketSecondary>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
            {
                state.UpdateRocketSecondary(packet);
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
            }
        }

        static void OnPacket_DestroyPart(NetIncomingMessage msg)
        {
            Packet_DestroyPart packet = msg.Read<Packet_DestroyPart>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
            {
                state.RemovePart(packet.PartId);
                Interpolator.AddPacketToQueue(packet, packet.PartId, packet.WorldTime);
            }
        }

        static void OnPacket_UpdateStaging(NetIncomingMessage msg)
        {
            Packet_UpdateStaging packet = msg.Read<Packet_UpdateStaging>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
            {
                state.stages = packet.Stages;
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
            }
        }

        static void OnPacket_UpdatePart_EngineModule(NetIncomingMessage msg)
        {
            Packet_UpdatePart_EngineModule packet = msg.Read<Packet_UpdatePart_EngineModule>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState rocketState))
            {
                if (rocketState.parts.TryGetValue(packet.PartId, out PartState partState))
				{
					partState.part.TOGGLE_VARIABLES["engine_on"] = packet.EngineOn;
				}
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
            }
        }

        static void OnPacket_UpdatePart_WheelModule(NetIncomingMessage msg)
        {
            Packet_UpdatePart_WheelModule packet = msg.Read<Packet_UpdatePart_WheelModule>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState rocketState))
            {
                if (rocketState.parts.TryGetValue(packet.PartId, out PartState partState))
				{
					partState.part.TOGGLE_VARIABLES["wheel_on"] = packet.WheelOn;
				}
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
            }
        }

        static void OnPacket_UpdatePart_BoosterModule(NetIncomingMessage msg)
        {
            Packet_UpdatePart_BoosterModule packet = msg.Read<Packet_UpdatePart_BoosterModule>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState rocketState))
            {
                if (rocketState.parts.TryGetValue(packet.PartId, out PartState partState))
				{
					partState.part.NUMBER_VARIABLES["fuel_percent"] = packet.FuelPercent;
				}
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
            }
        }

        static void OnPacket_UpdatePart_ParachuteModule(NetIncomingMessage msg)
        {
            Packet_UpdatePart_ParachuteModule packet = msg.Read<Packet_UpdatePart_ParachuteModule>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState rocketState))
            {
                if (rocketState.parts.TryGetValue(packet.PartId, out PartState partState))
				{
					partState.part.NUMBER_VARIABLES["animation_state"] = packet.State;
					partState.part.NUMBER_VARIABLES["deploy_state"] = packet.TargetState;
				}
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
            }
        }

        static void OnPacket_UpdatePart_MoveModule(NetIncomingMessage msg)
        {
            Packet_UpdatePart_MoveModule packet = msg.Read<Packet_UpdatePart_MoveModule>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState rocketState))
            {
                if (rocketState.parts.TryGetValue(packet.PartId, out PartState partState))
				{
					partState.part.NUMBER_VARIABLES["state"] = packet.Time;
					partState.part.NUMBER_VARIABLES["state_target"] = packet.TargetTime;
				}
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
            }
        }

        static void OnPacket_UpdatePart_ResourceModule(NetIncomingMessage msg)
        {
            Packet_UpdatePart_ResourceModule packet = msg.Read<Packet_UpdatePart_ResourceModule>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState rocketState))
            {
                foreach (int partId in packet.PartIds)
                {
                    if (rocketState.parts.TryGetValue(partId, out PartState partState))
                    {
                        // TODO! A lot of these save variable names will most likely be different for non-vanilla parts, but currently idk what the best way to properly get them is.
                        // TODO! I might need some form of register that associates a part's name and module variable names to their save variable names.
                        partState.part.NUMBER_VARIABLES["fuel_percent"] = packet.ResourcePercent;
                    }
                }
                Interpolator.AddPacketToQueue(packet, packet.RocketId, packet.WorldTime);
            }
        }
    }
}