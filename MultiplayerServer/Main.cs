using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json;
using Lidgren.Network;
using MultiplayerSFS.Common;
using MultiplayerSFS.Common.Packets;
using System.Threading.Tasks;

namespace MultiplayerSFS.Server
{
    static class Program
    {
        static string password;
        static ServerWorldState worldState;
        static Dictionary<string, ConnectedPlayer> connectedPlayers;
        static NetPeerConfiguration netConfig;
        static NetServer server;
        static Thread listenThread;

        static void Main()
        {
            string path = "server_config.json";
            if (!ServerConfig.TryLoad(path, out ServerConfig config))
            {
                // File.Create(path).Dispose();
                // string new_json = JsonConvert.SerializeObject(new ServerConfig(), Formatting.Indented);
                // File.WriteAllText(path, new_json);
                // Console.WriteLine("Main(): '{0}' created, edit to change server settings (password, port, etc).", path);
            }

            worldState = new ServerWorldState();

            password = config.password;
            connectedPlayers = new Dictionary<string, ConnectedPlayer>();

            netConfig = new NetPeerConfiguration("MultiplayerSFS")
            {
                Port = config.port,
                AutoExpandMTU = true,
                AcceptIncomingConnections = true,
                MaximumConnections = config.maxPlayers,
            };
            netConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            // netConfig.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);

            server = new NetServer(netConfig);
            server.Start();

            listenThread = new Thread(Listen);
            listenThread.Start();

            Console.WriteLine("Main(): Running SFS Multiplayer at {0}:{1}.", server.Configuration.LocalAddress, server.Configuration.Port);
        }

        static void Listen()
        {
            // TODO: Add method to safely shutdown the server.
            while (true)
            {
                try
                {
                    // * Read incoming messages until none in queue.
                    NetIncomingMessage msg;
                    while ((msg = server.ReadMessage()) != null)
                    {
                        switch (msg.MessageType)
                        {
                            default:
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.WriteLine("Listen(): Recieved unhandled message of type '{0}' from '{1}'.", msg.MessageType, msg.SenderEndPoint);
                                Console.ResetColor();
                                break;
                            case NetIncomingMessageType.Error:
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("ERROR: \"{0}\"", msg.ReadString());
                                Console.ResetColor();
                                break;
                            case NetIncomingMessageType.WarningMessage:
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("WARN: \"{0}\"", msg.ReadString());
                                Console.ResetColor();
                                break;
                            case NetIncomingMessageType.StatusChanged:
                                Console.WriteLine("STAT: Status of '{0}' changed to '{1}'.", msg.SenderEndPoint, (NetConnectionStatus) msg.ReadByte());
                                break;
                            case NetIncomingMessageType.ConnectionApproval:
                                CheckJoinRequest(msg);
                                break;
                            // TODO: Manage regualar packets.
                            // case NetIncomingMessageType.Data:
                            //     break;
                        }
                    }

                    // TODO: Send update messages (player movement, chat, etc) to players.


                    RemoveDisconnectedPlayers();
                }
                catch (Exception e)
                {
                    Console.WriteLine("Listen(): Encountered an exception - {0}", e);
                }
            }
        }

        static async void CheckJoinRequest(NetIncomingMessage msg)
        {
            try
            {
                NetConnection sender = msg.SenderConnection;
                NetOutgoingMessage response = server.CreateMessage();

                if (msg.DeserializeMessageToPacket() is JoinRequestPacket joinRequest)
                {
                    Console.WriteLine("CheckJoinRequest(): Valid join request packet recieved.");

                    if (joinRequest.password != password)
                    {
                        sender.Deny("Password is incorrect...");
                    }
                    else if (connectedPlayers.ContainsKey(joinRequest.username))
                    {
                        sender.Deny("Username already in use...");
                    }
                    else
                    {
                        sender.Approve();
                        server.Recycle(msg);
                        connectedPlayers.Add(joinRequest.username, new ConnectedPlayer(sender));

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("CheckJoinRequest(): Player '{0}' joined.", joinRequest.username);
                        Console.ResetColor();

                        do
                        {
                            if (sender.Status == NetConnectionStatus.Disconnected)
                                return;
                            await Task.Yield();
                        }
                        while (sender.Status != NetConnectionStatus.Connected);

                        NetOutgoingMessage worldLoadMessage = server.CreateMessage();
                        worldLoadMessage.SerializePacketToMessage(worldState.ToPacket());
                        server.SendMessage(worldLoadMessage, sender, NetDeliveryMethod.ReliableOrdered);

                    }

                    server.Recycle(msg);
                }
                else
                {
                    Console.WriteLine("CheckJoinRequest(): Recieved an incorrect packet type.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("CheckJoinRequest(): Encountered and exception - {0}", e);
            }
        }

        static void RemoveDisconnectedPlayers()
        {
            HashSet<string> toRemove = new HashSet<string>();
            foreach (KeyValuePair<string, ConnectedPlayer> kvp in connectedPlayers)
            {
                if (kvp.Value.client.Status != NetConnectionStatus.Connected && kvp.Value.client.Status != NetConnectionStatus.RespondedConnect)
                    toRemove.Add(kvp.Key);
            }

            foreach (string player in toRemove)
            {
                connectedPlayers[player].client.Disconnect("Disconnected");
                connectedPlayers.Remove(player);
                Console.WriteLine("RemoveDisconnectedPlayers(): '{0}' disconnected.", player);
            }
        }
    }

    public class ConnectedPlayer
    {
        public NetConnection client;

        public ConnectedPlayer(NetConnection client)
        {
            this.client = client;
        }
    }

    public class ServerConfig
    {
        public string password = "";
        public int port = 9807;
        public int maxPlayers = 16;

        public static bool TryLoad(string path, out ServerConfig config)
        {
            try
            {
                if (!File.Exists(path))
                {
                    if (Directory.Exists(path))
                    {
                        throw new Exception($"'{path}' is a directory, not a file.");
                    }
                    config = new ServerConfig();
                    return false;
                }

                string json = File.ReadAllText(path);
                config = JsonConvert.DeserializeObject<ServerConfig>(json);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("ServerConfig.TryLoad() encountered an exception: {0}", e);
                config = new ServerConfig();
                return false;
            };
        }
    }
}