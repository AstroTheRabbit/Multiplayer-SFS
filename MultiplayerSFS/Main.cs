using HarmonyLib;
using ModLoader;
using ModLoader.Helpers;
using MultiplayerSFS.GUI;

namespace MultiplayerSFS
{
    public class Main : Mod
    {
        public static Main main;
        public override string ModNameID => "multiplayersfs";
        public override string DisplayName => "Multiplayer SFS";
        public override string Author => "pixelgaming579";
        public override string MinimumGameVersionNecessary => "1.5.8";
        public override string ModVersion => "v0.1";
        public override string Description => "Basic host/client multiplayer for SFS.";

        public override void Early_Load()
        {
            new Harmony(ModNameID).PatchAll();
            main = this;
        }

        public override void Load()
        {
            StartMenu.AddMultiplayerButton();
            SceneHelper.OnHomeSceneLoaded += StartMenu.AddMultiplayerButton;
        }
    }
}