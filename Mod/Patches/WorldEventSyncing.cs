using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS.UI;
using SFS.World;
using SFS.Parts;
using SFS.Parts.Modules;
using MultiplayerSFS.Common;
using System.Reflection.Emit;

namespace MultiplayerSFS.Mod.Patches
{
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
                if (ClientManager.multiplayerEnabled.Value)
                {
                    if (newPlayer is Rocket rocket)
                    {
                        int rocketId = LocalManager.GetLocalRocketID(rocket);
                        if (rocketId == -1 || LocalManager.players.Values.Any(player => player.currentRocket == rocketId))
                        {
                            // * Cannot switch to an unsynced rocket or a rocket that's already controlled by another player.
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

        /// <summary>
        /// Syncs the newly created rockets of a launched blueprint with the multiplayer server.
        /// </summary>
        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.SpawnBlueprint))]
        [HarmonyDebug]
        public class RocketManager_SpawnBlueprint
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                // ? Wraps `Rocket rocket = array3.FirstOrDefault((Rocket a) => a.hasControl.Value); ...`
                // ? in an if statement that instead calls `SyncLaunch(Rocket[]);` if the client is currently in multiplayer.

                Label label_multiplayer = generator.DefineLabel();
                bool found_ldloc = false;

                foreach (CodeInstruction code in instructions)
                {
                    if (!found_ldloc && code.opcode == OpCodes.Ldloc_3)
                    {
                        found_ldloc = true;

                        // * Jump to original code if not in multiplayer.
                        yield return CodeInstruction.Call(typeof(RocketManager_SpawnBlueprint), nameof(GetInMultiplayer));
                        yield return new CodeInstruction(OpCodes.Brfalse, label_multiplayer);

                        // * Call `SyncLaunch(Rocket[])` and return.
                        yield return new CodeInstruction(OpCodes.Ldloc_3);
                        yield return CodeInstruction.Call(typeof(RocketManager_SpawnBlueprint), nameof(SyncLaunch));
                        yield return new CodeInstruction(OpCodes.Ret);

                        yield return code.WithLabels(label_multiplayer);
                    }
                    else
                    {
                        yield return code;
                    }

                }
            }

            public static bool GetInMultiplayer()
            {
                return ClientManager.multiplayerEnabled;
            }

            public static void SyncLaunch(Rocket[] rockets)
            {
                Menu.loading.Open("Sending launch request to server...");
                Dictionary<Rocket, int> locals = new Dictionary<Rocket, int>(rockets.Length);
                foreach (Rocket rocket in rockets)
                {
                    LocalRocket lr = new LocalRocket(rocket);
                    int localId = LocalManager.unsyncedRockets.InsertNew(lr);
                    ClientManager.SendPacket
                    (
                        new Packet_CreateRocket()
                        {
                            LocalId = localId,
                            Rocket = lr.ToState(),
                        }
                    );
                    locals.Add(rocket, localId);
                }
                Rocket controllable = rockets.FirstOrDefault(r => r.hasControl.Value);
                Rocket toControl = controllable ?? ((rockets.Length != 0) ? rockets[0] : null);
                LocalManager.unsyncedToControl = toControl != null && locals.TryGetValue(toControl, out int idToControl) ? idToControl : -1;
            }
        }

        /// <summary>
        /// Prevents the destruction of a part in multiplayer if this client doesn't have update authority over the part's rocket.
        /// If the client does have update authority, this patch also sends a `DestroyPart` packet to the server.
        /// </summary>
        [HarmonyPatch(typeof(Part), nameof(Part.DestroyPart))]
        public class Part_DestroyPart
        {
            public static bool Prefix(Part __instance, bool createExplosion, bool updateJoints, DestructionReason reason)
            {
                if (ClientManager.multiplayerEnabled && GameManager.main != null)
                {
                    // ? This mod uses `(DestructionReason) 4` as a way to signal that the server has told the client to destroy this part.
                    if (reason == (DestructionReason) 4)
                    {
                        // * This part was intentionally destroyed by the server (e.g. another player's rocket crashed).
                        return true;
                    }

                    int rocketId = LocalManager.GetLocalRocketID(__instance.Rocket);
                    if (rocketId == -1)
                    {
                        // * This part's rocket isn't synced, so we won't destroy it.
                        return false;
                    }
                    int partId = LocalManager.GetLocalPartID(rocketId, __instance);
                    if (partId == -1)
                    {
                        Debug.LogError("Couldn't find destroyed part's id!");
                        return false;
                    }
                    if (LocalManager.updateAuthority.Contains(rocketId))
                    {
                        // * This client has update authority over the destroyed part's rocket.
                        ClientManager.SendPacket
                        (
                            new Packet_DestroyPart()
                            {
                                RocketId = rocketId,
                                PartId = partId,
                                CreateExplosion = createExplosion,
                            }
                        );
                        return true;
                    }
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Syncs the creation of 'child' rockets, which result from part destruction, split modules, etc.
        /// This patch also prevents child rockets from being created if the local player doesn't have update authority over the parent rocket.
        /// </summary>
        [HarmonyPatch(typeof(JointGroup), nameof(JointGroup.RecreateRockets))]
        public class JointGroup_RecreateRockets
        {
            public static void Postfix(Rocket rocket, List<Rocket> childRockets)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    if (childRockets.Count == 0)
                    {
                        // * `RecreateRockets` resulted in no change to the rocket, and so it does not need to be re-synced.
                        return;
                    }

                    int rocketId = LocalManager.GetLocalRocketID(rocket);
                    if (rocketId == -1)
                    {
                        // * The rocket isn't synced yet.
                        // TODO: idk what the best thing to do when the rocket isn't synced yet, so I'll ignore it for now :)
                        return;
                    }

                    if (!LocalManager.updateAuthority.Contains(rocketId))
                    {
                        // * Players that don't have update authority over `rocket` shouldn't spawn child rockets.
                        foreach (Rocket child in childRockets)
                        {
                            RocketManager.DestroyRocket(child, DestructionReason.Intentional);
                        }
                        return;
                    }

                    LocalRocket localRocket = LocalManager.syncedRockets[rocketId];
                    RocketState localState = localRocket.ToState();
                    foreach (Rocket child in childRockets)
                    {
                        LocalRocket localChild = new LocalRocket(child);
                        RocketState localChildState = localChild.ToState();
                        int localId = LocalManager.unsyncedRockets.InsertNew(localChild);

                        // TODO: This is a "temorary" fix for the weird "duplicating part" bug.
                        // * Remove seperated parts from the parent rocket.
                        foreach (KeyValuePair<int, Part> kvp in localChild.parts)
                        {
                            int id = localRocket.GetPartID(kvp.Value);
                            bool succ = localState.RemovePart(id);
                            Debug.Log($"{id} was succ? {succ}");

                        }

                        // * Sync new child rockets with the server.
                        ClientManager.SendPacket
                        (
                            new Packet_CreateRocket()
                            {
                                LocalId = localId,
                                Rocket = localChildState,
                            }
                        );
                    }

                    // * Sync the parent rocket with the server.
                    ClientManager.SendPacket
                    (
                        new Packet_CreateRocket()
                        {
                            GlobalId = rocketId,
                            Rocket = localState,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Sends `UpdateStaging` packets after the staging of a rocket has been changed.
        /// </summary>
        [HarmonyPatch]
        public class StagingUpdates
        {
            static IEnumerable<MethodBase> TargetMethods()
            {
                string[] methods = new[]
                {
                    "AddStage",
                    "UseStage",
                    "OnReorder",
                    "RemoveStage",
                    "TogglePartSelected",
                };
                return methods.Select(n => AccessTools.Method(typeof(StagingDrawer), n)).Cast<MethodBase>();
            }

            public static void Postfix(StagingDrawer __instance)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    Staging staging = __instance.FieldRef<Staging_Local>("staging").Value;
                    int rocketId = LocalManager.GetLocalRocketID(staging.rocket);
                    if (GameManager.main != null && LocalManager.syncedRockets.TryGetValue(rocketId, out LocalRocket rocket))
                    {
                        List<StageState> stages = new List<StageState>(staging.stages.Count);
                        foreach (Stage stage in staging.stages)
                        {
                            StageState state = new StageState()
                            {
                                stageID = stage.stageId,
                                partIDs = stage.parts.Select(rocket.GetPartID).ToList(),
                            };
                            stages.Add(state);
                        }
                        ClientManager.SendPacket
                        (
                            new Packet_UpdateStaging()
                            {
                                RocketId = rocketId,
                                Stages = stages,
                            }
                        );
                    }
                    else
                    {
                        Debug.LogError("Missing local rocket when trying to send staging update!");
                    }
                }
            }
        }
    }
}

