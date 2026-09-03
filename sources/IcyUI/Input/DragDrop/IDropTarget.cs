// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Input.DragDrop
{
    /// <summary>
    /// Opts a <see cref="UIElement"/> into receiving a <see cref="DragDropSession"/> started by an
    /// <see cref="IDragSource"/> elsewhere in the tree.
    /// </summary>
    /// <remarks>
    /// Unlike ordinary pointer routing, a <see cref="Icy.UI.Canvas"/> re-hit-tests every frame while a
    /// <see cref="DragDropSession"/> is active (see <see cref="DragDropSession"/>'s own remarks), checking each
    /// candidate from the hit leaf up through its ancestors for the first one implementing this interface whose
    /// <see cref="CanDrop"/> returns <see langword="true"/> - that becomes <see cref="DragDropSession.CurrentTarget"/>
    /// until the cursor moves somewhere else or the session ends.
    /// </remarks>
    public interface IDropTarget
    {
        /// <summary>
        /// Called every time the current candidate target changes to this element, before <see cref="OnDragEnter"/>.
        /// </summary>
        /// <param name="session">The active drag session.</param>
        /// <returns>
        /// <see langword="true"/> if this element accepts <paramref name="session"/>'s payload right now (checked
        /// again, every frame the cursor stays over it, and once more at drop time before <see cref="OnDrop"/>
        /// fires).
        /// </returns>
        bool CanDrop(DragDropSession session);

        /// <summary>
        /// Called once, when this element becomes <see cref="DragDropSession.CurrentTarget"/> (after <see cref="CanDrop"/>
        /// returned <see langword="true"/>).
        /// </summary>
        /// <param name="session">The active drag session.</param>
        void OnDragEnter(DragDropSession session);

        /// <summary>
        /// Called once, when this element stops being <see cref="DragDropSession.CurrentTarget"/> - the cursor
        /// moved elsewhere, or the session ended without a drop here.
        /// </summary>
        /// <param name="session">The active drag session.</param>
        void OnDragLeave(DragDropSession session);

        /// <summary>
        /// Called every frame the cursor stays over this element while it's still <see cref="DragDropSession.CurrentTarget"/>,
        /// after the initial <see cref="OnDragEnter"/>.
        /// </summary>
        /// <param name="session">The active drag session.</param>
        void OnDragOver(DragDropSession session);

        /// <summary>
        /// Called once, when the drag ends while this element is <see cref="DragDropSession.CurrentTarget"/> and
        /// <see cref="CanDrop"/> still returns <see langword="true"/>.
        /// </summary>
        /// <param name="session">The active drag session.</param>
        void OnDrop(DragDropSession session);
    }
}
