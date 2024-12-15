using System.Linq;
using System.Reflection.Emit;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS.Input;
using SFS.World;

namespace MultiplayerSFS.Mod.Patches
{
    /// <summary>
    /// Removes code that changes the time scale at which the world operates.
    /// </summary>
    public class DisablePausing
    {
        [HarmonyPatch(typeof(ScreenManager), "Awake")]
        public class ScreenManager_Awake
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    // ? Removes `Time.timeScale = 0f;` for its multiplayer replacement in `Postfix`.
                    if (codes[i].Calls(AccessTools.PropertySetter(typeof(Time), nameof(Time.timeScale))))
                    {
                        codes[i - 1].opcode = OpCodes.Nop;
                        codes.RemoveAt(i);
                        break;
                    }
                }
                return codes;
            }

            public static void Postfix(ScreenManager __instance)
            {
                if (!ClientManager.multiplayerEnabled.Value && !__instance.selfInitialize)
                    Time.timeScale = 0f;
            }
        }

        [HarmonyPatch(typeof(ScreenManager), nameof(ScreenManager.OpenScreen))]
        public class ScreenManager_OpenScreen
        {
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = instructions.ToList();
                for (int i = 0; i < codes.Count; i++)
                {
                    // ? Removes `Time.timeScale = (CurrentScreen.PauseWhileOpen ? 0f : ((WorldTime.main != null) ? WorldTime.main.TimeScale : 1f));` for its multiplayer replacement in `Postfix`.
                    if (codes[i].Calls(AccessTools.PropertySetter(typeof(Time), nameof(Time.timeScale))))
                    {
                        codes.RemoveRange(i - 14, 15);
                        break;
                    }
                }
                return codes;
            }

            public static void Postfix(ScreenManager __instance)
            {
                if (!ClientManager.multiplayerEnabled.Value)
                {
                    Time.timeScale = __instance.CurrentScreen.PauseWhileOpen ? 0f : ((WorldTime.main != null) ? WorldTime.main.TimeScale : 1f);
                }
            }
        }
    }
}

