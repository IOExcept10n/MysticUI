// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Builds the element the text now has at <paramref name="start"/> and adds it to every live copy of its parent.
    /// When <paramref name="restoredIds"/> is given, the element is a removed one coming back, and gets its old ids.
    /// </summary>
    internal sealed class ElementInsertedAction(NodeId parent, int start, IReadOnlyList<NodeId>? restoredIds = null) : MirrorAction
    {
        private readonly List<(object Parent, UIElement Child, MarkupLoadScope Scope)> inserted = [];
        private NodeId? insertedNode;

        public override RestoreHint? Restore => restoredIds != null ? new RestoreHint(start, restoredIds) : null;

        public override void Execute(MirrorContext context)
        {
            DesignDocument document = context.Document;
            ElementSyntax element = document.Syntax.FindElementAt(start)
                ?? throw new InvalidOperationException("The inserted element isn't where the edit put it.");
            insertedNode = document.GetNodeId(element);
            context.ResultNode = insertedNode;

            if (document.GetNode(parent) is not { } parentElement || !document.IsEditable(parentElement) || element.IsPropertyElement)
            {
                document.MarkNeedsReload();
                return;
            }

            foreach ((object liveParent, MarkupLoadScope scope) in document.Map.GetObjects(parent))
            {
                UIElement built = document.BuildElement(scope, element, (UIElement)liveParent);
                try
                {
                    LiveContent.Insert(liveParent, document.ComputeLiveIndex(element, liveParent, scope), built, document.Registry);
                }
                catch
                {
                    // Built but never added: Revert won't see it, so take its names and map entries back here.
                    LiveTree.UnregisterNames(scope, built);
                    document.Map.RemoveSubtree(built);
                    throw;
                }

                inserted.Add((liveParent, built, scope));
            }
        }

        public override void Revert(MirrorContext context)
        {
            DesignDocument document = context.Document;
            for (int i = inserted.Count - 1; i >= 0; i--)
            {
                (object liveParent, UIElement child, MarkupLoadScope scope) = inserted[i];
                LiveTree.UnregisterNames(scope, child);
                LiveContent.Remove(liveParent, child, document.Registry);
                document.Map.RemoveSubtree(child);
            }

            inserted.Clear();
        }

        public override MirrorAction CreateInverse(MirrorContext context) =>
            new ElementRemovedAction(insertedNode ?? throw new InvalidOperationException("The inserted element has no id."));
    }
}
