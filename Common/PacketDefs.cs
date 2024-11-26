using Lidgren.Network;

namespace MultiplayerSFS.Common.Packets
{
    // * Packet types are prefixed by the sender (if applicable).
    public enum PacketType
    {
        Client_JoinRequest,
        Server_PlayerDisconnected,
        Server_PlayerConnected,
        DebugMessage,
    }

    public interface INetBase
    {
        void Serialize(NetOutgoingMessage msg);
        void Deserialize(NetIncomingMessage msg);
    }

    public abstract class Packet : INetBase
    {
        public abstract PacketType Type { get; }
        public abstract void Serialize(NetOutgoingMessage msg);
        public abstract void Deserialize(NetIncomingMessage msg);

        public static P Deserialize<P>(NetIncomingMessage msg) where P: Packet, new()
        {
            P packet = new P();
            packet.Deserialize(msg);
            return packet;
        }
    }

    public class Client_JoinRequest : Packet
    {
        
        public string Name { get; set; }
        public string Password { get; set; }

        public override PacketType Type => PacketType.Client_JoinRequest;
        public override void Deserialize(NetIncomingMessage msg)
        {
            msg.Write(Name);
            msg.Write(Password);
        }
        public override void Serialize(NetOutgoingMessage msg)
        {
            Name = msg.ReadString();
            Password = msg.ReadString();
        }
    }

    public class Server_PlayerConnected : Packet
    {
        public string Name { get; set; }

        public override PacketType Type => PacketType.Server_PlayerConnected;
        public override void Deserialize(NetIncomingMessage msg)
        {
            msg.Write(Name);
        }
        public override void Serialize(NetOutgoingMessage msg)
        {
            Name = msg.ReadString();
        }
    }

    public class Server_PlayerDisconnected : Packet
    {
        public string Name { get; set; }

        public override PacketType Type => PacketType.Server_PlayerDisconnected;
        public override void Deserialize(NetIncomingMessage msg)
        {
            msg.Write(Name);
        }
        public override void Serialize(NetOutgoingMessage msg)
        {
            Name = msg.ReadString();
        }
    }

    public class DebugMessage : Packet
    {
        public string Message { get; set; }

        public override PacketType Type => PacketType.DebugMessage;
        public override void Deserialize(NetIncomingMessage msg)
        {
            msg.Write(Message);
        }
        public override void Serialize(NetOutgoingMessage msg)
        {
            Message = msg.ReadString();
        }
    }
}