using System.Collections.Generic;
using Lidgren.Network;
using SFS.World;
using SFS.WorldBase;

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
        /// Sent to update a rocket's location (position, velocity, etc).
        /// </summary>
        UpdateRocketLocation,
        /// <summary>
        /// Sent to update a rocket's throttle, RCS, and movement controls.
        /// </summary>
        UpdateRocketControls,

        // * Part packets
        /// <summary>
        /// Sent whenever a part has been activated.
        /// </summary>
        ActivatePart,
        /// <summary>
        /// Sent whenever a part is destroyed.
        /// </summary>
        DestroyPart,
        
        // * Staging Packets
        /// <summary>
        /// Sent whenever a stage has been created.
        /// </summary>
        CreateStage,
        /// <summary>
        /// Sent whenever a stage has been removed (including activation, which also sends <c>Server_ActivateParts</c> for each part of that stage).
        /// </summary>
        RemoveStage,
        /// <summary>
        /// Sent whenever a part is added to a stage.
        /// </summary>
        AddPartToStage,
        /// <summary>
        /// Sent whenever a part is removed from a stage.
        /// </summary>
        RemovePartFromStage,
        /// <summary>
        /// Sent whenever the order of a rocket's stages has been changed.
        /// </summary>
        ReorderStage,
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
        public double WorldTime { get; set; }
        public Difficulty.DifficultyType Difficulty { get; set; }
        public int PlayerId { get; set; }

        public override PacketType Type => PacketType.JoinResponse;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(PlayerId);
            msg.Write(WorldTime);
            msg.Write((byte) Difficulty);
        }

        public override void Deserialize(NetIncomingMessage msg)
        {
            PlayerId = msg.ReadInt32();
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
    public class Packet_UpdateRocketLocation : Packet
    {
        public int Id { get; set; }
        public WorldSave.LocationData Location { get; set; }
        public float Rotation { get; set; }
        public float AngularVelocity { get; set; }

        public override PacketType Type => PacketType.UpdateRocketLocation;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Id);
            msg.Write(Location);
            msg.Write(Rotation);
            msg.Write(AngularVelocity);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Id = msg.ReadInt32();
            Location = msg.ReadLocation();
            Rotation = msg.ReadFloat();
            AngularVelocity = msg.ReadFloat();
        }
    }
    public class Packet_UpdateRocketControls : Packet
    {
        public int Id { get; set; }
        public bool ThrottleOn { get; set; }
        public float ThrottlePercent { get; set; }
        public bool RCS { get; set; }
        public float Input_TurnAxis { get; set; }
        public float Input_HorizontalAxis { get; set; }
        public float Input_VerticalAxis { get; set; }

        public override PacketType Type => PacketType.UpdateRocketControls;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Id);
            msg.Write(ThrottleOn);
            msg.Write(ThrottlePercent);
            msg.Write(RCS);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Id = msg.ReadInt32();
            ThrottleOn = msg.ReadBoolean();
            ThrottlePercent = msg.ReadFloat();
            RCS = msg.ReadBoolean();
        }
    } 

    // * Part Packets
    public class Packet_ActivatePart : Packet
    {
        // TODO: Parts like the launch escape system activate differently depending on which region of the part is clicked, and so that information will have to be added to this packet.
        public int RocketId { get; set; }
        public int PartId { get; set; }

        public override PacketType Type => PacketType.ActivatePart;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
        }
    }
    public class Packet_DestroyPart : Packet
    {
        public int RocketId { get; set; }
        public int PartId { get; set; }

        public override PacketType Type => PacketType.DestroyPart;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
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