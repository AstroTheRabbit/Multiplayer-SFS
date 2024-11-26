using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lidgren.Network;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Server
{
    public static class Server
	{
		static Thread thread;
		static NetServer server;
		static ServerSettings settings;

		static Dictionary<NetConnection, ConnectedPlayer> connectedPlayers;

		public static void Initialize(ServerSettings settings)
		{
			Server.settings = settings;
            NetPeerConfiguration npc = new NetPeerConfiguration("multiplayersfs")
            {
                Port = settings.port,
				MaximumConnections = settings.maxConnections,
            };

			WorldState.LoadFromSave(settings.worldSavePath);

            server = new NetServer(npc);
			server.Start();

			thread = new Thread(Listen);
			thread.Start();

			connectedPlayers = new Dictionary<NetConnection, ConnectedPlayer>();
		}

		public static void Listen()
		{
			try
			{
				Logger.Info($"Multiplayer SFS server started. Listening for connections on port {server.Port}...");

				while (true)
				{
					NetIncomingMessage msg;
					while ((msg = server.ReadMessage()) != null)
					{
						Logger.Info($"Recieved packet from {msg.SenderConnection.RemoteEndPoint.Address}");

						switch (msg.MessageType)
						{

							case NetIncomingMessageType.StatusChanged:
								NetConnectionStatus status = (NetConnectionStatus) msg.ReadByte();
								string reason = msg.ReadString();

								switch (status)
								{
									case NetConnectionStatus.ReceivedInitiation:
                                        Client_JoinRequest request = Packet.Deserialize<Client_JoinRequest>(msg);
										OnPlayerConnectionAttempt(msg.SenderConnection, request);
										break;
									case NetConnectionStatus.Disconnected:
										OnPlayerDisconnected(msg.SenderConnection, reason);
										break;
									case NetConnectionStatus.RespondedConnect:
										// TODO: Send world state to newly connected player.
									default:
										Logger.Warning($"Unhandled connection status: {status}, \"{reason}\".");
										break;
								}
								break;

							case NetIncomingMessageType.Data:
								OnIncomingPacket(msg);
								break;

							case NetIncomingMessageType.DebugMessage:
							case NetIncomingMessageType.VerboseDebugMessage:
								Logger.Info($"Lidgren Debug: {msg.ReadString()}");
								break;
							case NetIncomingMessageType.WarningMessage:
								Logger.Warning($"Lidgren Warning: {msg.ReadString()}");
								break;
							case NetIncomingMessageType.ErrorMessage:
								Logger.Error($"Lidgren Error: {msg.ReadString()}");
								break;
							default:
								Logger.Warning($"Unhandled message type: {msg.MessageType}, {msg.DeliveryMethod}, {msg.LengthBytes} bytes.");
								break;
						}

						server.Recycle(msg);
					}
				}
			}
			catch (Exception e)
			{
				Logger.Error(e);
			}
		}

		public static ConnectedPlayer FindPlayer(NetConnection connection)
		{
			if (connectedPlayers.TryGetValue(connection, out ConnectedPlayer res))
				return res;
			return null;
		}

		public static ConnectedPlayer FindPlayer(string name, out NetConnection connection)
		{
			foreach (KeyValuePair<NetConnection, ConnectedPlayer> kvp in connectedPlayers)
			{
				if (kvp.Value.name == name)
				{
					connection = kvp.Key;
					return kvp.Value;
				}
			}
			connection = null;
			return null;
		}

		public static void SendPacketToPlayer(string playerName, Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
		{
			if (FindPlayer(playerName, out NetConnection connection) is ConnectedPlayer player)
			{
				NetOutgoingMessage msg = server.CreateMessage();
				msg.Write((byte) packet.Type);
				packet.Serialize(msg);
				server.SendMessage(msg, connection, method);
			}
		}

		public static void SendPacketToPlayer(NetConnection connection, Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
		{
			NetOutgoingMessage msg = server.CreateMessage();
			msg.Write((byte) packet.Type);
			packet.Serialize(msg);
			server.SendMessage(msg, connection, method);
		}

		public static void SendPacketToAllPlayers(Packet packet, NetDeliveryMethod method = NetDeliveryMethod.ReliableOrdered)
		{
			NetOutgoingMessage msg = server.CreateMessage();
			msg.Write((byte) packet.Type);
			packet.Serialize(msg);
			server.SendToAll(msg, method);
		}

        static void OnPlayerConnectionAttempt(NetConnection connection, Client_JoinRequest request)
		{
			Logger.Info($"Recieved join request from '{request.Name}' @ {connection.RemoteEndPoint.Address}.");
			
			if (connectedPlayers.Count >= settings.maxConnections && settings.maxConnections != 0)
			{
				connection.Deny($"Server is full ({connectedPlayers.Count}/{settings.maxConnections}).");
			}
			else if (string.IsNullOrWhiteSpace(request.Name))
			{
				connection.Deny($"Username cannot be empty.");
			}
			else if (settings.blockDuplicatePlayerNames && connectedPlayers.Values.Select((ConnectedPlayer player) => player.name).Contains(request.Name))
			{
				connection.Deny($"Username '{request.Name}' is already in use.");
			}
			else if (request.Password != settings.password && settings.password != "")
			{
				connection.Deny($"Invalid password.");
			}
			else
			{
				connection.Approve();
				connectedPlayers.Add(connection, new ConnectedPlayer(request.Name));
			}
		}

		static void OnPlayerDisconnected(NetConnection connection, string reason)
        {
			ConnectedPlayer player = FindPlayer(connection);
			Logger.Info($"'{player.name}' @ {connection.RemoteEndPoint.Address} disconnected - \"{reason}\".");
			connectedPlayers.Remove(connection);
            SendPacketToAllPlayers(new Server_PlayerDisconnected() { Name = player.name });
        }

        static void OnIncomingPacket(NetIncomingMessage msg)
        {
			PacketType packetType = (PacketType) msg.ReadByte();
			switch (packetType)
			{
				case PacketType.Client_JoinRequest:
					Logger.Warning("Recieved join request outside of connection attempt.");
					break;
				case PacketType.DebugMessage:
                    DebugMessage packet = Packet.Deserialize<DebugMessage>(msg);
					ConnectedPlayer player = FindPlayer(msg.SenderConnection);
					Logger.Info($"Recieved debug message from '{player.name}' - \"{packet.Message}\".");
					break;

				case PacketType.Server_PlayerDisconnected:
					Logger.Error($"Recieved packet (of type {packetType}) intended for clients.");
					break;

				default:
					Logger.Error($"Unhandled packet type: {packetType}, {msg.LengthBytes} bytes.");
					break;
			}
        }
	}

	public class ConnectedPlayer
	{
		public string name;

		public ConnectedPlayer(string name)
		{
			this.name = name;
		}
	}
}