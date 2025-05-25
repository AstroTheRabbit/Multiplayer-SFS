using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using SFS.World;
using SFS.Parts;
using SFS.Parts.Modules;
using MultiplayerSFS.Common;

namespace MultiplayerSFS.Mod
{
    public class Interpolator : MonoBehaviour
    {
        // TODO: Make this a dynamic value; set so that 2-3 primary packets are kept buffered at all times.
        public static double TimeDelay => 2 * LocalManager.updateRocketsPeriod / 1000;
        public static double DelayedWorldTime => ClientManager.world.WorldTime - TimeDelay;
        public const int MaxBuffer = 10;

        public LocalRocket rocket;
        public Packet_UpdateRocketPrimary currentUpdate;
        /// <summary>
        /// Buffer of update packets used for interpolating position, velocity, etc.
        /// </summary>
        public Queue<Packet_UpdateRocketPrimary> updateBuffer = new Queue<Packet_UpdateRocketPrimary>();
        /// <summary>
        /// Buffer of packets which is used to update local rocket at the correct time, taking into account `TimeDelay`.
        /// </summary>
        public List<(double, Packet)> packetBuffer = new List<(double, Packet)>();

        public static void AddPacketToQueue(Packet packet, int rocketId, double worldTime)
        {
            if (LocalManager.syncedRockets.TryGetValue(rocketId, out LocalRocket rocket) && rocket.interpolator is Interpolator interpolator)
            {
                if (interpolator.rocket == null)
                {
                    interpolator.rocket = rocket;
                    interpolator.currentUpdate = rocket.rocket.ToUpdatePacketPrimary(rocketId);
                }

                if (packet is Packet_UpdateRocketPrimary updatePacket)
                {
                    if (interpolator.updateBuffer.Count < MaxBuffer)
                    {
                        interpolator.updateBuffer.Enqueue(updatePacket);
                    }
                }
                else
                {
                    interpolator.packetBuffer.Add((worldTime, packet));
                }
            }
        }

