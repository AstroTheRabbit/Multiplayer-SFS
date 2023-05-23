using System.Linq;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine;
using MultiplayerSFS.GUI;
using MultiplayerSFS.Networking.Packets;

namespace MultiplayerSFS.Networking
{
    public class Host
    {
        public HostInfo hostInfo;
        public TcpListener server;
        public List<ConnectedPlayer> connectedPlayers = new List<ConnectedPlayer>();

        public Host(HostInfo hostInfo)
        {
            this.hostInfo = hostInfo;
        }

        public bool BindAndStart()
        {
            try
            {
                server = TcpListener.Create(hostInfo.port);
                server.Start();
                Debug.Log(server.LocalEndpoint.Serialize().ToString());
                return true;

            }
            catch (SocketException)
            {
                return false;
            }
        }

        public ConnectionManager CreateConnectionManager()
        {
            GameObject go = new GameObject("MultiplayerSFS - Connection Manager");
            Object.DontDestroyOnLoad(go);
            ConnectionManager cm = go.AddComponent<ConnectionManager>();
            cm.host = this;
            return cm;
        }

        public async void ListenForConnection()
        {
            Socket connection = await server.AcceptSocketAsync();
            Packet packet = await connection.RecievePacket();
            string addr = connection.RemoteEndPoint.Serialize().ToString();

            if (packet.type != PacketType.Client_Join)
            {
                Debug.Log($"Invalid connection attempt from {addr}");
            }
            else
            {
                try
                {
                    JoinPacket joinPacket = JsonPacket.FromPacket<JoinPacket>(packet);
                    if (joinPacket.password == hostInfo.password)
                    {
                        if (connectedPlayers.Select(p => p.username).All(u => u != joinPacket.username))
                        {
                            connection.SendPacket(new JoinResponsePacket(JoinResponsePacket.JoinResponse.Accepted).ToPacket());
                            ConnectedPlayer connectedPlayer = new ConnectedPlayer(joinPacket.username, connection);
                            connectedPlayers.Add(connectedPlayer);
                        }
                        else
                        {
                            connection.SendPacket(new JoinResponsePacket(JoinResponsePacket.JoinResponse.Denied_UsernameAlreadyInUse).ToPacket());
                        }
                    }
                    else
                    {
                        connection.SendPacket(new JoinResponsePacket(JoinResponsePacket.JoinResponse.Denied_IncorrectPassword).ToPacket());
                    }
                }
                catch (System.Exception e)
                {
                    Debug.Log($"Invalid join packet from {addr}\n{e.ToString()}");
                }
            }
        }
    }

    public class ConnectedPlayer
    {
        public string username;
        public Socket connection;

        public ConnectedPlayer(string username, Socket connection)
        {
            this.username = username;
            this.connection = connection;
        }
    }

    public class ConnectionManager : MonoBehaviour
    {
        public Host host;
        void Update()
        {
            host.ListenForConnection();
        }
    }
}