using System.Threading;
using HarmonyLib;
using UnityEngine;
using SFS;
using SFS.UI;
using SFS.Stats;
using SFS.World;
using SFS.Builds;
using SFS.Career;
using SFS.WorldBase;
using SFS.World.Maps;

namespace MultiplayerSFS.Mod.Patches
{
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
                if (ClientManager.multiplayerEnabled.Value)
                {
                    ref SavingCache.Data<WorldSave> worldPersistent = ref __instance.FieldRef<SavingCache.Data<WorldSave>>("worldPersistent");

                    if (worldPersistent == null || needsRocketsAndBranches && (worldPersistent.result.data.rockets == null || worldPersistent.result.data.branches == null))
                    {
                        worldPersistent = new SavingCache.Data<WorldSave>
                        {
                            thread = new Thread(
                                (ThreadStart)delegate
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
    }
}