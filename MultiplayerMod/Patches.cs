using System.Linq;
using System.Threading;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using SFS;
using SFS.IO;
using SFS.UI;
using SFS.Input;
using SFS.World;
using SFS.Builds;
using SFS.Career;
using SFS.WorldBase;
using SFS.Variables;
using SFS.Translations;
using SFS.World.Maps;

namespace MultiplayerSFS.Mod.Patches
{
    public static class Patches
    {
        public static Bool_Local multiplayerEnabled = new Bool_Local() { Value = false };

        public static ref F FieldRef<F>(this object instance, string field)
        {
            return ref AccessTools.FieldRefAccess<F>(instance.GetType(), field).Invoke(instance);
        }
    }

    // TODO: Load rockets from `ClientStateManager`.
    public class DivertLoading
    {
        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.Preload_WorldPersistent))]
        public class SavingCache_Preload_WorldPersistent
        {
            public static bool Prefix(SavingCache __instance, bool needsRocketsAndBranches)
            {
                if (Patches.multiplayerEnabled.Value)
                {
                    ref SavingCache.Data<WorldSave> worldPersistent = ref AccessTools.FieldRefAccess<SavingCache, SavingCache.Data<WorldSave>>("worldPersistent").Invoke(__instance);

                    if (worldPersistent == null || needsRocketsAndBranches && (worldPersistent.result.data.rockets == null || worldPersistent.result.data.branches == null))
                    {
                        worldPersistent = new SavingCache.Data<WorldSave>
                        {
                            thread = new Thread(
                                (ThreadStart)delegate
                                {
                                    AccessTools.FieldRefAccess<SavingCache, SavingCache.Data<WorldSave>>("worldPersistent")
                                        .Invoke(__instance).result = (true, WorldSave.CreateEmptyQuicksave(Application.version), null);
                                }
                            )
                        };
                        worldPersistent.thread.Start();
                    }
                    return false;
                }
                return true;
            }
        }
    }

    public class DivertBuildSavingAndLoading
    {
        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.SaveBuildPersistent))]
        public class SavingCache_SaveBuildPersistent
        {
            public static bool Prefix(SavingCache __instance, Blueprint new_BuildPersistent, bool cache)
            {
                if (Patches.multiplayerEnabled.Value)
                {
                    __instance.FieldRef<SavingCache.Data<Blueprint>>("buildPersistent") = SavingCache.Data<Blueprint>.Cache(new_BuildPersistent, cache);
                    SavingCache.SaveAsync(delegate
                    {
                        Blueprint.Save(Main.buildPersistentFolder, new_BuildPersistent, Application.version);
                    });
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.Preload_BlueprintPersistent))]
        public class SavingCache_Preload_BlueprintPersistent
        {
            public static bool Prefix(SavingCache __instance)
            {
                if (Patches.multiplayerEnabled.Value)
                {
                    ref SavingCache.Data<Blueprint> buildPersistent = ref __instance.FieldRef<SavingCache.Data<Blueprint>>("buildPersistent");
                    if (buildPersistent == null)
                    {
                        FolderPath path = Main.buildPersistentFolder;
                        MsgCollector logger = new MsgCollector();
                        buildPersistent = new SavingCache.Data<Blueprint>
                        {
                            thread = new Thread((ThreadStart)delegate
                        {
                            ref SavingCache.Data<Blueprint> buildPersistent_Thread = ref __instance.FieldRef<SavingCache.Data<Blueprint>>("buildPersistent");
                            if (path.FolderExists() && Blueprint.TryLoad(path, logger, out Blueprint blueprint))
                            {
                                buildPersistent_Thread.result = (true, blueprint, (logger.msg.Length > 0) ? logger.msg.ToString() : null);
                            }
                            else
                            {
                                buildPersistent_Thread.result = (false, null, null);
                            }
                        })
                        };
                        buildPersistent.thread.Start();
                    }
                    return false;
                }
                return true;
            }
        }
    }

    public class DisableWorldSaving
    {
        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.SaveWorldPersistent))]
        public class SavingCache_SaveWorldPersistent
        {
            public static bool Prefix()
            {
                return !Patches.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(WorldBaseManager), "UpdateWorldPlaytime")]
        public class WorldBaseManager_UpdateWorldPlaytime
        {
            public static bool Prefix(WorldBaseManager __instance)
            {
                if (Patches.multiplayerEnabled.Value)
                {
                    __instance.settings.playtime.lastPlayedTime_Ticks = System.DateTime.Now.Ticks;
                    __instance.settings.playtime.totalPlayTime_Seconds += 10.0;
                    return false;
                }
                return true;
            }
        }
    }

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
                    .SetEnabled(!Patches.multiplayerEnabled.Value && Base.worldBase.paths.CanResumeGame());
            }
        }

        [HarmonyPatch(typeof(BuildManager), nameof(BuildManager.OpenMenu))]
        public class BuildManager_OpenMenu
        {
            public static bool Prefix(BuildManager __instance)
            {
                if (Patches.multiplayerEnabled.Value)
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
                if (Patches.multiplayerEnabled.Value)
                {
                    ResourcesLoader.ButtonIcons buttonIcons = ResourcesLoader.main.buttonIcons;
                    List<MenuElement> list = new List<MenuElement>
                    {
                        new SizeSyncerBuilder(out var carrier).HorizontalMode(SizeMode.MaxChildSize),
                        new SizeSyncerBuilder(out var carrier2).HorizontalMode(SizeMode.MaxChildSize),
                        // // Quicksaves & reverting
                        // list.Add(ElementGenerator.VerticalSpace(25));
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

    public class DisableWorldSavingUI
    {
        [HarmonyPatch(typeof(LoadMenu), nameof(LoadMenu.OpenSaveMenu), new[] { typeof(CloseMode) })]
        public class LoadMenu_OpenSaveMenu
        {
            public static bool Prefix()
            {
                if (Patches.multiplayerEnabled.Value && SceneManager.GetActiveScene().name == "World_PC")
                {
                    MsgDrawer.main.Log("Saving is disabled in multiplayer.");
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
                if (Patches.multiplayerEnabled.Value && __instance is LoadMenu && SceneManager.GetActiveScene().name == "World_PC")
                {
                    MsgDrawer.main.Log("Saving is disabled in multiplayer.");
                    return false;
                }
                return true;
            }
        }


    }

    public class DisableReverting
    {
        [HarmonyPatch(typeof(Revert), "HasRevert")]
        public class Revert_HasRevert
        {
            public static bool Prefix(ref bool __result)
            {
                if (Patches.multiplayerEnabled.Value)
                {
                    // MsgDrawer.main.Log("Reverting is disabled in multiplayer.");
                    __result = false;
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(Revert), nameof(Revert.DeleteAll))]
        public class Revert_DeleteAll
        {
            public static bool Prefix()
            {
                return !Patches.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.SaveRevertToLaunch))]
        public class SavingCache_SaveRevertToLaunch
        {
            public static bool Prefix()
            {
                return !Patches.multiplayerEnabled.Value;
            }
        }
    }

    public class DisableTimewarp
    {
        [HarmonyPatch(typeof(WorldTime), nameof(WorldTime.AccelerateTime))]
        public class WorldTime_AccelerateTime
        {
            public static bool Prefix()
            {
                if (Patches.multiplayerEnabled.Value)
                {
                    MsgDrawer.main.Log("Timewarp is disabled in multiplayer.");
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
                if (Patches.multiplayerEnabled.Value)
                {
                    MsgDrawer.main.Log("Timewarp is disabled in multiplayer.");
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
                if (Patches.multiplayerEnabled.Value)
                {
                    MsgDrawer.main.Log("Timewarp is disabled in multiplayer.");
                    return false;
                }
                return true;
            }
        }
    }
}