using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS.World;
using SFS.World.Drag;

namespace MultiplayerSFS.Mod
{
    public static class Extrapolation
    {
        const int iterationSteps = 100;

        static Location initLoc;
        static double mass;
        static double dragCoefficent;

        static void InitConstants(Rocket rocket, Location packetLoc)
        {
            float angle = (float) (packetLoc.velocity.AngleRadians - (Mathf.PI / 2));
            List<Surface> exposedSurfaces = AeroModule.GetExposedSurfaces(Aero_Rocket.GetDragSurfaces(rocket.partHolder, Matrix2x2.Angle(-angle)));
            (float coefficent, Vector2 _) = AeroModule_CalculateDragForce.CalculateDragForce(exposedSurfaces);
            
            initLoc = packetLoc;
            mass = rocket.rb2d.mass;
            dragCoefficent = coefficent;
        }

        static Double2 GetAcceleration(Double2 pos, Double2 vel)
        {
            Double2 result = initLoc.planet.GetGravity(pos);
            if (initLoc.planet.IsInsideAtmosphere(pos))
            {
                double dragMult = 1.5 * dragCoefficent * vel.sqrMagnitude;
                double atmoDensity = initLoc.planet.GetAtmosphericDensity(pos.magnitude - initLoc.planet.Radius);
                Double2 drag = -vel.normalized * dragMult * atmoDensity / mass;
                result += drag;
            }
            return result;
        }

        public static Location Extrapolate(Rocket rocket, Location packetLoc)
        {
            double delta = ClientManager.world.WorldTime - packetLoc.time;
            Debug.Log(delta);
            if (delta <= 0)
            {
                // * The packet's time is at or ahead of the local time.
                return packetLoc;
            }
            double dt = delta / iterationSteps;
            Debug.Log($"dt: {dt}");

            InitConstants(rocket, packetLoc);

            Double2 pos = packetLoc.position;
            Double2 vel = packetLoc.velocity;

            for (int i = 0; i < iterationSteps; i++)
            {
                // ? RK4 integration: https://en.wikipedia.org/wiki/Runge%E2%80%93Kutta_methods#The_Runge%E2%80%93Kutta_method

                Double2 k1_p = vel;
                Double2 k1_v = GetAcceleration(pos, k1_p);

                Double2 k2_p = vel + (k1_v * dt / 2);
                Double2 k2_v = GetAcceleration(pos + (k1_p * dt / 2), k2_p);

                Double2 k3_p = vel + (k2_v * dt / 2);
                Double2 k3_v = GetAcceleration(pos + (k2_p * dt / 2), k3_p);

                Double2 k4_p = vel + (k3_v * dt);
                Double2 k4_v = GetAcceleration(pos + (k3_p * dt), k4_p);

                Double2 dp = (k1_p + (2 * k2_p) + (2 * k3_p) + k4_p) / 6;
                Double2 dv = (k1_v + (2 * k2_v) + (2 * k3_v) + k4_v) / 6;

                pos += dp * dt;
                vel += dv * dt;
            }

            Debug.Log($"p: {packetLoc.position} -> {pos}");
            Debug.Log($"v: {packetLoc.velocity} -> {vel}");
            ChatWindow.AddMessage(new ChatMessage($"DBG: {rocket.location.position - pos}"));
            return new Location(ClientManager.world.WorldTime, packetLoc.planet, pos, vel);
        }
    }

    /// <summary>
    /// Reverse patch of `AeroModule.CalculateDragForce` for use in `Extrapolation.GetAcceleration`.
    /// </summary>
    [HarmonyPatch(typeof(AeroModule), "CalculateDragForce")]
    public static class AeroModule_CalculateDragForce
    {
        [HarmonyReversePatch]
        public static (float drag, Vector2 centerOfDrag) CalculateDragForce(List<Surface> surfaces) => throw new NotImplementedException("Harmony Reverse Patch");
    }
}