using System;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using Lidgren.Network;
using MultiplayerSFS.Common;
using System.Timers;
using UnityEngine.Lumin;

namespace MultiplayerSFS.Server
{
    public static class Server
	{
		public static NetServer server;
		public static ServerSettings settings;
		public static WorldState world;
		public static Dictionary<IPEndPoint, ConnectedPlayer> connectedPlayers;

		public static Timer resyncTimer;
		public static Timer authorityTimer;

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

			if (settings.completeResyncPeriod > 0)
			{
				resyncTimer = new Timer(1000 * settings.completeResyncPeriod);
				resyncTimer.Elapsed += (object source, ElapsedEventArgs e) => OnCompleteResync();
				resyncTimer.Enabled = true;
			}

			if (settings.updateAuthoritiesPeriod <= 0)
			{
				Logger.Error("The setting `updateAuthoritiesPeriod` cannot be less than or equal to 0!");
				return;
			}
			authorityTimer = new Timer(1000 * settings.updateAuthoritiesPeriod);
			authorityTimer.Elapsed += (object source, ElapsedEventArgs e) => UpdatePlayerAuthorities();
			authorityTimer.Enabled = true;

            server = new NetServer(npc);
			server.Start();
		}

		public static void Run()
		{
			try
			{
				Logger.Info($"Multiplayer SFS server started, listening for connections on port {server.Port}...", true);

				while (true)
				{
					Listen();
				}
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		}

		static void Listen()
		{
			NetIncomingMessage msg;
			while ((msg = server.ReadMessage()) != null)
			{
				switch (msg.MessageType)
				{
					case NetIncomingMessageType.StatusChanged:
						OnStatusChanged(msg);
						break;
					case NetIncomingMessageType.ConnectionApproval:
						OnPlayerConnectionAttempt(msg);
						break;
					case NetIncomingMessageType.Data:
						OnIncomingPacket(msg);
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
					case NetIncomingMessageType.ConnectionLatencyUpdated:
						if (FindPlayer(msg.SenderConnection) is ConnectedPlayer player)
						{
							string username = player.username.FormatUsername();
							float avgTripTime = msg.SenderConnection.AverageRoundtripTime;
							player.avgTripTime = avgTripTime;
							Logger.Info($"Average roundtrip time updated for {username} @ {msg.SenderEndPoint} - {1000 * avgTripTime}ms.");
						}

						break;
					default:
						Logger.Warning($"Unhandled message type: {msg.MessageType}, {msg.DeliveryMethod}, {msg.LengthBytes} bytes.");
						break;
				}

				server.Recycle(msg);
			}
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

		static void OnStatusChanged(NetIncomingMessage msg)
		{
			NetConnectionStatus status = (NetConnectionStatus) msg.ReadByte();
			string reason = msg.ReadString();
			string playerName = FindPlayer(msg.SenderConnection)?.username.FormatUsername();
			Logger.Info($"Status of {playerName} @ {msg.SenderEndPoint} changed to {status} - \"{reason}\".");

			switch (status)
			{
				case NetConnectionStatus.Disconnected:
					OnPlayerDisconnect(msg.SenderConnection);
					break;
				case NetConnectionStatus.Connected:
					OnPlayerSuccessfulConnect(msg.SenderConnection);
					break;
				default:
					break;
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
			if (settings.blockDuplicatePlayerNames && connectedPlayers.Values.Select((ConnectedPlayer player) => player.username).Contains(request.Username))
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
					UpdateRocketsPeriod = settings.updateAuthoritiesPeriod,
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
				Logger.Warning("Missing new player while sending world info!");
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

		static void OnCompleteResync()
		{
			Logger.Info("Sending complete resync...");
			foreach (KeyValuePair<int, RocketState> kvp in world.rockets)
			{
				SendPacketToAll
				(
					new Packet_CreateRocket()
					{
						GlobalId = kvp.Key,
						Rocket = kvp.Value,
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
			if (connectedPlayers.All((KeyValuePair<IPEndPoint, ConnectedPlayer> kvp) => kvp.Value.controlledRocket < 0))
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
						if (distance.sqrMagnitude <= settings.sqrLoadRange)
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

        static void OnIncomingPacket(NetIncomingMessage msg)
        {
            PacketType packetType = (PacketType) msg.ReadByte();
			// Logger.Debug($"Packet Bits: {msg.LengthBits}");
			// Logger.Debug($"Recieved packet of type '{packetType}'.");
			switch (packetType)
			{
				case PacketType.UpdatePlayerControl:
					OnPacket_UpdatePlayerControl(msg);
					break;

				case PacketType.CreateRocket:
					OnPacket_CreateRocket(msg);
					break;
				case PacketType.DestroyRocket:
					OnPacket_DestroyRocket(msg);
					break;
				case PacketType.UpdateRocket:
					OnPacket_UpdateRocket(msg);
					break;
				
				case PacketType.JoinRequest:
					Logger.Warning("Recieved join request outside of connection attempt.");
					break;

				case PacketType.PlayerConnected:
				case PacketType.PlayerDisconnected:
				case PacketType.JoinResponse:
				case PacketType.UpdatePlayerAuthority:
					Logger.Warning($"Recieved packet (of type {packetType}) intended for clients.");
					break;

				default:
					Logger.Error($"Unhandled packet type: {packetType}, {msg.LengthBytes} bytes.");
					break;
			}
        }

		static void OnPacket_UpdatePlayerControl(NetIncomingMessage msg)
		{
			Packet_UpdatePlayerControl packet = msg.Read<Packet_UpdatePlayerControl>();
			if (connectedPlayers.TryGetValue(msg.SenderEndPoint, out ConnectedPlayer player))
			{
				if (player.id == packet.PlayerId)
				{
					player.controlledRocket = packet.RocketId;
					SendPacketToAll
					(
						packet,
						msg.SenderConnection
					);
					UpdatePlayerAuthorities();
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

		static void OnPacket_CreateRocket(NetIncomingMessage msg)
		{
			Packet_CreateRocket packet = msg.Read<Packet_CreateRocket>();
			packet.GlobalId = world.rockets.InsertNew(packet.Rocket);
			SendPacketToAll(packet);
		}

		static void OnPacket_DestroyRocket(NetIncomingMessage msg)
		{
			Packet_DestroyRocket packet = msg.Read<Packet_DestroyRocket>();
			if (world.rockets.Remove(packet.Id))
				SendPacketToAll(packet, msg.SenderConnection);
		}

		static void OnPacket_UpdateRocket(NetIncomingMessage msg)
		{
			Packet_UpdateRocket packet = msg.Read<Packet_UpdateRocket>();
			if (world.rockets.TryGetValue(packet.Id, out RocketState state))
			{
				state.input_Turn = packet.Input_Turn;
                state.input_Raw = packet.Input_Raw;
                state.input_Horizontal = packet.Input_Horizontal;
                state.input_Vertical = packet.Input_Vertical;
                state.rotation = packet.Rotation;
                state.angularVelocity = packet.AngularVelocity;
                state.throttlePercent = packet.ThrottlePercent;
                state.throttleOn = packet.ThrottleOn;
                state.RCS = packet.RCS;
                state.location = packet.Location;
				SendPacketToAll(packet, msg.SenderConnection);
			}
		}
	}

	public class ConnectedPlayer
	{
		public static Random idGenerator = new Random();
		public int id;
		public string username;
		public float avgTripTime;

		public int controlledRocket;
		public HashSet<int> updateAuthority;


		public ConnectedPlayer(string username)
		{
			HashSet<int> connectedPlayerIDs = Server.connectedPlayers.Select((KeyValuePair<IPEndPoint, ConnectedPlayer> kvp) => kvp.Value.id).ToHashSet();
			do
			{
				id = idGenerator.Next();
			}
			while (connectedPlayerIDs.Contains(id));

			this.username = username;
			avgTripTime = float.PositiveInfinity;
			controlledRocket = -1;
			updateAuthority = new HashSet<int>();
		}
	}
}