using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using SFS;
using SFS.UI;
using SFS.Input;
using SFS.World;
using SFS.Builds;
using SFS.Career;
using SFS.Translations;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Changes various UI screens to remove elements that would break in multiplayer.
    /// </summary>
    public class EditUI
    {
        [HarmonyPatch(typeof(HubManager), "Start")]
        public class HubManager_Start
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    // ? Removes `resumeGameButton.SetEnabled(Base.worldBase.paths.CanResumeGame());` for the `Postfix` since `Base.worldBase.paths` is null in multiplayer.
                    if (codes[i].LoadsField(typeof(HubManager).GetField(nameof(HubManager.resumeGameButton))))
                    {
                        codes.RemoveRange(i - 1, 6);
                        break;
                    }
                }
                return codes;
            }

            public static void Postfix(HubManager __instance)
            {
                __instance.FieldRef<Button>("resumeGameButton")
                    .SetEnabled(!ClientManager.multiplayerEnabled.Value && Base.worldBase.paths.CanResumeGame());
            }
        }

        [HarmonyPatch(typeof(HubManager), nameof(HubManager.OpenMenu))]
        public class HubManager_OpenMenu
        {
            public static bool Prefix(HubManager __instance)
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    ResourcesLoader.ButtonIcons buttonIcons = ResourcesLoader.main.buttonIcons;
                    MenuGenerator.OpenMenu
                    (
                        CancelButton.Close,
                        CloseMode.Current,

                        new SizeSyncerBuilder(out var carrier).HorizontalMode(SizeMode.MaxChildSize),
                        // // Cheats
                        ButtonBuilder.CreateIconButton(carrier, buttonIcons.settings, () => Loc.main.Open_Settings_Button, Menu.settings.Open, CloseMode.None),
                        ElementGenerator.VerticalSpace(10),
                        // // Resume game
                        ButtonBuilder.CreateIconButton(carrier, buttonIcons.exit, () => Loc.main.Exit_To_Main_Menu, () => {
                            ClientManager.client?.Shutdown("Left world");
                            __instance.ExitToMainMenu();
                        }, CloseMode.None)
                    );
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(BuildManager), nameof(BuildManager.OpenMenu))]
        public class BuildManager_OpenMenu
        {
            public static bool Prefix(BuildManager __instance)
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    ResourcesLoader.ButtonIcons buttonIcons = ResourcesLoader.main.buttonIcons;
                    List<MenuElement> list = new List<MenuElement>
                    {
                        new SizeSyncerBuilder(out var carrier).HorizontalMode(SizeMode.MaxChildSize),
                        new SizeSyncerBuilder(out var carrier2).HorizontalMode(SizeMode.MaxChildSize),
                        ElementGenerator.VerticalSpace(10),
                        ElementGenerator.DefaultHorizontalGroup(ButtonBuilder.CreateIconButton(carrier, buttonIcons.save, () => Loc.main.Save_Blueprint, __instance.OpenSaveMenu, CloseMode.None), ButtonBuilder.CreateIconButton(carrier, buttonIcons.load, () => Loc.main.Load_Blueprint, __instance.OpenLoadMenu, CloseMode.None)),
                        ElementGenerator.VerticalSpace(10),
                        ElementGenerator.DefaultHorizontalGroup(ButtonBuilder.CreateIconButton(carrier, buttonIcons.moveRocket, () => Loc.main.Move_Rocket_Button, MoveRocket, CloseMode.Current), ButtonBuilder.CreateIconButton(carrier, buttonIcons.clear, () => Loc.main.Clear_Confirm, __instance.AskClear, CloseMode.Current)),
                        // // Example rockets and video tutorials
                        ElementGenerator.VerticalSpace(10),
                        ButtonBuilder.CreateIconButton(carrier2, buttonIcons.shareRocket, () => Loc.main.Share_Button, __instance.UploadPC, CloseMode.Current),
                        // // Cheats
                        ButtonBuilder.CreateIconButton(carrier2, buttonIcons.settings, () => Loc.main.Open_Settings_Button, Menu.settings.Open, CloseMode.None),
                        ElementGenerator.VerticalSpace(10),
                        // // Resume game
                        ButtonBuilder.CreateIconButton(carrier2, buttonIcons.exit, () => Loc.main.Exit_To_Space_Center, ExitToHub, CloseMode.None)
                    };
                    MenuGenerator.OpenMenu(CancelButton.Close, CloseMode.Current, list.ToArray());

                    void ExitToHub()
                    {
                        // // BuildState.main.UpdatePersistent();
                        Base.sceneLoader.LoadHubScene();
                    }

                    void MoveRocket()
                    {
                        __instance.selector.Select(__instance.buildGrid.activeGrid.partsHolder.GetArray());
                    }

                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.OpenMenu))]
        public class GameManager_OpenMenu
        {
            public static bool Prefix(GameManager __instance)
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    ResourcesLoader.ButtonIcons buttonIcons = ResourcesLoader.main.buttonIcons;
                    List<MenuElement> list = new List<MenuElement>
                    {
                        new SizeSyncerBuilder(out var carrier).HorizontalMode(SizeMode.MaxChildSize),
                        new SizeSyncerBuilder(out var carrier2).HorizontalMode(SizeMode.MaxChildSize),
                        // // Quicksaves & reverting
                        ButtonBuilder.CreateIconButton(carrier2, buttonIcons.newRocket, () => Loc.main.Build_New_Rocket, __instance.ExitToBuild, CloseMode.None),
                        // // Clear debris
                        ElementGenerator.VerticalSpace(10),
                        // // Cheats
                        ButtonBuilder.CreateIconButton(carrier2, buttonIcons.settings, () => Loc.main.Open_Settings_Button, Menu.settings.Open, CloseMode.None),
                        // // Video tutorials
                        ElementGenerator.VerticalSpace(10),
                        ButtonBuilder.CreateIconButton(carrier2, buttonIcons.exit, () => Loc.main.Exit_To_Space_Center, __instance.ExitToHub, CloseMode.None)
                    };
                    MenuGenerator.OpenMenu(CancelButton.Close, CloseMode.Current, list.ToArray());

                    return false;
                }
                return true;
            }
        }
    }
}

