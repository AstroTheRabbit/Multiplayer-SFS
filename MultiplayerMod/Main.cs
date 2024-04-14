using System.IO;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using SFS.UI;
using SFS.Audio;
using SFS.Translations;
using ModLoader.Helpers;
using MultiplayerSFS.Mod.GUI;
using System.Net;
using System.Reflection;

namespace MultiplayerSFS.Mod
{
    public class Main : ModLoader.Mod//, IUpdatable
    {
        public static Main main;
        public override string ModNameID => "multiplayersfs";
        public override string DisplayName => "Multiplayer SFS";
        public override string Author => "Astro The Rabbit";
        public override string MinimumGameVersionNecessary => "1.5.10.2";
        public override string ModVersion => "v0.1";
        public override string Description => "A mod that adds multiplayer ofc, what did you think it was?";

        public override Dictionary<string, string> Dependencies { get; } = new Dictionary<string, string> { { "UITools", "1.1.1" } };
        // public Dictionary<string, FilePath> UpdatableFiles => new Dictionary<string, FilePath>() { { "https://github.com/AstroTheRabbit/Multiplayer-Mod-SFS/releases/latest/download/MultiplayerMod.dll", new FolderPath(ModFolder).ExtendToFile("MultiplayerMod.dll") } };

        public override void Early_Load()
        {
            new Harmony(ModNameID).PatchAll();
            main = this;
        }

        public override void Load()
        {
            CheckLidgrenInstalled();
            AddMultiplayerButton();
            SceneHelper.OnHomeSceneLoaded += AddMultiplayerButton;
            Patches.Patches.multiplayerEnabled.OnChange += (bool value) => { Application.runInBackground = value; };
        }

        public static async void CheckLidgrenInstalled()
        {
            try
            {
                string path = main.ModFolder + "/Lidgren.Network.dll";
                if (!File.Exists(main.ModFolder + "Lidgren.Network.dll"))
                {
                    using (WebClient client = new WebClient())
                    {
                        await client.DownloadFileTaskAsync("https://github.com/AstroTheRabbit/Multiplayer-SFS/releases/download/lidgren/Lidgren.Network.dll", path);
                    }
                }
                Assembly.LoadFrom(path);
            }
            catch (System.Exception e)
            {
                Debug.Log($"Failed to load Lidgren.Network.dll, which is required for multiplayer: {e}");
            }
        }

        public static void AddMultiplayerButton()
        {
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
            buttonPC.clickEvent.AddListener(
                delegate
                {
                    SoundPlayer.main.clickSound.Play();
                    JoinMenu.OpenMenu();
                }
            );
        }
    }
}
