using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
using ProtoBuf;
using SFS;
using SFS.World;
using SFS.WorldBase;
using MultiplayerSFS.Networking.Packets;

namespace MultiplayerSFS.Networking
{
    public static class PacketExtensions
    {
        class RecieveState
        {
            public Socket socket;
            public IAsyncResult currentRecieveProcess;
            public byte[] dataSizeBuffer = new byte[4];
            public int dataSize;
            public byte[] packetBuffer; // packet type (1 byte) + packet data
        }

        public static async Task<byte[]> RecievePacket(this Socket socket)
        {
            RecieveState state = new RecieveState() { socket = socket };
            state.currentRecieveProcess = socket.BeginReceive(state.dataSizeBuffer, 0, 4, SocketFlags.None, new AsyncCallback(RecieveSizeCallback), state);
            while (!state.currentRecieveProcess.IsCompleted)
                await Task.Yield();
            return state.packetBuffer;
            
            void RecieveSizeCallback(IAsyncResult ar)
            {
                int bytesRead = state.socket.EndReceive(ar);
                if (bytesRead != 4)
                    throw new Exception("RecivePacket - received size != 4 bytes!");
                state.dataSize = BitConverter.ToInt32(state.dataSizeBuffer, 0);
                state.packetBuffer = new byte[state.dataSize];
                state.currentRecieveProcess = state.socket.BeginReceive(state.packetBuffer, 0, state.dataSize, SocketFlags.None, new AsyncCallback(RecievePacketCallback), state);
            }

            void RecievePacketCallback(IAsyncResult ar)
            {
                int bytesRead = state.socket.EndReceive(ar);
                if (bytesRead != state.dataSize)
                    throw new Exception("RecivePacket - Bytes read != recieved size!");
            }
        }

        public static void SendPacket(this Socket socket, PacketType packetType, object packet)
        {
            int serializedSize = 1 + (int) Serializer.Measure(packet).Length; // 1 + for packet type byte.
            byte[] buffer = new byte[4 + serializedSize]; // 4 + for data size itself.
            using (MemoryStream stream = new MemoryStream(buffer))
            {
                stream.Write(BitConverter.GetBytes(serializedSize), 0, 4);
                stream.WriteByte((byte) packetType);
                Serializer.Serialize(stream, packet);
            }
            socket.BeginSend(buffer, 0, 4 + serializedSize, SocketFlags.None, new AsyncCallback(SendCallback), socket);
            
            void SendCallback(IAsyncResult ar)
            {
                int bytesSent = socket.EndSend(ar);
                if (bytesSent != serializedSize + 4)
                    throw new Exception("SendPacket - bytes sent != data size!");
            }
        }
    }

    public static class PacketManager
    {
        public static void OnRecievePacket(ReadOnlySpan<byte> packetData, Socket sender)
        {
            PacketType packetType = (PacketType) packetData[0];
            ReadOnlySpan<byte> packetBytes = packetData.Slice(1);
            switch (packetType)
            {
                case PacketType.Invalid:
                    Debug.Log("Invalid packet recieved!");
                    return;

                case PacketType.Server_JoinRequest:
                    JoinRequestPacket joinRequest = Serializer.Deserialize<JoinRequestPacket>(packetBytes);
                    OnRecieve_Server_JoinRequest(joinRequest, sender);
                    return;

                case PacketType.Client_JoinResponse:
                    JoinResponsePacket joinResponse = Serializer.Deserialize<JoinResponsePacket>(packetBytes);
                    OnRecieve_Client_JoinResponse(joinResponse, sender);
                    return;

                case PacketType.Server_WorldSaveRequest:
                    WorldSaveRequestPacket worldSaveRequest = Serializer.Deserialize<WorldSaveRequestPacket>(packetBytes);
                    OnRecieve_Server_WorldSaveRequest(worldSaveRequest, sender);
                    return;
                
                default:
                    Debug.Log($"Missing implementation for packet type {packetType.ToString()}!");
                    return;
            }
        }

        static void OnRecieve_Server_JoinRequest(JoinRequestPacket packet, Socket sender)
        {
            if (packet.password == HostConnectionManager.hostInfo.password)
            {
                if (HostConnectionManager.connectedPlayers.Count < HostConnectionManager.hostInfo.maxPlayerCount)
                {
                    if (HostConnectionManager.connectedPlayers.All(p => p.username != packet.username))
                    {
                        sender.SendPacket(PacketType.Client_JoinResponse, new JoinResponsePacket() { response = JoinResponsePacket.JoinResponse.Accepted });
                        HostConnectionManager.AllowPendingPlayer(packet.username);
                    }
                    else
                        sender.SendPacket(PacketType.Client_JoinResponse, new JoinResponsePacket() { response = JoinResponsePacket.JoinResponse.Denied_UsernameAlreadyInUse });
                }
                else
                    sender.SendPacket(PacketType.Client_JoinResponse, new JoinResponsePacket() { response = JoinResponsePacket.JoinResponse.Denied_MaxPlayersReached });
            }
            else
                sender.SendPacket(PacketType.Client_JoinResponse, new JoinResponsePacket() { response = JoinResponsePacket.JoinResponse.Denied_IncorrectPassword });
        }

        static void OnRecieve_Client_JoinResponse(JoinResponsePacket packet, Socket sender)
        {
            if (packet.response != JoinResponsePacket.JoinResponse.Accepted)
            {
                ClientConnectionManager.client.Close();
                ClientConnectionManager.client = null;
            }
            ClientConnectionManager.joinResponse = packet.response;
        }

        static void OnRecieve_Server_WorldSaveRequest(WorldSaveRequestPacket packet, Socket sender)
        {
            WorldSave.TryLoad(HostConnectionManager.hostInfo.world.worldPersistentPath, true, new MsgCollector(), out WorldSave world);
            sender.SendPacket(PacketType.Client_WorldSaveResponse, new WorldSaveReponsePacket() {
                timewarpType = HostConnectionManager.hostInfo.timewarpType,
                worldSave = world,
                worldSettings = HostConnectionManager.hostInfo.world.LoadWorldSettings(),
            });
        }

        static void OnRecieve_Client_WorldSaveResponse(WorldSaveReponsePacket packet, Socket sender)
        {
            Patches.MultiplayerEnabled = true;
            Patches.timewarpType = packet.timewarpType;
            
            WorldReference worldReference = new WorldReference(".TempWorldSave");
            WorldSave.Save(worldReference.path, true, packet.worldSave, false);
            worldReference.SaveWorldSettings(packet.worldSettings);

            Base.worldBase.EnterWorld(worldReference, packet.worldSettings, Base.sceneLoader.LoadHubScene);
        }
    }
}