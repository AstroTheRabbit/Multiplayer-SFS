using System;
using System.Net;
using System.Net.Sockets;
using System.Collections.Generic;
using UnityEngine;
using MultiplayerSFS.GUI;
using System.Threading;

namespace MultiplayerSFS.Networking
{
    public static class HostConnectionManager
    {
        public static HostInfo hostInfo;
        public static TcpListener listener;
        public static Thread listenThread;
        public static bool Initialized => listener != null && listener.Server.IsBound;

        public static TcpClient pendingConnection;
        public static List<ConnectedPlayer> connectedPlayers = new List<ConnectedPlayer>();

        public class ConnectedPlayer
        {
            public string username;
            public TcpClient connection;
            public Thread managerThread;
        }

        public static bool StartHost(HostInfo newHostInfo)
        {
            try
            {
                hostInfo = newHostInfo;
                listener = new TcpListener(IPAddress.Parse("127.0.0.1"), hostInfo.port); // TODO: only use 127.0.0.1 for local testing (afaik).
                listener.Start();
                return true;
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
                return false;
            }
        }

        public static void BeginListenThread()
        {
            listenThread = new Thread(() =>
            {
                while (true)
                {
                    if (HostConnectionManager.listener.Pending())
                        HostConnectionManager.ManagePendingConnection();
                }
            });
            listenThread.Start();
        }

        static async void ManagePendingConnection()
        {
            pendingConnection = await listener.AcceptTcpClientAsync();
            byte[] packetBytes = await pendingConnection.Client.RecievePacket();
            PacketManager.OnRecievePacket(packetBytes.AsSpan(), pendingConnection.Client);
        }

        public static void AllowPendingPlayer(string username)
        {
            ConnectedPlayer newPlayer = new ConnectedPlayer() { username = username, connection = pendingConnection };
            BeginPlayerThread(newPlayer);
            connectedPlayers.Add(newPlayer);
        }

        static void BeginPlayerThread(ConnectedPlayer connectedPlayer)
        {
            connectedPlayer.managerThread = new Thread(async () =>
            {
                try
                {
                    while (connectedPlayer.connection.Connected)
                    {
                        if (connectedPlayer.connection.Available > 0)
                        {
                            byte[] packetBytes = await connectedPlayer.connection.Client.RecievePacket();
                            PacketManager.OnRecievePacket(packetBytes.AsSpan(), connectedPlayer.connection.Client);
                        }
                    }

                }
                catch (ObjectDisposedException)
                {
                    PlayerDisconnect(connectedPlayer.username);
                }
            });
            connectedPlayer.managerThread.Start();
        }

        public static void PlayerDisconnect(string username)
        {
            ConnectedPlayer player = connectedPlayers.Find(p => p.username == username);
            if (player.connection.Connected)
                player.connection.Close();
            connectedPlayers.Remove(player);
        }
    }
}