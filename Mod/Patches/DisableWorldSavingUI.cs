using UnityEngine.SceneManagement;
using HarmonyLib;
using SFS.UI;
using SFS.Input;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Prevents players from opening the world save/load screens.
    /// </summary>
    public class DisableWorldSavingUI
    {
        [HarmonyPatch(typeof(LoadMenu), nameof(LoadMenu.OpenSaveMenu), new[] { typeof(CloseMode) })]
        public class LoadMenu_OpenSaveMenu
        {
            public static bool Prefix()
            {
                if (ClientManager.multiplayerEnabled.Value && SceneManager.GetActiveScene().name == "World_PC")
                {
                    MsgDrawer.main.Log("Saving is disabled in multiplayer");
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Screen_Menu), nameof(Screen_Menu.Open))]
        public class LoadMenu_Open
        {
            public static bool Prefix(Screen_Menu __instance)
            {
                if (ClientManager.multiplayerEnabled.Value && __instance is LoadMenu && SceneManager.GetActiveScene().name == "World_PC")
                {
                    MsgDrawer.main.Log("Saving is disabled in multiplayer");
                    return false;
                }
                return true;
            }
        }


    }
}

