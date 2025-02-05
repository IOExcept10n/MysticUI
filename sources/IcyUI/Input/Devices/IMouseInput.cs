// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;

namespace Icy.Input.Devices
{
    /// <summary>
    /// Represents an interface for the mouse input listener.
    /// </summary>
    public interface IMouseInput : IInputDeviceListener, IInitializable
    {
        /// <summary>
        /// Occurs when the mouse button is pressed. Passes the pressed button info.
        /// </summary>
        event EventHandler<GenericEventArgs<MouseButtons>>? MouseButtonPressed;

        /// <summary>
        /// Occurs then the mouse button is released. Passes the released button info.
        /// </summary>
        event EventHandler<GenericEventArgs<MouseButtons>>? MouseButtonReleased;

        /// <summary>
        /// Gets the current mouse state.
        /// </summary>
        MouseInfo MouseInfo { get; }
    }
}
