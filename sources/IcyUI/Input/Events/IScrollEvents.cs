// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.UI;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents an interface for the scroll events.
    /// </summary>
    public interface IScrollEvents : IInputEventProvider, IRepeatableInputEvents
    {
        /// <summary>
        /// Occurs when player scrolls controls with mouse.
        /// </summary>
        /// <remarks>
        /// Raised for the mouse wheel, middle-button autoscroll and the gamepad right stick. Touch scrolling is built on
        /// <see cref="Gestures.IGestureEvents"/>. Two-dimensional input raises the event twice: once with the
        /// <b>horizontal</b> and once with the <b>vertical</b> component.
        /// </remarks>
        event EventHandler<GenericEventArgs<ScrollInfo>>? Scroll;
    }

    /// <summary>
    /// Information about the scroll event.
    /// </summary>
    public readonly struct ScrollInfo
    {
        /// <summary>
        /// Value of the scroll (relative, platform-dependent).
        /// </summary>
        public readonly float Delta;

        /// <summary>
        /// Direction of the scroll.
        /// </summary>
        public readonly Orientation ScrollOrientation;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrollInfo"/> struct.
        /// </summary>
        /// <param name="delta">Value of the scroll since the last frame.</param>
        /// <param name="scrollOrientation">Orientation of the scroll.</param>
        public ScrollInfo(float delta, Orientation scrollOrientation)
        {
            Delta = delta;
            ScrollOrientation = scrollOrientation;
        }
    }
}