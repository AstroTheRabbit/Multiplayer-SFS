using HarmonyLib;
using SFS.UI;
using SFS.World;
using SFS.World.Maps;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Prevents players from entering timewarp.
    /// </summary>
    public class DisableTimewarp
    {
        [HarmonyPatch(typeof(WorldTime), nameof(WorldTime.AccelerateTime))]
        public class WorldTime_AccelerateTime
        {
            public static bool Prefix()
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    MsgDrawer.main.Log("Timewarp is disabled in multiplayer");
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(WorldTime), nameof(WorldTime.DecelerateTime))]
        public class WorldTime_DecelerateTime
        {
            public static bool Prefix()
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    MsgDrawer.main.Log("Timewarp is disabled in multiplayer");
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(TimewarpTo), nameof(TimewarpTo.StartTimewarp))]
        public class TimewarpTo_StartTimewarp
        {
            public static bool Prefix()
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    MsgDrawer.main.Log("Timewarp is disabled in multiplayer");
                    return false;
                }
                return true;
            }
        }
    }
}

