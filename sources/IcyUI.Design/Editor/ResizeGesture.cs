// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor.Placement;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// A resize in progress: every <see cref="Update"/> writes the new attributes through the fast path, so the element
    /// follows the pointer live, and the whole gesture is one undo step.
    /// </summary>
    public sealed class ResizeGesture
    {
        private readonly EditorSession session;
        private readonly EditorSelection selection;
        private readonly UIElement container;
        private readonly IResizeOperation operation;
        private readonly GestureEdits edits;
        private readonly Point start;
        private EditResult? failure;
        private bool finished;

        internal ResizeGesture(EditorSession session, EditorSelection selection, UIElement container, IResizeOperation operation, Point start)
        {
            this.session = session;
            this.selection = selection;
            this.container = container;
            this.operation = operation;
            this.start = start;
            edits = new GestureEdits(selection.Document, $"Resize {EditorSession.Syntax(selection)?.Name}");
        }

        /// <summary>
        /// Gets the area the element will occupy in its container, in the container's local space, as the
        /// placement strategy reports it (<see cref="IResizeOperation.Indicator"/>); or <see langword="null"/> when it reports
        /// none.
        /// </summary>
        public RectangleF? Indicator => operation.Indicator;

        /// <summary>
        /// Gets the container the element is resized in; <see cref="Indicator"/> is in its local space.
        /// </summary>
        internal UIElement Container => container;

        /// <summary>
        /// Resizes to the pointer at <paramref name="screenPoint"/>.
        /// </summary>
        /// <param name="screenPoint">The pointer, in screen space.</param>
        /// <param name="snap">
        /// <see langword="true"/> to let the placement strategy snap the dragged edge to its guides; <see langword="false"/>
        /// to follow the pointer exactly.
        /// </param>
        public void Update(Point screenPoint, bool snap = true)
        {
            if (finished || failure != null)
                return;

            // Both points are mapped in the same frame, so a container that moves as the element resizes doesn't feed
            // its own movement back into the delta.
            Vector2 delta = container.PointToLocal(screenPoint) - container.PointToLocal(start);
            EditResult result = edits.ApplyExactly(selection.Node, operation.Update(delta, snap));
            if (!result.Succeeded)
                failure = result;
        }

        /// <summary>
        /// Keeps the resize as one undo step.
        /// </summary>
        /// <returns>The outcome; when an update failed, the gesture is rolled back and the failure returned.</returns>
        public EditResult Complete()
        {
            if (finished)
                return failure ?? EditResult.Success();
            finished = true;
            session.EndResize(this);

            if (failure != null)
            {
                edits.Rollback();
                return failure;
            }

            edits.Commit();
            session.NoteEdited(selection.Document);
            return EditResult.Success();
        }

        /// <summary>
        /// Abandons the resize and puts everything back, leaving no undo entry.
        /// </summary>
        public void Cancel()
        {
            if (finished)
                return;

            finished = true;
            session.EndResize(this);
            edits.Rollback();
        }
    }
}
