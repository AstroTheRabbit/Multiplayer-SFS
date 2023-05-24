using ProtoBuf;
using SFS.World;
using SFS.WorldBase;

namespace MultiplayerSFS.Networking.Packets
{
    public enum PacketType : byte
    {
        // Client/Server at start the person recieving the packet.
        Invalid,
        Server_JoinRequest,
        Client_JoinResponse,
        Server_WorldSaveRequest,
        Client_WorldSaveResponse,
    }

    [ProtoContract]
    public class JoinRequestPacket
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

    [ProtoContract]
    public class WorldSaveRequestPacket
    {
        
    }

    [ProtoContract]
    public class WorldSaveReponsePacket
    {
        [ProtoMember(1)]
        public TimewarpType timewarpType;
        [ProtoMember(2)]
        public WorldSave worldSave;
        [ProtoMember(3)]
        public WorldSettings worldSettings;
    }
}