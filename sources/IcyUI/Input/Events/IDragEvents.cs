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
    /// <item>While all the route of drag process, the <see cref="DragPerforming"/> event is raised every frame.</item>
    /// <item>At the end of the drag, the <see cref="DragEnded"/> event is raised.</item>
    /// </list>
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
        /// Occurs every frame while the drag sequence is performing.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? DragPerforming;

        /// <summary>
        /// Occurs when the drag sequence has been ended.
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? DragEnded;

        /// <summary>
        /// Notifies to start listening for the mouse movement.
        /// </summary>
        /// <param name="lastCursorPosition">Last cursor position to start drag tracking.</param>
        void OnMouseMove(Point lastCursorPosition);
    }
}
