using System.Collections.Generic;
using Lidgren.Network;
using SFS.WorldBase;

namespace MultiplayerSFS.Common.Networking
{
    // * Packet types are prefixed by the sender (if applicable).
    public enum PacketType
    {
        /// <summary>
        /// Request sent by a connecting player to join the server.
        /// </summary>
        Client_JoinRequest,
        /// <summary>
        /// Contains info about the multiplayer world (including its <c>WorldState</c>) for newly connected players.
        /// </summary>
        Server_ServerInfo,
        
        /// <summary>
        /// Informs other players that a new player has connected.
        /// </summary>
        Server_PlayerConnected,
        /// <summary>
        /// Informs other players that a player has disconnected.
        /// </summary>
        Server_PlayerDisconnected,

        /// <summary>
        /// Sent by the server to all players whenever a rocket is created.
        /// </summary>
        Server_SpawnRocket,
    }

    public abstract class Packet : INetData
    {
        public abstract PacketType Type { get; }
        public abstract void Serialize(NetOutgoingMessage msg);
        public abstract void Deserialize(NetIncomingMessage msg);
    }

    public class Client_JoinRequest : Packet
    {
        
        public string Username { get; set; }
        public string Password { get; set; }

        public override PacketType Type => PacketType.Client_JoinRequest;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Username);
            msg.Write(Password);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Username = msg.ReadString();
            Password = msg.ReadString();
        }
    }

    public class Server_ServerInfo : Packet
    {
        public double WorldTime { get; set; }
        public Difficulty.DifficultyType Difficulty { get; set; }
        public Dictionary<int, RocketState> Rockets { get; set; }
        public List<string> ConnectedPlayers { get; set; }

        public override PacketType Type => PacketType.Server_ServerInfo;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(WorldTime);
            msg.Write((byte) Difficulty);
            msg.WriteCollection
            (
                Rockets,
                (KeyValuePair<int, RocketState> kvp) =>
                {
                    msg.Write(kvp.Key);
                    msg.Write(kvp.Value);
                }
            );
            msg.WriteCollection(ConnectedPlayers, msg.Write);
        }

        public override void Deserialize(NetIncomingMessage msg)
        {
            WorldTime = msg.ReadDouble();
            Difficulty = (Difficulty.DifficultyType) msg.ReadByte();
            Rockets = msg.ReadCollection
            (
                (int count) => new Dictionary<int, RocketState>(),
                () => new KeyValuePair<int, RocketState>(msg.ReadInt32(), msg.Read<RocketState>())
            );
            ConnectedPlayers = msg.ReadCollection((int count) => new List<string>(count), msg.ReadString);
        }
    }

    public class Server_SpawnRocket : Packet
    {
        public int Id { get; set; }
        public RocketState Rocket { get; set; }

        public override PacketType Type => PacketType.Server_SpawnRocket;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Id);
            msg.Write(Rocket);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Id = msg.ReadInt32();
            Rocket = msg.Read<RocketState>();
        }
    }

    public class Server_PlayerConnected : Packet
    {
        public string Name { get; set; }

        public override PacketType Type => PacketType.Server_PlayerConnected;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Name);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Name = msg.ReadString();
        }
    }

    public class Server_PlayerDisconnected : Packet
    {
        public string Name { get; set; }

        public override PacketType Type => PacketType.Server_PlayerDisconnected;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Name);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Name = msg.ReadString();
        }
    }
}