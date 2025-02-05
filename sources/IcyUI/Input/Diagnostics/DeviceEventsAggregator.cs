// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input.Devices;

namespace Icy.Input.Diagnostics
{
    /// <summary>
    /// Represents a simple aggregator class that aggregates all the input events from all of the input devices.
    /// </summary>
    public class DeviceEventsAggregator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DeviceEventsAggregator"/> class.
        /// </summary>
        /// <param name="input">Input system to access the devices.</param>
        public DeviceEventsAggregator(IInputSystem input)
        {
            foreach (var device in input)
            {
                Subscribe(device);
            }

            input.Events.Devices.DeviceConnected += Devices_DeviceConnected;
            input.Events.Devices.DeviceDisconnected += Devices_DeviceDisconnected;
        }

        /// <summary>
        /// Occurs when any device-related event is raised.
        /// </summary>
        public event EventHandler<LoggerEventInfo>? OnEvent;

        /// <summary>
        /// Defines the type of the event captured by <see cref="DeviceEventsAggregator"/> class.
        /// </summary>
        public enum DeviceEventType
        {
            /// <summary>
            /// No device events defined.
            /// </summary>
            None,

            /// <summary>
            /// <see cref="IMouseInput.MouseButtonPressed"/> event.
            /// </summary>
            MouseButtonPressed,

            /// <summary>
            /// <see cref="IMouseInput.MouseButtonReleased"/> event.
            /// </summary>
            MouseButtonReleased,

            /// <summary>
            /// <see cref="IKeyboardInput.KeyDown"/> event.
            /// </summary>
            KeyboardKeyDown,

            /// <summary>
            /// <see cref="IKeyboardInput.KeyUp"/> event.
            /// </summary>
            KeyboardKeyUp,

            /// <summary>
            /// <see cref="IGamepadInput.ButtonPressed"/> event.
            /// </summary>
            GamepadButtonPressed,

            /// <summary>
            /// <see cref="IGamepadInput.ButtonReleased"/> event.
            /// </summary>
            GamepadButtonReleased,

            /// <summary>
            /// <see cref="IGamepadInput.LeftShoulderUpdate"/> event.
            /// </summary>
            GamepadLeftShoulderUpdate,

            /// <summary>
            /// <see cref="IGamepadInput.RightShoulderUpdate"/> event.
            /// </summary>
            GamepadRightShoulderUpdate,

            /// <summary>
            /// <see cref="IGamepadInput.LeftStickMove"/> event.
            /// </summary>
            GamepadLeftStickMove,

            /// <summary>
            /// <see cref="IGamepadInput.RightStickMove"/> event.
            /// </summary>
            GamepadRightStickMove,

            /// <summary>
            /// <see cref="ITouchInput.Tap"/> event.
            /// </summary>
            TouchTap,

            /// <summary>
            /// <see cref="ITouchInput.Hold"/> event.
            /// </summary>
            TouchHold,

            /// <summary>
            /// <see cref="ITouchInput.Swipe"/> event.
            /// </summary>
            TouchSwipe,

            /// <summary>
            /// <see cref="ITouchInput.Drag"/> event.
            /// </summary>
            TouchDrag,
        }

        /// <summary>
        /// Subscribes to all the events for the specified built-in device listener.
        /// </summary>
        /// <param name="device">An instance of the device listener to subscribe the events.</param>
        public void Subscribe(IInputDeviceListener device)
        {
            switch (device)
            {
                case IMouseInput mouse:
                    mouse.MouseButtonPressed += Mouse_MouseButtonPressed;
                    mouse.MouseButtonReleased += Mouse_MouseButtonReleased;
                    break;

                case IKeyboardInput keyboard:
                    keyboard.KeyDown += Keyboard_KeyDown;
                    keyboard.KeyUp += Keyboard_KeyUp;
                    break;

                case IGamepadInput gamepad:
                    gamepad.ButtonPressed += Gamepad_ButtonPressed;
                    gamepad.ButtonReleased += Gamepad_ButtonReleased;
                    gamepad.LeftShoulderUpdate += Gamepad_LeftShoulderUpdate;
                    gamepad.RightShoulderUpdate += Gamepad_RightShoulderUpdate;
                    gamepad.LeftStickMove += Gamepad_LeftStickMove;
                    gamepad.RightStickMove += Gamepad_RightStickMove;
                    break;

                case ITouchInput touch:
                    touch.Tap += Touch_Tap;
                    touch.Hold += Touch_Hold;
                    touch.Drag += Touch_Drag;
                    touch.Swipe += Touch_Swipe;
                    break;
            }
        }

