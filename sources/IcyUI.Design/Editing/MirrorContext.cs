// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;

namespace Icy.Design.Editing
{
    /// <summary>
    /// What a <see cref="MirrorAction"/> can see: the document (already holding the new text) and its state from
    /// before the step.
    /// </summary>
    internal sealed class MirrorContext(DesignDocument document, DocumentSnapshot before)
    {
        public DesignDocument Document { get; } = document;

        public DocumentSnapshot Before { get; } = before;

        /// <summary>
        /// Gets or sets the element the step created, reported back through <see cref="EditResult.Node"/>.
        /// </summary>
        public NodeId? ResultNode { get; set; }

        public ElementSyntax? GetOldNode(NodeId id) => Before.Nodes.GetValueOrDefault(id);

        public NodeId? GetOldId(ElementSyntax element) => Before.Ids.TryGetValue(element, out NodeId id) ? id : null;
    }
}
