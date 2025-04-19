using System.Linq;
using System.Timers;
using System.Threading.Tasks;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using TMPro;
using SFS.UI.ModGUI;
using UITools;
using MultiplayerSFS.Common;
using MultiplayerSFS.Mod.Patches;

namespace MultiplayerSFS.Mod
{
    public static class ChatWindow
    {
        public static readonly int windowID = Builder.GetRandomID();
        public static readonly int maxMessagesCount = 100;
        public static Color defaultInputColor;
        public static bool InputSelected { get; private set; }

        internal const int WindowWidth = 500;
        internal const int WindowHeight = 700;
        internal const int InnerWidth = WindowWidth - 20;

        public static GameObject holder_window;
        public static ClosableWindow window;

        public static Container container_colorPicker;
        // TODO: I wanted this to be a `Slider`, but they seem quite broken so I have to use UI Tools' `NumberInput` instead.
        public static NumberInput input_colorPicker;
        public static Label label_colorPicker;
        public static Button button_colorPicker;

        public static Window window_messages;
        public static readonly Queue<ChatMessage> messages = new Queue<ChatMessage>();
        public static int LastSenderId => messages
            .Where(m => m.label_message != null)
            .Select(m => m.senderId)
            .DefaultIfEmpty(int.MinValue)
            .Last();

        public static Timer cooldownTimer;
        public static bool canSendMessage = true;
        public static TextInput input_sendMessage;

        public static async void CreateUI(string sceneName)
        {
            if (holder_window != null || !ClientManager.multiplayerEnabled)
                return;

            while (LocalManager.Player == null)
            {
                if (ClientManager.multiplayerEnabled)
                    return;
                await Task.Yield();
            }

            holder_window = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, "Multiplayer SFS - Chat Window Holder");

            window = UIToolsBuilder.CreateClosableWindow
            (
                holder_window.transform,
                windowID,
                WindowWidth,
                WindowHeight,
                draggable: true,
                titleText: "Multiplayer Chat"
            );
            window.CreateLayoutGroup(Type.Vertical);
            window.RegisterPermanentSaving($"multiplayer-sfs.chat-window.{sceneName}");
            int RemainingHeight = WindowHeight - 80;

            container_colorPicker = Builder.CreateContainer(window);
            container_colorPicker.CreateLayoutGroup(Type.Horizontal);
            
            Color.RGBToHSV(LocalManager.Player.iconColor, out float hue, out _, out _);
            hue *= 100;

            input_colorPicker = UIToolsBuilder.CreateNumberInput(container_colorPicker, InnerWidth / 3, 50, hue, 5f);
            label_colorPicker = Builder.CreateLabel(container_colorPicker, InnerWidth / 6, 50, text: "▲");
            button_colorPicker = Builder.CreateButton(container_colorPicker, InnerWidth / 3, 50, onClick: OnColorPickerSubmit, text: "Change");
            
            TMP_InputField field = input_colorPicker.FieldRef<TextInput>("input").field;
            field.onSelect.AddListener(_ => InputSelected = true);
            field.onDeselect.AddListener(_ => InputSelected = false);

            input_colorPicker.OnValueChangedEvent += OnColorPickerChange;
            OnColorPickerChange(hue);

            // * 2 * -60 for both the color picker and the chat input.
            RemainingHeight -= 60 + 60;

            window_messages = Builder.CreateWindow
            (
                window,
                Builder.GetRandomID(),
                InnerWidth,
                RemainingHeight,
                savePosition: false
            );
            window_messages.CreateLayoutGroup(Type.Vertical, TextAnchor.LowerLeft, 5, new RectOffset(5, 5, 5, 5));
            window_messages.EnableScrolling(Type.Vertical);

            foreach (ChatMessage message in messages)
            {
                message.CreateUI();
            }

            input_sendMessage = Builder.CreateTextInput(window, InnerWidth, 50);
            defaultInputColor = input_sendMessage.FieldColor;
            input_sendMessage.field.onSubmit.AddListener(OnMessageSubmit);
            input_sendMessage.field.onSelect.AddListener(_ => InputSelected = true);
            input_sendMessage.field.onDeselect.AddListener(_ => InputSelected = false);
            input_sendMessage.field.textComponent.alignment = TextAlignmentOptions.Left;
            input_sendMessage.field.textComponent.fontSize = 20;
            ChangeCooldownStatus(canSendMessage);
        }

        public static void DestroyUI()
        {
            if (holder_window != null)
            {
                foreach (ChatMessage msg in messages)
                {
                    msg.DestroyUI();
                }
                Object.Destroy(holder_window);
            }
        }

        public static void OnColorPickerChange(float hue)
        {
            float clamped = hue % 100;
            if (clamped < 0)
            {
                clamped += 100;
            }
            if (clamped != hue)
            {
                input_colorPicker.Value = clamped;
                return;
            }
            if (label_colorPicker != null)
            {
                label_colorPicker.Color = Color.HSVToRGB(hue / 100, 1, 1);
                
            }
        }

