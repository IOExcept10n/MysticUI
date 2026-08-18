// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;
using Icy.Input.Events;
using Microsoft.Xna.Framework;

namespace Icy.MonoGame.Input.Events
{
    /// <summary>
    /// Represents MonoGame implementation of the text input events listener.
    /// </summary>
    /// <param name="input">An instance of the input system to access devices for.</param>
    /// <param name="game">An instance of the game to subscribe for window input events.</param>
    internal class TextInputEvents(IInputSystem input, Game game) : ITextEvents
    {
        private readonly Game game = game;
        private bool listening = false;

        /// <inheritdoc/>
        public event EventHandler? CopyText;

        /// <inheritdoc/>
        public event EventHandler? CutText;

        /// <inheritdoc/>
        public event EventHandler? PasteText;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<ITextInputEventInfo>>? TextInput;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; } = input;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public TimeSpan RepeatDelay { get; set; }

        /// <inheritdoc/>
        public TimeSpan RepeatStartDelay { get; set; }

        /// <inheritdoc/>
        public void DisableTextInput()
        {
            listening = false;
        }

        /// <inheritdoc/>
        public void EnableTextInput()
        {
            listening = true;
        }

        /// <inheritdoc/>
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

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
        }

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            if (e is IKeyboardInput keyboard)
            {
                keyboard.KeyDown += Keyboard_KeyDown;
            }
        }

        private void Devices_DeviceDisconnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            if (e is IKeyboardInput keyboard)
            {
                keyboard.KeyDown -= Keyboard_KeyDown;
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

        private void Window_TextInput(object? sender, TextInputEventArgs e)
        {
            if (!listening) return;

            // MonoGame's Window.TextInput (backed by the platform's WM_CHAR-equivalent) fires for control
            // characters too, not just printable ones - Backspace produces '\b', Enter '\r', Tab '\t', etc.
            // Consumers like TextBox already handle those via the raw KeyDown event (Keys.Back/.../Keys.Tab);
            // forwarding them here too as "typed text" double-processes a single key press (e.g. Backspace both
            // deletes the character before the caret via KeyDown *and* inserts a literal '\b' via this event).
            if (char.IsControl(e.Character))
                return;

            TextInput?.Invoke(this, new TextInputInfo(Range.All, e.Character.ToString(), TextInputEventType.Input));
        }
    }
}