using System.Collections.Generic;
using Lidgren.Network;
using UnityEngine;
using SFS.WorldBase;
using static SFS.World.WorldSave;

namespace MultiplayerSFS.Common
{
    public enum PacketType
    {
        // * Player/server Info Packets
        /// <summary>
        /// Request sent by a connecting player to join the server.
        /// </summary>
        JoinRequest,
        /// <summary>
        /// Contains info about the multiplayer world (including its <c>WorldState</c>) for newly connected players.
        /// </summary>
        JoinResponse,
        /// <summary>
        /// Informs other players that a player has connected.
        /// </summary>
        PlayerConnected,
        /// <summary>
        /// Informs other players that a player has disconnected.
        /// </summary>
        PlayerDisconnected,
        /// <summary>
        /// Sent whenever a player has switched to controlling a different rocket (also includes launching a rocket).
        /// </summary>
        UpdatePlayerControl,
        /// <summary>
        /// Sent by the server to all players, allocating authority for the 'update cycle' of a set of rockets.
        /// </summary>
        UpdatePlayerAuthority,

        // * Rocket Packets
        /// <summary>
        /// Sent whenever a rocket is created (launching, seperating, undocking, etc). If the rocket already exists client-side, then this packet acts as a complete 'resync' of that rocket.
        /// </summary>
        CreateRocket,
        /// <summary>
        /// Sent whenever a rocket is destroyed (include events such as docking, where two rockets are merged).
        /// </summary>
        DestroyRocket,
        /// <summary>
        /// Sent to update a rocket's current state, which includes throttle, RCS, movement controls, location, rotation, and angular velocity.
        /// </summary>
        UpdateRocket,

        // * Part/staging packets
        /// <summary>
        /// Sent whenever a part has changed state - staging, manual activation, etc.
        /// </summary>
        UpdatePart,
        /// <summary>
        /// Sent whenever a part is destroyed.
        /// </summary>
        DestroyPart,
        // UpdateStaging
    }

    public abstract class Packet : INetData
    {
        public abstract PacketType Type { get; }
        public abstract void Serialize(NetOutgoingMessage msg);
        public abstract void Deserialize(NetIncomingMessage msg);
    }

    // * Player/server Info Packets
    public class Packet_JoinRequest : Packet
    {
        public string Username { get; set; }
        public string Password { get; set; }

        public override PacketType Type => PacketType.JoinRequest;
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
    public class Packet_JoinResponse : Packet
    {
        public int PlayerId { get; set; }
        public double UpdateRocketsPeriod { get; set; }
        public double WorldTime { get; set; }
        public Difficulty.DifficultyType Difficulty { get; set; }

        public override PacketType Type => PacketType.JoinResponse;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(PlayerId);
            msg.Write(UpdateRocketsPeriod);
            msg.Write(WorldTime);
            msg.Write((byte) Difficulty);
        }

        public override void Deserialize(NetIncomingMessage msg)
        {
            PlayerId = msg.ReadInt32();
            UpdateRocketsPeriod = msg.ReadDouble();
            WorldTime = msg.ReadDouble();
            Difficulty = (Difficulty.DifficultyType) msg.ReadByte();
        }
    }
    public class Packet_PlayerConnected : Packet
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public bool PrintMessage { get; set; }

