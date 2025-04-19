using System;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using SFS;
using SFS.UI;
using SFS.Stats;
using SFS.World;
using SFS.Input;
using SFS.Builds;
using SFS.Career;
using SFS.WorldBase;
using SFS.World.Maps;
using MultiplayerSFS.Common;
using Environment = SFS.World.Environment;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Diverts loading of the world when in multiplayer.
    /// </summary>
    // TODO: These patches should probably be transpilers instead of overriding prefixes.
    public class DivertLoading
    {
        [HarmonyPatch(typeof(SavingCache), nameof(SavingCache.Preload_WorldPersistent))]
        public class SavingCache_Preload_WorldPersistent
        {
            public static bool Prefix(SavingCache __instance, bool needsRocketsAndBranches)
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    ref SavingCache.Data<WorldSave> worldPersistent = ref __instance.FieldRef<SavingCache.Data<WorldSave>>("worldPersistent");

                    if (worldPersistent == null || needsRocketsAndBranches && (worldPersistent.result.data.rockets == null || worldPersistent.result.data.branches == null))
                    {
                        worldPersistent = new SavingCache.Data<WorldSave>
                        {
                            thread = new Thread
                            (
                                (ThreadStart) delegate
                                {
                                    __instance.FieldRef<SavingCache.Data<WorldSave>>("worldPersistent").result = (true, WorldSave.CreateEmptyQuicksave(Application.version), null);
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
                if (ClientManager.multiplayerEnabled.Value)
                {
                    SavingCache.main.Preload_WorldPersistent(true);
                    SavingCache.main.FieldRef<SavingCache.Data<WorldSave>>("worldPersistent") = null;
                    
                    AccessTools.Method(typeof(GameManager), "ClearWorld").Invoke(__instance, null);
                    CareerState.main.SetState(new WorldSave.CareerState());

                    // // Branches, challenges, logs
                    // TODO: Fix up errors related to branches, etc
                    
                    WorldTime.main.worldTime = ClientManager.world.WorldTime;
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

                    if (SavingCache.main.TryLoadBuildPersistent(MsgDrawer.main, out Blueprint buildPersistent, eraseCache: false))
                    {
                        RocketManager.SpawnBlueprint(buildPersistent);
                    }
                    GameCamerasManager.main.InstantlyRotateCamera();

                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Prevents launching a rocket in multiplayer if the launchpad is currently occupied by another player.
        /// Also allows a launching player to clear the launchpad if it's only occupied by non-player rockets.
        /// </summary>
        [HarmonyPatch(typeof(BuildManager), "<Launch>g__Launch_2|33_5")]
        public static class BuildManager_Launch_2
        {
            [HarmonyReversePatch]
            public static void OrginalMethod(bool forceVertical) => throw new NotImplementedException("Harmony Reverse Patch");

            public static bool Prefix(bool forceVertical)
            {
                if (ClientManager.multiplayerEnabled && BuildManager.main.buildGrid.activeGrid.partsHolder.parts.Count > 0)
                {
                    ReplacementMethod(forceVertical);
                    return false;
                }
                return true;
            }

            public static async void ReplacementMethod(bool forceVertical)
            {
                HashSet<int> rockets = new HashSet<int>();
                HashSet<int> players = new HashSet<int>();

                bool confirmationOpen = true;
                bool confirmationVisible = true;
                int stackCount = ScreenManager.main.GetStackCount();

                void OnOpen()
                {
                    confirmationVisible = true;
                }

                void OnClose()
                {
                    // * `OnClose` is technically also closed when another screen is opened after the confirmation pop-up (like the debug console).
                    // * Therefore we have to differentiate between the confirmation pop-up actually being closed, or it being hidden by another screen.
                    if (ScreenManager.main.GetStackCount() > stackCount)
                    {
                        confirmationVisible = false;
                    }
                    else
                    {
                        confirmationOpen = false;
                    }
                }

                void ClearLaunchpad()
                {
                    confirmationOpen = false;
                    foreach (int id in rockets)
                    {
                        LocalManager.syncedRockets.Remove(id);
                        LocalManager.updateAuthority.Remove(id);
                        ClientManager.world.rockets.Remove(id);
                        ClientManager.SendPacket
                        (
                            new Packet_DestroyRocket()
                            {
                                Id = id,
                            }
                        );
                    }
                    OrginalMethod(forceVertical);
                }

                while (confirmationOpen)
                {
                    if (UpdateLaunchpadStatus(ref rockets, ref players, out bool updateText))
                    {
                        OrginalMethod(forceVertical);
                        return;
                    }
                    else if (confirmationVisible && updateText)
                    {
                        Debug.Log("ABSOLUTE CINEMA");
                        Debug.Log($"0: {confirmationOpen}");
                        ScreenManager.main.CloseStack();
                        Debug.Log($"1: {confirmationOpen}");
                        confirmationOpen = true;
                        if (players.Count > 0)
                        {
                            string message = "Waiting for the following players to leave the launchpad:\n";
                            message += string.Join("\n", players.Select(id => LocalManager.players[id].username));

                            Func<Screen_Base> menu = MenuGenerator.CreateMenu
                            (
                                CancelButton.Close,
                                CloseMode.Current,
                                OnOpen,
                                OnClose,
                                TextBuilder.CreateText(() => message),
                                ButtonBuilder.CreateButton
                                (
                                    null,
                                    () => "Close",
                                    OnClose,
                                    CloseMode.Current
                                )
                            );
                            Debug.Log($"A: {confirmationOpen}");
                            ScreenManager.main.OpenScreen(menu);
                            Debug.Log($"B: {confirmationOpen}");
                        }
                        else
                        {
                            string message;
                            if (rockets.Count == 1)
                                message = "There is currently 1 uncontrolled rocket blocking the launchpad...";
                            else
                                message = $"There are currently {rockets.Count} uncontrolled rockets blocking the launchpad...";

                            Func<Screen_Base> menu = MenuGenerator.CreateMenu
                            (
                                CancelButton.Close,
                                CloseMode.Current,
                                OnOpen,
                                OnClose,
                                TextBuilder.CreateText(() => message),
                                ElementGenerator.HorizontalGroup
                                (
                                    group =>
                                    {
                                        group.spacing = 10f;
                                        ((RectTransform) group.transform).pivot = new Vector2(0.5f, 0.5f);
                                    },
                                    true,
                                    true,
                                    ButtonBuilder.CreateButton
                                    (
                                        null,
                                        () => "Close",
                                        OnClose,
                                        CloseMode.Current
                                    ),
                                    ButtonBuilder.CreateButton
                                    (
                                        null,
                                        () => "Clear Launchpad",
                                        ClearLaunchpad,
                                        CloseMode.Current
                                    )
                                )
                            );
                            Debug.Log($"C: {confirmationOpen}");
                            ScreenManager.main.OpenScreen(menu);
                            Debug.Log($"D: {confirmationOpen}");
                        }
                    }
                    Debug.Log($"2: {confirmationOpen}");
                    await Task.Delay(500);
                    Debug.Log("new UI!");
                    Debug.Log($"3: {confirmationOpen}");
                }
            }

            static bool UpdateLaunchpadStatus(ref HashSet<int> rockets, ref HashSet<int> players, out bool updateText)
            {
                HashSet<int> newRockets = new HashSet<int>();
                HashSet<int> newPlayers = new HashSet<int>();

                foreach (KeyValuePair<int, RocketState> rocket in ClientManager.world.rockets)
                {
                    if (IsOnLaunchpad(rocket.Value))
                    {
                        newRockets.Add(rocket.Key);
                        foreach (KeyValuePair<int, LocalPlayer> player in LocalManager.players)
                        {
                            if (player.Value.controlledRocket == rocket.Key)
                            {
                                newPlayers.Add(player.Key);
                            }
                        }
                    }
                }
                if (newPlayers.Count == 0 && newRockets.Count == 0)
                {
                    updateText = false;
                    return true;
                }
                else
                {
                    updateText = !newPlayers.SetEquals(players) || !newRockets.SetEquals(rockets);
                    rockets = newRockets;
                    players = newPlayers;
                    return false;
                }
            }

            static readonly MethodInfo m_IsOnLaunchpad = AccessTools.Method(typeof(GameManager), "IsOnLaunchpad");
            static bool IsOnLaunchpad(RocketState rocket)
            {
                return (bool) m_IsOnLaunchpad.Invoke(null, new object[] { rocket.location.address, rocket.location.position });
            }
        }
    }
}