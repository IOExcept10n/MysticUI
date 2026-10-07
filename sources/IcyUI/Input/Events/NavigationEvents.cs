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
        // The stick releases below this fraction of MinimalFocusChangeDistance, so a stick resting at the edge doesn't flicker.
        private const float StickReleaseFraction = 0.7f;

        private HeldSource heldSource;
        private Keys heldKey;
        private GamePadButtons heldButton;
        private Vector2 heldDirection;
        private TimeSpan heldTime;
        private bool repeating;
        private Vector2 stickDirection;

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

        private enum HeldSource
        {
            None,
            Key,
            PadButton,
            Stick,
        }

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public float MinimalFocusChangeDistance { get; set; } = 0.5f;

        /// <inheritdoc/>
        public TimeSpan RepeatDelay { get; set; } = TimeSpan.FromSeconds(0.1);

        /// <inheritdoc/>
        public TimeSpan RepeatStartDelay { get; set; } = TimeSpan.FromSeconds(0.4);

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        /// <remarks>
        /// Repeats a held direction (arrow key, D-pad button or stick): first after <see cref="RepeatStartDelay"/>, then
        /// every <see cref="RepeatDelay"/>.
        /// </remarks>
        public void Update(TimeSpan deltaTime)
        {
            if (heldSource == HeldSource.None)
                return;

            heldTime += deltaTime;
            if (heldTime >= (repeating ? RepeatDelay : RepeatStartDelay))
            {
                repeating = true;
                heldTime = TimeSpan.Zero;
                OnDirectionalFocus(heldDirection);
            }
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            if (InputSystem.Keyboard != null)
            {
                InputSystem.Keyboard.KeyDown += Keyboard_KeyDown;
                InputSystem.Keyboard.KeyUp += Keyboard_KeyUp;
            }

            if (InputSystem.Gamepad != null)
            {
                InputSystem.Gamepad.ButtonPressed += Gamepad_ButtonPressed;
                InputSystem.Gamepad.ButtonReleased += Gamepad_ButtonReleased;
                InputSystem.Gamepad.LeftStickMove += Gamepad_LeftStickMove;
            }

            InputSystem.Mouse.MouseButtonPressed += Mouse_MouseButtonPressed;

            InputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            InputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        private static Vector2 Snap(Vector2 value) =>
            MathF.Abs(value.X) >= MathF.Abs(value.Y) ? new Vector2(MathF.Sign(value.X), 0) : new Vector2(0, MathF.Sign(value.Y));

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IKeyboardInput keyboard:
                    keyboard.KeyDown += Keyboard_KeyDown;
                    keyboard.KeyUp += Keyboard_KeyUp;
                    break;
                case IGamepadInput gamepad:
                    gamepad.ButtonPressed += Gamepad_ButtonPressed;
                    gamepad.ButtonReleased += Gamepad_ButtonReleased;
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
                    keyboard.KeyDown -= Keyboard_KeyDown;
                    keyboard.KeyUp -= Keyboard_KeyUp;

                    // A device that goes away mid-hold never sends its release.
                    Release(HeldSource.Key);
                    break;
                case IGamepadInput gamepad:
                    gamepad.ButtonPressed -= Gamepad_ButtonPressed;
                    gamepad.ButtonReleased -= Gamepad_ButtonReleased;
                    gamepad.LeftStickMove -= Gamepad_LeftStickMove;
                    Release(HeldSource.PadButton);
                    Release(HeldSource.Stick);
                    stickDirection = Vector2.Zero;
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
                    Press(-Vector2.UnitY, HeldSource.PadButton, button: e.Data);
                    break;

                case GamePadButtons.PadDown:
                    Press(Vector2.UnitY, HeldSource.PadButton, button: e.Data);
                    break;

                case GamePadButtons.PadLeft:
                    Press(-Vector2.UnitX, HeldSource.PadButton, button: e.Data);
                    break;

                case GamePadButtons.PadRight:
                    Press(Vector2.UnitX, HeldSource.PadButton, button: e.Data);
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
            // Devices report +Y as up; UI space is +Y down.
            var value = new Vector2(e.Data.X, -e.Data.Y);
            float magnitude = value.Length();
            if (stickDirection == Vector2.Zero)
            {
                if (magnitude > MinimalFocusChangeDistance)
                {
                    stickDirection = Snap(value);
                    Press(stickDirection, HeldSource.Stick);
                }

                return;
            }

            if (magnitude <= MinimalFocusChangeDistance * StickReleaseFraction)
            {
                stickDirection = Vector2.Zero;
                Release(HeldSource.Stick);
                return;
            }

            Vector2 snapped = Snap(value);
            if (snapped != stickDirection)
            {
                stickDirection = snapped;
                Press(snapped, HeldSource.Stick);
            }
        }

        private void Gamepad_ButtonReleased(object? sender, GenericEventArgs<GamePadButtons> e)
        {
            if (heldSource == HeldSource.PadButton && heldButton == e.Data)
                Release(HeldSource.PadButton);
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

            // Ctrl/Shift+arrow belong to editing (word jumps, selection), not to focus navigation.
            bool editingModifiers = (keyboard.ModifierKeys & (ModifierKeys.Ctrl | ModifierKeys.Shift)) != 0;
            switch (e.Data)
            {
                case Keys.Escape:
                    CloseModal?.Invoke(this, EventArgs.Empty);
                    break;

                case Keys.Enter:
                    SelectElement?.Invoke(this, EventArgs.Empty);
                    break;

                case Keys.Up:
                    if (!editingModifiers)
                        Press(-Vector2.UnitY, HeldSource.Key, key: e.Data);
                    break;

                case Keys.Down:
                    if (!editingModifiers)
                        Press(Vector2.UnitY, HeldSource.Key, key: e.Data);
                    break;

                case Keys.Left:
                    if (!editingModifiers)
                        Press(-Vector2.UnitX, HeldSource.Key, key: e.Data);
                    break;

                case Keys.Right:
                    if (!editingModifiers)
                        Press(Vector2.UnitX, HeldSource.Key, key: e.Data);
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

        private void Keyboard_KeyUp(object? sender, GenericEventArgs<Keys> e)
        {
            if (heldSource == HeldSource.Key && heldKey == e.Data)
                Release(HeldSource.Key);
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

        private void OnDirectionalFocus(Vector2 direction) => FocusChanging?.Invoke(this, new AcceptableEventArgs<Vector2>() { Data = direction });

        /// <summary>Raises a direction now and makes it the held one; the latest press wins over any older one.</summary>
        private void Press(Vector2 direction, HeldSource source, Keys key = Keys.None, GamePadButtons button = default)
        {
            heldSource = source;
            heldKey = key;
            heldButton = button;
            heldDirection = direction;
            heldTime = TimeSpan.Zero;
            repeating = false;
            OnDirectionalFocus(direction);
        }

        private void Release(HeldSource source)
        {
            if (heldSource == source)
                heldSource = HeldSource.None;
        }
    }
}
