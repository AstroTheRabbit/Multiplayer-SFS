using HarmonyLib;
using MultiplayerSFS.Common;
using SFS.Parts;
using SFS.Parts.Modules;
using SFS.World;

// ! TODO
// UpdatePart_SplitModule,
// WHEELS,
// RESOURCES,

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Patches for syncing part updates e.g. activation via staging, etc.
    /// </summary>
    public class PartUpdateSyncing
    {
        /// <summary>
        /// Syncs the docking of rockets.
        /// </summary>
        [HarmonyPatch(typeof(RocketManager), nameof(RocketManager.MergeRockets))]
        public class RocketManager_MergeRockets
        {
            public static void Prefix(Rocket rocket_A, Part part_A, Rocket rocket_B, Part part_B, out int __state)
            {
                if (rocket_A == rocket_B || !ClientManager.multiplayerEnabled)
                {
                    __state = -1;
                    return;
                }
                int id_a = LocalManager.GetLocalRocketID(rocket_A);
                int id_b = LocalManager.GetLocalRocketID(rocket_B);
                __state = id_a;

                ClientManager.SendPacket
                (
                    new Packet_DestroyRocket()
                    {
                        Id = id_b,
                    }
                );
            }
            public static void Postfix(Rocket rocket_A, Part part_A, Rocket rocket_B, Part part_B, int __state)
            {
                int id_a = __state;
                if (id_a == -1)
                    return;

                ClientManager.SendPacket
                (
                    new Packet_CreateRocket()
                    {
                        GlobalId = id_a,
                        Rocket = LocalManager.syncedRockets[id_a].ToState(),
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

                void OnToggle(bool engineOn)
                {    
                    Rocket rocket = __instance.GetComponentInParentTree<Rocket>();
                    int rocketId = LocalManager.GetLocalRocketID(rocket);

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
                            EngineOn = engineOn,
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
                    int rocketId = LocalManager.GetLocalRocketID(rocket);

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
    }
}