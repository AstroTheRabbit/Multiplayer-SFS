using System.Collections.Generic;
using HarmonyLib;
using SFS.World;
using SFS.WorldBase;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Disables the world's automatic saving in multiplayer.
    /// </summary>
    public class DisableWorldSaving
    {
        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.UpdateWorldPersistent))]
        public class SavingCache_UpdateWorldPersistent
        {
            public static bool Prefix()
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    // ? This is only called when exiting to the hub or build scenes, since its use in `GameManager.Update` is patched out.
                    LocalManager.syncedRockets.Clear();
                    LocalManager.unsyncedRockets.Clear();
                    LocalManager.unsyncedToControl = -1;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.SaveWorldPersistent))]
        public class SavingCache_SaveWorldPersistent
        {
            public static bool Prefix()
            {
                return !ClientManager.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        public class GameManager_Update
        {
            public static bool Prefix()
            {
                return !ClientManager.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(WorldBaseManager), "UpdateWorldPlaytime")]
        public class WorldBaseManager_UpdateWorldPlaytime
        {
            public static bool Prefix(WorldBaseManager __instance)
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    __instance.settings.playtime.lastPlayedTime_Ticks = System.DateTime.Now.Ticks;
                    __instance.settings.playtime.totalPlayTime_Seconds += 10.0;
                    return false;
                }
                return true;
            }
        }
    }
}

