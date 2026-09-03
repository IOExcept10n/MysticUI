// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;

namespace Icy.Input.DragDrop
{
    /// <summary>
    /// Opts a <see cref="UIElement"/> into cross-container drag-and-drop (see <see cref="DragDropSession"/>) -
    /// separate from <see cref="UIElement.OnDragStarted(Point)"/>/<see cref="UIElement.OnDragPerforming(Point)"/>/
    /// <see cref="UIElement.OnDragEnded(Point)"/>, which only ever support a single-target "hold and move the
    /// pointer" gesture within the one element that was originally hit (e.g. a <c>Slider</c>'s thumb, or a future
    /// <c>SplitPane</c>'s divider) - <see cref="Icy.UI.Canvas"/> hit-tests once at drag-start and never again.
    /// </summary>
    /// <remarks>
    /// A <see cref="Icy.UI.Canvas"/> checks every element from the drag's hit leaf up through its ancestors (the
    /// same walk <see cref="UIElement.OnDragStarted(Point)"/> already uses) for the first one implementing this
    /// interface whose <see cref="TryBeginDrag"/> returns <see langword="true"/> - both mechanisms can coexist on
    /// the same gesture, since one is about a specific element moving itself and the other is about carrying data
    /// to a different element entirely.
    /// </remarks>
    public interface IDragSource
    {
        /// <summary>
        /// Called once, when a drag gesture starts on this element (or a descendant).
        /// </summary>
        /// <param name="screenPoint">The drag's starting position, in screen/window space.</param>
        /// <param name="payload">
        /// The data to carry for the duration of the drag, made available to every <see cref="IDropTarget"/>
        /// considered along the way via <see cref="DragDropSession.Payload"/>. Must be non-<see langword="null"/>
        /// when this method returns <see langword="true"/>.
        /// </param>
        /// <param name="preview">
        /// An optional element rendered via <see cref="Icy.UI.Canvas.Overlays"/>, repositioned to follow the
        /// cursor for the rest of the gesture and removed automatically once it ends. <see langword="null"/> draws
        /// no preview.
        /// </param>
        /// <returns>
        /// <see langword="true"/> to begin a <see cref="DragDropSession"/>; <see langword="false"/> to decline (the
        /// gesture still proceeds as an ordinary single-target drag for any element that handles it that way).
        /// </returns>
        bool TryBeginDrag(Point screenPoint, out object? payload, out UIElement? preview);
    }
}
