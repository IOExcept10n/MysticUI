// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input;
using Icy.Input.Devices;
using Icy.Input.Events;
using TInputEvent = Stride.Input.InputEvent;
using TInputManager = Stride.Input.InputManager;
using TTextInputEvent = Stride.Input.TextInputEvent;

namespace Icy.Stride.Input.Events
{
    /// <summary>
    /// Represents the Stride implementation of the text input events listener.
    /// </summary>
    /// <remarks>
    /// Unlike MonoGame (which has no built-in text-input assembly and needs a <c>Window.TextInput</c> event
    /// subscription), Stride's own <see cref="TInputManager"/> already assembles raw key presses into
    /// text via its <c>TextInput</c> API - this class just surfaces that through <see cref="ITextEvents"/>.
    /// </remarks>
    /// <param name="input">An instance of the input system to access devices for.</param>
    /// <param name="strideInput">The underlying Stride input manager to read text-input events from.</param>
    internal class TextInputEvents(IInputSystem input, TInputManager strideInput) : ITextEvents
    {
        private bool listening;

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
            strideInput.TextInput?.DisableTextInput();
        }

        /// <inheritdoc/>
        public void EnableTextInput()
        {
            listening = true;
            strideInput.TextInput?.EnabledTextInput();
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized)
                return;
            IsInitialized = true;

            if (InputSystem.Keyboard != null)
            {
                InputSystem.Keyboard.KeyDown += Keyboard_KeyDown;
            }

            InputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            InputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            if (!listening)
                return;

            foreach (TInputEvent inputEvent in strideInput.Events)
            {
                if (inputEvent is TTextInputEvent textInputEvent)
                    TextInput?.Invoke(this, new TextInputInfo(Range.All, textInputEvent.Text, TextInputEventType.Input));
            }
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
    }
}
