using ProtoBuf;

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
}