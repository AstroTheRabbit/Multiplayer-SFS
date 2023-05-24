using System;
using System.Threading;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using MultiplayerSFS.GUI;
using MultiplayerSFS.Networking.Packets;

namespace MultiplayerSFS.Networking
{
    public static class ClientConnectionManager
    {
        public static TcpClient client;
        public static Thread managerThread;
        public static bool Initialized => client != null && client.Connected;
        public static JoinResponsePacket.JoinResponse? joinResponse = null;
        public static string username;

        public static async Task<Packets.JoinResponsePacket.JoinResponse?> TryConnect(JoinInfo joinInfo)
        {
            try
            {
                username = joinInfo.username;
                client = new TcpClient();
                joinResponse = null;
                
                JoinRequestPacket joinPacket = joinInfo.ToPacket();
                await client.ConnectAsync(joinInfo.ipAddress, joinInfo.port);
                client.Client.SendPacket(PacketType.Server_JoinRequest, joinPacket);
                RecievePacket();

                while (!joinResponse.HasValue) // Dunno if this is necessary because OnRecievePacket runs synchronously.
                    await Task.Yield();
                return joinResponse.Value;
            }
            catch (Exception e)
            {
                Debug.Log(e.ToString());
                return null;
            }
        }

        public static async void RecievePacket()
        {
            byte[] packetBytes = await client.Client.RecievePacket();
            PacketManager.OnRecievePacket(packetBytes.AsSpan(), client.Client);
        }

        public static void BeginManagerThread()
        {
            managerThread = new Thread(() =>
            {
                try
                {
                    while (client.Connected)
                    {
                        if (client.Available > 0)
                        {
                            RecievePacket();
                        }
                    }

                }
                catch (Exception e)
                {
                    Debug.Log(e.ToString());
                }
            });
            managerThread.Start();
        }
    }
}