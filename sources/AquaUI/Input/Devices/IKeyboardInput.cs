// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data;

namespace AquaUI.Input.Devices
{
    /// <summary>
    /// Represents an interface for the keyboard input listener.
    /// </summary>
    public interface IKeyboardInput : IInputDeviceListener, IInitializable
    {
        /// <summary>
        /// Occurs when the keyboard key is down. Passes a value of the pressed key.
        /// </summary>
        event EventHandler<GenericEventArgs<Keys>>? KeyDown;

        /// <summary>
        /// Occurs when the keyboard key is up. Passes a value of the released key.
        /// </summary>
        event EventHandler<GenericEventArgs<Keys>>? KeyUp;

        /// <summary>
        /// Gets the set of keys that are down during this frame.
        /// </summary>
        IEnumerable<Keys> KeysDown { get; }

        /// <summary>
        /// Gets all the pressed modifier keys.
        /// </summary>
        ModifierKeys ModifierKeys { get; }
    }
}