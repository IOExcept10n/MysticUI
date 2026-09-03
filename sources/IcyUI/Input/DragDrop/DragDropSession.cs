// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;

namespace Icy.Input.DragDrop
{
    /// <summary>
    /// State for one active cross-container drag gesture, from the moment an <see cref="IDragSource"/> accepts it
    /// (see <see cref="IDragSource.TryBeginDrag"/>) until the gesture ends.
    /// </summary>
    /// <remarks>
    /// Created and owned entirely by <see cref="Icy.UI.Canvas"/> - an <see cref="IDragSource"/>/<see cref="IDropTarget"/>
    /// only ever observes one handed to it, never constructs one itself.
    /// </remarks>
    public sealed class DragDropSession
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DragDropSession"/> class.
        /// </summary>
        /// <param name="source">The element that started the drag (see <see cref="IDragSource.TryBeginDrag"/>).</param>
        /// <param name="payload">The data carried for the duration of the drag.</param>
        /// <param name="screenPoint">The drag's starting position, in screen/window space.</param>
        public DragDropSession(UIElement source, object payload, Point screenPoint)
        {
            Source = source;
            Payload = payload;
            ScreenPoint = screenPoint;
        }

        /// <summary>
        /// Gets the element that started this drag.
        /// </summary>
        public UIElement Source { get; }

        /// <summary>
        /// Gets the data carried for the duration of this drag, as supplied by <see cref="Source"/>.
        /// </summary>
        public object Payload { get; }

        /// <summary>
        /// Gets the drag's current position, in screen/window space - updated every frame by
        /// <see cref="Icy.UI.Canvas"/> as the gesture continues.
        /// </summary>
        public Point ScreenPoint { get; internal set; }

        /// <summary>
        /// Gets the element rendered via <see cref="Icy.UI.Canvas.Overlays"/> and repositioned to follow the
        /// cursor for the rest of the gesture - set once, from <see cref="IDragSource.TryBeginDrag"/>'s own
        /// <c>preview</c> output.
        /// </summary>
        public UIElement? Preview { get; internal set; }

        /// <summary>
        /// Gets the <see cref="IDropTarget"/> currently under the cursor and willing to accept this session (see
        /// <see cref="IDropTarget.CanDrop"/>), or <see langword="null"/> when nothing under the cursor accepts it.
        /// </summary>
        public IDropTarget? CurrentTarget { get; internal set; }
    }
}
