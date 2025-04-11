using System.Collections.Generic;
using Lidgren.Network;
using UnityEngine;
using SFS.World;
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
        /// <summary>
        /// Sent by the server to keep the world time synchronised between players & the server, taking into account latency.
        /// </summary>
        UpdateWorldTime,

        // * Rocket Packets
        /// <summary>
        /// Sent whenever a rocket is created (launching, seperating, undocking, etc). If the rocket already exists client-side, then this packet acts as a complete 'resync' of that rocket.
        /// </summary>
        CreateRocket,
        /// <summary>
        /// Sent whenever a rocket is destroyed (includes events such as docking, where two rockets are merged).
        /// </summary>
        DestroyRocket,
        /// <summary>
        /// Sent to update a rocket's current state, which includes throttle, RCS, movement controls, location, rotation, and angular velocity.
        /// </summary>
        UpdateRocket,

        // * Part & Staging Packets
        /// <summary>
        /// Sent whenever a part is destroyed.
        /// </summary>
        DestroyPart,
        /// <summary>
        /// Sent whenever the staging of a rocket has been changed.
        /// </summary>
        UpdateStaging,
        // ? The effects of parts like docking ports and seperators are handled seperately in patches with `CreateRocket` and `DestroyRocket` packets.
        // TODO: However, a seperate packet may be needed for stuff like the particle effects of seperators (but that's a minor issue).
        /// <summary>
        /// Synchronises the toggling of rocket engines.
        /// </summary>
        UpdatePart_EngineModule,
        /// <summary>
        /// Synchronises the toggling of wheels.
        /// </summary>
        UpdatePart_WheelModule,
        /// <summary>
        /// Synchronises the toggling of booster modules.
        /// </summary>
        UpdatePart_BoosterModule,
        /// <summary>
        /// Synchronises the activation of parachutes.
        /// </summary>
        UpdatePart_ParachuteModule,
        /// <summary>
        /// Synchronises the state of move modules when used through `MoveModule.Toggle`.
        /// </summary>
        UpdatePart_MoveModule,
        /// <summary>
        /// Synchronises the state of resource modules e.g. fuel tanks. Changes are bunched into groups of connected resource modules to minimise network strain.
        /// </summary>
        UpdatePart_ResourceModule,
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
        public int PlayerId { get; set; } = -1;
        public double UpdateRocketsPeriod { get; set; }
        public double WorldTime { get; set; }
        public double SendTime { get; set; }
        public Difficulty.DifficultyType Difficulty { get; set; }

        public override PacketType Type => PacketType.JoinResponse;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(PlayerId);
            msg.Write(UpdateRocketsPeriod);
            msg.Write(WorldTime);
            msg.Write(SendTime);
            msg.Write((byte) Difficulty);
        }

        public override void Deserialize(NetIncomingMessage msg)
        {
            PlayerId = msg.ReadInt32();
            UpdateRocketsPeriod = msg.ReadDouble();
            WorldTime = msg.ReadDouble();
            SendTime = msg.ReadDouble();
            Difficulty = (Difficulty.DifficultyType) msg.ReadByte();
        }
    }
    public class Packet_PlayerConnected : Packet
    {
        public int Id { get; set; } = -1;
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
        public int Id { get; set; } = -1;

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
        public int PlayerId { get; set; } = -1;
        public int RocketId { get; set; } = -1;
        
        public override PacketType Type => PacketType.UpdatePlayerControl;
        public override void Serialize(NetOutgoingMessage msg)
        {
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
            RocketIds = msg.ReadCollection(count => new HashSet<int>(count), msg.ReadInt32);
        }
    }

    public class Packet_UpdateWorldTime : Packet
    {
        public double WorldTime { get; set; }
        
        public override PacketType Type => PacketType.UpdateWorldTime;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(WorldTime);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            WorldTime = msg.ReadDouble();
        }
    }

    // * Rocket Packets
    public class Packet_CreateRocket : Packet
    {
        public int LocalId { get; set; } = -1;
        public int GlobalId { get; set; } = -1;
        public RocketState Rocket { get; set; }

        public override PacketType Type => PacketType.CreateRocket;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(LocalId);
            msg.Write(GlobalId);
            msg.Write(Rocket);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            LocalId = msg.ReadInt32();
            GlobalId = msg.ReadInt32();
            Rocket = msg.Read<RocketState>();
        }
    }
    public class Packet_DestroyRocket : Packet
    {
        public int Id { get; set; } = -1;
        public DestructionReason Reason { get; set; }

        public override PacketType Type => PacketType.DestroyRocket;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(Id);
            msg.Write((byte) Reason);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            Id = msg.ReadInt32();
            Reason = (DestructionReason) msg.ReadByte();
        }
    }
    public class Packet_UpdateRocket : Packet
    {
        public int Id { get; set; } = -1;
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
        public double WorldTime { get;  set; }

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
            msg.Write(WorldTime);
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
            WorldTime = msg.ReadDouble();
        }
    } 

    // * Part & Staging Packets
    public class Packet_DestroyPart : Packet
    {
        public int RocketId { get; set; } = -1;
        public int PartId { get; set; } = -1;
        public bool CreateExplosion { get; set; }
        public DestructionReason Reason { get; set; }

        public override PacketType Type => PacketType.DestroyPart;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
            msg.Write(CreateExplosion);
            msg.Write((byte) Reason);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
            CreateExplosion = msg.ReadBoolean();
            Reason = (DestructionReason) msg.ReadByte();
        }
    }
    public class Packet_UpdateStaging : Packet
    {
        public int RocketId { get; set; } = -1;
        public List<StageState> Stages {get; set; }

        public override PacketType Type => PacketType.UpdateStaging;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.WriteCollection(Stages, msg.Write);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            Stages = msg.ReadCollection(count => new List<StageState>(count), () => msg.Read<StageState>());
        }
    }
    public class Packet_UpdatePart_EngineModule : Packet
    {
        public int RocketId { get; set; } = -1;
        public int PartId { get; set; } = -1;
        public bool EngineOn { get; set; }

        public override PacketType Type => PacketType.UpdatePart_EngineModule;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
            msg.Write(EngineOn);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
            EngineOn = msg.ReadBoolean();
        }
    }
    public class Packet_UpdatePart_WheelModule : Packet
    {
        public int RocketId { get; set; } = -1;
        public int PartId { get; set; } = -1;
        public bool WheelOn { get; set; }

        public override PacketType Type => PacketType.UpdatePart_WheelModule;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
            msg.Write(WheelOn);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
            WheelOn = msg.ReadBoolean();
        }
    }
    public class Packet_UpdatePart_BoosterModule : Packet
    {
        public int RocketId { get; set; } = -1;
        public int PartId { get; set; } = -1;
        public bool Primed { get; set; }
        public float Throttle { get; set; }
        public float FuelPercent { get; set; }

        public override PacketType Type => PacketType.UpdatePart_BoosterModule;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
            msg.Write(Primed);
            msg.Write(Throttle);
            msg.Write(FuelPercent);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
            Primed = msg.ReadBoolean();
            Throttle = msg.ReadFloat();
            FuelPercent = msg.ReadFloat();
        }
    }
    public class Packet_UpdatePart_ParachuteModule : Packet
    {
        public int RocketId { get; set; } = -1;
        public int PartId { get; set; } = -1;
        public float State { get; set; }
        public float TargetState { get; set; }

        public override PacketType Type => PacketType.UpdatePart_ParachuteModule;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
            msg.Write(State);
            msg.Write(TargetState);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
            State = msg.ReadFloat();
            TargetState = msg.ReadFloat();
        }
    }
    public class Packet_UpdatePart_MoveModule : Packet
    {
        public int RocketId { get; set; } = -1;
        public int PartId { get; set; } = -1;
        public float Time { get; set; }
        public float TargetTime { get; set; }

        public override PacketType Type => PacketType.UpdatePart_MoveModule;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(PartId);
            msg.Write(Time);
            msg.Write(TargetTime);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            PartId = msg.ReadInt32();
            Time = msg.ReadFloat();
            TargetTime = msg.ReadFloat();
        }
    }

    public class Packet_UpdatePart_ResourceModule : Packet
    {
        public int RocketId { get; set; } = -1;
        public double ResourcePercent { get; set; }
        public HashSet<int> PartIds { get; set; }

        public override PacketType Type => PacketType.UpdatePart_ResourceModule;
        public override void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(RocketId);
            msg.Write(ResourcePercent);
            msg.WriteCollection(PartIds, msg.Write);
        }
        public override void Deserialize(NetIncomingMessage msg)
        {
            RocketId = msg.ReadInt32();
            ResourcePercent = msg.ReadDouble();
            PartIds = msg.ReadCollection(count => new HashSet<int>(count), msg.ReadInt32);
        }
    }
}