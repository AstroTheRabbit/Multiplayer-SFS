using Lidgren.Network;

namespace MultiplayerSFS.Common.Packets
{
    public interface IPacket
	{
		void Serialize(NetOutgoingMessage msg);
		void Deserialize(NetIncomingMessage msg);
	}

    public class JoinRequestPacket : IPacket
    {
		public string Username { get; set; }
		public string Password { get; set; }

        public void Deserialize(NetIncomingMessage msg)
        {
            msg.Write(Username);
            msg.Write(Password);
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            Username = msg.ReadString();
            Password = msg.ReadString();
        }
    }

	public class JoinResponsePacket : IPacket
    {
		public enum JoinResponse
		{
			UnspecifiedBlocked,
			UsernameAlreadyInUse,
			IncorrectPassword,
			AccessGranted,
		}

		public JoinResponse Response { get; set; }

        public void Deserialize(NetIncomingMessage msg)
        {
            msg.Write((byte) Response);
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            Response = (JoinResponse) msg.ReadByte();
        }
    }
}