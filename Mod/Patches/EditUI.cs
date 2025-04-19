using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS;
using SFS.UI;
using SFS.Input;
using SFS.World;
using SFS.Builds;
using SFS.Career;
using SFS.World.Maps;
using SFS.Translations;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Changes various UI screens to remove elements that would break in multiplayer.
    /// Also applies multiplayer colors to rockets' map icons.
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
                        ElementGenerator.DefaultHorizontalGroup(ButtonBuilder.CreateIconButton(carrier, buttonIcons.moveRocket, () => Loc.main.Move_Rocket_Button, MoveRocket, CloseMode.Current), ButtonBuilder.CreateIconButton(carrier, buttonIcons.clear, () => Loc.main.Clear_Confirm, __instance.AskClear, CloseMode.Current))
                    };
                    if (RemoteSettings.GetBool("Example_Rockets", defaultValue: true) || RemoteSettings.GetBool("Video_Tutorials", defaultValue: true))
                    {
                        list.Add(ElementGenerator.VerticalSpace(25));
                    }
                    if (RemoteSettings.GetBool("Example_Rockets", defaultValue: true))
                    {
                        list.Add(ButtonBuilder.CreateIconButton(carrier2, buttonIcons.exampleRockets, () => Loc.main.Example_Rockets_OpenMenu, OpenExampleRocketsMenu, CloseMode.None));
                    }
                    if (RemoteSettings.GetBool("Video_Tutorials", defaultValue: true))
                    {
                        list.Add(ButtonBuilder.CreateIconButton(carrier2, buttonIcons.videoTutorials, () => Loc.main.Video_Tutorials_OpenButton, HomeManager.OpenTutorials_Static, CloseMode.None));
                    }
                    list.Add(ElementGenerator.VerticalSpace(10));
                    list.Add(ButtonBuilder.CreateIconButton(carrier2, buttonIcons.shareRocket, () => Loc.main.Share_Button, __instance.UploadPC, CloseMode.Current));
                    // // Cheats
                    list.Add(ButtonBuilder.CreateIconButton(carrier2, buttonIcons.settings, () => Loc.main.Open_Settings_Button, Menu.settings.Open, CloseMode.None));
                    list.Add(ElementGenerator.VerticalSpace(10));
                    // // Resume game
                    list.Add(ButtonBuilder.CreateIconButton(carrier2, buttonIcons.exit, () => Loc.main.Exit_To_Space_Center, ExitToHub, CloseMode.None));
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

                    void OpenExampleRocketsMenu()
                    {
                        AccessTools.Method(typeof(BuildManager), "OpenExampleRocketsMenu").Invoke(__instance, null);
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

        [HarmonyPatch(typeof(MapIcon), "UpdateAlpha")]
        public static class MapIcon_UpdateAlpha
        {
            public static void Postfix(MapIcon __instance)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    Rocket rocket = __instance.GetComponent<Rocket>();
                    int id = LocalManager.GetSyncedRocketID(rocket);
                    foreach (LocalPlayer player in LocalManager.players.Values)
                    {
                        if (player.controlledRocket == id)
                        {
                            SpriteRenderer renderer = __instance.mapIcon.GetComponentInChildren<SpriteRenderer>();
                            renderer.color = new Color
                            (
                                // * multiplied with the result of `UpdateAlpha` so that stuff like VanillaUpgrades' patch still shows.
                                renderer.color.r * player.iconColor.r,
                                renderer.color.g * player.iconColor.g,
                                renderer.color.b * player.iconColor.b,
                                renderer.color.a
                            );
                            return;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Stops the world or build screens from processing input if the player is typing the multiplayer chat.
        /// This *really* should be in the vanilla game lol.
        /// </summary>
        [HarmonyPatch(typeof(Screen_Game), nameof(Screen_Game.ProcessInput))]
        class Screen_Game_ProcessInput
        {
            static bool Prefix()
            {
                return !ChatWindow.InputSelected;
            }
        }
    }
}

