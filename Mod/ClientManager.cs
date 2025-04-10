using System;
using System.Net;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Lidgren.Network;
using SFS;
using SFS.UI;
using SFS.Parts;
using SFS.World;
using SFS.Variables;
using SFS.WorldBase;
using SFS.Parts.Modules;
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
            // TODO: ping readout UI
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
            if (packetType != PacketType.UpdateRocket)
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
            if (packet.Type != PacketType.UpdateRocket)
                Debug.Log($"Sending packet of type {packet.Type}.");
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

        static void OnPacket_UpdateWorldTime(NetIncomingMessage msg)
        {
            Packet_UpdateWorldTime packet = msg.Read<Packet_UpdateWorldTime>();
            if (WorldTime.main != null)
            {
                WorldTime.main.worldTime = packet.WorldTime;
            }
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
            world.rockets.Remove(packet.Id);
            LocalManager.TrueDestructionReason = packet.Reason;
            LocalManager.DestroyLocalRocket(packet.Id);
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

        static void OnPacket_DestroyPart(NetIncomingMessage msg)
        {
            Packet_DestroyPart packet = msg.Read<Packet_DestroyPart>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
            {
                state.RemovePart(packet.PartId);
                LocalManager.TrueDestructionReason = packet.Reason;
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

        static void OnPacket_UpdatePart_EngineModule(NetIncomingMessage msg)
        {
            Packet_UpdatePart_EngineModule packet = msg.Read<Packet_UpdatePart_EngineModule>();
            if (world.rockets.TryGetValue(packet.RocketId, out RocketState rocketState))
            {
                if (rocketState.parts.TryGetValue(packet.PartId, out PartState partState))
				{
					partState.part.TOGGLE_VARIABLES["engine_on"] = packet.EngineOn;
				}
                
                if (LocalManager.syncedRockets.TryGetValue(packet.RocketId, out LocalRocket rocket))
                {
                    if (rocket.parts.TryGetValue(packet.PartId, out Part part))
                    {
                        EngineModule[] modules = part.GetModules<EngineModule>();
                        if (modules.Length > 1)
                        {
                            Debug.LogWarning($"OnPacket_UpdatePart_EngineModule: Found multiple engine modules on part \"{part.Name}\".");
                        }
                        modules[0].engineOn.Value = packet.EngineOn;
                    }
                }
            }
            else
            {
                Debug.LogError($"Missing rocket from world state!");
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
                
                if (LocalManager.syncedRockets.TryGetValue(packet.RocketId, out LocalRocket rocket))
                {
                    if (rocket.parts.TryGetValue(packet.PartId, out Part part))
                    {
                        WheelModule[] modules = part.GetModules<WheelModule>();
                        if (modules.Length > 1)
                        {
                            Debug.LogWarning($"OnPacket_UpdatePart_WheelModule: Found multiple wheel modules on part \"{part.Name}\".");
                        }
                        modules[0].on.Value = packet.WheelOn;
                    }
                }
            }
            else
            {
                Debug.LogError($"Missing rocket from world state!");
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
                
                if (LocalManager.syncedRockets.TryGetValue(packet.RocketId, out LocalRocket rocket))
                {
                    if (rocket.parts.TryGetValue(packet.PartId, out Part part))
                    {
                        BoosterModule[] modules = part.GetModules<BoosterModule>();
                        if (modules.Length > 1)
                        {
                            Debug.LogWarning($"OnPacket_UpdatePart_BoosterModule: Found multiple booster modules on part \"{part.Name}\".");
                        }
                        modules[0].boosterPrimed.Value = packet.Primed;
                        modules[0].throttle_Out.Value = packet.Throttle;
                        modules[0].fuelPercent.Value = packet.FuelPercent;
                    }
                }
            }
            else
            {
                Debug.LogError($"Missing rocket from world state!");
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
                
                if (LocalManager.syncedRockets.TryGetValue(packet.RocketId, out LocalRocket rocket))
                {
                    if (rocket.parts.TryGetValue(packet.PartId, out Part part))
                    {
                        ParachuteModule[] modules = part.GetModules<ParachuteModule>();
                        if (modules.Length > 1)
                        {
                            Debug.LogWarning($"OnPacket_UpdatePart_ParachuteModule: Found multiple parachute modules on part \"{part.Name}\".");
                        }
                        modules[0].state.Value = packet.State;
                        modules[0].targetState.Value = packet.TargetState;
                    }
                }
            }
            else
            {
                Debug.LogError($"Missing rocket from world state!");
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
                
                if (LocalManager.syncedRockets.TryGetValue(packet.RocketId, out LocalRocket rocket))
                {
                    if (rocket.parts.TryGetValue(packet.PartId, out Part part))
                    {
                        MoveModule[] modules = part.GetModules<MoveModule>();
                        if (modules.Length > 1)
                        {
                            Debug.LogWarning($"OnPacket_UpdatePart_MoveModule: Found multiple move modules on part \"{part.Name}\".");
                        }
                        modules[0].time.Value = packet.Time;
                        modules[0].targetTime.Value = packet.TargetTime;
                    }
                }
            }
            else
            {
                Debug.LogError($"Missing rocket from world state!");
            }
        }
    }
}