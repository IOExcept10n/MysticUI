// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents an interface for the touch events.
    /// </summary>
    /// <remarks>
    /// Built on <see cref="Gestures.IGestureEvents"/>; its thresholds live in <see cref="Gestures.IGestureEvents.Settings"/>.
    /// </remarks>
    public interface ITouchEvents : IInputEventProvider
    {
        /// <summary>
        /// Occurs on the end of the hold tap (or tap with the mouse right button).
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? Hold;

        /// <summary>
        /// Occurs on short tap (click). If the tap is performed repeatedly, event will accumulate touches count.
        /// </summary>
        event EventHandler<GenericEventArgs<TouchInfo>>? Tap;

        /// <summary>
        /// Occurs when the touch event was performed. Touch position is transferred to the event arguments.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? TouchDown;

        /// <summary>
        /// Occurs when the touch event has ended. Touch end position is transferred to the event arguments.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? TouchUp;
    }

    /// <summary>
    /// Provides the information about the screen tap event.
    /// </summary>
    public readonly struct TouchInfo
    {
        /// <summary>
        /// Point of the last touch.
        /// </summary>
        public readonly Point LastTouch;

        /// <summary>
        /// Count of total taps since the start of multiple taps.
        /// </summary>
        public readonly int TouchCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="TouchInfo"/> struct.
        /// </summary>
        /// <param name="lastTouch">Point of the last touch.</param>
        /// <param name="touchCount">Count of touches performed.</param>
        public TouchInfo(Point lastTouch, int touchCount)
        {
            LastTouch = lastTouch;
            TouchCount = touchCount;
        }
    }
}
