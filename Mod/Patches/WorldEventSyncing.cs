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
                        if (rocketId == -1 || LocalManager.players.Values.Any((LocalPlayer player) => player.currentRocket == rocketId))
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
        public class RocketManager_SpawnBlueprint
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                // ? Replaces `Rocket rocket = array3.FirstOrDefault((Rocket a) => a.hasControl.Value); ...` with `Extension`.
                foreach (CodeInstruction code in instructions)
                {
                    yield return code;
                    if (code.opcode == OpCodes.Ldloc_3)
                    {
                        yield return CodeInstruction.Call(typeof(RocketManager_SpawnBlueprint), nameof(SyncLaunch));
                        yield return new CodeInstruction(OpCodes.Ret);
                        break;
                    }
                }
            }

            public static void SyncLaunch(Rocket[] rockets)
            {
                Debug.Log("SYNC START");
                Menu.loading.Open("Sending launch request to server...");
                Dictionary<Rocket, int> locals = new Dictionary<Rocket, int>(rockets.Length);
                foreach (Rocket rocket in rockets)
                {
                    int localId = LocalManager.unsyncedRockets.InsertNew(rocket);
                    RocketState state = new RocketState(new RocketSave(rocket));
                    ClientManager.SendPacket
                    (
                        new Packet_CreateRocket()
                        {
                            PlayerId = ClientManager.playerId,
                            LocalId = localId,
                            Rocket = state,
                        }
                    );
                    locals.Add(rocket, localId);
                }
                Rocket controllable = rockets.FirstOrDefault((Rocket r) => r.hasControl.Value);
                Rocket toControl = controllable ?? ((rockets.Length != 0) ? rockets[0] : null);
                LocalManager.unsyncedToControl = toControl != null && locals.TryGetValue(toControl, out int idToControl) ? idToControl : -1;
                Debug.Log("SYNC CONT");
            }
        }

        /// <summary>
        /// Reverse patch of `Rocket.InjectPartDependencies`, so it can be called in `LocalManager.UpdateLocalPart`.
        /// </summary>
        [HarmonyPatch(typeof(Rocket), "InjectPartDependencies")]
        public class Rocket_InjectPartDependencies
        {
            [HarmonyReversePatch]
            public static void InjectPartDependencies(Rocket rocket)
            {
                throw new NotImplementedException("Reverse patch error!");
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
                    // ? This mod uses `(DestructionReason) 4` as a way to signal that the server has told the client to destroy this part.
                    return reason == (DestructionReason) 4;
                }
                return true;
            }
        }

        /// <summary>
        /// Sends `UpdatePart` packets after each part has been used.
        /// </summary>
        [HarmonyPatch(typeof(Rocket), nameof(Rocket.UseParts))]
        public class Rocket_UseParts
        {
            public static void Postfix((Part, PolygonData)[] regions)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    foreach ((Part part, PolygonData _) in regions)
                    {
                        // * I don't think this needs a check to ensure the rocket is under this client's authority,
                        // * since (at least in vanilla) parts can't be activated by a rocket that isn't currently controlled.

                        int rocketId = LocalManager.GetLocalRocketID(part.Rocket);
                        int partId = LocalManager.GetLocalPartID(rocketId, part);
                        PartState state = new PartState(new PartSave(part));
                        ClientManager.SendPacket
                        (
                            new Packet_UpdatePart()
                            {
                                RocketId = rocketId,
                                PartId = partId,
                                NewPart = state
                            }
                        );
                    }
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

        /// <summary>
        /// Syncs the creation of 'child' rockets, which result from part destruction, split modules, etc.
        /// This patch also prevents child rockets from being created if the local player doesn't have update authority over the parent rocket.
        /// </summary>
        [HarmonyPatch(typeof(JointGroup), nameof(JointGroup.RecreateRockets))]
        public class JointGroup_RecreateRockets
        {
            public static bool Prefix(Rocket rocket)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    int rocketId = LocalManager.GetLocalRocketID(rocket);
                    return LocalManager.updateAuthority.Contains(rocketId);
                }
                return true;
            }
            public static void Postfix(Rocket rocket, List<Rocket> childRockets)
            {
                // ! TODO: This currently creates insane lag to the point of freezing SFS. I should probably change how unsynced rockets are managed so they aren't completely recreated when verified by the server.
                if (ClientManager.multiplayerEnabled)
                {
                    int rocketId = LocalManager.GetLocalRocketID(rocket);
                    ClientManager.SendPacket
                    (
                        new Packet_CreateRocket()
                        {
                            PlayerId = ClientManager.playerId,
                            GlobalId = rocketId,
                            Rocket = new RocketState(new RocketSave(rocket)),
                        }
                    );
                    foreach (Rocket cr in childRockets)
                    {
                        int localId = LocalManager.unsyncedRockets.InsertNew(cr);
                        RocketState state = new RocketState(new RocketSave(cr));
                        ClientManager.SendPacket
                        (
                            new Packet_CreateRocket()
                            {
                                PlayerId = ClientManager.playerId,
                                LocalId = localId,
                                Rocket = state,
                            }
                        );
                    }
                }
            }
        }
    }
}