        public static void OnColorPickerSubmit()
        {
            Color color = LocalManager.Player.iconColor = label_colorPicker.Color;
            ClientManager.SendPacket
            (
                new Packet_UpdatePlayerColor()
                {
                    PlayerId = ClientManager.playerId,
                    Color = color,
                }
            );
            OnPlayerColorChange(ClientManager.playerId, color);
        }

        public static void OnMessageSubmit(string message)
        {
            if (!string.IsNullOrEmpty(message) && canSendMessage)
            {
                AddMessage(new ChatMessage(message, ClientManager.playerId));
                if (cooldownTimer != null)
                {
                    ChangeCooldownStatus(false);
                    cooldownTimer.Start();
                }
                ClientManager.SendPacket
                (
                    new Packet_SendChatMessage()
                    {
                        SenderId = ClientManager.playerId,
                        Message = message,
                    }
                );
                input_sendMessage.Text = "";
                input_sendMessage.field.ActivateInputField();
            }
        }

        public static void CreateCooldownTimer(double cooldownSeconds)
        {
            if (cooldownTimer == null && cooldownSeconds > 0)
            {
                cooldownTimer = new Timer()
                {
                    Interval = 1000 * cooldownSeconds,
                    AutoReset = false,
                };
                cooldownTimer.Elapsed += (s, e) => ChangeCooldownStatus(true);
                cooldownTimer.Start();
            }
        }

        public static void DestroyCooldownTimer()
        {
            if (cooldownTimer != null)
            {
                cooldownTimer.Dispose();
                cooldownTimer = null;
            }
        }

        public static void ChangeCooldownStatus(bool canSend)
        {
            canSendMessage = canSend;
            if (input_sendMessage != null)
            {
                input_sendMessage.FieldColor = canSend ? defaultInputColor : Color.red;
            }
        }

        public static void AddMessage(ChatMessage message)
        {
            messages.Enqueue(message);
            if (window_messages != null)
            {
                message.CreateUI();
            }
            while (messages.Count > maxMessagesCount)
            {
                messages.Dequeue().DestroyUI();
            }
        }

        public static void OnPlayerColorChange(int id, Color color)
        {
            foreach (ChatMessage msg in messages)
            {
                if (msg.senderId == id && msg.label_playerName != null)
                {
                    msg.label_playerName.Color = color;
                }
            }
            if (LocalManager.players.TryGetValue(id, out LocalPlayer player))
            {
                if (LocalManager.syncedRockets.TryGetValue(player.controlledRocket, out LocalRocket rocket) && rocket.rocket != null)
                {
                    // * Updates the rocket's map icon color manually.
                    new Traverse(rocket.rocket.mapIcon).Method("UpdateAlpha").GetValue();
                }
            }
        }
    }

    public class ChatMessage
    {
        public string message;
        public int senderId;

        public Label label_playerName;
        public Label label_message;

        public ChatMessage(string message, int senderId = -1)
        {
            this.message = message;
            this.senderId = senderId;
        }

        public void CreateUI()
        {
            // TODO: When the player is scrolled to the latest messages and a new message is added, the scroll should move to the latest message (which it currently doesn't).
            // SFS.UI.ScrollElement scroll = ChatWindow.window_messages.ChildrenHolder.GetComponent<SFS.UI.ScrollElement>();
            // bool changeScroll = scroll.PercentPosition.y <= 0.05;
            // Debug.Log(changeScroll);
            // Debug.Log(scroll.PercentPosition);

            if (ChatWindow.LastSenderId != senderId)
                {
                    if (LocalManager.players.TryGetValue(senderId, out LocalPlayer player))
                    {
                        label_playerName = Builder.CreateLabel(ChatWindow.window_messages, ChatWindow.InnerWidth, 30, text: player.username);
                        label_playerName.TextAlignment = TextAlignmentOptions.Left;
                        label_playerName.Color = player.iconColor;
                    }
                    else
                    {
                        label_playerName = Builder.CreateLabel(ChatWindow.window_messages, ChatWindow.InnerWidth, 30, text: "SERVER");
                        label_playerName.TextAlignment = TextAlignmentOptions.Left;
                        label_playerName.FontStyle = FontStyles.Bold;
                    }
                }
            label_message = Builder.CreateLabel(ChatWindow.window_messages, ChatWindow.InnerWidth, 25, text: message);
            label_message.TextAlignment = TextAlignmentOptions.Left;

            // if (changeScroll)
            // {
            //     scroll.ResetPosition();
            // }
            // Debug.Log(scroll.PercentPosition);
        }

        public void DestroyUI()
        {
            if (label_playerName != null)
            {
                Object.Destroy(label_playerName.gameObject);
                label_playerName = null;
            }
            if (label_message != null)
            {
                Object.Destroy(label_message.gameObject);
                label_message = null;
            }
        }
    }
}