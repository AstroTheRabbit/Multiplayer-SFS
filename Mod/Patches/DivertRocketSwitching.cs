using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS.World;
using MultiplayerSFS.Common;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Patches related to managing players changing their controlled rocket in multiplayer.
    /// </summary>
    public class DivertRocketSwitching
    {
        /// <summary>
        /// Common method used to handle changing controlled rockets in multiplayer.
        /// </summary>
        public static bool TrySwitchPlayer(Player player)
        {
            if (ClientManager.multiplayerEnabled)
            {
                if (player is Rocket rocket)
                {
                    int id = LocalManager.GetSyncedRocketID(rocket);
                    if (id >= 0)
                    {
                        if (LocalManager.players.Any(kvp => kvp.Key != ClientManager.playerId && kvp.Value.currentRocket == id))
                        {
                            // * `player` is already controlled by another player.
                            return false;
                        }
                        
                        LocalManager.players[ClientManager.playerId].currentRocket.Value = id;
                        ClientManager.SendPacket
                        (
                            new Packet_UpdatePlayerControl()
                            {
                                PlayerId = ClientManager.playerId,
                                RocketId = id,
                            }
                        );
                        return true;
                    }
                    id = LocalManager.GetUnsyncedRocketID(rocket);
                    if (id >= 0)
                    {
                        // * `player` currently isn't synced with the server.
                        LocalManager.unsyncedToControl = id;
                        return false;
                    }
                    else
                    {
                        Debug.LogError("`TrySwitchPlayer`: `player` isn't registered!");
                        return false;
                    }
                }
                else if (player is null)
                {
                    Debug.LogError("`TrySwitchPlayer`: `player` is null!");
                    return false;
                }
                else
                {
                    Debug.LogError("`TrySwitchPlayer`: `player` is not a rocket!");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Handles players switching rockets - delays early switching to unsynced rockets, and prevents switching to rockets that are already controlled by another player.
        /// </summary>
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.SmoothChangePlayer))]
        public static class PlayerController_SmoothChangePlayer
        {
            public static bool Prefix(Player newPlayer)
            {
                if (TrySwitchPlayer(newPlayer))
                {
                    if (PlayerController.main.player.Value == null)
                    {
                        // ? This is required because of a bug with the vanilla game.
                        // ? `PlayerController.TrackPlayer()` assumes that `PlayerController.main.player.Value` is never null, which isn't always true.
                        PlayerController.main.player.Value = newPlayer;
                    }
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Prevents `RocketSetPlayerToBestControllable` from trying to switch to an unsynced rocket early.
        /// </summary>
        [HarmonyPatch(typeof(Rocket), nameof(Rocket.SetPlayerToBestControllable))]
        public static class Rocket_SetPlayerToBestControllable
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                Label label_CheckSwitch = generator.DefineLabel();

                foreach (CodeInstruction code in instructions)
                {
                    if (code.opcode == OpCodes.Ret)
                    {
                        yield return code.WithLabels(label_CheckSwitch);
                    }
                    else
                    {
                        if (code.opcode == OpCodes.Brfalse_S)
                        {
                            yield return new CodeInstruction(OpCodes.Ldloc_0);
                            yield return CodeInstruction.Call(typeof(Rocket_SetPlayerToBestControllable), nameof(CheckSwitch));
                            yield return new CodeInstruction(OpCodes.Brfalse, label_CheckSwitch);
                        }
                        yield return code;
                    }

                }
            }

            public static bool CheckSwitch(List<Rocket> rockets)
            {
                if (rockets[0] != null)
                {
                    return TrySwitchPlayer(rockets[0]);
                }
                return false;
            }
        }

        [HarmonyPatch(typeof(GameSelector), "SwitchTo")]
        public static class GameSelector_SwitchTo
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                bool found_ldsfld = false;
                Label label_CheckSwitch = generator.DefineLabel();

                foreach (CodeInstruction code in instructions)
                {
                    if (!found_ldsfld && code.opcode == OpCodes.Ldsfld && code.operand is FieldInfo fieldInfo && fieldInfo.Name == "view")
                    {
                        found_ldsfld = true;
                        yield return new CodeInstruction(OpCodes.Ldloc_0);
                        yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(MapPlayer), nameof(MapPlayer.Player)));
                        yield return CodeInstruction.Call(typeof(DivertRocketSwitching), nameof(TrySwitchPlayer));
                        yield return new CodeInstruction(OpCodes.Brfalse, label_CheckSwitch);
                    }
                    if (found_ldsfld && code.opcode == OpCodes.Ret)
                    {
                        yield return code.WithLabels(label_CheckSwitch);
                    }
                    else
                    {
                        yield return code;
                    }
                }
            }
        }
    }
}