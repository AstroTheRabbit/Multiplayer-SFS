﻿using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using ModLoader.Helpers;
using SFS.IO;
using SFS.UI;
using SFS.Audio;
using SFS.Translations;
using SFS.World;
using MultiplayerSFS.Common;

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
            // ! VID CREATION ONLY
            SceneHelper.OnWorldSceneLoaded += (System.Action) delegate
            {
                if (ClientManager.multiplayerEnabled)
                {
                    // SandboxSettings.main.settings.noGravity = true;
                    // SandboxSettings.main.settings.infiniteFuel = true;
                    SandboxSettings.main.settings.noHeatDamage = true;
                }
            };

            // * Send `UpdateControl` packet when the player leaves the world scene in multiplayer.
            SceneHelper.OnWorldSceneUnloaded += (System.Action) delegate
            {
                if (ClientManager.multiplayerEnabled.Value)
                {
                    LocalManager.players[ClientManager.playerId].currentRocket.Value = -1;
                    ClientManager.SendPacket
                    (
                        new Packet_UpdatePlayerControl()
                        {
                            PlayerId = ClientManager.playerId,
                            RocketId = -1,
                        }
                    );
                }
            };
            AddMultiplayerButton();
            SceneHelper.OnHomeSceneLoaded += AddMultiplayerButton;

            buildPersistentFolder = new FolderPath(ModFolder).Extend(".BlueprintPersistent");
            
            Application.quitting += () => ClientManager.client?.Shutdown("Application quitting");
            ClientManager.multiplayerEnabled.OnChange += value => { Application.runInBackground = value; };
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
