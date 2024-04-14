using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Collections.Generic;
using Newtonsoft.Json;
using Lidgren.Network;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Server
{
    static class Program
    {
        static string password;
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
                // Console.WriteLine("Main() - '{0}' created, edit to change server settings (password, port, etc).", path);
            }

            password = config.password;
            connectedPlayers = new Dictionary<string, ConnectedPlayer>();

            netConfig = new NetPeerConfiguration("MultiplayerSFS")
            {
                Port = config.port,
                AutoExpandMTU = true,
                AcceptIncomingConnections = true,
                MaximumConnections = config.maxPlayers,
            };
            netConfig.EnableMessageType(NetIncomingMessageType.Error);
            netConfig.EnableMessageType(NetIncomingMessageType.StatusChanged);
            netConfig.EnableMessageType(NetIncomingMessageType.UnconnectedData);
            netConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);
            netConfig.EnableMessageType(NetIncomingMessageType.Data);
            netConfig.EnableMessageType(NetIncomingMessageType.Receipt);
            netConfig.EnableMessageType(NetIncomingMessageType.DiscoveryRequest);
            netConfig.EnableMessageType(NetIncomingMessageType.DiscoveryResponse);
            netConfig.EnableMessageType(NetIncomingMessageType.VerboseDebugMessage);
            netConfig.EnableMessageType(NetIncomingMessageType.DebugMessage);
            netConfig.EnableMessageType(NetIncomingMessageType.WarningMessage);
            netConfig.EnableMessageType(NetIncomingMessageType.ErrorMessage);
            netConfig.EnableMessageType(NetIncomingMessageType.NatIntroductionSuccess);
            netConfig.EnableMessageType(NetIncomingMessageType.ConnectionLatencyUpdated);

            server = new NetServer(netConfig);
            server.Start();

            listenThread = new Thread(Listen);
            listenThread.Start();

            Console.WriteLine("Main() - Running SFS Multiplayer at {0}:{1}.", server.Configuration.LocalAddress, server.Configuration.Port);
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
                            case NetIncomingMessageType.ConnectionApproval:
                                Console.WriteLine("Listen() - New Connection attempt.");
                                CheckJoinRequest(msg);
                                break;
                            case NetIncomingMessageType.WarningMessage:
                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.WriteLine("WARN: \"{0}\"", msg.ReadString());
                                Console.ResetColor();
                                break;
                            // case NetIncomingMessageType.Data:
                            //     break;
                            default:
                                Console.ForegroundColor = ConsoleColor.Magenta;
                                Console.WriteLine("Listen() - Recieved unhandled message of type '{0}' from '{1}'.", msg.MessageType, msg.SenderEndPoint);
                                Console.ResetColor();
                                break;
                        }
                    }

                    // TODO: Send update messages (player movement, chat, etc) to players.
                }
                catch (Exception e)
                {
                    Console.WriteLine("Listen() - Encountered an exception: {0}", e);
                }
            }
        }

        static void CheckJoinRequest(NetIncomingMessage msg)
        {
            try
            {
                NetConnection sender = msg.SenderConnection;
                NetOutgoingMessage response = server.CreateMessage();
                bool allowPlayer = false;

                if (msg.DeserializeMessageToPacket() is JoinRequestPacket joinRequest)
                {
                    Console.WriteLine("CheckJoinRequest() - Valid join request packet recieved.");

                    if (joinRequest.password != password)
                    {
                        response.SerializePacketToMessage(new JoinResponsePacket()
                        {
                            response = JoinResponsePacket.JoinResponse.IncorrectPassword
                        });
                    }
                    else if (connectedPlayers.ContainsKey(joinRequest.username))
                    {
                        response.SerializePacketToMessage(new JoinResponsePacket()
                        {
                            response = JoinResponsePacket.JoinResponse.UsernameAlreadyInUse
                        });
                    }

                    response.SerializePacketToMessage(new JoinResponsePacket()
                    {
                        response = JoinResponsePacket.JoinResponse.AccessGranted
                    });
                    
                    allowPlayer = true;
                    connectedPlayers.Add(joinRequest.username, new ConnectedPlayer(sender));
                    Console.WriteLine($"CheckJoinRequest() - Player '{0}' joined.", joinRequest.username);
                }
                else
                {
                    Console.WriteLine("CheckJoinRequest(): Recieved an incorrect packet type.");
                }

                server.SendMessage(response, sender, NetDeliveryMethod.ReliableOrdered);
                if (allowPlayer)
                    sender.Approve();
                else
                    sender.Deny();
            }
            catch (Exception e)
            {
                Console.WriteLine("CheckJoinRequest() - Encountered and exception: {0}", e);
            }
        }
    }

    public class ServerConfig
    {
        public string password = "";
        public int port = 14242;
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
                Console.WriteLine("ServerConfig.TryLoad() encountered an exception: {}", e);
                config = new ServerConfig();
                return false;
            };
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
}