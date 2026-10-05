// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Rebuilds every live copy of an element from its current text, keeping its id: for an element whose type, own
    /// text or namespace declarations changed, or that fell out of sync.
    /// </summary>
    internal sealed class ElementReplacedAction(NodeId node) : MirrorAction
    {
        private bool rebuilt;

        public override void Execute(MirrorContext context)
        {
            DesignDocument document = context.Document;
            if (document.GetNode(node) is not { } element)
                return;

            if (!document.IsEditable(element))
            {
                document.MarkNeedsReload();
                return;
            }

            // Set first: a rebuild that fails halfway may already have replaced some copies.
            rebuilt = true;
            document.Rebuild(element);
        }

        public override void Revert(MirrorContext context)
        {
            // The document holds the old tree again, so rebuilding shows the old markup.
            if (rebuilt && context.Document.GetNode(node) is { } element)
                context.Document.Rebuild(element);
            rebuilt = false;
        }

        public override MirrorAction CreateInverse(MirrorContext context) => new ElementReplacedAction(node);
    }
}
