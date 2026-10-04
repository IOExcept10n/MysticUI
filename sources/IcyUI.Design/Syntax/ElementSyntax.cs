// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// An element: its start tag with attributes, its content, and its end tag.
    /// </summary>
    public sealed class ElementSyntax : MarkupSyntaxNode
    {
        private IReadOnlyList<AttributeSyntax> attributes = [];
        private IReadOnlyList<MarkupSyntaxNode> content = [];

        /// <summary>Initializes a new instance of the <see cref="ElementSyntax"/> class. Only the parser creates them.</summary>
        /// <param name="start">The offset of the element's <c>&lt;</c>.</param>
        /// <param name="name">The name as written.</param>
        /// <param name="nameSpan">The range of the name.</param>
        internal ElementSyntax(int start, string name, TextSpan nameSpan)
            : base(new TextSpan(start, 0))
        {
            Name = name;
            NameSpan = nameSpan;
        }

        /// <summary>
        /// Gets the element's name as written, prefix included (<c>ui:Border</c>, <c>Grid.RowDefinitions</c>).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the part of <see cref="Name"/> before the first <c>:</c>, or an empty string when there is none.
        /// </summary>
        public string Prefix => Name.IndexOf(':', StringComparison.Ordinal) is int colon and > 0 ? Name[..colon] : string.Empty;

        /// <summary>
        /// Gets the part of <see cref="Name"/> after the first <c>:</c>, or the whole name when there is none.
        /// </summary>
        public string LocalName => Name.IndexOf(':', StringComparison.Ordinal) is int colon and > 0 ? Name[(colon + 1)..] : Name;

        /// <summary>
        /// Gets the range of the name in the start tag.
        /// </summary>
        public TextSpan NameSpan { get; }

        /// <summary>
        /// Gets the attributes, in source order.
        /// </summary>
        public IReadOnlyList<AttributeSyntax> Attributes => attributes;

        /// <summary>
        /// Gets the offset just past the start tag's closing <c>&gt;</c> or <c>/&gt;</c>.
        /// </summary>
        public int StartTagEnd { get; internal set; }

        /// <summary>
        /// Gets the range of the start tag.
        /// </summary>
        public TextSpan StartTagSpan => TextSpan.FromBounds(Span.Start, StartTagEnd);

        /// <summary>
        /// Gets a value indicating whether the element is written as <c>&lt;Name/&gt;</c>.
        /// </summary>
        public bool IsSelfClosing { get; internal set; }

        /// <summary>
        /// Gets the nodes between the start and end tags, in source order, whitespace included.
        /// </summary>
        public IReadOnlyList<MarkupSyntaxNode> Content => content;

        /// <summary>
        /// Gets the range of the end tag, or <see langword="null"/> for a self-closing element or one whose end tag
        /// is missing.
        /// </summary>
        public TextSpan? EndTagSpan { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the element was never properly closed.
        /// </summary>
        public bool IsMissingEndTag { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this is a property element such as <c>&lt;Grid.RowDefinitions&gt;</c>.
        /// </summary>
        public bool IsPropertyElement => LocalName.Contains('.', StringComparison.Ordinal);

        /// <summary>
        /// Gets the child elements, property elements included.
        /// </summary>
        public IEnumerable<ElementSyntax> Elements
        {
            get
            {
                foreach (MarkupSyntaxNode node in content)
                {
                    if (node is ElementSyntax element)
                        yield return element;
                }
            }
        }

        /// <summary>
        /// Gets the child elements that are content, excluding property elements.
        /// </summary>
        public IEnumerable<ElementSyntax> ContentElements => Elements.Where(x => !x.IsPropertyElement);

        /// <summary>
        /// Finds an attribute by its name as written.
        /// </summary>
        /// <param name="name">The attribute name, prefix included.</param>
        /// <returns>The attribute, or <see langword="null"/> when the element has none of that name.</returns>
        public AttributeSyntax? FindAttribute(string name)
        {
            foreach (AttributeSyntax attribute in attributes)
            {
                if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
                    return attribute;
            }

            return null;
        }

        /// <summary>
        /// Enumerates this element and every element below it, in document order.
        /// </summary>
        /// <returns>The elements, this one first.</returns>
        public IEnumerable<ElementSyntax> DescendantsAndSelf()
        {
            var stack = new Stack<ElementSyntax>();
            stack.Push(this);
            while (stack.Count > 0)
            {
                ElementSyntax current = stack.Pop();
                yield return current;

                for (int i = current.content.Count - 1; i >= 0; i--)
                {
                    if (current.content[i] is ElementSyntax child)
                        stack.Push(child);
                }
            }
        }

        /// <summary>
        /// Determines whether this element contains <paramref name="other"/> at any depth.
        /// </summary>
        /// <param name="other">The element to look for.</param>
        /// <returns><see langword="true"/> when <paramref name="other"/> is below this element.</returns>
        public bool IsAncestorOf(ElementSyntax other)
        {
            ArgumentNullException.ThrowIfNull(other);
            for (ElementSyntax? parent = other.Parent; parent != null; parent = parent.Parent)
            {
                if (ReferenceEquals(parent, this))
                    return true;
            }

            return false;
        }

        /// <summary>Sets the attributes, once, while parsing.</summary>
        /// <param name="value">The attributes.</param>
        internal void SetAttributes(IReadOnlyList<AttributeSyntax> value) => attributes = value;

        /// <summary>Sets the content, once, while parsing.</summary>
        /// <param name="value">The content nodes.</param>
        internal void SetContent(IReadOnlyList<MarkupSyntaxNode> value) => content = value;
    }
}
