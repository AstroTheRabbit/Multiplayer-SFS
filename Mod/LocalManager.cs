using System;
using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using SFS.Parts;
using SFS.World;
using UnityEngine;
using MultiplayerSFS.Common;
using SFS.UI;
using SFS.Variables;

namespace MultiplayerSFS.Mod
{
    public class LocalManager : MonoBehaviour
    {
        static LocalManager main;
        public static Dictionary<int, LocalPlayer> players;
        public static Dictionary<int, LocalRocket> syncedRockets;
        public static HashSet<int> unsyncedRockets;
        /// <summary>
        /// The local id of the rocket that this player should be controlling after their launched rockets has been synced with the server.
        /// </summary>
        public static int unsyncedToControl;
        public static HashSet<int> updateAuthority;
        
        static Rocket RocketPrefab => AccessTools.StaticFieldRefAccess<Rocket>(typeof(RocketManager), "prefab");
        
        public const int UPDATE_TICKS = 5;
        public static int current_ticks = 0;

        public static void Initialize()
        {
            if (main == null)
            {
                GameObject go = new GameObject("MultiplayerSFS - Local Manager");
                DontDestroyOnLoad(go);
                main = go.AddComponent<LocalManager>();

                players = new Dictionary<int, LocalPlayer>();
                syncedRockets = new Dictionary<int, LocalRocket>();
                unsyncedRockets = new HashSet<int>();
                unsyncedToControl = -1;
                updateAuthority = new HashSet<int>();
            }
        }

        void Update()
        {
            // TODO: This tick counting method is kinda silly; should be replaced with a seperate update cycle that isn't determined by the game's FPS.
            if (current_ticks > 0)
            {
                current_ticks--;
                return;
            }
            current_ticks = UPDATE_TICKS;
            foreach (int id in updateAuthority)
            {
                if (syncedRockets.TryGetValue(id, out LocalRocket localRocket) && localRocket.rocket is Rocket rocket)
                {
                    ClientManager.SendPacket
                    (
                        new Packet_UpdateRocket()
                        {
                            Id = id,
                            Input_Turn = rocket.arrowkeys.turnAxis,
                            Input_Raw = rocket.arrowkeys.rawArrowkeysAxis,
                            Input_Horizontal = rocket.arrowkeys.horizontalAxis,
                            Input_Vertical = rocket.arrowkeys.verticalAxis,
                            Rotation = rocket.rb2d.transform.eulerAngles.z,
                            AngularVelocity = rocket.rb2d.angularVelocity,
                            ThrottlePercent = rocket.throttle.throttlePercent,
                            ThrottleOn = rocket.throttle.throttleOn,
                            RCS = rocket.arrowkeys.rcs,
                        }
                    );
                }
                else
                {
                    Debug.LogError("Missing local rocket while try to send update packets!");
                }
            }
        }

        /// <summary>
        /// Returns the id of the provided rocket, otherwise returns -1 if not found.
        /// </summary>
        public static int GetLocalRocketID(Rocket rocket)
        {
            try
            {
                return syncedRockets.First((KeyValuePair<int, LocalRocket> kvp) => kvp.Value.rocket == rocket).Key;
            }
            catch (InvalidOperationException)
            {
                return -1;
            }
        }

        public static void CreateRocket(Packet_CreateRocket packet)
        {
            unsyncedRockets.Remove(packet.LocalId);
            DestroyLocalRocket(packet.GlobalId);
            ClientManager.world.rockets[packet.GlobalId] = packet.Rocket;
            if (GameManager.main != null)
            {
                LocalRocket rocket = SpawnLocalRocket(packet.Rocket);
                syncedRockets.Add(packet.GlobalId, rocket);
                if (packet.LocalId == unsyncedToControl)
                {
                    PlayerController.main.player.Value = rocket.rocket;
                    GameCamerasManager.main.InstantlyRotateCamera();

                    unsyncedToControl = -1;
                    ClientManager.SendPacket(new Packet_UpdatePlayerControl() { PlayerId = ClientManager.playerId, RocketId = packet.GlobalId });
                    // * This loading screen is opened in the `SceneLoader_LoadWorldScene` patch.
                    Menu.loading.Close();
                }
            }
        }

