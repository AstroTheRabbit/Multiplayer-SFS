using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using Lidgren.Network;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Server
{
    static class Program
    {
        static ServerConfig serverConfig;
        static ServerWorldState worldState;
        static Dictionary<string, ConnectedPlayer> connectedPlayers;
        static NetPeerConfiguration netConfig;
        static NetServer server;
        static Thread listenThread;

        static void Main()
        {
            string configPath = "server_config.json";
            if (!ServerConfig.TryLoad(configPath, out serverConfig))
            {
                File.Create(configPath).Dispose();
                string new_json = JsonConvert.SerializeObject(new ServerConfig(), Formatting.Indented);
                File.WriteAllText(configPath, new_json);
                
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Main(): '{0}' created, edit to change server settings (world save, password, etc).", configPath);
                Console.ResetColor();
            }

            try
            {
                worldState = new ServerWorldState(serverConfig.worldSaveFolder);
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Was unable to load world save, server state will not be saved! '{0}'", e.InnerException.Message);
                Console.ResetColor();
                worldState = new ServerWorldState();
            }

            connectedPlayers = new Dictionary<string, ConnectedPlayer>();
            netConfig = new NetPeerConfiguration("MultiplayerSFS")
            {
                Port = serverConfig.port,
                AutoExpandMTU = true,
                AcceptIncomingConnections = true,
                MaximumConnections = serverConfig.maxPlayers,
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
                    if (server.ConnectionsCount >= serverConfig.maxPlayers)
                    {
                        sender.Deny("Server full..");
                    }
                    if (joinRequest.password != serverConfig.password)
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
                    sender.Deny("Incorrect packet...");
                    throw new Exception("Recieved an incorrect packet type.");
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
        public int maxPlayers = 8;
        public string worldSaveFolder = "";

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