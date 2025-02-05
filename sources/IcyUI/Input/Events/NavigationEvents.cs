// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Data;
using Icy.Input.Devices;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a default implementation of the <see cref="INavigationEvents"/> interface.
    /// </summary>
    internal class NavigationEvents : INavigationEvents
    {
        // TODO: implement repeat system.

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationEvents"/> class.
        /// </summary>
        /// <param name="inputSystem">An instance of the <see cref="IInputSystem"/> to provide access to input devices and events.</param>
        public NavigationEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<AcceptableEventArgs<Keys>>? AltKeyNavigation;

        /// <inheritdoc/>
        public event EventHandler? CloseModal;

        /// <inheritdoc/>
        public event EventHandler<AcceptableEventArgs<Vector2>>? FocusChanging;

        /// <inheritdoc/>
        public event EventHandler? FocusNext;

        /// <inheritdoc/>
        public event EventHandler? FocusPrevious;

        /// <inheritdoc/>
        public event EventHandler? NavigateBack;

        /// <inheritdoc/>
        public event EventHandler? NavigateForward;

        /// <inheritdoc/>
        public event EventHandler? SelectElement;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public float MinimalFocusChangeDistance { get; set; }

        /// <inheritdoc/>
        public TimeSpan RepeatDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <inheritdoc/>
        public TimeSpan RepeatStartDelay { get; set; } = TimeSpan.FromSeconds(2);

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
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

            if (InputSystem.Gamepad != null)
            {
                InputSystem.Gamepad.ButtonPressed += Gamepad_ButtonPressed;
                InputSystem.Gamepad.LeftStickMove += Gamepad_LeftStickMove;
            }

            InputSystem.Mouse.MouseButtonPressed += Mouse_MouseButtonPressed;

            InputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            InputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IKeyboardInput keyboard:
                    keyboard.KeyDown += Keyboard_KeyDown; break;
                case IGamepadInput gamepad:
                    gamepad.ButtonPressed += Gamepad_ButtonPressed;
                    gamepad.LeftStickMove += Gamepad_LeftStickMove;
                    break;

                case IMouseInput mouse:
                    mouse.MouseButtonPressed += Mouse_MouseButtonPressed;
                    break;
            }
        }

        private void Devices_DeviceDisconnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IKeyboardInput keyboard:
                    keyboard.KeyDown -= Keyboard_KeyDown; break;
                case IGamepadInput gamepad:
                    gamepad.ButtonPressed -= Gamepad_ButtonPressed;
                    gamepad.LeftStickMove -= Gamepad_LeftStickMove;
                    break;

                case IMouseInput mouse:
                    mouse.MouseButtonPressed -= Mouse_MouseButtonPressed;
                    break;
            }
        }

        private void Gamepad_ButtonPressed(object? sender, GenericEventArgs<GamePadButtons> e)
        {
            switch (e.Data)
            {
                case GamePadButtons.PadUp:
                    OnDirectionalFocus(-Vector2.UnitY);
                    break;

                case GamePadButtons.PadDown:
                    OnDirectionalFocus(Vector2.UnitY);
                    break;

                case GamePadButtons.PadLeft:
                    OnDirectionalFocus(-Vector2.UnitX);
                    break;

                case GamePadButtons.PadRight:
                    OnDirectionalFocus(Vector2.UnitX);
                    break;

                case GamePadButtons.A:
                    SelectElement?.Invoke(this, EventArgs.Empty);
                    break;

                case GamePadButtons.B:
                    CloseModal?.Invoke(this, EventArgs.Empty);
                    break;

                case GamePadButtons.X:
                    NavigateBack?.Invoke(this, EventArgs.Empty);
                    break;

                case GamePadButtons.Y:
                    NavigateForward?.Invoke(this, EventArgs.Empty);
                    break;

                case GamePadButtons.LeftThumb:
                    FocusPrevious?.Invoke(this, EventArgs.Empty);
                    break;

                case GamePadButtons.RightThumb:
                    FocusNext?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        private void Gamepad_LeftStickMove(object? sender, GenericEventArgs<Vector2> e)
        {
            if (e.Data.LengthSquared() > MinimalFocusChangeDistance * MinimalFocusChangeDistance)
            {
                OnDirectionalFocus(e.Data);
            }
        }

        private void Keyboard_KeyDown(object? sender, GenericEventArgs<Keys> e)
        {
            if (sender is not IKeyboardInput keyboard)
                return;
            if ((keyboard.ModifierKeys & ModifierKeys.Alt) != 0)
            {
                var args = new AcceptableEventArgs<Keys>() { Data = e.Data };
                if (e.Data.IsModifier() || e.Data.IsWrongAltKey())
                    return;
                AltKeyNavigation?.Invoke(this, args);
                return;
            }

            switch (e.Data)
            {
                case Keys.Escape:
                    CloseModal?.Invoke(this, EventArgs.Empty);
                    break;

                case Keys.Enter:
                    SelectElement?.Invoke(this, EventArgs.Empty);
                    break;

                case Keys.Up:
                    OnDirectionalFocus(-Vector2.UnitY);
                    break;

                case Keys.Down:
                    OnDirectionalFocus(Vector2.UnitY);
                    break;

                case Keys.Left:
                    OnDirectionalFocus(-Vector2.UnitX);
                    break;

                case Keys.Right:
                    OnDirectionalFocus(Vector2.UnitX);
                    break;

                case Keys.Tab:
                    if ((keyboard.ModifierKeys & ModifierKeys.Shift) != 0)
                    {
                        FocusPrevious?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        FocusNext?.Invoke(this, EventArgs.Empty);
                    }

                    break;
            }
        }

        private void Mouse_MouseButtonPressed(object? sender, GenericEventArgs<MouseButtons> e)
        {
            switch (e.Data)
            {
                case MouseButtons.ExtendedButton1:
                    NavigateForward?.Invoke(this, EventArgs.Empty);
                    break;

                case MouseButtons.ExtendedButton2:
                    NavigateBack?.Invoke(this, EventArgs.Empty);
                    break;
            }
        }

        private void OnDirectionalFocus(Vector2 direction)
        {
            var args = new AcceptableEventArgs<Vector2>() { Data = direction };
            FocusChanging?.Invoke(this, args);
            if (!args.Cancel && args.Handled)
            {
                // TODO: make repeat assignment.
            }
        }
    }
}
