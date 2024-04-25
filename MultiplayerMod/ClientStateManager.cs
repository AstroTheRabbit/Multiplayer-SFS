using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using SFS.World;
using SFS.Parts;
using SFS.WorldBase;
using SFS.Parts.Modules;
using MultiplayerSFS.Common;
using MultiplayerSFS.Common.Packets;
using HarmonyLib;

namespace MultiplayerSFS.Mod.Networking
{
    public class ClientStateManager
    {
        public double worldTime;
        public Difficulty.DifficultyType difficulty;

        public Dictionary<int, RocketState> rockets;
        public Dictionary<int, PartState> parts;

        public Dictionary<int, Rocket> localRockets = new Dictionary<int, Rocket>();
        public Dictionary<int, Part> localParts = new Dictionary<int, Part>();

        public ClientStateManager(LoadWorldPacket packet)
        {
            worldTime = packet.worldTime;
            difficulty = packet.difficulty;
            rockets = packet.rockets;
            parts = packet.parts;
        }

        public void ClearLocal()
        {
            localRockets = new Dictionary<int, Rocket>();
            localParts = new Dictionary<int, Part>();
        }

        public void LoadRocket(int rocketID)
        {

            if (rockets.TryGetValue(rocketID, out RocketState rocketState))
            {
                Part[] createdParts = new Part[rocketState.parts.Count];
                Dictionary<int, int> partIDsToIndices = new Dictionary<int, int>();
                for (int i = 0; i < rocketState.parts.Count; i++)
                {
                    int partID = rocketState.parts[i];
                    createdParts[i] = PartsLoader.CreatePart(
                        parts[partID].ToPartSave(),
                        null,
                        null,
                        OnPartNotOwned.Allow,
                        out OwnershipState _
                    );
                    localParts.Add(partID, createdParts[i]);
                    partIDsToIndices.Add(partID, i);
                }

                Rocket rocket = Object.Instantiate(AccessTools.StaticFieldRefAccess<RocketManager, Rocket>("prefab"));
                rocket.rocketName = rocketState.name;
                rocket.throttle.throttleOn.Value = rocketState.throttleOn;
                rocket.throttle.throttlePercent.Value = rocketState.throttlePercent;
                rocket.arrowkeys.rcs.Value = rocketState.RCS;

                var jointGroup = new JointGroup(
                    rocketState.joints.Select((JointState a) => new PartJoint(
                        createdParts[partIDsToIndices[a.partID_A]],
                        createdParts[partIDsToIndices[a.partID_B]],
                        createdParts[partIDsToIndices[a.partID_B]].Position - createdParts[partIDsToIndices[a.partID_A]].Position
                    )).ToList(),
                    createdParts.ToList());
                rocket.SetJointGroup(jointGroup);

                rocket.rb2d.transform.eulerAngles = new Vector3(0f, 0f, rocketState.position.rotation);
                rocket.physics.SetLocationAndState(rocketState.position.ToLocation(), false);
                rocket.rb2d.angularVelocity = rocketState.position.angularVelocity;

                localRockets.Add(rocketID, rocket);
            }
        }

        // public void UpdateRocket(UpdateRocketPacket change)
        // {

        // }
    }
}