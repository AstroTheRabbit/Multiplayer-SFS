using System;
using System.Net;
using System.Linq;
using System.Net.Sockets;
using System.Collections.Generic;
using MultiplayerSFS.Common.Packets;
using static MultiplayerSFS.Common.Packets.JoinResponsePacket.Types;
using System.IO;
using Newtonsoft.Json;

namespace MultiplayerSFS.Server
{
    static class Program
    {
        static string password;
        static IPEndPoint ipEndPoint;
        static TcpListener listener;
        static List<ConnectedPlayer> connectedPlayers;

        static void Main()
        {
            string path = "server_config.json";
            if (!ServerConfig.TryLoad(path, out ServerConfig config))
            {
                File.Create(path).Dispose();
                string new_json = JsonConvert.SerializeObject(new ServerConfig(), Formatting.Indented);
                File.WriteAllText(path, new_json);
                Console.WriteLine("'{0}' created, edit to change server settings (password, port, etc).", path);
            }
            password = config.password;
            ipEndPoint = new IPEndPoint(IPAddress.Any, config.port);
            listener = new TcpListener(ipEndPoint);
            connectedPlayers = new List<ConnectedPlayer>();

            Console.WriteLine("Running SFS Multiplayer on port {0}, IP address {1}", ipEndPoint.Port, ipEndPoint.Address);
            
            
            while (true)
            {
                try
                {
                    CheckPlayerJoining();
                }
                catch (Exception e)
                {
                    Console.WriteLine("ServerConfig.TryLoad() encountered an exception: {}", e);
                }
            }
        }

        static async void CheckPlayerJoining()
        {
            TcpClient client = await listener.AcceptTcpClientAsync();
            NetworkStream stream = client.GetStream();

            if (await stream.RecievePacketAsync() is JoinRequestPacket joinRequest)
            {
                Console.WriteLine("New join request");
                if (joinRequest.Password != password)
                {
                    await stream.SendPacketAsync(new JoinResponsePacket()
                    {
                        Response = JoinResponse.IncorrectPassword
                    });
                    client.Close();
                    return;
                }
                if (connectedPlayers.Select(p => p.username).Any(u => u == joinRequest.Username.Trim()))
                {
                    await stream.SendPacketAsync(new JoinResponsePacket()
                    {
                        Response = JoinResponse.UsernameAlreadyInUse
                    });
                    client.Close();
                    return;
                }

                await stream.SendPacketAsync(new JoinResponsePacket()
                {
                    Response = JoinResponse.AccessGranted
                });

                connectedPlayers.Add(new ConnectedPlayer(joinRequest.Username.Trim(), client));
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
        public string username;
        public TcpClient client;

        public ConnectedPlayer(string username, TcpClient client)
        {
            this.username = username;
            this.client = client;
        }
    }
}