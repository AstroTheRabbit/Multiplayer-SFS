using System.Net;
using System.Threading;
using UnityEngine;
using SFS.UI;
using SFS.Input;
using SFS.UI.ModGUI;
using MultiplayerSFS.Mod.Networking;

namespace MultiplayerSFS.Mod.GUI
{
    public class JoinInfo
    {
        public IPAddress ipAddress = IPAddress.Loopback;
        public int port = 9807;
        public string username = "DEFAULT_USERNAME";
        public string password = "";
    }

    public class JoinMenu : BasicMenu
    {
        public static JoinMenu main;
        public static Window window;
        public static GameObject windowHolder;
        static readonly int windowID = Builder.GetRandomID();
        static readonly Vector2Int windowSize = new Vector2Int(1000, 500);
        protected override CloseMode OnEscape => CloseMode.Current;

        public JoinInfo joinInfo = new JoinInfo();
        Color defaultTextInputColor;
        TextInput Input_IPAddress;
        TextInput Input_Port;
        TextInput Input_Username;
        TextInput Input_Password;
        readonly CancellationTokenSource connectCancelToken = new CancellationTokenSource();

        public static void OpenMenu() {
            windowHolder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "Multiplayer SFS - Join Menu Holder");
            main = windowHolder.AddComponent<JoinMenu>();
            main.OnOpen();
        }

        public override void OnOpen()
        {
            if (ScreenManager.main.CurrentScreen != this)
            {
                ScreenManager.main.OpenScreen(() => this);
                windowHolder.SetActive(true);
                Patches.Patches.multiplayerEnabled.Value = true;
                window = Builder.CreateWindow(
                    windowHolder.transform,
                    windowID,
                    windowSize.x,
                    windowSize.y,
                    0,
                    windowSize.y / 2,
                    draggable: false,
                    savePosition: false,
                    titleText: "Multiplayer SFS - Join Menu"
                );
                CreateUI();
            }

        }

        public override void Close()
        {
            if (ScreenManager.main.CurrentScreen == this && windowHolder != null)
            {
                connectCancelToken.Cancel();
                NetworkingManager.client?.Shutdown("");
                Patches.Patches.multiplayerEnabled.Value = false;
                
                ScreenManager.main.CloseCurrent();
                windowHolder.SetActive(false);
            }
        }

        void CreateUI()
        {
            window.CreateLayoutGroup(Type.Vertical, padding: new RectOffset(5,5,5,5));
            Container settings = Builder.CreateContainer(window);
            settings.CreateLayoutGroup(Type.Horizontal);

            Container settingLabels = Builder.CreateContainer(settings);
            Container settingsValues = Builder.CreateContainer(settings);
            settingLabels.CreateLayoutGroup(Type.Vertical, childAlignment: TextAnchor.MiddleLeft);
            settingsValues.CreateLayoutGroup(Type.Vertical, childAlignment: TextAnchor.MiddleLeft);

            Builder.CreateLabel(settingLabels, 300, 50, text: "IP Address").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_IPAddress = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.ipAddress.ToString(),
                onChange: (string input) =>
                {
                    if (IPAddress.TryParse(input, out IPAddress result))
                    {
                        Input_IPAddress.FieldColor = defaultTextInputColor;
                        joinInfo.ipAddress = result;
                    }
                    else
                    {
                        Input_IPAddress.FieldColor = Color.red;
                    }
                }
            );
            Input_IPAddress.field.onEndEdit.AddListener((string input) => Input_IPAddress.Text = IPAddress.Parse(input).ToString());
            defaultTextInputColor = Input_IPAddress.FieldColor;
            

            Builder.CreateLabel(settingLabels, 300, 50, text: "Port").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_Port = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.port.ToString(),
                onChange: (string input) =>
                {
                    if (int.TryParse(input, out int result))
                    {
                        Input_Port.FieldColor = defaultTextInputColor;
                        joinInfo.port = result;
                    }
                    else
                    {
                        Input_Port.FieldColor = Color.red;
                    }
                }
            );

            Builder.CreateLabel(settingLabels, 300, 50, text: "Username").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_Username = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.username,
                onChange: (string input) =>
                {
                    Input_Username.Text = input.Trim();
                    joinInfo.username = input.Trim();
                    Input_Username.FieldColor = defaultTextInputColor;
                }
            );

            Builder.CreateLabel(settingLabels, 300, 50, text: "Password").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_Password = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.password,
            onChange: (string input) =>
                {
                    joinInfo.password = input;
                    Input_Password.FieldColor = defaultTextInputColor;
                }
            );

            Container backJoinButtons = Builder.CreateContainer(window);
            backJoinButtons.CreateLayoutGroup(Type.Horizontal, childAlignment: TextAnchor.MiddleLeft);
            Builder.CreateButton(backJoinButtons, 300, 100, text: "Back", onClick: Close);
            Builder.CreateButton(backJoinButtons, 300, 100, text: "Join", onClick: CheckAndJoin);
        }

        async void CheckAndJoin()
        {
            try
            {
                Input_IPAddress.FieldColor = defaultTextInputColor;
                Input_Port.FieldColor      = defaultTextInputColor;
                Input_Username.FieldColor  = defaultTextInputColor;
                Input_Password.FieldColor  = defaultTextInputColor;

                if (!IPAddress.TryParse(Input_IPAddress.Text, out IPAddress _))
                {
                    Input_IPAddress.FieldColor = Color.red;
                    MsgDrawer.main.Log("IP address is invalid");
                    return;
                }
                if (!(int.TryParse(Input_Port.Text, out int n_port) && n_port > 0))
                {
                    Input_Port.FieldColor = Color.red;
                    MsgDrawer.main.Log("Port is invalid");
                    return;
                }
                if (string.IsNullOrWhiteSpace(Input_Username.Text))
                {
                    Input_Username.FieldColor = Color.red;
                    MsgDrawer.main.Log("Username cannot be empty");
                    return;
                }
                MsgDrawer.main.Log("Attempting to connect...");

                (bool approved, string reason) = await NetworkingManager.TryConnect(joinInfo, connectCancelToken.Token);

                MsgDrawer.main.Log(reason);
                if (approved)
                {
                    await NetworkingManager.LoadWorld();
                }
            }
            catch (System.Exception e)
            {
                if (e is System.AggregateException ae && ae.InnerException is System.OperationCanceledException)
                {
                    MsgDrawer.main.Log("");
                }
                else
                {
                    MsgDrawer.main.Log("An error occured... (Check console)");
                    Debug.Log(e);
                }
            }
        }
    }
}
