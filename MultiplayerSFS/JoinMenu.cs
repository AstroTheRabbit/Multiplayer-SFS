using System.Net;
using UnityEngine;
using SFS.UI;
using SFS.UI.ModGUI;
using MultiplayerSFS.Networking;
using MultiplayerSFS.Networking.Packets;

namespace MultiplayerSFS.GUI
{
    public class JoinInfo
    {
        public IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
        public int port = 7579;
        public string password = "";
        public string username = "";

        public JoinRequestPacket ToPacket()
        {
            return new JoinRequestPacket()
            {
                username = this.username,
                password = this.password,
            };
        }
    }

    public class JoinMenu : MultiplayerMenu
    {
        public override Vector2Int windowSize => new Vector2Int(1000, 1000);
        public override string windowTitle => "Join Game";

        public JoinInfo joinInfo = new JoinInfo();
        Color defaultTextInputColor;
        TextInput Input_IPAddress;
        TextInput Input_Port;
        TextInput Input_Username;
        TextInput Input_Password;

        public override void CreateUI()
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
                    joinInfo.username = input;
                    Input_Username.FieldColor = defaultTextInputColor;
                }
            );

            Builder.CreateLabel(settingLabels, 300, 50, text: "Password").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_Password = Builder.CreateTextInput(settingsValues, 620, 50, text: joinInfo.password,
            onChange: (string input) =>
                {
                    joinInfo.username = input;
                    Input_Password.FieldColor = defaultTextInputColor;
                }
            );

            Builder.CreateButton(window, 300, 100, text: "Join Game", onClick: CheckAndJoin);
        }

        async void CheckAndJoin()
        {
            if (!(IPAddress.TryParse(Input_IPAddress.Text, out IPAddress _)))
            {
                MsgDrawer.main.Log("IP address is invalid");
                return;
            }
            if (!(int.TryParse(Input_Port.Text, out int n_port) && n_port > 0))
            {
                MsgDrawer.main.Log("Port is invalid");
                return;
            }
            JoinResponsePacket.JoinResponse? joinResponse = await ClientConnectionManager.TryConnect(joinInfo);
            if (!joinResponse.HasValue)
            {
                MsgDrawer.main.Log("Connection error");
                return;
            }

            switch (joinResponse.Value)
            {
                case JoinResponsePacket.JoinResponse.Accepted:
                    MsgDrawer.main.Log("Connection successful!");

                    return;
                case JoinResponsePacket.JoinResponse.Denied_IncorrectPassword:
                    MsgDrawer.main.Log("Incorrect password");
                    return;
                case JoinResponsePacket.JoinResponse.Denied_MaxPlayersReached:
                    MsgDrawer.main.Log("Server full");
                    return;
                case JoinResponsePacket.JoinResponse.Denied_UsernameAlreadyInUse:
                    MsgDrawer.main.Log("Username already in use");
                    return;
                default:
                    MsgDrawer.main.Log("huh?");
                    return;
            }
        }
    }
}