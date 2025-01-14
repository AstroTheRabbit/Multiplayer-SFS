using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS;
using SFS.UI;
using SFS.World;
using SFS.Parts;
using SFS.Builds;
using SFS.WorldBase;
using SFS.Parts.Modules;
using ModLoader.Helpers;
using MultiplayerSFS.Common;

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
        [HarmonyPatch(typeof(SceneLoader), nameof(SceneLoader.LoadWorldScene))]
        public static class SceneLoader_LoadWorldScene
        {
            static List<RocketState> launchedRockets;

            public static bool Prefix(bool launch)
            {
                if (ClientManager.multiplayerEnabled.Value && launch)
                {
                    if (!SavingCache.main.TryLoadBuildPersistent(MsgDrawer.main, out Blueprint buildPersistent, eraseCache: false))
                    {
                        MsgDrawer.main.Log("Failed to load blueprint");
                        return false;
                    }
                    launchedRockets = BlueprintToRocketStates(buildPersistent);

                    if (launchedRockets.Count == 0)
                    {
                        MsgDrawer.main.Log("Cannot launch an empty blueprint in multiplayer");
                        return false;
                    }
                    // TODO: Prevent launches if another player is currently on the launchpad.
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
            /// I would use a patched version of the original, but that relies on the rocket prefab which is only loaded in the world scene.
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

                        input_Turn = 0,
                        input_Raw = Vector2.zero,
                        input_Horizontal = Vector2.zero,
                        input_Vertical = Vector2.zero,

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
        /// Reverse patch of `RocketManager.GetSpawnLocation`, so it can be called in `SceneLoader_LoadWorldScene.SpawnBlueprint`.
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
}

