using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ModLoader.Helpers;
using SFS.IO;
using SFS.UI;
using SFS.Audio;
using SFS.Translations;

namespace MultiplayerSFS.Mod
{
    public class Main : ModLoader.Mod//, IUpdatable
    {
        public static Main main;
        public static FolderPath buildPersistentFolder;
        public override string ModNameID => "multiplayersfs";
        public override string DisplayName => "SFS Multiplayer";
        public override string Author => "Astro The Rabbit";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "1.0";
        public override string Description => "Adds server-client multiplayer to SFS!";

        public override Dictionary<string, string> Dependencies { get; } = new Dictionary<string, string> { { "UITools", "1.1.5" } };
        // public Dictionary<string, FilePath> UpdatableFiles => new Dictionary<string, FilePath>()
        // {
        //     {
        //         "https://github.com/AstroTheRabbit/Multiplayer-SFS/releases/latest/download/MultiplayerMod.dll",
        //         new FolderPath(ModFolder).ExtendToFile("MultiplayerMod.dll")
        //     }
        // };

        public override void Early_Load()
        {
            new Harmony(ModNameID).PatchAll();
            main = this;
        }

        public override void Load()
        {
            AddMultiplayerButton();
            SceneHelper.OnHomeSceneLoaded += AddMultiplayerButton;
            buildPersistentFolder = new FolderPath(ModFolder).Extend(".BlueprintPersistent");
            Patches.multiplayerEnabled.OnChange += (bool value) => { Application.runInBackground = value; };
        }

        public static void AddMultiplayerButton()
        {
            Patches.multiplayerEnabled.Value = false;
            Application.quitting += () => ClientManager.client?.Shutdown("Application quitting");
            
            Transform buttons = GameObject.Find("Buttons").transform;
            GameObject playButton = GameObject.Find("Play Button");
            GameObject multiplayerButton = Object.Instantiate(playButton, buttons, true);
            multiplayerButton.GetComponent<RectTransform>().SetSiblingIndex(playButton.GetComponent<RectTransform>().GetSiblingIndex() + 1);

            var textAdapter = multiplayerButton.GetComponentInChildren<TextAdapter>();
            Object.Destroy(multiplayerButton.GetComponent<TranslationSelector>());
            multiplayerButton.name = "Multiplayer SFS - Button";
            textAdapter.Text = "Multiplayer";

            ButtonPC buttonPC = multiplayerButton.GetComponent<ButtonPC>();
            buttonPC.holdEvent = new HoldUnityEvent();
            buttonPC.clickEvent = new ClickUnityEvent();
            buttonPC.clickEvent.AddListener
            (
                delegate
                {
                    SoundPlayer.main.clickSound.Play();
                    JoinMenu.OpenMenu();
                }
            );
        }
    }
}
