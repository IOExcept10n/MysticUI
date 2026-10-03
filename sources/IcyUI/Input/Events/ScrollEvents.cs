// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Data;
using Icy.Input.Devices;
using Icy.UI;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IScrollEvents"/> interface.
    /// </summary>
    internal class ScrollEvents : IScrollEvents
    {
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

            InputSystem.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            InputSystem.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IGamepadInput gamepad:
                    gamepad.RightStickMove += Gamepad_RightStickMove;
                    break;
            }
        }

        private void Devices_DeviceDisconnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            switch (e.Data)
            {
                case IGamepadInput gamepad:
                    gamepad.RightStickMove -= Gamepad_RightStickMove;
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
    }
}