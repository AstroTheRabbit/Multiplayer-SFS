using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS.World;
using SFS.Parts;
using SFS.Parts.Modules;
using MultiplayerSFS.Common;

// ! TODO
// RESOURCES,
// Burnmarks

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Patches for syncing part updates e.g. activation via staging, etc.
    /// </summary>
    public class PartUpdateSyncing
    {
        /// <summary>
        /// Prevents two rockets from docking if they are both controlled by players, or if one of the rockets isn't synced yet.
        /// </summary>
        [HarmonyPatch(typeof(DockingPortModule), "Dock")]
        public static class DockingPortModule_Dock
        {
            public static bool Prefix(DockingPortModule __instance, DockingPortModule otherPort)
            {
                if (otherPort.Rocket.isPlayer)
                {
                    return false;
                }
                if (ClientManager.multiplayerEnabled)
                {
                    int id_a = LocalManager.GetSyncedRocketID(__instance.Rocket);
                    int id_b = LocalManager.GetSyncedRocketID(otherPort.Rocket);

                    if (id_a == -1 || id_b == -1)
                    {
                        // * One of the rockets are not synced with the server yet.
                        return false;
                    }

                    bool controlled_a = false;
                    bool controlled_b = false;

                    foreach (KeyValuePair<int, LocalPlayer> kvp in LocalManager.players)
                    {
                        controlled_a |= kvp.Value.currentRocket == id_a;
                        controlled_b |= kvp.Value.currentRocket == id_b;

                        if (controlled_a && controlled_b)
                        {
                            // * Both of the rockets are controlled by players.
                            return false;
                        }
                    }
                }
                return true;
            }
        }

        /// <summary>
        /// Syncs the docking of rockets.
        /// </summary>
        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.MergeRockets))]
        public class RocketManager_MergeRockets
        {
            public static void Prefix(Rocket rocket_A, Rocket rocket_B)
            {
                if (!ClientManager.multiplayerEnabled || rocket_A == rocket_B)
                {
                    return;
                }

                int id_a = LocalManager.GetSyncedRocketID(rocket_A);
                int id_b = LocalManager.GetSyncedRocketID(rocket_B);
                // * Both rockets are guaranteed to be synced by the `DockingPortModule_Dock` patch.
                LocalRocket local_a = LocalManager.syncedRockets[id_a];
                LocalRocket local_b = LocalManager.syncedRockets[id_b];


                foreach (KeyValuePair<int, Part> kvp in local_a.parts)
                {
                    local_b.parts.InsertNew(kvp.Value);
                }
                // ? Rocket B's destruction is handled by the `RocketManager_DestroyRocket` patch.

                if (rocket_B.isPlayer)
                {
                    ClientManager.SendPacket
                    (
                        new Packet_UpdatePlayerControl()
                        {
                            PlayerId = ClientManager.playerId,
                            RocketId = id_a,
                        }
                    );
                }

                ClientManager.SendPacket
                (
                    new Packet_CreateRocket()
                    {
                        GlobalId = id_a,
                        Rocket = local_a.ToState(),
                    }
                );
            }
        }

        /// <summary>
        /// Syncs the toggling of an `EngineModule`.
        /// </summary>
        [HarmonyPatch(typeof(EngineModule), "Start")]
        public class EngineModule_Start
        {
            public static void Postfix(EngineModule __instance)
            {
                if (GameManager.main != null && ClientManager.multiplayerEnabled)
                {
                    __instance.engineOn.OnChange += OnToggle;
                }

                void OnToggle(bool engineOn_old, bool engineOn_new)
                {    
                    if (engineOn_old == engineOn_new)
                        return;

                    Rocket rocket = __instance.GetComponentInParentTree<Rocket>();
                    int rocketId = LocalManager.GetSyncedRocketID(rocket);

                    if (!LocalManager.updateAuthority.Contains(rocketId))
                        return;

                    Part part = __instance.GetComponentInParent<Part>();
                    int partId = LocalManager.GetLocalPartID(rocketId, part);

                    ClientManager.SendPacket
                    (
                        new Packet_UpdatePart_EngineModule()
                        {
                            RocketId = rocketId,
                            PartId = partId,
                            EngineOn = engineOn_new,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Syncs the variables of a `BoosterModule`.
        /// </summary>
        [HarmonyPatch]
        public class BoosterModuleUpdates
        {
            public static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(BoosterModule), "FixedUpdate");
                yield return AccessTools.Method(typeof(BoosterModule), nameof(BoosterModule.Fire));
                yield return AccessTools.Method(typeof(BoosterModule), nameof(BoosterModule.Fire_Instantly));
            }

            public static void Prefix(BoosterModule __instance, out (bool primed, float throttle) __state)
            {
                __state.primed = __instance.boosterPrimed.Value;
                __state.throttle = __instance.throttle_Out.Value;
            }

            public static void Postfix(BoosterModule __instance, (bool primed, float throttle) __state, MethodBase __originalMethod)
            {
                bool primed = __instance.boosterPrimed.Value;
                float throttle = __instance.throttle_Out.Value;
                Debug.Log("A");
                if (ClientManager.multiplayerEnabled && GameManager.main != null && (primed != __state.primed || throttle != __state.throttle))
                {
                Debug.Log(__originalMethod.FullDescription());
                Debug.Log($"{__state.primed} -> {primed}");
                Debug.Log($"{__state.throttle} -> {throttle}");
                Debug.Log("B");
                    Rocket rocket = __instance.GetComponentInParentTree<Rocket>();
                    int rocketId = LocalManager.GetSyncedRocketID(rocket);

                Debug.Log("C");
                    if (!LocalManager.updateAuthority.Contains(rocketId))
                        return;

                Debug.Log("D");
                    Part part = __instance.GetComponentInParent<Part>();
                    int partId = LocalManager.GetLocalPartID(rocketId, part);

                Debug.Log("E");
                    ClientManager.SendPacket
                    (
                        new Packet_UpdatePart_BoosterModule()
                        {
                            RocketId = rocketId,
                            PartId = partId,
                            Primed = primed,
                            Throttle = throttle,
                            FuelPercent = __instance.fuelPercent.Value,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Syncs the toggling of a `WheelModule`.
        /// </summary>
        [HarmonyPatch(typeof(WheelModule), nameof(WheelModule.ToggleEnabled))]
        public static class WheelModule_ToggleEnabled
        {
            public static void Postfix(WheelModule __instance)
            {
                if (GameManager.main != null && ClientManager.multiplayerEnabled)
                {
                    Rocket rocket = __instance.GetComponentInParentTree<Rocket>();
                    int rocketId = LocalManager.GetSyncedRocketID(rocket);

                    if (!LocalManager.updateAuthority.Contains(rocketId))
                        return;

                    Part part = __instance.GetComponentInParent<Part>();
                    int partId = LocalManager.GetLocalPartID(rocketId, part);

                    ClientManager.SendPacket
                    (
                        new Packet_UpdatePart_WheelModule()
                        {
                            RocketId = rocketId,
                            PartId = partId,
                            WheelOn = __instance.on.Value,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Syncs the target state of a `ParachuteModule`.
        /// </summary>
        [HarmonyPatch(typeof(ParachuteModule), "Start")]
        public class ParachuteModule_Start
        {
            public static void Postfix(ParachuteModule __instance)
            {
                if (GameManager.main != null && ClientManager.multiplayerEnabled)
                {
                    __instance.targetState.OnChange += UpdateState;
                }
                
                void UpdateState()
                {    
                    Rocket rocket = __instance.GetComponentInParentTree<Rocket>();
                    int rocketId = LocalManager.GetSyncedRocketID(rocket);

                    if (!LocalManager.updateAuthority.Contains(rocketId))
                        return;

                    Part part = __instance.GetComponentInParent<Part>();
                    int partId = LocalManager.GetLocalPartID(rocketId, part);

                    ClientManager.SendPacket
                    (
                        new Packet_UpdatePart_ParachuteModule()
                        {
                            RocketId = rocketId,
                            PartId = partId,
                            State = __instance.state.Value,
                            TargetState = __instance.targetState.Value,
                        }
                    );
                }
            }
        }

        /// <summary>
        /// Synchronises the use of `MoveModule.Toggle` e.g. landing legs and solar panels.
        /// </summary>
        [HarmonyPatch(typeof(MoveModule), nameof(MoveModule.Toggle))]
        public static class MoveModule_Toggle
        {
            public static void Postfix(MoveModule __instance)
            {
                if (GameManager.main != null && ClientManager.multiplayerEnabled)
                {
                    Rocket rocket = __instance.GetComponentInParentTree<Rocket>();
                    int rocketId = LocalManager.GetSyncedRocketID(rocket);

                    if (!LocalManager.updateAuthority.Contains(rocketId))
                        return;

                    Part part = __instance.GetComponentInParent<Part>();
                    int partId = LocalManager.GetLocalPartID(rocketId, part);

                    ClientManager.SendPacket
                    (
                        new Packet_UpdatePart_MoveModule()
                        {
                            RocketId = rocketId,
                            PartId = partId,
                            Time = __instance.time.Value,
                            TargetTime = __instance.targetTime.Value,
                        }
                    );
                }
            }
        }
    }
}