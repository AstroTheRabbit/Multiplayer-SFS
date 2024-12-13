using System;
using System.Linq;
using System.Threading;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using HarmonyLib;
using SFS;
using SFS.IO;
using SFS.UI;
using SFS.Input;
using SFS.World;
using SFS.Stats;
using SFS.Parts;
using SFS.Builds;
using SFS.Career;
using SFS.WorldBase;
using SFS.Variables;
using SFS.World.Maps;
using SFS.Translations;
using SFS.Parts.Modules;
using MultiplayerSFS.Common;
using Environment = SFS.World.Environment;
using ModLoader.Helpers;

namespace MultiplayerSFS.Mod
{
    public static class Patches
    {
        public static Bool_Local multiplayerEnabled = new Bool_Local() { Value = false };

        public static ref F FieldRef<F>(this object instance, string field)
        {
            return ref AccessTools.FieldRefAccess<F>(instance.GetType(), field).Invoke(instance);
        }
    }

    /// <summary>
    /// Diverts loading of the world when in multiplayer.
    /// </summary>
    public class DivertLoading
    {
        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.Preload_WorldPersistent))]
        public class SavingCache_Preload_WorldPersistent
        {
            public static bool Prefix(SavingCache __instance, bool needsRocketsAndBranches)
            {
                if (Patches.multiplayerEnabled.Value)
                {
                    // ref SavingCache.Data<WorldSave> worldPersistent = ref AccessTools.FieldRefAccess<SavingCache, SavingCache.Data<WorldSave>>("worldPersistent").Invoke(__instance);
                    ref SavingCache.Data<WorldSave> worldPersistent = ref __instance.FieldRef<SavingCache.Data<WorldSave>>("worldPersistent");

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

        [HarmonyPatch(typeof(GameManager), "LoadPersistentAndLaunch")]
        public class GameManager_LoadPersistentAndLaunch
        {
            public static bool Prefix(GameManager __instance)
            {
                if (Patches.multiplayerEnabled.Value)
                {
                    SavingCache.main.Preload_WorldPersistent(true);
                    SavingCache.main.FieldRef<SavingCache.Data<WorldSave>>("worldPersistent") = null;
                    
                    AccessTools.Method(typeof(GameManager), "ClearWorld").Invoke(__instance, null);
                    CareerState.main.SetState(new WorldSave.CareerState());

                    // // Branches, challenges, logs
                    // TODO: Fix up errors related to branches, etc
                    
                    WorldTime.main.worldTime = ClientManager.world.worldTime;
                    WorldTime.main.SetTimewarpIndex_ForLoad(0);
                    WorldView.main.SetViewLocation(Base.planetLoader.spaceCenter.LaunchPadLocation);
                    WorldView.main.viewDistance.Value = 32f;

                    LocalManager.OnLoadWorld();

                    AstronautState.main.state = new WorldSave.Astronauts();
                    // // Astronauts loading
                    
                    Map.manager.mapMode.Value = false;
                    Map.view.view.target.Value = Base.planetLoader.spaceCenter.Planet.mapPlanet;
                    Map.view.view.position.Value = Base.planetLoader.spaceCenter.LaunchPadLocation.position;
                    Map.view.view.distance.Value = Base.planetLoader.spaceCenter.LaunchPadLocation.position.y * 0.65;
                    Map.navigation.SetTarget(Map.view.view.target.Value);
                   
                    PlayerController.main.player.Value = null;
                    PlayerController.main.cameraDistance.Value = 32f;
                    
                    if (__instance.environment.environments != null)
                    {
                        Environment[] environments = __instance.environment.environments;
                        foreach (Environment environment in environments)
                        {
                            environment.terrain?.LoadFully();
                        }
                    }
                    LogManager.main.ClearBranches();

                    // * The job of spawning the blueprint is transferred to the `SceneLoader.LoadWorldScene` patch, and changing `PlayerController.main.player.Value` is done by `LocalManager.CreateRocket`.
                    // // if (SavingCache.main.TryLoadBuildPersistent(MsgDrawer.main, out Blueprint buildPersistent, eraseCache: false))
                    // // {
                    // //     RocketManager.SpawnBlueprint(buildPersistent);
                    // // }
                    // // GameCamerasManager.main.InstantlyRotateCamera();

                    return false;
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Diverts the use of the `BuildPersistent` folder when in multiplayer.
    /// </summary>
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
                if (Patches.multiplayerEnabled.Value)
                {
                    // ? This is only called when exiting to the hub or build scenes, since its use in `GameManager.Update` is patched out.
                    LocalManager.syncedRockets = new Dictionary<int, LocalRocket>();
                    LocalManager.unsyncedRockets = new HashSet<int>();
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
                return !Patches.multiplayerEnabled.Value;
            }
        }

        [HarmonyPatch(typeof(GameManager), "Update")]
        public class GameManager_Update
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
                    .SetEnabled(!Patches.multiplayerEnabled.Value && Base.worldBase.paths.CanResumeGame());
            }
        }

        [HarmonyPatch(typeof(HubManager), nameof(HubManager.OpenMenu))]
        public class HubManager_OpenMenu
        {
            public static bool Prefix(HubManager __instance)
            {
                if (Patches.multiplayerEnabled.Value)
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

    /// <summary>
    /// Prevents players from using the revert feature to try and load a prior save.
    /// </summary>
    public class DisableReverting
    {
        [HarmonyPatch(typeof(Revert), "HasRevert")]
        public class Revert_HasRevert
        {
            public static bool Prefix(ref bool __result)
            {
                if (Patches.multiplayerEnabled.Value)
                {
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

    /// <summary>
    /// Removes code that changes the time scale at which the world operates.
    /// </summary>
    public class DisablePausing
    {
        [HarmonyPatch(typeof(ScreenManager), "Awake")]
        public class ScreenManager_Awake
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    // ? Removes `Time.timeScale = 0f;` for its multiplayer replacement in `Postfix`.
                    if (codes[i].Calls(AccessTools.PropertySetter(typeof(Time), nameof(Time.timeScale))))
                    {
                        codes[i - 1].opcode = OpCodes.Nop;
                        codes.RemoveAt(i);
                        break;
                    }
                }
                return codes;
            }

            public static void Postfix(ScreenManager __instance)
            {
                if (!Patches.multiplayerEnabled.Value && !__instance.selfInitialize)
                    Time.timeScale = 0f;
            }
        }

        [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.OpenScreen))]
        public class ScreenManager_OpenScreen
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    // ? Removes `Time.timeScale = (CurrentScreen.PauseWhileOpen ? 0f : ((WorldTime.main != null) ? WorldTime.main.TimeScale : 1f));` for its multiplayer replacement in `Postfix`.
                    if (codes[i].Calls(AccessTools.PropertySetter(typeof(Time), nameof(Time.timeScale))))
                    {
                        codes.RemoveRange(i - 14, 15);
                        break;
                    }
                }
                return codes;
            }

            public static void Postfix(ScreenManager __instance)
            {
                if (!Patches.multiplayerEnabled.Value)
                {
                    Time.timeScale = __instance.CurrentScreen.PauseWhileOpen ? 0f : ((WorldTime.main != null) ? WorldTime.main.TimeScale : 1f);
                }
            }
        }
    }

    /// <summary>
    /// Patches related to syncing world events (like players switching rockets) with the multiplayer server.
    /// </summary>
    public class WorldEventSyncing
    {
        /// <summary>
        /// Prevents a player from switching to a rocket that is already controlled by another player in multiplayer.
        /// </summary>
        [HarmonyPatch(typeof(PlayerController), "Start")]
        public class PlayerController_Start
        {
            public static void Postfix(PlayerController __instance)
            {
                __instance.player.Filter = CheckSwitchRocket;
            }

            static Player CheckSwitchRocket(Player oldPlayer, Player newPlayer)
            {
                // TODO: When a switch is prevented, the camera still pans as if it were switching.
                if (Patches.multiplayerEnabled.Value)
                {
                    if (newPlayer is Rocket rocket)
                    {
                        int rocketId = LocalManager.GetLocalRocketID(rocket);
                        if (LocalManager.players.Values.Any((LocalPlayer player) => player.currentRocket == rocketId))
                        {
                            // * Cannot switch to a rocket that's already controlled by another player.
                            return oldPlayer;
                        }
                        else
                        {
                            ClientManager.SendPacket
                            (
                                new Packet_UpdatePlayerControl()
                                {
                                    PlayerId = ClientManager.playerId,
                                    RocketId = rocketId,
                                }
                            );
                            return newPlayer;
                        }
                    }
                    else if (newPlayer != null)
                    {
                        Debug.LogError("Unsupported `Player` type in multiplayer!");
                        return oldPlayer;
                    }
                    else
                    {
                        ClientManager.SendPacket
                        (
                            new Packet_UpdatePlayerControl()
                            {
                                PlayerId = ClientManager.playerId,
                                RocketId = -1,
                            }
                        );
                        return newPlayer;
                    }
                }
                else
                {
                    return newPlayer;
                }
            }
        }

        // /// <summary>
        // /// This method is usually called when the game is loading the world scene, after the game has left the build scene.
        // /// Instead this patch sets up `RocketManager.SpawnBlueprint` for its use by 
        // /// </summary>
        // [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.SpawnBlueprint))]
        // public class RocketManager_SpawnBlueprint
        // {
        //     public static Rocket[] newRockets = null;
        //     public static Rocket toControl = null;
        //     public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        //     {
        //         // ? Skips `WorldView.main.SetViewLocation(Base.planetLoader.spaceCenter.LaunchPadLocation);`, which causes an exception.
        //         instructions = instructions.Skip(5);
        //         foreach (var code in instructions)
        //         {
        //             yield return code;
        //             if (code.opcode == OpCodes.Ldloc_3)
        //             {
        //                 yield return CodeInstruction.Call(typeof(RocketManager_SpawnBlueprint), nameof(ReplacementMethod));
        //                 yield return new CodeInstruction(OpCodes.Ret);
        //             }
        //         }
        //     }

        //     public static void ReplacementMethod(Rocket[] rockets)
        //     {
        //         Rocket rocket = rockets.FirstOrDefault((Rocket a) => a.hasControl.Value);
        //         toControl = rocket ?? ((rockets.Length != 0) ? rockets[0] : null);
        //         if (Patches.multiplayerEnabled.Value)
        //         {
        //             // ? Skip setting `PlayerController.main.player.Value`, and instead set `newRockets` so that it can be used in the `SceneLoader_LoadWorldScene` patch.
        //             newRockets = rockets;
        //         }
        //         else
        //         {
	    //             PlayerController.main.player.Value = toControl;
        //             WorldView.main.SetViewLocation(Base.planetLoader.spaceCenter.LaunchPadLocation);
        //         }
        //     }
        // }

        /// <summary>
        /// Syncs the newly created rockets of a launched blueprint with the multiplayer server.
        /// </summary>
        [HarmonyPatch(typeof(SceneLoader), nameof(SceneLoader.LoadWorldScene))]
        public static class SceneLoader_LoadWorldScene
        {
            static List<RocketState> launchedRockets;

            public static bool Prefix(bool launch)
            {
                if (Patches.multiplayerEnabled.Value && launch)
                {
                    if (!SavingCache.main.TryLoadBuildPersistent(MsgDrawer.main, out Blueprint buildPersistent, eraseCache: false))
                    {
                        MsgDrawer.main.Log("Failed to load blueprint!");
                        return false;
                    }
                    launchedRockets = BlueprintToRocketStates(buildPersistent);

                    if (launchedRockets.Count == 0)
                    {
                        MsgDrawer.main.Log("Cannot launch an empty blueprint in multiplayer!");
                        return false;
                    }
                    SceneHelper.OnWorldSceneLoaded += OnWorldLoad;
                }
                return true;
            }

            static void OnWorldLoad()
            {
                SceneHelper.OnWorldSceneLoaded -= OnWorldLoad;
                foreach (RocketState rocket in launchedRockets)
                {
                    int localId = LocalManager.unsyncedRockets.InsertNew();
                    if (LocalManager.unsyncedToControl == -1)
                        LocalManager.unsyncedToControl = localId;

                    ClientManager.SendPacket(new Packet_CreateRocket() { LocalId = localId, Rocket = rocket });
                }
                Menu.loading.Open("Sending launch request to server...");
                // * Loading screen is later closed in `LocalManager.CreateRocket`.
            }

            /// <summary>
            /// An altered version of `RocketManager.SpawnBlueprint` that instead returns a `List<RocketState>`.
            /// I would use a patched version of the original, but it relies on the world scene being loaded to use the rocket prefab.
            /// </summary>
            static List<RocketState> BlueprintToRocketStates(Blueprint blueprint)
            {
                if (blueprint.rotation != 0f)
                {
                    PartSave[] parts = blueprint.parts;
                    foreach (PartSave obj in parts)
                    {
                        obj.orientation += new Orientation(1f, 1f, blueprint.rotation);
                        obj.position *= new Orientation(1f, 1f, blueprint.rotation);
                    }
                }

                Part[] createdParts = PartsLoader.CreateParts(blueprint.parts, null, null, OnPartNotOwned.Delete, out OwnershipState[] _);
                Part[] ownedParts = createdParts.Where((Part a) => a != null).ToArray();
                
                if (blueprint.rotation != 0f)
                {
                    PartSave[] parts = blueprint.parts;
                    foreach (PartSave obj2 in parts)
                    {
                        obj2.orientation += new Orientation(1f, 1f, -blueprint.rotation);
                        obj2.position *= new Orientation(1f, 1f, -blueprint.rotation);
                    }
                }

                Part_Utility.PositionParts(WorldView.ToLocalPosition(Base.planetLoader.spaceCenter.LaunchPadLocation.position), new Vector2(0.5f, 0f), round: true, useLaunchBounds: true, ownedParts);
                new JointGroup(RocketManager.GenerateJoints(ownedParts), ownedParts.ToList()).RecreateGroups(out var newGroups);

                Dictionary<int, int> partIndexToID = new Dictionary<int, int>(createdParts.Length);
                Dictionary<Part, int> partToID = new Dictionary<Part, int>(createdParts.Length);
                Dictionary<int, PartState> partIDs = new Dictionary<int, PartState>(createdParts.Length);
                for (int i = 0; i < createdParts.Length; i++)
                {
                    Part part = createdParts[i];
                    int id = partIDs.InsertNew(new PartState(new PartSave(part)));
                    partIndexToID[i] = id;
                    partToID[part] = id;
                }

                List<RocketState> result = new List<RocketState>(newGroups.Count);
                foreach (JointGroup group in newGroups)
                {
                    HashSet<int> groupPartIDs = group.parts.Select((Part part) => partToID[part]).ToHashSet();
                    RocketState state = new RocketState
                    {
                        rocketName = "",
                        location = new WorldSave.LocationData(RocketManager_GetSpawnLocation.GetSpawnLocation(group)),
                        rotation = 0,
                        angularVelocity = 0,
                        throttleOn = false,
                        throttlePercent = 0.5f,
                        RCS = false,

                        input_TurnAxis = 0,
                        input_HorizontalAxis = 0,
                        input_VerticalAxis = 0,

                        parts = group.parts.Select((Part part) => partToID[part]).ToDictionary((int id) => id, (int id) => partIDs[id]),
                        joints = group.joints.Select((PartJoint joint) => new JointState(partToID[joint.a], partToID[joint.b])).ToList(),
                        stages = blueprint.stages.Select((StageSave stage) => new StageState(stage, partIndexToID, groupPartIDs)).ToList(),
                    };
                    result.Add(state);
                }
                return result;
            }
        }

        /// <summary>
        /// Reverse patch of `RocketManager.GetSpawnLocation` so it can be called in `SceneLoader_LoadWorldScene.SpawnBlueprint`.
        /// </summary>
        [HarmonyPatch(typeof(RocketManager), "GetSpawnLocation")]
        public class RocketManager_GetSpawnLocation
        {
            [HarmonyReversePatch]
            public static Location GetSpawnLocation(JointGroup group)
            {
                throw new NotImplementedException("Reverse patch error!");
            }
        }
    }
}

