using System;
using System.Linq;
using System.Collections.Generic;
using MultiplayerSFS.Common.Packets;
using System.IO;
using Newtonsoft.Json;
using System.Net;
using System.Threading;
using Lidgren.Network;

namespace MultiplayerSFS.Server
{
    static class Program
    {
        static string password;
        static Dictionary<string, ConnectedPlayer> connectedPlayers;
        static IPEndPoint ipEndPoint;
        static NetPeerConfiguration netConfig;
        static NetServer server;
        static Thread listenThread;

        static void Main()
        {
            string path = "server_config.json";
            if (!ServerConfig.TryLoad(path, out ServerConfig config))
            {
                File.Create(path).Dispose();
                string new_json = JsonConvert.SerializeObject(new ServerConfig(), Formatting.Indented);
                File.WriteAllText(path, new_json);
                Console.WriteLine("Main() - '{0}' created, edit to change server settings (password, port, etc).", path);
            }

            password = config.password;
            connectedPlayers = new Dictionary<string, ConnectedPlayer>();

            ipEndPoint = new IPEndPoint(IPAddress.Any, config.port);
            netConfig = new NetPeerConfiguration("MultiplayerSFS.Server");
            netConfig.EnableMessageType(NetIncomingMessageType.ConnectionApproval);

            server = new NetServer(netConfig);
            server.Start();

            listenThread = new Thread(Listen);
            listenThread.Start();

            Console.WriteLine("Main() - Running SFS Multiplayer on port {0}.", ipEndPoint.Port);
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
                                CheckJoinRequest(msg);
                                break;
                            case NetIncomingMessageType.Data:
                                break;
                            default:
                                Console.WriteLine($"Listen() - Recieved message of type {0} from {1}.", msg.MessageType, msg.SenderEndPoint);
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
                    Console.WriteLine("CheckJoinRequests() - New join request recieved.");
                    if (joinRequest.Password != password)
                    {
                        response.SerializePacketToMessage(new JoinResponsePacket()
                        {
                            Response = JoinResponsePacket.JoinResponse.IncorrectPassword
                        });
                    }
                    else if (connectedPlayers.ContainsKey(joinRequest.Username))
                    {
                        response.SerializePacketToMessage(new JoinResponsePacket()
                        {
                            Response = JoinResponsePacket.JoinResponse.UsernameAlreadyInUse
                        });
                    }

                    response.SerializePacketToMessage(new JoinResponsePacket()
                    {
                        Response = JoinResponsePacket.JoinResponse.AccessGranted
                    });
                    
                    allowPlayer = true;
                    connectedPlayers.Add(joinRequest.Username, new ConnectedPlayer(sender));
                    Console.WriteLine($"CheckJoinRequests() - Player '{0}' joined.", joinRequest.Username);
                }
                else
                {
                    Console.WriteLine("CheckPlayerJoining(): Recieved an incorrect packet type.");
                }

                server.SendMessage(response, sender, NetDeliveryMethod.ReliableOrdered);
                if (allowPlayer)
                {
                    sender.Approve();
                }
                else
                {
                    sender.Deny();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("CheckPlayerJoining() - Encountered and exception: {0}", e);
            }
        }
    }

    public class ServerConfig
    {
        public string password = "";
        public int port = 9807;

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