        /// <summary>
        /// Unsubscribes to all the events for the specified built-in device listener.
        /// </summary>
        /// <param name="device">An instance of the device listener to unsubscribe the events.</param>
        public void Unsubscribe(IInputDeviceListener device)
        {
            switch (device)
            {
                case IMouseInput mouse:
                    mouse.MouseButtonPressed -= Mouse_MouseButtonPressed;
                    mouse.MouseButtonReleased -= Mouse_MouseButtonReleased;
                    break;

                case IKeyboardInput keyboard:
                    keyboard.KeyDown -= Keyboard_KeyDown;
                    keyboard.KeyUp -= Keyboard_KeyUp;
                    break;

                case IGamepadInput gamepad:
                    gamepad.ButtonPressed -= Gamepad_ButtonPressed;
                    gamepad.ButtonReleased -= Gamepad_ButtonReleased;
                    gamepad.LeftShoulderUpdate -= Gamepad_LeftShoulderUpdate;
                    gamepad.RightShoulderUpdate -= Gamepad_RightShoulderUpdate;
                    gamepad.LeftStickMove -= Gamepad_LeftStickMove;
                    gamepad.RightStickMove -= Gamepad_RightStickMove;
                    break;

                case ITouchInput touch:
                    touch.Tap -= Touch_Tap;
                    touch.Hold -= Touch_Hold;
                    touch.Drag -= Touch_Drag;
                    touch.Swipe -= Touch_Swipe;
                    break;
            }
        }

        private void Devices_DeviceConnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            Subscribe(e.Data);
        }

        private void Devices_DeviceDisconnected(object? sender, GenericEventArgs<IInputDeviceListener> e)
        {
            Unsubscribe(e.Data);
        }

        private void Gamepad_ButtonPressed(object? sender, GenericEventArgs<GamePadButtons> e) => RaiseEvent(DeviceEventType.GamepadButtonPressed, sender, e);

        private void Gamepad_ButtonReleased(object? sender, GenericEventArgs<GamePadButtons> e) => RaiseEvent(DeviceEventType.GamepadButtonReleased, sender, e);

        private void Gamepad_LeftShoulderUpdate(object? sender, GenericEventArgs<float> e) => RaiseEvent(DeviceEventType.GamepadLeftShoulderUpdate, sender, e);

        private void Gamepad_LeftStickMove(object? sender, GenericEventArgs<System.Numerics.Vector2> e) => RaiseEvent(DeviceEventType.GamepadLeftStickMove, sender, e);

        private void Gamepad_RightShoulderUpdate(object? sender, GenericEventArgs<float> e) => RaiseEvent(DeviceEventType.GamepadRightShoulderUpdate, sender, e);

        private void Gamepad_RightStickMove(object? sender, GenericEventArgs<System.Numerics.Vector2> e) => RaiseEvent(DeviceEventType.GamepadRightStickMove, sender, e);

        private void Keyboard_KeyDown(object? sender, GenericEventArgs<Keys> e) => RaiseEvent(DeviceEventType.KeyboardKeyDown, sender, e);

        private void Keyboard_KeyUp(object? sender, GenericEventArgs<Keys> e) => RaiseEvent(DeviceEventType.KeyboardKeyUp, sender, e);

        private void Mouse_MouseButtonPressed(object? sender, GenericEventArgs<MouseButtons> e) => RaiseEvent(DeviceEventType.MouseButtonPressed, sender, e);

        private void Mouse_MouseButtonReleased(object? sender, GenericEventArgs<MouseButtons> e) => RaiseEvent(DeviceEventType.MouseButtonReleased, sender, e);

        private void Touch_Drag(object? sender, GenericEventArgs<TranslationInfo> e) => RaiseEvent(DeviceEventType.TouchDrag, sender, e);

        private void Touch_Hold(object? sender, GenericEventArgs<System.Drawing.Point> e) => RaiseEvent(DeviceEventType.TouchHold, sender, e);

        private void Touch_Swipe(object? sender, GenericEventArgs<TranslationInfo> e) => RaiseEvent(DeviceEventType.TouchSwipe, sender, e);

        private void Touch_Tap(object? sender, GenericEventArgs<System.Drawing.Point> e) => RaiseEvent(DeviceEventType.TouchTap, sender, e);

        private void RaiseEvent(DeviceEventType type, object? sender, EventArgs args)
        {
            if (sender is IInputDeviceListener device)
                OnEvent?.Invoke(this, new LoggerEventInfo(device, type, args));
        }

        /// <summary>
        /// Provides arguments for the logger events.
        /// </summary>
        /// <param name="InputDevice">Gets an instance of the input device listener that raised the event.</param>
        /// <param name="EventType">Gets the aggregated event type.</param>
        /// <param name="Args">Gets the arguments provided with an event.</param>
        public readonly record struct LoggerEventInfo(IInputDeviceListener InputDevice, DeviceEventType EventType, EventArgs Args);
    }
}
