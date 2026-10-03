// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Data;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents an interface for the drag and drop related events.
    /// </summary>
    /// <remarks>
    /// Usually, when player uses mouse for control,
    /// he can <i>hold</i> the pointer with left button pressed
    /// and then <i>move</i> it anywhere out of minimal drag distance.
    /// This will trigger <b>drag event sequence</b>:
    /// <list type="bullet">
    /// <item>At the start of cursor movement out of range, the <see cref="DragStarted"/> event is raised.</item>
    /// <item>Whenever the pointer moves during the drag, the <see cref="DragPerforming"/> event is raised.</item>
    /// <item>When the pointer is released, the <see cref="DragEnded"/> event is raised. When the drag ends without a
    /// release (a second finger started a pinch, or the contact vanished), <see cref="DragCanceled"/> is raised instead.</item>
    /// </list>
    /// Drags come from touch and the left mouse button, recognized by <see cref="Gestures.IGestureEvents"/>.
    /// The <see cref="ITouchEvents.Hold"/> event is suppressed while dragging.
    /// However, the <see cref="ITouchEvents.TouchDown"/> and <see cref="ITouchEvents.TouchUp"/> events are performed anyway.
    /// </remarks>
    public interface IDragEvents : IInputEventProvider
    {
        /// <summary>
        /// Occurs when the drag sequence is started.
        /// </summary>
        event EventHandler<AcceptableEventArgs<Point>>? DragStarted;

        /// <summary>
        /// Occurs when the pointer moves while the drag sequence is performing.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? DragPerforming;

        /// <summary>
        /// Occurs when the drag sequence has been ended.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? DragEnded;

        /// <summary>
        /// Occurs when a drag ends without being released - a second finger started a pinch, or the contact vanished.
        /// Handlers must undo the drag rather than complete it (no drop).
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? DragCanceled;
    }
}
