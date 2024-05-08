using AquaUI.Controls;
using AquaUI.Data;

namespace AquaUI.Input.Events
{
    /// <summary>
    /// Represents an interface for the scroll events.
    /// </summary>
    public interface IScrollEvents
    {
        /// <summary>
        /// Occurs when player scrolls controls with mouse.
        /// </summary>
        /// <remarks>
        /// Note that long-timed press with gestures can be recognized as <see cref="IDragEvents"/>.
        /// This method handle only mouse wheel scroll and touch flicks.
        /// When the flick is performed, the event is performed two times: with <b>vertical</b> and <b>horizontal</b> components separated.
        /// </remarks>
        event EventHandler<GenericEventArgs<ScrollInfo>> Scroll;
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