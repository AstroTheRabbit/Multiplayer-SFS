using System.Net.Sockets;
using UnityEngine;
using SFS.UI;
using SFS.UI.ModGUI;
using SFS.WorldBase;
using Button = SFS.UI.ModGUI.Button;
using MultiplayerSFS.Networking;
using System.Threading.Tasks;

namespace MultiplayerSFS.GUI
{
    public class HostInfo
    {
        public int port = 7579;
        public int maxPlayerCount = 10;
        public TimewarpType timewarpType = TimewarpType.Disabled;
        public float loadedRocketTickrate = 30;
        public float unloadedRocketTickrate = 1;
        public WorldReference world = new WorldReference("Multiplayer");
        public string password = "";
        public string username = "";
        
        public enum TimewarpType
        {
            Disabled,
            Resync // TODO: Implement (when I can).
        }
    }

    public class HostMenu : MultiplayerMenu
    {
        public override Vector2Int windowSize => new Vector2Int(1000, 1000);
        public override string windowTitle => "Host Game";
        
        public HostInfo hostInfo = new HostInfo();
        Color defaultTextInputColor;
        TextInput Input_Port;
        TextInput Input_MaxPlayerCount;
        Button TimewarpType_Disabled;
        // Button TimewarpType_Resync;
        TextInput Input_LoadedTickRate;
        TextInput Input_UnloadedTickRate;
        TextInput Input_World;

