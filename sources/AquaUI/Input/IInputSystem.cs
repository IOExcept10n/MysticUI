// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using AquaUI.Data;
using AquaUI.Input.Clipboard;
using AquaUI.Input.Devices;

namespace AquaUI.Input
{
    /// <summary>
    /// An interface that provides engine-independent input system for the UI system.
    /// </summary>
    public interface IInputSystem : IEnumerable<IInputDeviceListener>, IInitializable
    {
        /// <summary>
        /// Gets the service that provides input events for the UI elements.
        /// </summary>
        IInputEventSystem Events { get; }

        /// <summary>
        /// Gets the listener for the keyboard events.
        /// </summary>
        IKeyboardInput? Keyboard { get; }

        /// <summary>
        /// Gets the listener for the mouse events.
        /// </summary>
        IMouseInput Mouse { get; }

        /// <summary>
        /// Gets the listener for the gamepad events.
        /// </summary>
        /// <remarks>
        /// Note that this UI framework doesn't define multiple gamepad support, so only main gamepad input will be received.
        /// You can implement it by yourself if it's necessary for your game.
        /// However, it will require multiple focus support. For that purposes, you'll have to make your own control focus system.
        /// </remarks>
        IGamepadInput? Gamepad { get; }

        /// <summary>
        /// Gets the service for the touch events.
        /// </summary>
        ITouchInput? Touch { get; }

        /// <summary>
        /// Gets the service for the platform clipboard implementation.
        /// </summary>
        IClipboard Clipboard { get; }

        /// <summary>
        /// Determines whether the specified input device listener is connected to the input system.
        /// </summary>
        /// <typeparam name="T">Type of the input device listener.</typeparam>
        /// <returns><see langword="true"/> if the device is available; otherwise <see langword="false"/>.</returns>
        bool IsDeviceAvailable<T>()
            where T : IInputDeviceListener;

        /// <summary>
        /// Gets an input device listener for the specified type.
        /// </summary>
        /// <typeparam name="T">Type of the device to get listener for.</typeparam>
        /// <returns>An input device listener for the specified device type.</returns>
        /// <exception cref="KeyNotFoundException">Occurs when the device for the specified type is not connected.</exception>
        T GetInputDevice<T>()
            where T : IInputDeviceListener;

        /// <summary>
        /// Tries to get a listener for the specified input device.
        /// </summary>
        /// <typeparam name="T">Type of the device to get a listener for.</typeparam>
        /// <param name="device">Listener for the specified device.</param>
        /// <returns><see langword="true"/> if the device listener was acquired successfully; otherwise <see langword="false"/>.</returns>
        bool TryGetInputDevice<T>([NotNullWhen(true)] out T? device)
            where T : IInputDeviceListener;
    }
}