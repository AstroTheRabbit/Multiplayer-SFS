using HarmonyLib;
using SFS.Stats;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Disable various methods of the `StatsRecorder` that cause exceptions in multiplayer.
    /// </summary>
    public class DisableStats
    {
        [HarmonyPatch(typeof(StatsRecorder), "Record")]
        public class StatsRecorder_Record
        {
            public static bool Prefix()
            {
                return !ClientManager.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(StatsRecorder), nameof(StatsRecorder.OnCrash))]
        public class StatsRecorder_OnCrash
        {
            public static bool Prefix()
            {
                return !ClientManager.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(StatsRecorder), nameof(StatsRecorder.OnSplit))]
        public class StatsRecorder_OnSplit
        {
            public static bool Prefix()
            {
                return !ClientManager.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(StatsRecorder), nameof(StatsRecorder.OnMerge))]
        public class StatsRecorder_OnMerge
        {
            public static bool Prefix()
            {
                return !ClientManager.multiplayerEnabled.Value;
            }
        }
    }
}

