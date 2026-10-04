// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Takes every live copy of a removed element out of its logical parent.
    /// </summary>
    internal sealed class ElementRemovedAction(NodeId node) : MirrorAction
    {
        private readonly List<Removal> removals = [];
        private NodeId? parentNode;
        private int originalStart;

        public override void Execute(MirrorContext context)
        {
            DesignDocument document = context.Document;
            ElementSyntax element = context.GetOldNode(node)
                ?? throw new InvalidOperationException($"The removed element {node} wasn't in the document.");
            originalStart = element.Span.Start;
            parentNode = element.Parent is { } parent ? context.GetOldId(parent) : null;

            if (parentNode is not NodeId parentId || !document.IsEditable(element, context.Before.Ids))
            {
                document.MarkNeedsReload();
                return;
            }

            foreach ((object instance, MarkupLoadScope scope) in document.Map.GetObjects(node))
            {
                if (instance is not UIElement child || document.FindObject(parentId, scope) is not { } liveParent)
                    continue;

                int index = LiveContent.IndexOf(liveParent, child, document.Registry);
                if (index < 0)
                    continue; // The game already took it out.

                List<(string Name, UIElement Element)> names = LiveTree.UnregisterNames(scope, child);
                LiveContent.Remove(liveParent, child, document.Registry);
                removals.Add(new Removal(liveParent, child, index, scope, names));
            }
        }

        public override void Revert(MirrorContext context)
        {
            for (int i = removals.Count - 1; i >= 0; i--)
            {
                Removal removal = removals[i];
                LiveContent.Insert(removal.Parent, removal.Index, removal.Child, context.Document.Registry);
                LiveTree.RegisterNames(removal.Scope, removal.Names);
            }

            removals.Clear();
        }

        public override MirrorAction CreateInverse(MirrorContext context) =>
            new ElementInsertedAction(parentNode ?? throw new InvalidOperationException("The root element can't be inserted back."), originalStart);

        private sealed record Removal(object Parent, UIElement Child, int Index, MarkupLoadScope Scope, List<(string Name, UIElement Element)> Names);
    }
}