        // ? Similar to `SFS.World.RocketManager.LoadRocket(RocketSave, ...)`.
        public static LocalRocket SpawnLocalRocket(RocketState state)
        {
            Dictionary<int, Part> parts = SpawnLocalParts(state);
            Rocket rocket = Instantiate(RocketPrefab);
            rocket.rocketName = state.rocketName;
            rocket.throttle.throttleOn.Value = state.throttleOn;
            rocket.throttle.throttlePercent.Value = state.throttlePercent;
            rocket.arrowkeys.rcs.Value = state.RCS;

            List<PartJoint> joints = new List<PartJoint>(state.joints.Count);
            foreach (JointState joint in state.joints)
            {
                Part part_A = parts[joint.id_A];
                Part part_B = parts[joint.id_B];
                joints.Add(new PartJoint(part_A, part_B, part_B.Position - part_A.Position));
            }
            rocket.SetJointGroup(new JointGroup(joints, parts.Values.ToList()));

            rocket.rb2d.transform.eulerAngles = new Vector3(0f, 0f, state.rotation);
            rocket.physics.SetLocationAndState(state.location.GetSaveLocation(WorldTime.main.worldTime), true);
            rocket.rb2d.angularVelocity = state.angularVelocity;

            foreach (StageState stage in state.stages)
            {
                List<Part> stageParts = stage.partIDs.Select((int id) => parts[id]).ToList();
                rocket.staging.InsertStage(new Stage(stage.stageID, stageParts), false);
            }
            return new LocalRocket(rocket, parts);
        }

        static Dictionary<int, Part> SpawnLocalParts(RocketState state)
        {
            Dictionary<int, Part> result = new Dictionary<int, Part>(state.parts.Count);
            foreach (KeyValuePair<int, PartState> kvp in state.parts)
            {
                Part part = PartsLoader.CreatePart(kvp.Value.part, null, null, OnPartNotOwned.Allow, out _);
                result.Add(kvp.Key, part);
            }
            return result;
        }

        static void DestroyLocalRocket(int id, DestructionReason reason = DestructionReason.Intentional)
        {
            if (syncedRockets.TryGetValue(id, out LocalRocket rocket) && rocket.rocket != null)
            {
                RocketManager.DestroyRocket(rocket.rocket, reason);
            }
            syncedRockets.Remove(id);
        }

        public static void OnLoadWorld()
        {
            foreach (KeyValuePair<int, RocketState> kvp in ClientManager.world.rockets)
            {
                DestroyLocalRocket(kvp.Key);
                LocalRocket rocket = SpawnLocalRocket(kvp.Value);
                syncedRockets.Add(kvp.Key, rocket);
            }
        }

        public static void DestroyRocket(int id, DestructionReason reason = DestructionReason.Intentional)
        {
            DestroyLocalRocket(id, reason);
            ClientManager.world.rockets.Remove(id);
        }

        public static void UpdateLocalRocket(Packet_UpdateRocket packet)
        {
            if (syncedRockets.TryGetValue(packet.Id, out LocalRocket rocket) && rocket.rocket != null)
            {
                Arrowkeys arrowkeys = rocket.rocket.arrowkeys;
                arrowkeys.rawArrowkeysAxis.Value = packet.Input_Raw;
                arrowkeys.horizontalAxis.Value = packet.Input_Horizontal;
                arrowkeys.verticalAxis.Value = packet.Input_Vertical;
                arrowkeys.rcs.Value = packet.RCS;

                rocket.rocket.rb2d.transform.eulerAngles = new Vector3(0f, 0f, packet.Rotation);
                rocket.rocket.rb2d.angularVelocity = packet.AngularVelocity;
                rocket.rocket.throttle.throttlePercent.Value = packet.ThrottlePercent;
                rocket.rocket.throttle.throttleOn.Value = packet.ThrottleOn;
                rocket.rocket.physics.SetLocationAndState(packet.Location.GetSaveLocation(WorldTime.main.worldTime), true);
            }
        }
    }

    public class LocalRocket
    {
        public Rocket rocket;
        public Dictionary<int, Part> parts;

        public LocalRocket(Rocket rocket, Dictionary<int, Part> parts)
        {
            this.rocket = rocket;
            this.parts = parts;
        }

        public Part GetPart(int id)
        {
            if (parts.TryGetValue(id, out Part res))
                return res;
            return null;
        }
    }

    public class LocalPlayer
    {
        public string username;
        public Int_Local currentRocket;

        public LocalPlayer(string username)
        {
            this.username = username;
            currentRocket = new Int_Local() { Value = -1 };
            currentRocket.OnChange += OnControlledRocketChange;
        }

        public void OnControlledRocketChange()
        {
            // TODO: Name tags above controlled rockets (on map as well?).
        }
    }
}