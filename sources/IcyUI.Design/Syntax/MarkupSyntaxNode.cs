// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// The base class of every node in a markup syntax tree.
    /// </summary>
    /// <remarks>
    /// Nodes are immutable once <see cref="DocumentSyntax.Parse(string)"/> returns. Every node carries the exact
    /// range of source text it came from, delimiters included, so an edit can be expressed as a minimal
    /// <see cref="TextChange"/>.
    /// </remarks>
    public abstract class MarkupSyntaxNode
    {
        /// <summary>Initializes a new instance of the <see cref="MarkupSyntaxNode"/> class.</summary>
        /// <param name="span">The node's range of text.</param>
        private protected MarkupSyntaxNode(TextSpan span)
        {
            Span = span;
        }

        /// <summary>
        /// Gets the range of source text this node covers, including its markup delimiters.
        /// </summary>
        public TextSpan Span { get; internal set; }

        /// <summary>
        /// Gets the element this node belongs to: the owner of an attribute, or the element whose content holds this
        /// node. <see langword="null"/> for a top-level node.
        /// </summary>
        public ElementSyntax? Parent { get; internal set; }
    }
}
