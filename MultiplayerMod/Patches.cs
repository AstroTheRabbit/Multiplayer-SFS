using HarmonyLib;
using SFS.Variables;

namespace MultiplayerSFS.Mod.Patches
{
    public static class PatchesManager
    {
        public static Bool_Local multiplayerEnabled = new Bool_Local() { Value = false };
    }
    
    public class DisableSaving
    {
        [HarmonyPatch(typeof(SFS.World.GameManager), "Update")]
        public class GameManager_Update
        {
            public static bool Prefix()
            {
                return !PatchesManager.multiplayerEnabled.Value;
            }
        }
    }

    public class DisableRevert
    {
        
    }

    public class DisableTimewarp
    {
        
    }
}