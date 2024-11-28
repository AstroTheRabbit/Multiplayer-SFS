using System;
using System.Net;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Lidgren.Network;
using SFS.IO;
using MultiplayerSFS.Common;
using MultiplayerSFS.Common.Networking;

namespace MultiplayerSFS.Server
{
    public static class Server
	{
		static Thread thread;
		static NetServer server;
		static ServerSettings settings;
		static WorldState world;

		static Dictionary<IPEndPoint, ConnectedPlayer> connectedPlayers;

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

			world = new WorldState(settings.worldSavePath);

            server = new NetServer(npc);
			server.Start();

			thread = new Thread(Run);
			thread.Start();

			connectedPlayers = new Dictionary<IPEndPoint, ConnectedPlayer>();
		}

		public static void Run()
		{
			try
			{
				Logger.Info($"Multiplayer SFS server started. Listening for connections on port {server.Port}...", true);

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
				// string username = FindPlayer(msg.SenderConnection)?.username.FormatUsername();
				// Logger.Info($"Recieved '{msg.MessageType}' packet from {username} @ {msg.SenderConnection.RemoteEndPoint}.");

				switch (msg.MessageType)
				{
					case NetIncomingMessageType.StatusChanged:
						OnStatusChanged(msg);
						break;
					case NetIncomingMessageType.ConnectionApproval:
						OnPlayerConnect(msg);
						break;
					case NetIncomingMessageType.Data:
						OnIncomingPacket(msg);
						break;

					case NetIncomingMessageType.DebugMessage:
					case NetIncomingMessageType.VerboseDebugMessage:
						Logger.Info($"Lidgren Debug - \"{msg.ReadString()}\".");
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

		public static ConnectedPlayer FindPlayer(NetConnection connection)
		{
			if (connectedPlayers.TryGetValue(connection.RemoteEndPoint, out ConnectedPlayer res))
				return res;
			return null;
		}

		public static ConnectedPlayer FindPlayer(string username, out NetConnection connection)
		{
			foreach (KeyValuePair<IPEndPoint, ConnectedPlayer> kvp in connectedPlayers)
			{
				if (kvp.Value.username == username)
				{
					connection = server.GetConnection(kvp.Key);
					return kvp.Value;
				}
			}
			connection = null;
			return null;
		}

		public static void SendPacketToPlayer(string username, Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
		{
			if (FindPlayer(username, out NetConnection connection) != null)
			{
				SendPacketToPlayer(connection, packet, method);
			}
			else
			{
				Logger.Error($"Could not find player {username.FormatUsername()}!");
			}
		}

		public static void SendPacketToPlayer(NetConnection connection, Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
		{
			NetOutgoingMessage msg = server.CreateMessage();
			msg.Write((byte) packet.Type);
			msg.Write(packet);
			server.SendMessage(msg, connection, method);
		}

		public static void SendPacketToAll(Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
		{
			NetOutgoingMessage msg = server.CreateMessage();
			msg.Write((byte) packet.Type);
			packet.Serialize(msg);
			msg.Write(packet);
			server.SendToAll(msg, method);
		}

		static void OnStatusChanged(NetIncomingMessage msg)
		{
			NetConnectionStatus status = (NetConnectionStatus) msg.ReadByte();
			string reason = msg.ReadString();
			string playerName = FindPlayer(msg.SenderConnection)?.username.FormatUsername();
			Logger.Info($"Status of {playerName} @ {msg.SenderEndPoint} changed to {status} - \"{reason}\".");

			if (status == NetConnectionStatus.Disconnected)
			{
				OnPlayerDisconnect(msg.SenderConnection);
			}
		}

        static void OnPlayerConnect(NetIncomingMessage msg)
		{
			Client_JoinRequest request = msg.SenderConnection.RemoteHailMessage.Read<Client_JoinRequest>();
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
				reason = $"Username cannot be empty.";
				goto ConnectionDenied;
			}
			if (settings.blockDuplicatePlayerNames && connectedPlayers.Values.Select((ConnectedPlayer player) => player.username).Contains(request.Username))
			{
				reason = $"Username {request.Username} is already in use.";
				goto ConnectionDenied;
			}
			if (request.Password != settings.password && settings.password != "")
			{
				reason = $"Invalid password.";
				goto ConnectionDenied;
			}

			Logger.Info($"Approved join request, sending world info...", true);
			
			NetOutgoingMessage hail = server.CreateMessage();
			hail.Write
			(
				new Server_ServerInfo()
				{
					WorldTime = world.worldTime,
					Difficulty = world.difficulty,
					Rockets = world.rockets,
					ConnectedPlayers = connectedPlayers.Values.Select((ConnectedPlayer player) => player.username).ToList(),
				}
			);
			connection.Approve(hail);
			connectedPlayers.Add(connection.RemoteEndPoint, new ConnectedPlayer(request.Username));
			return;

			ConnectionDenied:
				Logger.Info($"Denied join request - {reason}", true);
				connection.Deny(reason);
		}

		static void OnPlayerDisconnect(NetConnection connection)
        {
			string username = FindPlayer(connection)?.username.FormatUsername();
			connectedPlayers.Remove(connection.RemoteEndPoint);
            SendPacketToAll(new Server_PlayerDisconnected() { Name = username });
        }

        static void OnIncomingPacket(NetIncomingMessage msg)
        {
			PacketType packetType = (PacketType) msg.ReadByte();
			switch (packetType)
			{
				case PacketType.Client_JoinRequest:
					Logger.Warning("Recieved join request outside of connection attempt.");
					break;

				case PacketType.Server_PlayerConnected:
				case PacketType.Server_PlayerDisconnected:
				case PacketType.Server_ServerInfo:
				case PacketType.Server_SpawnRocket:
					Logger.Warning($"Recieved packet (of type {packetType}) intended for clients.");
					break;

				default:
					Logger.Error($"Unhandled packet type: {packetType}, {msg.LengthBytes} bytes.");
					break;
			}
        }

		static string FormatUsername(this string username)
		{
            return string.IsNullOrWhiteSpace(username) ? "???" : $"'{username}'";
        }
	}

	public class ConnectedPlayer
	{
		public string username;
		public float avgTripTime;

		public ConnectedPlayer(string username)
		{
			this.username = username;
			avgTripTime = float.PositiveInfinity;
		}
	}
}