using System.Drawing;
using AquaUI.Data;

namespace AquaUI.Input.Events
{
    /// <summary>
    /// Represents an interface for the touch events.
    /// </summary>
    public interface ITouchEvents : IInputEventProvider
    {
        /// <summary>
        /// Occurs on the end of the hold tap (or tap with the mouse right button).
        /// </summary>
        event EventHandler<GenericEventArgs<Point>> Hold;

        /// <summary>
        /// Occurs on short tap (click). If the tap is performed repeatedly, event will accumulate touches count.
        /// </summary>
        event EventHandler<GenericEventArgs<TouchInfo>> Tap;

        /// <summary>
        /// Occurs when the touch event was performed. Touch position is transferred to the event arguments.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>> TouchDown;

        /// <summary>
        /// Occurs when the touch event has ended. Touch end position is transferred to the event arguments.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>> TouchUp;

        /// <summary>
        /// Gets or sets an option for the maximal delay for the multitap registering.
        /// </summary>
        TimeSpan MaxMultiTapDelay { get; set; }

        /// <summary>
        /// Gets or sets an option for the maximal distance of the touch to register hold event.
        /// </summary>
        /// <remarks>
        /// If the cursor is moved out of the hold area, the <see cref="IDragEvents"/> drag sequence will be started.
        /// </remarks>
        float HoldAreaSize { get; set; }

        /// <summary>
        /// Gets current virtual cursor position. It is used to highlight active controls under the cursor.
        /// </summary>
        Point CursorPosition { get; }
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