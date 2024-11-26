using HarmonyLib;
using UnityEngine;
using ModLoader;
using ModLoader.Helpers;
using UITools;
using SFS.IO;
using System.Collections.Generic;

namespace MultiplayerSFS.Mod
{
    public class Main : ModLoader.Mod
    {
        public static Main main;
        public override string ModNameID => "multiplayersfs";
        public override string DisplayName => "SFS Multiplayer";
        public override string Author => "Astro The Rabbit";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "1.0";
        public override string Description => "Adds server-client multiplayer to SFS!";

        public override Dictionary<string, string> Dependencies { get; } = new Dictionary<string, string> { { "UITools", "1.1.5" } };
        public Dictionary<string, FilePath> UpdatableFiles => new Dictionary<string, FilePath>()
        {
            {
                "https://github.com/AstroTheRabbit/Multiplayer-SFS/releases/latest/download/MultiplayerMod.dll",
                new FolderPath(ModFolder).ExtendToFile("MultiplayerMod.dll")
            }
        };

        public override void Early_Load()
        {
            new Harmony(ModNameID).PatchAll();
            main = this;
        }

        public override void Load()
        {
            Debug.Log("Hiya!");
        }
    }
}
