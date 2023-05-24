using System.Collections.Generic;
using UnityEngine;
using SFS.Audio;
using SFS.Input;
using SFS.UI;
using SFS.UI.ModGUI;
using SFS.Translations;

namespace MultiplayerSFS.GUI
{
    public static class StartMenu
    {
        public static MultiplayerMenu menu;
        public static GameObject windowHolder;
        public static readonly int windowID = Builder.GetRandomID();

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
                    OpenMenu();
                }
            );
        }
        private static void OpenMenu()
        {
            windowHolder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "Multiplayer SFS - Host/Join Menu");
            List<MenuElement> list = new List<MenuElement>();
            list.Add(new SizeSyncerBuilder(out var carrier).HorizontalMode(SizeMode.MaxChildSize));
            list.Add(ButtonBuilder.CreateButton(
                carrier,
                () => "Host",
                () => {
                    menu = windowHolder.AddComponent<HostMenu>();
                    menu.Open();
                },
                CloseMode.None
            ));
            list.Add(ButtonBuilder.CreateButton(
                carrier,
                () => "Join",
                () => {
                    menu = windowHolder.AddComponent<JoinMenu>();
                    menu.Open();
                },
                CloseMode.None
            ));
            list.Add(ButtonBuilder.CreateButton(
                carrier,
                () => "Close",
                () => Object.Destroy(windowHolder),
                CloseMode.Current
            ));
            MenuGenerator.OpenMenu(CancelButton.Close, CloseMode.Current, list.ToArray());
        }
    }

    public abstract class MultiplayerMenu : BasicMenu
    {
        public Window window;
        public abstract Vector2Int windowSize { get; }
        public abstract string windowTitle { get; }
        protected override CloseMode OnEscape => CloseMode.Current;
        public abstract void CreateUI();

        public override void OnOpen()
        {
            (menuHolder = StartMenu.windowHolder).SetActive(true);
            window = Builder.CreateWindow(
                StartMenu.windowHolder.transform,
                StartMenu.windowID,
                windowSize.x,
                windowSize.y,
                0,
                windowSize.y / 2,
                draggable: false,
                savePosition: false,
                titleText: windowTitle
            );
            CreateUI();
        }
    }
}