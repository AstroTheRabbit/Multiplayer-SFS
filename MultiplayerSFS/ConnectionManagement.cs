using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using MultiplayerSFS.GUI;
using MultiplayerSFS.Networking.Packets;

namespace MultiplayerSFS.Networking
{
    public static class HostConnectionManager
    {
        public static HostInfo hostInfo;
        public static TcpListener listener;
        public static bool Initialized => listener != null && listener.Server.IsBound;

        public static TcpClient pendingConnection;
        public static List<ConnectedPlayer> connectedPlayers = new List<ConnectedPlayer>();

        public class ConnectedPlayer
        {
            public string username;
            public TcpClient connection;
        }

        public static bool StartHost(HostInfo newHostInfo)
        {
            try
            {
                hostInfo = newHostInfo;
                listener = new TcpListener(IPAddress.Parse("127.0.0.1"), hostInfo.port); // TODO: only use IPAddress.Any for local testing (afaik).
                listener.Start();
                return true;
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
                return false;
            }
        }

        public static async void ManagePendingConnection()
        {
            pendingConnection = await listener.AcceptTcpClientAsync();
            Debug.Log(pendingConnection.Connected);
            byte[] packetBytes = await pendingConnection.Client.RecievePacket();
            Debug.Log("-");
            Debug.Log(pendingConnection.Connected);
            PacketManager.OnRecievePacket(packetBytes.AsSpan());
        }

        public static void AllowPendingPlayer(string username)
        {
            connectedPlayers.Add(new ConnectedPlayer() { username = username, connection = pendingConnection });
        }
    }

    public static class ClientConnectionManager
    {
        public static JoinInfo JoinInfo;
        public static TcpClient client;
        public static bool Initialized => client != null && client.Connected;
        public static JoinResponsePacket.JoinResponse? joinResponse = null;

        public static async Task<Packets.JoinResponsePacket.JoinResponse> ConnectToHost(JoinInfo joinInfo)
        {
            JoinPacket joinPacket = joinInfo.ToPacket();
            client = new TcpClient();
            joinResponse = null;
            
            await client.ConnectAsync(joinInfo.ipAddress, joinInfo.port);
            client.Client.SendPacket(PacketType.Client_Join, joinPacket); // Send client join request.
            
            byte[] packetBytes = await client.Client.RecievePacket();
            PacketManager.OnRecievePacket(packetBytes.AsSpan()); // Recieve server join response.

            while (!joinResponse.HasValue) // Dunno if this is necessary because OnRecievePacket runs synchronously.
                await Task.Yield();
            return joinResponse.Value;
        }
    }
}