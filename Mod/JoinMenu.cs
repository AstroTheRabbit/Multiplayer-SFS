using System;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using SFS.UI;
using SFS.Input;
using SFS.UI.ModGUI;
using Type = SFS.UI.ModGUI.Type;

namespace MultiplayerSFS.Mod
{
    public class JoinInfo
    {
        public IPAddress address = IPAddress.Parse("10.0.0.3"); // ! FOR TESTING ONLY
        public int port = 9806;
        public string username = "DEFAULT_USERNAME";
        public string password = "DEFAULT_PASSWORD";
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
        TextInput input_address;
        TextInput input_port;
        TextInput input_username;
        TextInput input_password;

        public static void OpenMenu() {
            windowHolder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "MultiplayerSFS - Join Menu Holder");
            main = windowHolder.AddComponent<JoinMenu>();
            main.OnOpen();
        }

        public override void OnOpen()
        {
            if (ScreenManager.main.CurrentScreen != this)
            {
                ScreenManager.main.OpenScreen(() => this);
                windowHolder.SetActive(true);
                ClientManager.multiplayerEnabled.Value = true;
                window = Builder.CreateWindow
                (
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
                ClientManager.multiplayerEnabled.Value = false;
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

            Builder.CreateLabel(settingLabels, 300, 50, text: "Address").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            input_address = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.address.ToString(),
                onChange: async input =>
                {
                    if (await TryParseAddress(input) is IPAddress result)
                    {
                        input_address.FieldColor = defaultTextInputColor;
                        joinInfo.address = result;
                    }
                    else
                    {
                        input_address.FieldColor = Color.red;
                    }
                }
            );
            defaultTextInputColor = input_address.FieldColor;
            

            Builder.CreateLabel(settingLabels, 300, 50, text: "Port").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            input_port = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.port.ToString(),
                onChange: input =>
                {
                    if (int.TryParse(input, out int result))
                    {
                        input_port.FieldColor = defaultTextInputColor;
                        joinInfo.port = result;
                    }
                    else
                    {
                        input_port.FieldColor = Color.red;
                    }
                }
            );

            Builder.CreateLabel(settingLabels, 300, 50, text: "Username").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            input_username = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.username,
                onChange: input =>
                {
                    
                    input_username.Text = joinInfo.username = input.Trim();
                    input_username.FieldColor = defaultTextInputColor;
                }
            );

            Builder.CreateLabel(settingLabels, 300, 50, text: "Password").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            input_password = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.password,
            onChange: input =>
            {
                    joinInfo.password = input;
                    input_password.FieldColor = defaultTextInputColor;
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
                input_address.FieldColor   = defaultTextInputColor;
                input_port.FieldColor      = defaultTextInputColor;
                input_username.FieldColor  = defaultTextInputColor;
                input_password.FieldColor  = defaultTextInputColor;

                if (await TryParseAddress(input_address.Text) is null)
                {
                    input_address.FieldColor = Color.red;
                    MsgDrawer.main.Log("IP address is invalid");
                    return;
                }
                if (!(int.TryParse(input_port.Text, out int n_port) && n_port > 0))
                {
                    input_port.FieldColor = Color.red;
                    MsgDrawer.main.Log("Port is invalid");
                    return;
                }
                if (string.IsNullOrWhiteSpace(input_username.Text))
                {
                    input_username.FieldColor = Color.red;
                    MsgDrawer.main.Log("Username cannot be empty");
                    return;
                }
                MsgDrawer.main.Log("Attempting to connect...");
                
                await ClientManager.TryConnect(joinInfo);
            }
            catch (Exception e)
            {
                if (e is AggregateException ae && ae.InnerException is OperationCanceledException)
                {
                    MsgDrawer.main.Log("");
                }
                else
                {
                    MsgDrawer.main.Log("An error occured... (Check console)");
                    Debug.LogError(e);
                }
            }
        }

        static async Task<IPAddress> TryParseAddress(string input)
        {
            if (IPAddress.TryParse(input, out IPAddress ip))
            {
                return ip;
            }
            try
            {
                if ((await Dns.GetHostAddressesAsync(input)).First() is IPAddress res)
                {
                    return res;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}