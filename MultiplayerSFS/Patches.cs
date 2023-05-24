using System.Collections.Generic;
using HarmonyLib;
using SFS.UI;
using SFS.Input;
using UnityEngine;
using SFS.WorldBase;
using MultiplayerSFS.Networking;

namespace MultiplayerSFS
{
    public enum TimewarpType
    {
        Disabled,
        Resync // TODO: Implement (when/if I can).
    }
    public static class Patches
    {
        public static bool MultiplayerEnabled = false;
        public static TimewarpType? timewarpType = null;
    }
}