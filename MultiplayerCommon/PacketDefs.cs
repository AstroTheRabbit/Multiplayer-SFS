using System.Collections.Generic;
using Lidgren.Network;
using SFS.WorldBase;

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
            username = msg.ReadString();
            password = msg.ReadString();
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(username);
            msg.Write(password);
        }
    }

    public class LoadWorldPacket : IPacket
    {
        public double worldTime;
        public Difficulty.DifficultyType difficulty;
        public Dictionary<int, RocketState> rockets;
        public Dictionary<int, PartState> parts;

        public void Deserialize(NetIncomingMessage msg)
        {
            worldTime = msg.ReadDouble();
            difficulty = (Difficulty.DifficultyType) msg.ReadByte();

            int rocketsLength = msg.ReadInt32();
            rockets = new Dictionary<int, RocketState>(rocketsLength);
            for (int i = 0; i < rocketsLength; i++)
            {
                int id = msg.ReadInt32();
                RocketState state = new RocketState(); state.Deserialize(msg);
                rockets.Add(id, state);
            }

            int partsLength = msg.ReadInt32();
            parts = new Dictionary<int, PartState>(partsLength);
            for (int i = 0; i < partsLength; i++)
            {
                int id = msg.ReadInt32();
                PartState state = new PartState(); state.Deserialize(msg);
                parts.Add(id, state);
            }
        }

        public void Serialize(NetOutgoingMessage msg)
        {
            msg.Write(worldTime);
            msg.Write((byte) difficulty);
            
            msg.Write(rockets.Count);
            foreach (KeyValuePair<int, RocketState> kvp in rockets)
            {
                msg.Write(kvp.Key);
                kvp.Value.Serialize(msg);
            }

            msg.Write(parts.Count);
            foreach (KeyValuePair<int, PartState> kvp in parts)
            {
                msg.Write(kvp.Key);
                kvp.Value.Serialize(msg);
            }
        }
    }
}