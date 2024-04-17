using System.Collections.Generic;
using MultiplayerSFS.Common;
using MultiplayerSFS.Common.Packets;

namespace MultiplayerSFS.Mod.Networking
{
    public class ClientStateManager
    {
        public Dictionary<int, RocketState> rockets;
        public Dictionary<int, PartState> parts;

        public ClientStateManager(LoadWorldPacket packet)
        {
            rockets = packet.rockets;
            parts = packet.parts;
        }

        // public void UpdateRocket(UpdateRocketPacket change)
        // {

        // }
    }
}