using System.Collections.Generic;
using SFS.WorldBase;
using MultiplayerSFS.Common;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Mod.Networking
{
    public class ClientStateManager
    {
        public double worldTime;
        public Difficulty.DifficultyType difficulty;
        public Dictionary<int, RocketState> rockets;
        public Dictionary<int, PartState> parts;

        public ClientStateManager(LoadWorldPacket packet)
        {
            worldTime = packet.worldTime;
            difficulty = packet.difficulty;
            rockets = packet.rockets;
            parts = packet.parts;
        }

        public void OnLoadWorld()
        {
            // TODO: Load rockets into multiplayer world.
        }

        // public void UpdateRocket(UpdateRocketPacket change)
        // {

        // }
    }
}