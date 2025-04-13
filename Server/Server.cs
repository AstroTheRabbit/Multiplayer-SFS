using System;
using System.Net;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using UnityEngine;
using Lidgren.Network;
using MultiplayerSFS.Common;

namespace MultiplayerSFS.Server
{
    public static class Server
	{
		public static NetServer server;
		public static ServerSettings settings;
		public static WorldState world;
		public static Dictionary<IPEndPoint, ConnectedPlayer> connectedPlayers;

		public static void Initialize(ServerSettings settings)
		{
			Server.settings = settings;
            NetPeerConfiguration npc = new NetPeerConfiguration("multiplayersfs")
            {
                Port = settings.port,
				MaximumConnections = settings.maxConnections,
            };
			npc.EnableMessageType(NetIncomingMessageType.StatusChanged);
			npc.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
			npc.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);
			npc.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);

			world = new WorldState(settings.worldSavePath);
			connectedPlayers = new Dictionary<IPEndPoint, ConnectedPlayer>();

            server = new NetServer(npc);
			server.Start();
		}

		public static void Run()
		{
			try
			{
				Logger.Info($"Multiplayer SFS server started, listening for connections on port {server.Port}...", true);
				Stopwatch worldTimer = Stopwatch.StartNew();
				
				while (true)
				{
					if (Listen())
                    {
                        UpdatePlayerAuthorities();
                    }

                    if (connectedPlayers.Values.Any(p => p.controlledRocket >= 0))
					{
						world.worldTime += worldTimer.Elapsed.TotalSeconds;
					}
					worldTimer.Restart();
				}
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		}

		/// <summary>
		/// Returns `true` if a refresh of the players' update authorities is required.
		/// </summary>
		static bool Listen()
		{
			NetIncomingMessage msg;
			bool requiresRefresh = false;
			while ((msg = server.ReadMessage()) != null)
			{
				switch (msg.MessageType)
				{
					case NetIncomingMessageType.StatusChanged:
						requiresRefresh |= OnStatusChanged(msg);
						break;
					case NetIncomingMessageType.ConnectionApproval:
						OnPlayerConnectionAttempt(msg);
						break;
					case NetIncomingMessageType.ConnectionLatencyUpdated:
						OnLatencyUpdated(msg);
						break;
					case NetIncomingMessageType.Data:
						requiresRefresh |= OnIncomingPacket(msg);
						break;

					case NetIncomingMessageType.DebugMessage:
					case NetIncomingMessageType.VerboseDebugMessage:
						Logger.Info($"Lidgren Debug - \"{msg.ReadString()}\".", true);
						break;
					case NetIncomingMessageType.WarningMessage:
						Logger.Warning($"Lidgren Warning - \"{msg.ReadString()}\".");
						break;
					case NetIncomingMessageType.ErrorMessage:
						Logger.Error($"Lidgren Error - \"{msg.ReadString()}\".");
						break;
					default:
						Logger.Warning($"Unhandled message type: {msg.MessageType} - {msg.DeliveryMethod} - {msg.LengthBytes} bytes.");
						break;
				}
				server.Recycle(msg);
			}
			return requiresRefresh;
		}

		static ConnectedPlayer FindPlayer(NetConnection connection)
		{
			if (connectedPlayers.TryGetValue(connection.RemoteEndPoint, out ConnectedPlayer res))
				return res;
			return null;
		}

		static string FormatUsername(this string username)
		{
            return string.IsNullOrWhiteSpace(username) ? "???" : $"'{username}'";
        }

		static void SendPacketToPlayer(NetConnection connection, Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
		{
			// Logger.Debug($"Sending packet of type '{packet.Type}'.");
			NetOutgoingMessage msg = server.CreateMessage();
			msg.Write((byte) packet.Type);
			msg.Write(packet);
			server.SendMessage(msg, connection, method);
		}

		static void SendPacketToAll(Packet packet, NetConnection except = null, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
		{
			// Logger.Debug($"Sending packet of type '{packet.Type}' to all.");
			NetOutgoingMessage msg = server.CreateMessage();
			msg.Write((byte) packet.Type);
			msg.Write(packet);
			server.SendToAll(msg, except, method, 0);
		}

		/// <summary>
		/// Returns `true` if a refresh of the players' update authorities is required.
		/// </summary>
		static bool OnStatusChanged(NetIncomingMessage msg)
		{
			NetConnectionStatus status = (NetConnectionStatus) msg.ReadByte();
			string reason = msg.ReadString();
			string playerName = FindPlayer(msg.SenderConnection)?.username.FormatUsername();
			Logger.Info($"Status of {playerName} @ {msg.SenderEndPoint} changed to {status} - \"{reason}\".");

			switch (status)
			{
				case NetConnectionStatus.Disconnected:
					OnPlayerDisconnect(msg.SenderConnection);
					return true;
				case NetConnectionStatus.Connected:
					OnPlayerSuccessfulConnect(msg.SenderConnection);
					return false;
				default:
					return false;
			}
		}

        static void OnPlayerConnectionAttempt(NetIncomingMessage msg)
		{
			Packet_JoinRequest request = msg.SenderConnection.RemoteHailMessage.Read<Packet_JoinRequest>();
            NetConnection connection = msg.SenderConnection;
			Logger.Info($"Recieved join request from {request.Username.FormatUsername()} @ {connection.RemoteEndPoint}.", true);

			string reason = "Connection approved!";
			if (connectedPlayers.Count >= settings.maxConnections && settings.maxConnections != 0)
			{
				reason = $"Server is full ({connectedPlayers.Count}/{settings.maxConnections}).";
				goto ConnectionDenied;
			}
			if (string.IsNullOrWhiteSpace(request.Username))
			{
				reason = $"Username cannot be empty";
				goto ConnectionDenied;
			}
			if (settings.blockDuplicatePlayerNames && connectedPlayers.Values.Select(player => player.username).Contains(request.Username))
			{
				reason = $"Username '{request.Username}' is already in use";
				goto ConnectionDenied;
			}
			if (request.Password != settings.password && settings.password != "")
			{
				reason = $"Invalid password";
				goto ConnectionDenied;
			}

			Logger.Info($"Approved join request, sending world info...", true);
			
            ConnectedPlayer newPlayer = new ConnectedPlayer(request.Username);
			connectedPlayers.Add(connection.RemoteEndPoint, newPlayer);

			NetOutgoingMessage hail = server.CreateMessage();
			hail.Write
			(
				new Packet_JoinResponse()
				{
					PlayerId = newPlayer.id,
					UpdateRocketsPeriod = settings.updateRocketsPeriod,
					ChatMessageCooldown = settings.chatMessageCooldown,
					WorldTime = world.worldTime,
					Difficulty = world.difficulty,
				}
			);
			connection.Approve(hail);
			return;

			ConnectionDenied:
				Logger.Info($"Denied join request - {reason}", true);
				connection.Deny(reason);
		}

		static void OnPlayerSuccessfulConnect(NetConnection connection)
		{
			ConnectedPlayer player = FindPlayer(connection);
			if (player == null)
			{
				Logger.Warning("Missing new player while sending join response!");
				return;
			}

			SendPacketToAll
			(
				new Packet_PlayerConnected()
				{
					Id = player.id,
					Username = player.username,
					PrintMessage = true,
				},
				connection
			);
			foreach (KeyValuePair<int, RocketState> kvp in world.rockets)
			{
				SendPacketToPlayer
				(
					connection,
					new Packet_CreateRocket()
					{
						GlobalId = kvp.Key,
						Rocket = kvp.Value,
					}
				);
			}
			foreach (KeyValuePair<IPEndPoint, ConnectedPlayer> kvp in connectedPlayers)
			{
				SendPacketToPlayer
				(
					connection,
					new Packet_PlayerConnected()
					{
						Id = kvp.Value.id,
						Username = kvp.Value.username,
						IconColor = kvp.Value.iconColor,
						PrintMessage = false,
					}
				);
				SendPacketToPlayer
				(
					connection,
					new Packet_UpdatePlayerControl()
					{
						PlayerId = kvp.Value.id,
						RocketId = kvp.Value.controlledRocket,
					}
				);
			}
		}

		static void OnPlayerDisconnect(NetConnection connection)
        {
			ConnectedPlayer player = FindPlayer(connection);
            if (player != null)
			{
				SendPacketToAll(new Packet_PlayerDisconnected() { Id = player.id });
				connectedPlayers.Remove(connection.RemoteEndPoint);
			}
        }
		
		static void OnLatencyUpdated(NetIncomingMessage msg)
		{
			if (FindPlayer(msg.SenderConnection) is ConnectedPlayer player)
			{
				string username = player.username.FormatUsername();
				player.avgTripTime = msg.SenderConnection.AverageRoundtripTime;
				Logger.Info($"Average roundtrip time updated for {username} @ {msg.SenderEndPoint} - {1000 * player.avgTripTime}ms.");

				SendPacketToPlayer
				(
					msg.SenderConnection,
					new Packet_UpdateWorldTime()
					{
						WorldTime = world.worldTime + (player.avgTripTime / 2),
					}
				);
			}
		}

		static void UpdatePlayerAuthorities()
		{
			foreach (ConnectedPlayer player in connectedPlayers.Values)
			{
				player.updateAuthority.Clear();
			}

			// * No players are connected or controlling rockets.
			if (connectedPlayers.All(kvp => kvp.Value.controlledRocket == -1))
            {
                return;
            }

			int maxCount = 1;
			foreach (KeyValuePair<int, RocketState> kvp in world.rockets)
			{
				ConnectedPlayer bestPlayer = null;
				foreach (ConnectedPlayer player in connectedPlayers.Values)
				{
					if (player.updateAuthority.Count > maxCount)
                    {
                        maxCount = player.updateAuthority.Count;
                    }

                    if (world.rockets.TryGetValue(player.controlledRocket, out RocketState controlledRocket))
					{
						// * Players controlling a rocket should always have update authority over that rocket.
						if (player.controlledRocket == kvp.Key)
						{
							bestPlayer = player;
							break;
						}

						// * Players in 'load range' of a rocket should have update authority over that rocket.
						Double2 distance = controlledRocket.location.position - kvp.Value.location.position;
						if (distance.magnitude <= settings.loadRange)
						{
							// * If two or more players are in load range of a rocket, update authority should be given to the player with the lowest latency.
							if (bestPlayer != null && bestPlayer.avgTripTime < player.avgTripTime)
							{
								continue;
							}
							bestPlayer = player;
						}

                        // * All other rockets should be distributed between players.
                        // TODO: There is likely a better way to distribute the remaining rockets which takes into account connection latency.
                        // TODO: (currently it just checks if the current player's number of 'authorities' is below the highest count)
                        if (bestPlayer == null && player.updateAuthority.Count <= maxCount)
                        {
                            bestPlayer = player;
                        }
                    }

				}
				// TODO: There was a null ref exception coming from `UpdatePlayerAuthorities()`,
				// TODO: but it only seems to occur in chaotic situations when lots of rockets are being destroyed,
				// TODO: which is quite hard to debug. I'll probably see if other players report the error when I release the mod.
				if (bestPlayer == null)
				{
					Logger.Error("bestPlayer is null!");
				}
				else if (bestPlayer.updateAuthority == null)
				{
					Logger.Error("bestPlayer.updateAuthority is null!");
				}
				bestPlayer.updateAuthority.Add(kvp.Key);
			}

			foreach (KeyValuePair<IPEndPoint, ConnectedPlayer> kvp in connectedPlayers)
			{
				SendPacketToPlayer
				(
					server.GetConnection(kvp.Key),
					new Packet_UpdatePlayerAuthority()
					{
						RocketIds = kvp.Value.updateAuthority,
					}
				);
			}
		}

		/// <summary>
		/// Returns `true` if a refresh of the players' update authorities is required.
		/// </summary>
        static bool OnIncomingPacket(NetIncomingMessage msg)
        {
            PacketType packetType = (PacketType) msg.ReadByte();
			if (packetType != PacketType.UpdateRocket)
				Logger.Debug($"Recieved packet of type '{packetType}'.");
			switch (packetType)
			{
				case PacketType.UpdatePlayerControl:
					OnPacket_UpdatePlayerControl(msg);
					return true;
				case PacketType.UpdatePlayerColor:
                    OnPacket_UpdatePlayerColor(msg);
                    return false;
				case PacketType.SendChatMessage:
                    OnPacket_SendChatMessage(msg);
                    return false;

				case PacketType.CreateRocket:
					OnPacket_CreateRocket(msg);
					return true;
				case PacketType.DestroyRocket:
					OnPacket_DestroyRocket(msg);
					return true;
				case PacketType.UpdateRocket:
					OnPacket_UpdateRocket(msg);
					return false;

				case PacketType.DestroyPart:
					OnPacket_DestroyPart(msg);
					return false;
				case PacketType.UpdateStaging:
					OnPacket_UpdateStaging(msg);
					return false;
				case PacketType.UpdatePart_EngineModule:
					OnPacket_UpdatePart_EngineModule(msg);
					return false;
				case PacketType.UpdatePart_WheelModule:
					OnPacket_UpdatePart_WheelModule(msg);
					return false;
				case PacketType.UpdatePart_BoosterModule:
					OnPacket_UpdatePart_BoosterModule(msg);
					return false;
				case PacketType.UpdatePart_ParachuteModule:
					OnPacket_UpdatePart_ParachuteModule(msg);
					return false;
				case PacketType.UpdatePart_MoveModule:
                    OnPacket_UpdatePart_MoveModule(msg);
                    return false;
				case PacketType.UpdatePart_ResourceModule:
                    OnPacket_UpdatePart_ResourceModule(msg);
                    return false;
				
				case PacketType.JoinRequest:
					Logger.Warning("Recieved join request outside of connection attempt.");
					return false;

				case PacketType.PlayerConnected:
				case PacketType.PlayerDisconnected:
				case PacketType.JoinResponse:
				case PacketType.UpdatePlayerAuthority:
					Logger.Warning($"Recieved packet (of type {packetType}) intended for clients.");
					return false;

				default:
					Logger.Error($"Unhandled packet type: {packetType}, {msg.LengthBytes} bytes.");
					return false;
			}
        }

		static void OnPacket_UpdatePlayerControl(NetIncomingMessage msg)
		{
			Packet_UpdatePlayerControl packet = msg.Read<Packet_UpdatePlayerControl>();
			if (FindPlayer(msg.SenderConnection) is ConnectedPlayer player)
			{
				if (player.id == packet.PlayerId)
				{
					player.controlledRocket = packet.RocketId;
					Logger.Debug($"Player switch - {msg.SenderConnection.RemoteEndPoint.Address}");
					SendPacketToAll
					(
						packet,
						msg.SenderConnection
					);
				}
				else
				{
					Logger.Warning("Incorrect player id while trying to update controlled rocket!");
				}
			}
			else
			{
				Logger.Error("Missing connected player while trying to update controlled rocket!");
			}
		}

		static void OnPacket_UpdatePlayerColor(NetIncomingMessage msg)
        {
            Packet_UpdatePlayerColor packet = msg.Read<Packet_UpdatePlayerColor>();
			if (FindPlayer(msg.SenderConnection) is ConnectedPlayer player)
            {
                player.iconColor = packet.Color;
				SendPacketToAll(packet, msg.SenderConnection);
            }
        }

		static void OnPacket_SendChatMessage(NetIncomingMessage msg)
        {
            Packet_SendChatMessage packet = msg.Read<Packet_SendChatMessage>();
			SendPacketToAll(packet, msg.SenderConnection);
        }

		static void OnPacket_CreateRocket(NetIncomingMessage msg)
		{
			Packet_CreateRocket packet = msg.Read<Packet_CreateRocket>();
			if (world.rockets.ContainsKey(packet.GlobalId))
            {
				Logger.Debug($"existing: {packet.Rocket.parts.Count}");
                world.rockets[packet.GlobalId] = packet.Rocket;
            	SendPacketToAll(packet, msg.SenderConnection);
            }
            else
            {
				Logger.Debug($"new: {packet.Rocket.parts.Count}");
                packet.GlobalId = world.rockets.InsertNew(packet.Rocket);
            	SendPacketToAll(packet);
				// UpdatePlayerAuthorities(new KeyValuePair<int, int>(FindPlayer(msg.SenderConnection).id, packet.GlobalId));
				if (FindPlayer(msg.SenderConnection) is ConnectedPlayer player)
				{
					player.updateAuthority.Add(packet.GlobalId);
					SendPacketToPlayer
					(
						msg.SenderConnection, new Packet_UpdatePlayerAuthority()
						{
							RocketIds = player.updateAuthority,
						}
					);
				}
            }

		}

		static void OnPacket_DestroyRocket(NetIncomingMessage msg)
		{
			Packet_DestroyRocket packet = msg.Read<Packet_DestroyRocket>();
			if (world.rockets.Remove(packet.Id))
            {
                SendPacketToAll(packet, msg.SenderConnection);
            	// UpdatePlayerAuthorities();
            }
		}

		static void OnPacket_UpdateRocket(NetIncomingMessage msg)
		{
			Packet_UpdateRocket packet = msg.Read<Packet_UpdateRocket>();
			if (world.rockets.TryGetValue(packet.Id, out RocketState state))
			{
				state.UpdateRocket(packet);
				SendPacketToAll(packet, msg.SenderConnection);
			}
		}

		static void OnPacket_DestroyPart(NetIncomingMessage msg)
		{
			Packet_DestroyPart packet = msg.Read<Packet_DestroyPart>();
			if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
			{
				if (state.RemovePart(packet.PartId))
					SendPacketToAll(packet, msg.SenderConnection);
			}
		}

		static void OnPacket_UpdateStaging(NetIncomingMessage msg)
		{
			Packet_UpdateStaging packet = msg.Read<Packet_UpdateStaging>();
			if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
			{
				state.stages = packet.Stages;
				SendPacketToAll(packet, msg.SenderConnection);
			}
		}

		static void OnPacket_UpdatePart_EngineModule(NetIncomingMessage msg)
		{
			Packet_UpdatePart_EngineModule packet = msg.Read<Packet_UpdatePart_EngineModule>();
			if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
			{
				if (state.parts.TryGetValue(packet.PartId, out PartState part))
				{
					part.part.TOGGLE_VARIABLES["engine_on"] = packet.EngineOn;
					SendPacketToAll(packet, msg.SenderConnection);
				}
			}
		}

		static void OnPacket_UpdatePart_WheelModule(NetIncomingMessage msg)
		{
			Packet_UpdatePart_WheelModule packet = msg.Read<Packet_UpdatePart_WheelModule>();
			if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
			{
				if (state.parts.TryGetValue(packet.PartId, out PartState part))
				{
					part.part.TOGGLE_VARIABLES["wheel_on"] = packet.WheelOn;
					SendPacketToAll(packet, msg.SenderConnection);
				}
			}
		}

		static void OnPacket_UpdatePart_BoosterModule(NetIncomingMessage msg)
		{
			Packet_UpdatePart_BoosterModule packet = msg.Read<Packet_UpdatePart_BoosterModule>();
			if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
			{
				if (state.parts.TryGetValue(packet.PartId, out PartState part))
				{
					// TODO: Booster modules seemingly don't save their on/off status? (At least not the RA retro pack)
					// TODO: I'm guessing that's why they get infinite thrust after loading a save when they're activated?
					// TODO: Anyway, I can't save either their "primed" state or their thrust output to the world state rn.
					// TODO: The booster module is only obtainable in vanilla through the RA retro pack, so it shouldn't matter too much for now.
					part.part.NUMBER_VARIABLES["fuel_percent"] = packet.FuelPercent;
					SendPacketToAll(packet, msg.SenderConnection);
				}
			}
		}

		static void OnPacket_UpdatePart_ParachuteModule(NetIncomingMessage msg)
		{
			Packet_UpdatePart_ParachuteModule packet = msg.Read<Packet_UpdatePart_ParachuteModule>();
			if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
			{
				if (state.parts.TryGetValue(packet.PartId, out PartState part))
				{
					part.part.NUMBER_VARIABLES["animation_state"] = packet.State;
					part.part.NUMBER_VARIABLES["deploy_state"] = packet.TargetState;
					SendPacketToAll(packet, msg.SenderConnection);
				}
			}
		}

		static void OnPacket_UpdatePart_MoveModule(NetIncomingMessage msg)
		{
			Packet_UpdatePart_MoveModule packet = msg.Read<Packet_UpdatePart_MoveModule>();
			if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
			{
				if (state.parts.TryGetValue(packet.PartId, out PartState part))
				{
					part.part.NUMBER_VARIABLES["state"] = packet.Time;
					part.part.NUMBER_VARIABLES["state_target"] = packet.TargetTime;
					SendPacketToAll(packet, msg.SenderConnection);
				}
			}
		}

		static void OnPacket_UpdatePart_ResourceModule(NetIncomingMessage msg)
		{
			Packet_UpdatePart_ResourceModule packet = msg.Read<Packet_UpdatePart_ResourceModule>();
			if (world.rockets.TryGetValue(packet.RocketId, out RocketState state))
			{
				bool foundPart = false;
				foreach (int partId in packet.PartIds)
                {
                    if (state.parts.TryGetValue(partId, out PartState partState))
                    {
                        // TODO! A lot of these save variable names will most likely be different for non-vanilla parts, but currently idk what the best way to properly get them is.
                        // TODO! I might need some form of register that associates a part's name and module variable names to their save variable names.
                        partState.part.NUMBER_VARIABLES["fuel_percent"] = packet.ResourcePercent;
						foundPart = true;
                    }
                }
				if (foundPart)
				{
					SendPacketToAll(packet, msg.SenderConnection);
				}
			}
		}
	}

	public class ConnectedPlayer
	{
		public int id;
		public string username;
		public Color iconColor;
		public float avgTripTime;

		public int controlledRocket;
		public HashSet<int> updateAuthority;


		public ConnectedPlayer(string playerName)
		{
            id = Server.connectedPlayers.Select(kvp => kvp.Value.id).ToHashSet().InsertNew();
			username = playerName;
			iconColor = Color.red;
			avgTripTime = float.PositiveInfinity;
			controlledRocket = -1;
			updateAuthority = new HashSet<int>();
		}
	}
}