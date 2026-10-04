// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// A document's text, tree and id maps at one moment, so a failed step can put them back.
    /// </summary>
    internal sealed record DocumentSnapshot(
        MarkupText Text,
        DocumentSyntax Syntax,
        LineMap LineMap,
        Dictionary<ElementSyntax, NodeId> Ids,
        Dictionary<NodeId, ElementSyntax> Nodes);
}
