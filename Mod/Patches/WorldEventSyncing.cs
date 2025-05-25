using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS.UI;
using SFS.World;
using SFS.Parts;
using MultiplayerSFS.Common;
using SFS.Variables;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Patches related to syncing world events with the multiplayer server.
    /// </summary>
    public class WorldEventSyncing
    {
        /// <summary>
        /// Syncs the newly created rockets of a launched blueprint with the multiplayer server.
        /// </summary>
        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.SpawnBlueprint))]
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
                // TODO: This should probably be replaced with regular OpCode calls to load `ClientManager.multiplayerEnabled.Value`.
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
                            WorldTime = ClientManager.world.WorldTime,
                            LocalId = localId,
                            ForLaunch = true,
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
            public static bool Prefix(Part __instance, bool createExplosion, ref DestructionReason reason)
            {
                if (ClientManager.multiplayerEnabled && GameManager.main != null)
                {
                    if (reason == LocalManager.CustomDestructionReason)
                    {
                        reason = LocalManager.TrueDestructionReason;
                        return true;
                    }

                    int rocketId = LocalManager.GetSyncedRocketID(__instance.Rocket);
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
                        ClientManager.world.rockets[rocketId].RemovePart(partId);
                        ClientManager.SendPacket
                        (
                            new Packet_DestroyPart()
                            {
                                WorldTime = ClientManager.world.WorldTime,
                                RocketId = rocketId,
                                PartId = partId,
                                CreateExplosion = createExplosion,
                                Reason = reason,
                            }
                        );
                        return true;
                    }
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.DestroyRocket))]
        public static class RocketManager_DestroyRocket
        {
            public static bool Prefix(Rocket rocket, ref DestructionReason reason)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    if (reason == LocalManager.CustomDestructionReason)
                    {
                        reason = LocalManager.TrueDestructionReason;
                        return true;
                    }

                    int id = LocalManager.GetSyncedRocketID(rocket);
                    // * `DestructionReason.Intentional` is a special case because of stuff like docking,
                    // * where players that may not have update authority still need to be able to send a `DestroyRocket` packet.
                    if (id >= 0 && (reason == DestructionReason.Intentional || LocalManager.updateAuthority.Contains(id)))
                    {
                        ClientManager.world.rockets.Remove(id);
                        LocalManager.syncedRockets.Remove(id);
                        LocalManager.updateAuthority.Remove(id);
                        ClientManager.SendPacket
                        (
                            new Packet_DestroyRocket()
                            {
                                WorldTime = ClientManager.world.WorldTime,
                                RocketId = id,
                                Reason = reason,
                            }
                        );
                        return true;
                    }
                    return false;
                }
                return true;
            }

            public static void Postfix(Rocket rocket)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    if (PlayerController.main.player.Value is Rocket newRocket && newRocket != rocket)
                    {
                        int id = LocalManager.GetSyncedRocketID(newRocket);
                        if (id >= 0)
                        {
                            ClientManager.SendPacket
                            (
                                new Packet_UpdatePlayerControl()
                                {
                                    PlayerId = ClientManager.playerId,
                                    RocketId = id,
                                }
                            );
                            return;
                        }
                        id = LocalManager.GetUnsyncedRocketID(newRocket);
                        if (id >= 0)
                        {
                            LocalManager.unsyncedToControl = id;
                            return;
                        }
                        Debug.LogWarning("`RocketManager_DestroyRocket`: Player is controlling unregistered rocket!");
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
            public static void Postfix(Rocket rocket, List<Rocket> childRockets)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    int rocketId = LocalManager.GetSyncedRocketID(rocket);
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
                            RocketManager.DestroyRocket(child, LocalManager.CustomDestructionReason);
                        }
                        return;
                    }

                    // * Remove parts that have been seperated from the parent rocket (also prevents a bug where split modules "delete" their original part).
                    LocalRocket localRocket = LocalManager.syncedRockets[rocketId];
                    localRocket.parts.Clear();
                    foreach (Part part in rocket.partHolder.partsSet)
                    {
                        localRocket.parts.InsertNew(part);
                    }

                    // * Sync the parent rocket with the server.
                    RocketState localState = localRocket.ToState();
                    ClientManager.world.rockets[rocketId] = localState;
                    ClientManager.SendPacket
                    (
                        new Packet_CreateRocket()
                        {
                            WorldTime = ClientManager.world.WorldTime,
                            GlobalId = rocketId,
                            Rocket = localState,
                        }
                    );

                    foreach (Rocket child in childRockets)
                    {
                        LocalRocket localChild = new LocalRocket(child);
                        RocketState localChildState = localChild.ToState();
                        int localId = LocalManager.unsyncedRockets.InsertNew(localChild);

                        // * Sync new child rockets with the server.
                        ClientManager.SendPacket
                        (
                            new Packet_CreateRocket()
                            {
                                WorldTime = ClientManager.world.WorldTime,
                                LocalId = localId,
                                Rocket = localChildState,
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
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(StagingDrawer), "AddStage");
                yield return AccessTools.Method(typeof(StagingDrawer), "UseStage");
                yield return AccessTools.Method(typeof(StagingDrawer), "OnReorder");
                yield return AccessTools.Method(typeof(StagingDrawer), "RemoveStage");
                yield return AccessTools.Method(typeof(StagingDrawer), "TogglePartSelected");
            }

            public static void Postfix(StagingDrawer __instance)
            {
                if (ClientManager.multiplayerEnabled)
                {
                    Staging staging = __instance.FieldRef<Staging_Local>("staging").Value;
                    int rocketId = LocalManager.GetSyncedRocketID(staging.rocket);
                    if (GameManager.main != null && LocalManager.syncedRockets.TryGetValue(rocketId, out LocalRocket rocket))
                    {
                        List<StageState> stages = new List<StageState>(staging.stages.Count);
                        foreach (Stage stage in staging.stages)
                        {
                            StageState state = new StageState()
                            {
                                stageID = stage.stageId,
                                partIDs = stage.parts.Select(rocket.GetPartID).Where(id => id >= 0).ToList(),
                            };
                            stages.Add(state);
                        }
                        ClientManager.world.rockets[rocketId].stages = stages;
                        ClientManager.SendPacket
                        (
                            new Packet_UpdateStaging()
                            {
                                WorldTime = ClientManager.world.WorldTime,
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
        /// Syncs the vanilla game's world time with that of the multiplayer client.
        /// </summary>
        [HarmonyPatch(typeof(WorldTime), "Update")]
        public static class WorldTime_Update
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                Label label_multiplayerStart = generator.DefineLabel();
                Label label_multiplayerEnd = generator.DefineLabel();

                bool found_ldarg = false;
                bool found_stfld = false;

                foreach (CodeInstruction code in instructions)
                {
                    if (found_ldarg && code.opcode == OpCodes.Ldarg_0)
                    {
                        yield return code.WithLabels(label_multiplayerStart);
                        found_ldarg = false;
                    }
                    else if (found_stfld && code.opcode == OpCodes.Ldarg_0)
                    {
                        yield return code.WithLabels(label_multiplayerEnd);
                        found_stfld = false;
                    }
                    else
                    {
                        yield return code;
                    }
                    if (code.opcode == OpCodes.Stloc_0)
                    {
                        // * Load `ClientManager.multiplayerEnabled.Value`.
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ClientManager), nameof(ClientManager.multiplayerEnabled)));
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Obs<bool>), nameof(Obs<bool>.Value)));
                        // * Branch to vanilla code if multiplayer isn't enabled.
                        yield return new CodeInstruction(OpCodes.Brfalse, label_multiplayerStart);
                        // * Load `ClientManager.world.WorldTime`.
                        yield return new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(ClientManager), nameof(ClientManager.world)));
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(WorldState), nameof(WorldState.WorldTime)));
                        yield return new CodeInstruction(OpCodes.Stloc_1);
                        // * Store `ClientManager.world.WorldTime` in `WorldTime.worldTime`.
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Ldloc_1);
                        yield return new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(WorldTime), nameof(WorldTime.worldTime)));
                        // * Branch to remaining vanilla code.
                        yield return new CodeInstruction(OpCodes.Br, label_multiplayerEnd);
                        found_ldarg = true;

                    }
                    if (code.opcode == OpCodes.Stfld)
                    {
                        found_stfld = true;
                    }
                }
            }
        }

        /// <summary>
        /// Prevents strange behaviour for interpolated rockets (falling through terrain, etc).
        /// </summary>
        [HarmonyPatch(typeof(Rocket), "SFS.World.I_Physics.OnFixedUpdate")]
        public static class Rocket_OnFixedUpdate
        {
            public static bool Prefix(Rocket __instance)
            {
                return !(ClientManager.multiplayerEnabled && __instance.rb2d.bodyType != RigidbodyType2D.Dynamic);
            }
        }
    }
}