        void Update()
        {
            if (currentUpdate == null)
            {
                // * This interpolator hasn't recieved any packets yet.
                return;
            }

            if (LocalManager.updateAuthority.Contains(currentUpdate.RocketId))
            {
                // * The player has update authority over this rocket, so no interpolation is required.
                // * All queued packets should be run to give the player with the latest rocket state.
                rocket.rocket.rb2d.bodyType = RigidbodyType2D.Dynamic;
                // rocket.rocket.rb2d.interpolation = RigidbodyInterpolation2D.None;
                RunAllPackets();
                currentUpdate = rocket.rocket.ToUpdatePacketPrimary(currentUpdate.RocketId);
                return;
            }
            else
            {
                rocket.rocket.rb2d.bodyType = RigidbodyType2D.Kinematic;
                // rocket.rocket.rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            }

            Debug.Log($"update: {updateBuffer.Count}");
            Debug.Log($"packet: {packetBuffer.Count}");

            while (updateBuffer.Count > 0)
            {
                Packet_UpdateRocketPrimary prev = currentUpdate;
                Packet_UpdateRocketPrimary next = updateBuffer.First();

                if (DelayedWorldTime > next.WorldTime)
                {
                    // * The current packet is now "out of date".
                    currentUpdate = updateBuffer.Dequeue();
                    continue;
                }
                
                // * The current packet is now "up to date" and the location of the rocket can be set via interpolation.
                InterpolatePackets(prev, next);
                break;
            }

            // * Run and remove any packets that have passed their world time.
            packetBuffer.RemoveAll
            (
                ((double time, Packet packet) tuple) =>
                {
                    if (double.IsNaN(tuple.time))
                    {
                        Debug.LogError($"Interpolator Error: WorldTime of `{tuple.packet.Type}` packet has not been set!");
                        return true;
                    }
                    if (tuple.time >= DelayedWorldTime)
                    {
                        RunPacket(tuple.packet);
                        return true;
                    }
                    return false;
                }
            );
        }

        void InterpolatePackets(Packet_UpdateRocketPrimary prev, Packet_UpdateRocketPrimary next)
        {
            if (prev.Location.address != next.Location.address)
            {
                // * If the rocket has changed planet, skip interpolation to avoid incorrect positioning.
                SetState(next.Location.ToVanillaLocation(), next.Rotation, next.AngularVelocity);
                return;
            }

            // * Hermite spline interpolation, with linear interpolation for rotation:
            // ? https://gafferongames.com/post/snapshot_interpolation/
            // ? https://en.wikipedia.org/wiki/Hermite_interpolation & https://en.wikipedia.org/wiki/Cubic_Hermite_spline

            double dt = DelayedWorldTime - prev.WorldTime;
            double t = dt / (next.WorldTime - prev.WorldTime);
            // Debug.Log($"t: {t}\t dt: {dt}");
            double t2 = t * t;
            double t3 = t2 * t;

            Double2 p0 = prev.Location.position;
            Double2 v0 = prev.Location.velocity;
            Double2 p1 = next.Location.position;
            Double2 v1 = next.Location.velocity;

            // Hermite basis functions
            double h00 = (2 * t3) + (-3 * t2) + 1;
            double h10 = t3 + (-2 * t2) + t;
            double h01 = (-2 * t3) + (3 * t2);
            double h11 = t3 - t2;

            // TODO: Try graphing the various positions (interpolated, current, etc) over time just to get an understanding of what the jitter looks like at a lower level.

            Location loc = prev.Location.ToVanillaLocation();
            loc.position = (h00 * p0) + (h10 * v0) + (h01 * p1) + (h11 * v1);
            loc.velocity = Double2.Lerp(v0, v1, t);

            float rot = Mathf.LerpAngle(prev.Rotation, next.Rotation, (float) t);
            float angVel = Mathf.Lerp(prev.AngularVelocity, next.AngularVelocity, (float) t);

            SetState(loc, rot, angVel);
        }

        void SetState(Location loc, float rot, float angVel)
        {
            rocket.rocket.rb2d.transform.eulerAngles = new Vector3(0, 0, rot);
            rocket.rocket.rb2d.angularVelocity = angVel;

            if (rocket.rocket.physics.PhysicsMode)
            {
                (rocket.rocket as I_Physics).LocalPosition = WorldView.ToLocalPosition(loc.position);
                (rocket.rocket as I_Physics).LocalVelocity = WorldView.ToLocalVelocity(loc.velocity);
            }
            else
            {
                rocket.rocket.physics.SetLocationAndState(loc, false);
            }
        }

        void RunAllPackets()
        {
            foreach ((double _, Packet packet) in packetBuffer)
            {
                RunPacket(packet);
            }
            packetBuffer.Clear();

            Packet_UpdateRocketPrimary updatePacket = null;
            while (updateBuffer.Count > 0)
            {
                updatePacket = updateBuffer.Dequeue();
            }
            if (updatePacket != null)
                SetState(updatePacket.Location.ToVanillaLocation(), updatePacket.Rotation, updatePacket.AngularVelocity);
        }

        /// <summary>
        /// Returns true if the packet ran successfully and should be removed from the `packetBuffer`.
        /// </summary>
        void RunPacket(Packet packet)
        {
            switch (packet.Type)
            {
                case PacketType.UpdateRocketSecondary:
                    OnPacket_UpdateRocketSecondary(packet as Packet_UpdateRocketSecondary);
                    break;
                case PacketType.DestroyPart:
                    OnPacket_DestroyPart(packet as Packet_DestroyPart);
                    break;
                case PacketType.UpdateStaging:
                    OnPacket_UpdateStaging(packet as Packet_UpdateStaging);
                    break;
                case PacketType.UpdatePart_EngineModule:
                    OnPacket_UpdatePart_EngineModule(packet as Packet_UpdatePart_EngineModule);
                    break;
                case PacketType.UpdatePart_WheelModule:
                    OnPacket_UpdatePart_WheelModule(packet as Packet_UpdatePart_WheelModule);
                    break;
                case PacketType.UpdatePart_BoosterModule:
                    OnPacket_UpdatePart_BoosterModule(packet as Packet_UpdatePart_BoosterModule);
                    break;
                case PacketType.UpdatePart_ParachuteModule:
                    OnPacket_UpdatePart_ParachuteModule(packet as Packet_UpdatePart_ParachuteModule);
                    break;
                case PacketType.UpdatePart_MoveModule:
                    OnPacket_UpdatePart_MoveModule(packet as Packet_UpdatePart_MoveModule);
                    break;
                case PacketType.UpdatePart_ResourceModule:
                    OnPacket_UpdatePart_ResourceModule(packet as Packet_UpdatePart_ResourceModule);
                    break;
                default:
                    Debug.LogError($"Invalid packet type used in interpolator: {packet.Type}");
                    break;
            }
        }

        void OnPacket_UpdateRocketSecondary(Packet_UpdateRocketSecondary packet)
        {
            Arrowkeys arrowkeys = rocket.rocket.arrowkeys;
            arrowkeys.turnAxis.Value = packet.Input_Turn;
            arrowkeys.rawArrowkeysAxis.Value = packet.Input_Raw;
            arrowkeys.horizontalAxis.Value = packet.Input_Horizontal;
            arrowkeys.verticalAxis.Value = packet.Input_Vertical;
            arrowkeys.rcs.Value = packet.RCS;
            rocket.rocket.throttle.throttlePercent.Value = packet.ThrottlePercent;
            rocket.rocket.throttle.throttleOn.Value = packet.ThrottleOn;

        }

        void OnPacket_DestroyPart(Packet_DestroyPart packet)
        {
            if (rocket.parts.TryGetValue(packet.PartId, out Part localPart) && localPart != null)
            {
                LocalManager.TrueDestructionReason = packet.Reason;
                localPart.DestroyPart(packet.CreateExplosion, true, LocalManager.CustomDestructionReason);
            }

        }

        void OnPacket_UpdateStaging(Packet_UpdateStaging packet)
        {
            rocket.rocket.staging.ClearStages(false);
            foreach (StageState stage in packet.Stages)
            {
                List<Part> stageParts = stage.partIDs.Select(id => rocket.parts[id]).ToList();
                rocket.rocket.staging.InsertStage(new Stage(stage.stageID, stageParts), false);
            }

        }

        void OnPacket_UpdatePart_EngineModule(Packet_UpdatePart_EngineModule packet)
        {
            if (rocket.parts.TryGetValue(packet.PartId, out Part part))
            {
                EngineModule[] modules = part.GetModules<EngineModule>();
                if (modules.Length > 1)
                {
                    Debug.LogWarning($"OnPacket_UpdatePart_EngineModule: Found multiple engine modules on part \"{part.Name}\".");
                }
                modules[0].engineOn.Value = packet.EngineOn;
            }

        }

        void OnPacket_UpdatePart_WheelModule(Packet_UpdatePart_WheelModule packet)
        {
            if (rocket.parts.TryGetValue(packet.PartId, out Part part))
            {
                WheelModule[] modules = part.GetModules<WheelModule>();
                if (modules.Length > 1)
                {
                    Debug.LogWarning($"OnPacket_UpdatePart_WheelModule: Found multiple wheel modules on part \"{part.Name}\".");
                }
                modules[0].on.Value = packet.WheelOn;
            }

        }

        void OnPacket_UpdatePart_BoosterModule(Packet_UpdatePart_BoosterModule packet)
        {
            if (rocket.parts.TryGetValue(packet.PartId, out Part part))
            {
                BoosterModule[] modules = part.GetModules<BoosterModule>();
                if (modules.Length > 1)
                {
                    Debug.LogWarning($"OnPacket_UpdatePart_BoosterModule: Found multiple booster modules on part \"{part.Name}\".");
                }
                modules[0].boosterPrimed.Value = packet.Primed;
                modules[0].throttle_Out.Value = packet.Throttle;
                modules[0].fuelPercent.Value = packet.FuelPercent;
            }

        }

        void OnPacket_UpdatePart_ParachuteModule(Packet_UpdatePart_ParachuteModule packet)
        {
            if (rocket.parts.TryGetValue(packet.PartId, out Part part))
            {
                ParachuteModule[] modules = part.GetModules<ParachuteModule>();
                if (modules.Length > 1)
                {
                    Debug.LogWarning($"OnPacket_UpdatePart_ParachuteModule: Found multiple parachute modules on part \"{part.Name}\".");
                }
                modules[0].state.Value = packet.State;
                modules[0].targetState.Value = packet.TargetState;
            }

        }

        void OnPacket_UpdatePart_MoveModule(Packet_UpdatePart_MoveModule packet)
        {
            if (rocket.parts.TryGetValue(packet.PartId, out Part part))
            {
                MoveModule[] modules = part.GetModules<MoveModule>();
                if (modules.Length > 1)
                {
                    Debug.LogWarning($"OnPacket_UpdatePart_MoveModule: Found multiple move modules on part \"{part.Name}\".");
                }
                modules[0].time.Value = packet.Time;
                modules[0].targetTime.Value = packet.TargetTime;
            }

        }

        void OnPacket_UpdatePart_ResourceModule(Packet_UpdatePart_ResourceModule packet)
        {
            foreach (int partId in packet.PartIds)
            {
                if (rocket.parts.TryGetValue(partId, out Part part))
                {
                    ResourceModule[] modules = part.GetModules<ResourceModule>();
                    if (modules.Length > 1)
                    {
                        Debug.LogWarning($"OnPacket_UpdatePart_ResourceModule: Found multiple resource modules on part \"{part.Name}\".");
                    }
                    modules[0].resourcePercent.Value = packet.ResourcePercent;
                }
            }

        }
    }
}