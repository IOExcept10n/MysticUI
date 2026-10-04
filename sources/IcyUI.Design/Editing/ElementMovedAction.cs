// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Moves every live copy of an element to its new logical parent, keeping the same instance and its runtime state.
    /// </summary>
    internal sealed class ElementMovedAction(NodeId node, NodeId newParent, int newStart) : MirrorAction
    {
        private readonly List<Move> moves = [];
        private NodeId oldParent;
        private int oldStart;

        public override MoveHint? Hint => new MoveHint(node, newStart);

        public override void Execute(MirrorContext context)
        {
            DesignDocument document = context.Document;
            ElementSyntax oldElement = context.GetOldNode(node)
                ?? throw new InvalidOperationException($"The moved element {node} wasn't in the document.");
            oldStart = oldElement.Span.Start;
            ElementSyntax oldParentElement = oldElement.Parent
                ?? throw new InvalidOperationException("The root element can't be moved.");
            oldParent = context.GetOldId(oldParentElement)
                ?? throw new InvalidOperationException("The moved element's old parent has no id.");
            ElementSyntax moved = document.GetNode(node)
                ?? throw new InvalidOperationException("The moved element lost its id.");
            ElementSyntax target = document.GetNode(newParent)
                ?? throw new InvalidOperationException("The new parent lost its id.");

            if (!document.IsEditable(oldElement, context.Before.Ids) || !document.IsEditable(oldParentElement, context.Before.Ids) || !document.IsEditable(target))
            {
                document.MarkNeedsReload();
                return;
            }

            foreach ((object instance, MarkupLoadScope scope) in document.Map.GetObjects(node))
            {
                if (instance is not UIElement child
                    || document.FindObject(oldParent, scope) is not { } from
                    || document.FindObject(newParent, scope) is not { } to)
                {
                    continue;
                }

                int oldIndex = LiveContent.IndexOf(from, child, document.Registry);
                if (oldIndex < 0)
                    continue;

                LiveContent.Remove(from, child, document.Registry);

                // Recorded before inserting, so a failing insert is still put back by Revert.
                moves.Add(new Move(child, from, oldIndex, to));
                LiveContent.Insert(to, document.ComputeLiveIndex(moved, to, scope), child, document.Registry);
            }
        }

        public override void Revert(MirrorContext context)
        {
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                Move move = moves[i];
                LiveContent.Remove(move.To, move.Child, context.Document.Registry);
                LiveContent.Insert(move.From, move.OldIndex, move.Child, context.Document.Registry);
            }

            moves.Clear();
        }

        public override MirrorAction CreateInverse(MirrorContext context) => new ElementMovedAction(node, oldParent, oldStart);

        private sealed record Move(UIElement Child, object From, int OldIndex, object To);
    }
}
