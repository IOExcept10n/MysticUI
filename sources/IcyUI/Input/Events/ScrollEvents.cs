// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.UI;
using Icy.Data;
using Icy.Input.Devices;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IScrollEvents"/> interface.
    /// </summary>
    internal class ScrollEvents : IScrollEvents
    {
        private TimeSpan delay;
        private Point? lastMousePressPosition;
        private Vector2 deltaTranslation;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrollEvents"/> class.
        /// </summary>
        /// <param name="inputSystem">An instance of the <see cref="IInputSystem"/> interface this event processor.</param>
        public ScrollEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<ScrollInfo>>? Scroll;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public TimeSpan RepeatDelay { get; set; } = TimeSpan.FromMilliseconds(100);

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            var mouseInfo = InputSystem.Mouse.MouseInfo;
            if (Math.Abs(mouseInfo.Wheel) > 0.01f)
            {
                ScrollInfo scrollInfo;
                if (InputSystem.Keyboard != null && (InputSystem.Keyboard.ModifierKeys & ModifierKeys.Shift) != 0)
                {
                    scrollInfo = new ScrollInfo(mouseInfo.Wheel, Orientation.Horizontal);
                }
                else
                {
                    scrollInfo = new ScrollInfo(mouseInfo.Wheel, Orientation.Vertical);
                }

                Scroll?.Invoke(this, scrollInfo);
            }
            else if (lastMousePressPosition != null)
            {
                deltaTranslation += new Vector2(mouseInfo.Position.X - lastMousePressPosition.Value.X, mouseInfo.Position.Y - lastMousePressPosition.Value.Y);
                lastMousePressPosition = mouseInfo.Position;
                if (delay > RepeatDelay)
                {
                    delay -= RepeatDelay;
                    DirectionScroll(-deltaTranslation);
                }

                delay += deltaTime;
            }
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            if (InputSystem.Gamepad != null)
            {
                InputSystem.Gamepad.RightStickMove += Gamepad_RightStickMove;
            }

            if (InputSystem.Touch != null)
            {
                InputSystem.Touch.Swipe += Touch_Swipe;
            }

            InputSystem.Mouse.MouseButtonPressed += Mouse_MouseButtonPressed;
            InputSystem.Mouse.MouseButtonReleased += Mouse_MouseButtonReleased;

            InputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            InputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IMouseInput mouse:
                    mouse.MouseButtonPressed += Mouse_MouseButtonPressed;
                    mouse.MouseButtonReleased += Mouse_MouseButtonReleased;
                    break;

                case IGamepadInput gamepad:
                    gamepad.RightStickMove += Gamepad_RightStickMove;
                    break;

                case ITouchInput touch:
                    touch.Swipe += Touch_Swipe;
                    break;
            }
        }

        private void Devices_DeviceDisconnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IMouseInput mouse:
                    mouse.MouseButtonPressed -= Mouse_MouseButtonPressed;
                    mouse.MouseButtonReleased -= Mouse_MouseButtonReleased;
                    break;

                case IGamepadInput gamepad:
                    gamepad.RightStickMove -= Gamepad_RightStickMove;
                    break;

                case ITouchInput touch:
                    touch.Swipe -= Touch_Swipe;
                    break;
            }
        }

        private void DirectionScroll(Vector2 direction)
        {
            Scroll?.Invoke(this, new ScrollInfo(direction.X, Orientation.Horizontal));
            Scroll?.Invoke(this, new ScrollInfo(direction.Y, Orientation.Vertical));
        }

        private void Gamepad_RightStickMove(object? sender, GenericEventArgs<Vector2> e)
        {
            DirectionScroll(e.Data);
        }

        private void Mouse_MouseButtonPressed(object? sender, GenericEventArgs<MouseButtons> e)
        {
            if (sender is IMouseInput mouse && e.Data == MouseButtons.MiddleButton)
            {
                lastMousePressPosition = mouse.MouseInfo.Position;
                delay = default;
                deltaTranslation = default;
            }
        }

        private void Mouse_MouseButtonReleased(object? sender, GenericEventArgs<MouseButtons> e)
        {
            if (sender is IMouseInput && (e.Data == MouseButtons.MiddleButton))
            {
                OnEndTouchScroll();
            }
        }

        private void Touch_Swipe(object? sender, GenericEventArgs<TranslationInfo> e)
        {
            // When scrolling with swipes, scroll should be performed to the direction opposite from the swipe direction.
            DirectionScroll(-e.Data.DeltaTranslation);
        }

        private void OnEndTouchScroll()
        {
            delay = default;
            lastMousePressPosition = null;
            deltaTranslation = default;
        }
    }
}