        public override PacketType Type => PacketType.PlayerConnected;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Id);
            msg.Write(Username);
            msg.Write(PrintMessage);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Id = msg.ReadInt32();
            Username = msg.ReadString();
            PrintMessage = msg.ReadBoolean();
        }
    }
    public class Packet_PlayerDisconnected : Packet
    {
        public int Id { get; set; }

        public override PacketType Type => PacketType.PlayerDisconnected;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Id);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Id = msg.ReadInt32();
        }
    }
    public class Packet_UpdatePlayerControl : Packet
    {
        public int PlayerId { get; set; }
        public int RocketId { get; set; }
        
        public override PacketType Type => PacketType.UpdatePlayerControl;
        public override void Serialize(NetOutgoingMessage msg)
        {
            if (RocketId == 0)
            {
                throw new System.Exception("AHHH!!!!");
            }
            msg.Write(PlayerId);
            msg.Write(RocketId);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            PlayerId = msg.ReadInt32();
            RocketId = msg.ReadInt32();
        }
    }
    public class Packet_UpdatePlayerAuthority : Packet
    {
        public HashSet<int> RocketIds { get; set; }
        
        public override PacketType Type => PacketType.UpdatePlayerAuthority;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.WriteCollection(RocketIds, msg.Write);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketIds = msg.ReadCollection((int count) => new HashSet<int>(count), msg.ReadInt32);
        }
    }

    // * Rocket Packets
    public class Packet_CreateRocket : Packet
    {
        public int PlayerId { get; set; }
        public int LocalId { get; set; }
        public int GlobalId { get; set; }
        public RocketState Rocket { get; set; }

        public override PacketType Type => PacketType.CreateRocket;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(PlayerId);
            msg.Write(LocalId);
            msg.Write(GlobalId);
            msg.Write(Rocket);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            PlayerId = msg.ReadInt32();
            LocalId = msg.ReadInt32();
            GlobalId = msg.ReadInt32();
            Rocket = msg.Read<RocketState>();
        }
    }
    public class Packet_DestroyRocket : Packet
    {
        public int Id { get; set; }

        public override PacketType Type => PacketType.DestroyRocket;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Id);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Id = msg.ReadInt32();
        }
    }
    public class Packet_UpdateRocket : Packet
    {
        public int Id { get; set; }
        public float Input_Turn { get; set; }
        // * Since the directional axes differ from the raw input depending on the controlling player's camera rotation, it's easier to just send all three values.
        public Vector2 Input_Raw { get; set; }
        public Vector2 Input_Horizontal { get; set; }
        public Vector2 Input_Vertical { get; set; }
        public float Rotation { get; set; }
        public float AngularVelocity { get; set; }
        public float ThrottlePercent { get; set; }
        public bool ThrottleOn { get; set; }
        public bool RCS { get; set; }
        public LocationData Location { get; set; }

        public override PacketType Type => PacketType.UpdateRocket;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Id);
            msg.Write(Input_Turn);
            msg.Write(Input_Raw);
            msg.Write(Input_Horizontal);
            msg.Write(Input_Vertical);
            msg.Write(Rotation);
            msg.Write(AngularVelocity);
            msg.Write(ThrottlePercent);
            msg.Write(ThrottleOn);
            msg.Write(RCS);
            msg.Write(Location);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Id = msg.ReadInt32();
            Input_Turn = msg.ReadFloat();
            Input_Raw = msg.ReadVector2();
            Input_Horizontal = msg.ReadVector2();
            Input_Vertical = msg.ReadVector2();
            Rotation = msg.ReadFloat();
            AngularVelocity = msg.ReadFloat();
            ThrottlePercent = msg.ReadFloat();
            ThrottleOn = msg.ReadBoolean();
            RCS = msg.ReadBoolean();
            Location = msg.ReadLocation();
        }
    } 

    // * Part Packets
    public class Packet_UpdatePart : Packet
    {
        public int RocketId { get; set; }
        public int PartId { get; set; }
        public PartState NewPart { get; set; }

        public override PacketType Type => PacketType.UpdatePart;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
            msg.Write(NewPart);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
            NewPart = msg.Read<PartState>();
        }
    }
    public class Packet_DestroyPart : Packet
    {
        public int RocketId { get; set; }
        public int PartId { get; set; }
        public bool CreateExplosion { get; set; }

        public override PacketType Type => PacketType.DestroyPart;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
            msg.Write(CreateExplosion);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
            CreateExplosion = msg.ReadBoolean();
        }
    }

    // * Staging Packets
    public class Packet_CreateStage : Packet
    {
        public int RocketId { get; set; }
        public int StageId { get; set; }

        public override PacketType Type => PacketType.CreateStage;
        public override void Serialize(NetOutgoingMessage msg)
        {
            // ! TODO
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            // ! TODO
        }
    }
    public class Packet_RemoveStage : Packet
    {
        public int RocketId { get; set; }
        public int StageId { get; set; }

        public override PacketType Type => PacketType.RemoveStage;
        public override void Serialize(NetOutgoingMessage msg)
        {
            // ! TODO
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            // ! TODO
        }
    }
    public class Packet_AddPartToStage : Packet
    {
        public int RocketId { get; set; }
        public int StageId { get; set; }
        public int PartId { get; set; }
        
        public override PacketType Type => PacketType.AddPartToStage;
        public override void Serialize(NetOutgoingMessage msg)
        {
            // ! TODO
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            // ! TODO
        }
    }
    public class Packet_RemovePartFromStage : Packet
    {
        public int RocketId { get; set; }
        public int StageId { get; set; }
        public int PartId { get; set; }

        public override PacketType Type => PacketType.RemovePartFromStage;
        public override void Serialize(NetOutgoingMessage msg)
        {
            // ! TODO
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            // ! TODO
        }
    }
    public class Packet_ReorderStage : Packet
    {
        public int RocketId { get; set; }
        public int StageId { get; set; }
        public int Index { get; set; }

        public override PacketType Type => PacketType.ReorderStage;
        public override void Serialize(NetOutgoingMessage msg)
        {
            // ! TODO
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            // ! TODO
        }
    }
}