        public override void CreateUI()
        {
            window.CreateLayoutGroup(Type.Vertical, padding: new RectOffset(5,5,5,5));
            Container settings = Builder.CreateContainer(window);
            settings.CreateLayoutGroup(Type.Horizontal);

            Container settingLabels = Builder.CreateContainer(settings);
            Container settingsValues = Builder.CreateContainer(settings);
            settingLabels.CreateLayoutGroup(Type.Vertical, childAlignment: TextAnchor.MiddleRight);
            settingsValues.CreateLayoutGroup(Type.Vertical, childAlignment: TextAnchor.MiddleLeft);

            Builder.CreateLabel(settingLabels, 300, 50, text: "Port").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_Port = Builder.CreateTextInput(settingsValues, 620, 50, text: hostInfo.port.ToString(),
                onChange: (string input) =>
                {
                    if (int.TryParse(input, out int result) && result > 0)
                    {
                        Input_Port.FieldColor = defaultTextInputColor;
                        hostInfo.port = result;
                    }
                    else
                    {
                        Input_Port.FieldColor = Color.red;
                    }
                }
            );
            defaultTextInputColor = Input_Port.FieldColor;

            Builder.CreateLabel(settingLabels, 300, 50, text: "Max Player Count").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_MaxPlayerCount = Builder.CreateTextInput(settingsValues, 620, 50, text: hostInfo.maxPlayerCount.ToString(),
                onChange: (string input) =>
                {
                    if (int.TryParse(input, out int result) && result > 0)
                    {
                        Input_MaxPlayerCount.FieldColor = defaultTextInputColor;
                        hostInfo.maxPlayerCount = result;
                    }
                    else
                    {
                        Input_MaxPlayerCount.FieldColor = Color.red;
                    }
                }
            );

            Builder.CreateLabel(settingLabels, 300, 50, text: "Timewarp Type").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Container container = Builder.CreateContainer(settingsValues);
            container.CreateLayoutGroup(Type.Horizontal);
            TimewarpType_Disabled = Builder.CreateButton(container, 300, 50, text: "Disabled",
                onClick: () =>
                {
                    TimewarpType_Disabled.gameObject.GetComponent<ButtonPC>().SetSelected(true);
                    // TimewarpType_Resync.gameObject.GetComponent<ButtonPC>().SetSelected(false);
                    hostInfo.timewarpType = HostInfo.TimewarpType.Disabled;
                }
            );
            // TimewarpType_Resync = Builder.CreateButton(container, 300, 50, text: "Subspace & Resync",
            //     onClick: () =>
            //     {
            //         TimewarpType_Disabled.gameObject.GetComponent<ButtonPC>().SetSelected(false);
            //         TimewarpType_Resync.gameObject.GetComponent<ButtonPC>().SetSelected(true);
            //         hostInfo.timewarpType = TimewarpType.Resync;
            //     }
            // );
            TimewarpType_Disabled.gameObject.GetComponent<ButtonPC>().SetSelected(true);

            Builder.CreateLabel(settingLabels, 300, 50, text: "Loaded Tick Rate").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_LoadedTickRate = Builder.CreateTextInput(settingsValues, 620, 50, text: hostInfo.loadedRocketTickrate.ToString(),
                onChange: (string input) =>
                {
                    if (float.TryParse(input, out float result) && result > 0)
                    {
                        Input_LoadedTickRate.FieldColor = defaultTextInputColor;
                        hostInfo.loadedRocketTickrate = result;
                    }
                    else
                    {
                        Input_LoadedTickRate.FieldColor = Color.red;
                    }
                }
            );

            Builder.CreateLabel(settingLabels, 300, 50, text: "Unloaded Tick Rate").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_UnloadedTickRate = Builder.CreateTextInput(settingsValues, 620, 50, text: hostInfo.unloadedRocketTickrate.ToString(),
                onChange: (string input) =>
                {
                    if (float.TryParse(input, out float result) && result > 0)
                    {
                        Input_UnloadedTickRate.FieldColor = defaultTextInputColor;
                        hostInfo.loadedRocketTickrate = result;
                    }
                    else
                    {
                        Input_UnloadedTickRate.FieldColor = Color.red;
                    }
                }
            );

            // TODO: Change from simple text input.
            Builder.CreateLabel(settingLabels, 300, 50, text: "World To Use").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Input_World = Builder.CreateTextInput(settingsValues, 620, 50, text: hostInfo.world.worldName,
                onChange: (string input) =>
                {
                    var result = new WorldReference(input);
                    if (result.WorldExists())
                    {
                        Input_World.FieldColor = defaultTextInputColor;
                        hostInfo.world = result;
                    }
                    else
                    {
                        Input_World.FieldColor = Color.red;
                    }
                }
            );
            Input_World.OnChange.Invoke(hostInfo.world.worldName);

            Builder.CreateLabel(settingLabels, 300, 50, text: "Username").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Builder.CreateTextInput(settingsValues, 620, 50, text: hostInfo.username, onChange: (string input) => hostInfo.username = input);

            Builder.CreateLabel(settingLabels, 300, 50, text: "Password").TextAlignment = TMPro.TextAlignmentOptions.MidlineLeft;
            Builder.CreateTextInput(settingsValues, 620, 50, text: hostInfo.password, onChange: (string input) => hostInfo.password = input);

            Builder.CreateButton(window, 300, 100, text: "Start Game", onClick: CheckAndStartHost);
        }

        async void CheckAndStartHost()
        {
            if (!(int.TryParse(Input_Port.Text, out int n_port) && n_port > 0))
            {
                MsgDrawer.main.Log("Port is invalid");
                return;
            }
            if (!(float.TryParse(Input_LoadedTickRate.Text, out float n_loaded) && n_loaded > 0))
            {
                MsgDrawer.main.Log("Loaded Tick Rate is invalid");
                return;
            }
            if (!(float.TryParse(Input_UnloadedTickRate.Text, out float n_unloaded) && n_unloaded > 0))
            {
                MsgDrawer.main.Log("Unloaded Tick Rate is invalid");
                return;
            }
            if (!new WorldReference(Input_World.Text).WorldExists())
            {
                MsgDrawer.main.Log($"World {Input_World.Text} doesn't exist");
                return;
            }

            if (HostConnectionManager.StartHost(hostInfo))
            {
                MsgDrawer.main.Log("Server start successful!");
                while (true)
                {
                    if (!HostConnectionManager.listener.Pending())
                        await Task.Yield();
                    else
                        HostConnectionManager.ManagePendingConnection();
                }
            }
            else
            {
                MsgDrawer.main.Log("Server start failed...");
            }
        }
    }
}