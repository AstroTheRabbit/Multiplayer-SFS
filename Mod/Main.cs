using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using SFS.IO;
using SFS.UI;
using SFS.World;
using SFS.Audio;
using SFS.Translations;
using ModLoader.Helpers;
using MultiplayerSFS.Common;
using Object = UnityEngine.Object;

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
            SceneHelper.OnWorldSceneLoaded += (Action) delegate
            {
                if (ClientManager.multiplayerEnabled)
                {
                    ChatWindow.CreateUI("world");
                }
            };
            SceneHelper.OnWorldSceneUnloaded += (Action) delegate
            {
                if (ClientManager.multiplayerEnabled)
                {
                    // * Send `UpdateControl` packet when the player leaves the world scene in multiplayer.
                    LocalManager.Player.currentRocket.Value = -1;
                    ClientManager.SendPacket
                    (
                        new Packet_UpdatePlayerControl()
                        {
                            PlayerId = ClientManager.playerId,
                            RocketId = -1,
                        }
                    );
                    ChatWindow.DestroyUI();
                }
            };
            SceneHelper.OnBuildSceneLoaded += (Action) delegate
            {
                ChatWindow.CreateUI("build");
            };
            SceneHelper.OnBuildSceneUnloaded += (Action) delegate
            {
                ChatWindow.DestroyUI();
            };
            SceneHelper.OnHubSceneLoaded += (Action) delegate
            {
                ChatWindow.CreateUI("hub");
            };
            SceneHelper.OnHubSceneUnloaded += (Action) delegate
            {
                ChatWindow.DestroyUI();
            };

            SceneHelper.OnHomeSceneLoaded += AddMultiplayerButton;
            AddMultiplayerButton();

            buildPersistentFolder = new FolderPath(ModFolder).Extend(".BlueprintPersistent");
            
            Application.quitting += () => ClientManager.client?.Shutdown("Application quitting");
            
            ClientManager.multiplayerEnabled.OnChange += value =>
            {
                Application.runInBackground = value;
                if (!value)
                {
                    ChatWindow.DestroyCooldownTimer();
                }
            };
        }

        public static void AddMultiplayerButton()
        {
            ClientManager.multiplayerEnabled.Value = false;
            
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
