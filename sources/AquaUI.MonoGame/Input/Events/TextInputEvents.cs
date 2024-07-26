// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data;
using AquaUI.Input;
using AquaUI.Input.Devices;
using AquaUI.Input.Events;
using Microsoft.Xna.Framework;

namespace AquaUI.MonoGame.Input.Events
{
    internal class TextInputEvents : ITextEvents
    {
        private bool listening = false;
        private readonly Game game;

        public IInputSystem InputSystem { get; }

        public TimeSpan RepeatDelay { get; set; }
        public TimeSpan RepeatStartDelay { get; set; }

        public bool IsInitialized { get; private set; }

        public event EventHandler<GenericEventArgs<ITextInputEventInfo>>? TextInput;

        public event EventHandler? CopyText;

        public event EventHandler? CutText;

        public event EventHandler? PasteText;

        public TextInputEvents(IInputSystem input, Game game)
        {
            InputSystem = input;
            this.game = game;
        }

        private void Window_TextInput(object? sender, TextInputEventArgs e)
        {
            if (!listening) return;
            TextInput?.Invoke(this, new TextInputInfo(Range.All, e.Character.ToString(), TextInputEventType.Input));
        }

        private void Devices_DeviceDisconnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            if (e is IKeyboardInput keyboard)
            {
                keyboard.KeyDown -= Keyboard_KeyDown;
            }
        }

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            if (e is IKeyboardInput keyboard)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }
        }

        private void Keyboard_KeyDown(object? sender, GenericEventArgs<Keys> e)
        {
            if (sender is not IKeyboardInput keyboard)
                return;

            bool isCtrlDown = (keyboard.ModifierKeys & ModifierKeys.Ctrl) != 0;
            if (isCtrlDown)
            {
                switch (e.Data)
                {
                    case Keys.X:
                        CutText?.Invoke(this, EventArgs.Empty); break;
                    case Keys.C:
                        CopyText?.Invoke(this, EventArgs.Empty); break;
                    case Keys.V:
                        PasteText?.Invoke(this, EventArgs.Empty); break;
                    default:
                        return;
                }
            }
        }

        public void DisableTextInput()
        {
            listening = false;
        }

        public void EnableTextInput()
        {
            listening = true;
        }

        public void Update(TimeSpan deltaTime)
        {
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            if (InputSystem.Keyboard != null)
            {
                InputSystem.Keyboard.KeyDown += Keyboard_KeyDown;
            }

            InputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            InputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
            game.Window.TextInput += Window_TextInput;
        }
    }
}