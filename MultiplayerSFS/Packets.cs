using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;

namespace MultiplayerSFS.Networking.Packets
{
    public enum PacketType : byte
    {
        // Client/Server at start is who sent the packet.
        Invalid,
        Client_Join,
        Server_JoinResponse,
    }

    [ProtoContract]
    public class JoinPacket
    {
        [ProtoMember(1)]
        public string username;
        [ProtoMember(2)]
        public string password;
    }

    [ProtoContract]
    public class JoinResponsePacket
    {
        [ProtoMember(1)]
        public JoinResponse response;
        public enum JoinResponse
        {
            Accepted,
            Denied_UsernameAlreadyInUse,
            Denied_IncorrectPassword,
            Denied_MaxPlayersReached,
        }
    }

    public static class PacketManager
    {

        public static void OnRecievePacket(ReadOnlySpan<byte> packetData)
        {
            PacketType packetType = (PacketType) packetData[0];
            ReadOnlySpan<byte> packetBytes = packetData.Slice(1);
            switch (packetType)
            {
                case PacketType.Invalid:
                    Debug.Log("Invalid packet recieved!");
                    return;

                case PacketType.Client_Join:
                    JoinPacket join = Serializer.Deserialize<JoinPacket>(packetBytes);
                    ServerOnRecieve_Join(join);
                    return;

                case PacketType.Server_JoinResponse:
                    JoinResponsePacket joinResponse = Serializer.Deserialize<JoinResponsePacket>(packetBytes);
                    ClientOnRecieve_JoinResponse(joinResponse);
                    return;
                
                default:
                    Debug.Log($"Missing implementation for packet type {packetType.ToString()}!");
                    return;
            }
        }

        public static void ServerOnRecieve_Join(JoinPacket packet)
        {

        }

        public static void ClientOnRecieve_JoinResponse(JoinResponsePacket packet)
        {

        }
    }
}