using System;
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
		public string username;
		public string password;

        public void Deserialize(NetIncomingMessage msg)
        {
            msg.Write(username);
            msg.Write(password);
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            username = msg.ReadString();
            password = msg.ReadString();
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

		public JoinResponse response;

        public void Deserialize(NetIncomingMessage msg)
        {
            msg.Write((byte) response);
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            response = (JoinResponse) msg.ReadByte();
        }
    }
}