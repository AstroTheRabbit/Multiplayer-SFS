using HarmonyLib;
using SFS.UI;
using SFS.Input;
using SFS.WorldBase;
using SFS.Variables;
using SFS.World;
using System.Collections.Generic;
using MultiplayerSFS.Mod.Networking;

namespace MultiplayerSFS.Mod.Patches
{
    public static class PatchesManager
    {
        public static Bool_Local multiplayerEnabled = new Bool_Local() { Value = false };
    }
    
    public class DivertLoading
    {
        // TODO: Any attempts to actually load the world (not quicksave/revert methods) need to patched to instead load the NetworkingManager's client state.
        
        // [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.Preload_WorldPersistent))]
        // public class SavingCache_Preload_WorldPersistent
        // {
        //     public static bool Prefix()
        //     {
        //         return !PatchesManager.multiplayerEnabled.Value;
        //     }
        // }

        // [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.LoadWorldPersistent))]
        // public class SavingCache_LoadWorldPersistent
        // {
        //     public static bool Prefix()
        //     {
        //         return !PatchesManager.multiplayerEnabled.Value;
        //     }
        // }
    }

    public class DisableSaving
    {
        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.SaveBuildPersistent))]
        public class SavingCache_SaveBuildPersistent
        {
            public static bool Prefix()
            {
                return !PatchesManager.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.SaveWorldPersistent))]
        public class SavingCache_SaveWorldPersistent
        {
            public static bool Prefix()
            {
                return !PatchesManager.multiplayerEnabled.Value;
            }
        }
    }

    public class DisableSavingUI
    {
        [HarmonyPatch(typeof(LoadMenu), nameof(LoadMenu.OpenSaveMenu), new[] { typeof(CloseMode) })]
        public class LoadMenu_OpenSaveMenu
        {
            public static bool Prefix()
            {
                if (PatchesManager.multiplayerEnabled.Value)
                    MsgDrawer.main.Log("Saving is disabled in multiplayer.");
                return !PatchesManager.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(Screen_Menu), nameof(Screen_Menu.Open))]
        public class LoadMenu_Open
        {
            public static bool Prefix(Screen_Menu __instance)
            {
                if (PatchesManager.multiplayerEnabled.Value && __instance is LoadMenu)
                {
                    MsgDrawer.main.Log("Saving is disabled in multiplayer.");
                    return false;
                }
                return true;
            }
        }
    }

    public class DisableRevert
    {
        [HarmonyPatch(typeof(Revert), "HasRevert")]
        public class Revert_HasRevert
        {
            public static bool Prefix(ref bool __result)
            {
                if (PatchesManager.multiplayerEnabled.Value)
                {
                    MsgDrawer.main.Log("Reverting is disabled in multiplayer.");
                    __result = false;
                }
                return !PatchesManager.multiplayerEnabled.Value;
            }
        }
    }

    public class DisableTimewarp
    {
        [HarmonyPatch(typeof(WorldTime), nameof(WorldTime.CanTimewarp))]
        public class WorldTime_CanTimewarp
        {
            public static bool Prefix(ref bool __result)
            {
                if (PatchesManager.multiplayerEnabled.Value)
                {
                    MsgDrawer.main.Log("Timewarp is disabled in multiplayer.");
                    __result = false;
                }
                return !PatchesManager.multiplayerEnabled.Value;
            }
        }
    }
}