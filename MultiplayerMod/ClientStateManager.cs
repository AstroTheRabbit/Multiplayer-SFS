using System.Collections.Generic;
using UnityEngine;
using SFS.World;
using SFS.Career;
using MultiplayerSFS.Common;
using MultiplayerSFS.Common.Packets;
using SFS.Stats;
using SFS.WorldBase;
using System.Linq;

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

        // public void UpdateRocket(UpdateRocketPacket change)
        // {

        // }
    }
}