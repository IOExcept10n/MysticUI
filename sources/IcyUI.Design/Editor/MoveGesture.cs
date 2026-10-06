// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor.Placement;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// A move in progress: the selected element follows the pointer as a ghost, and lands where the container under the
    /// pointer places it when the gesture completes. Nothing is written before <see cref="Complete"/>.
    /// </summary>
    public sealed class MoveGesture
    {
        private readonly EditorSession session;
        private readonly EditorSelection selection;
        private readonly MarkupLoadScope scope;
        private readonly UIElement? sourceContainer;
        private readonly Point start;
        private readonly Rectangle startScreenBounds;
        private bool finished;

        internal MoveGesture(EditorSession session, EditorSelection selection, MarkupLoadScope scope, UIElement? sourceContainer, Point start)
        {
            this.session = session;
            this.selection = selection;
            this.scope = scope;
            this.sourceContainer = sourceContainer;
            this.start = start;
            UIElement element = selection.Instance;
            Point topLeft = element.PointToScreen(Vector2.Zero);
            Point bottomRight = element.PointToScreen(new Vector2(element.ActualBounds.Width, element.ActualBounds.Height));
            startScreenBounds = Rectangle.FromLTRB(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y);
            GhostBounds = startScreenBounds;
        }

        /// <summary>
        /// Gets where the element would land, or <see langword="null"/> when nothing under the pointer can take it.
        /// </summary>
        public PlacementTarget? Target { get; private set; }

        /// <summary>
        /// Gets the container <see cref="Target"/> belongs to, or <see langword="null"/>.
        /// </summary>
        public UIElement? TargetContainer { get; private set; }

        /// <summary>
        /// Gets the dragged element's outline at the pointer, in screen space.
        /// </summary>
        public Rectangle GhostBounds { get; private set; }

        /// <summary>
        /// Moves the ghost and finds the drop target under <paramref name="screenPoint"/>.
        /// </summary>
        /// <param name="screenPoint">The pointer, in screen space.</param>
        public void Update(Point screenPoint)
        {
            if (finished)
                return;

            int dx = screenPoint.X - start.X;
            int dy = screenPoint.Y - start.Y;
            GhostBounds = startScreenBounds with { X = startScreenBounds.X + dx, Y = startScreenBounds.Y + dy };
            Target = null;
            TargetContainer = null;

            ElementSyntax? dragged = EditorSession.Syntax(selection);
            if (dragged == null)
                return;

            for (UIElement? candidate = session.ResolveSelectable(session.HitTest(screenPoint)); candidate != null; candidate = session.ResolveSelectable(candidate.Parent))
            {
                if (TryTarget(candidate, dragged, GhostBounds.Location))
                    return;
            }
        }

        /// <summary>
        /// Writes the move as one undo step, or does nothing when there's no target.
        /// </summary>
        /// <returns>The outcome; a failed edit rolls the whole move back.</returns>
        public EditResult Complete()
        {
            if (finished)
                return EditResult.Success();
            finished = true;

            if (Target is not { } target || TargetContainer is not { } container || session.IsBlocked)
                return EditResult.Success();
            if (session.Design.FindDocument(container, out NodeId containerId) != selection.Document)
                return EditResult.Success();

            var edits = new List<AttributeEdit>();
            bool changesContainer = !ReferenceEquals(container, sourceContainer);
            if (changesContainer && sourceContainer != null)
            {
                foreach (string owned in session.Placement.Resolve(sourceContainer).OwnedAttributes)
                    edits.Add(new AttributeEdit(owned, null));
            }

            edits.AddRange(target.Edits);
            var gesture = new GestureEdits(selection.Document, $"Move {EditorSession.Syntax(selection)?.Name}");
            EditResult moved = gesture.Run(() => selection.Document.Editor.MoveElement(selection.Node, containerId, target.Index));
            EditResult result = moved.Succeeded ? gesture.Apply(selection.Node, edits) : moved;
            if (result.Succeeded)
            {
                gesture.Commit();
                session.NoteEdited(selection.Document);
            }
            else
            {
                gesture.Rollback();
            }

            return result;
        }

        /// <summary>
        /// Abandons the move. Nothing was written, so nothing changes.
        /// </summary>
        public void Cancel()
        {
            finished = true;
            Target = null;
            TargetContainer = null;
        }

        private bool TryTarget(UIElement candidate, ElementSyntax dragged, Point ghostTopLeft)
        {
            DesignDocument document = selection.Document;
            if (ReferenceEquals(candidate, selection.Instance)
                || session.Design.FindDocument(candidate, out NodeId candidateId) != document
                || EditorSession.ScopeOf(document, candidate) != scope
                || document.GetNode(candidateId) is not { } candidateSyntax
                || ReferenceEquals(candidateSyntax, dragged)
                || dragged.IsAncestorOf(candidateSyntax)
                || !LiveAccepts(candidate))
            {
                return false;
            }

            var children = new List<PlacementChild>();
            int index = 0;
            foreach (ElementSyntax child in candidateSyntax.ContentElements)
            {
                if (ReferenceEquals(child, dragged))
                    continue;
                if (document.GetNodeId(child) is NodeId childId && EditorSession.FindInstance(document, childId, scope) is { } live)
                    children.Add(new PlacementChild(index, live));
                index++;
            }

            bool current = ReferenceEquals(candidate, sourceContainer);
            Rectangle bounds = current
                ? PlacementContext.ToLocal(candidate, selection.Instance.ActualBounds)
                : new Rectangle(0, 0, selection.Instance.ActualBounds.Width, selection.Instance.ActualBounds.Height);
            var context = new PlacementContext(candidate, selection.Instance, children, index, bounds, current);
            IPlacementStrategy candidateStrategy = session.Placement.Resolve(candidate);
            Vector2 local = candidate.PointToLocal(ghostTopLeft);
            if (candidateStrategy.GetDropTarget(context, local) is not { } found)
                return false;

            Target = found;
            TargetContainer = candidate;
            return true;
        }

        private bool LiveAccepts(UIElement candidate) =>
            Editing.LiveContent.TryResolve(candidate, selection.Document.Registry, out var member, out var list)
            && Editing.LiveContent.Accepts(member, list, selection.Instance.GetType());
    }
}
