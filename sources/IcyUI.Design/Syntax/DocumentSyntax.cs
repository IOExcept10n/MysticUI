// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Markup;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// The span-preserving syntax tree of one markup text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every character of <see cref="Text"/> belongs to exactly one node in <see cref="Nodes"/> or below, so the
    /// tree reproduces the text byte for byte, comments, formatting and entities included.
    /// </para>
    /// <para>
    /// Parsing never throws. Malformed text produces <see cref="Diagnostics"/>, and the offending characters are
    /// kept as <see cref="TextSyntax"/> nodes.
    /// </para>
    /// </remarks>
    public sealed class DocumentSyntax
    {
        private readonly Dictionary<int, ElementSyntax> elementsByStart;
        private readonly Dictionary<int, ElementSyntax> elementsByNameStart;
        private readonly Dictionary<int, AttributeSyntax> attributesByNameStart;

        /// <summary>Initializes a new instance of the <see cref="DocumentSyntax"/> class. Only the parser creates them.</summary>
        /// <param name="text">The parsed text.</param>
        /// <param name="nodes">The top-level nodes.</param>
        /// <param name="root">The root element.</param>
        /// <param name="diagnostics">The problems found.</param>
        /// <param name="elementsByStart">Elements by the offset of their <c>&lt;</c>.</param>
        /// <param name="elementsByNameStart">Elements by the offset of their name.</param>
        /// <param name="attributesByNameStart">Attributes by the offset of their name.</param>
        internal DocumentSyntax(
            string text,
            IReadOnlyList<MarkupSyntaxNode> nodes,
            ElementSyntax? root,
            IReadOnlyList<Diagnostic> diagnostics,
            Dictionary<int, ElementSyntax> elementsByStart,
            Dictionary<int, ElementSyntax> elementsByNameStart,
            Dictionary<int, AttributeSyntax> attributesByNameStart)
        {
            Text = text;
            Nodes = nodes;
            Root = root;
            Diagnostics = diagnostics;
            this.elementsByStart = elementsByStart;
            this.elementsByNameStart = elementsByNameStart;
            this.attributesByNameStart = attributesByNameStart;
        }

        /// <summary>
        /// Gets the text the tree was parsed from.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the top-level nodes: the root element and any whitespace, comments and processing instructions
        /// around it.
        /// </summary>
        public IReadOnlyList<MarkupSyntaxNode> Nodes { get; }

        /// <summary>
        /// Gets the root element, or <see langword="null"/> when the text has none.
        /// </summary>
        public ElementSyntax? Root { get; }

        /// <summary>
        /// Gets the problems found while parsing; empty for well-formed markup.
        /// </summary>
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Diagnostics"/> is non-empty.
        /// </summary>
        public bool HasErrors => Diagnostics.Count > 0;

        /// <summary>
        /// Gets every element under <see cref="Root"/>, the root first, in document order.
        /// </summary>
        public IEnumerable<ElementSyntax> Elements => Root?.DescendantsAndSelf() ?? [];

        /// <summary>
        /// Parses <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The markup text.</param>
        /// <returns>The syntax tree.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        public static DocumentSyntax Parse(string text) => MarkupParser.Parse(text);

        /// <summary>
        /// Finds the element whose start tag begins (its <c>&lt;</c>) at <paramref name="start"/>.
        /// </summary>
        /// <param name="start">The offset of the element's <c>&lt;</c>.</param>
        /// <returns>The element, or <see langword="null"/> when none starts there.</returns>
        public ElementSyntax? FindElementAt(int start) => elementsByStart.GetValueOrDefault(start);

        /// <summary>
        /// Collects the namespace declarations in effect for <paramref name="element"/>, keyed by prefix (the empty
        /// string for the default namespace).
        /// </summary>
        /// <param name="element">The element to collect for.</param>
        /// <param name="includeSelf">
        /// <see langword="true"/> to include <paramref name="element"/>'s own declarations; <see langword="false"/>
        /// to collect only what it inherits from its ancestors.
        /// </param>
        /// <returns>The declarations, an inner declaration hiding an outer one with the same prefix.</returns>
        public IReadOnlyDictionary<string, string> GetNamespacesInScope(ElementSyntax element, bool includeSelf = false)
        {
            ArgumentNullException.ThrowIfNull(element);

            var chain = new List<ElementSyntax>();
            for (ElementSyntax? current = includeSelf ? element : element.Parent; current != null; current = current.Parent)
                chain.Add(current);

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                foreach (AttributeSyntax attribute in chain[i].Attributes)
                {
                    if (attribute.IsNamespaceDeclaration)
                        result[attribute.Name == "xmlns" ? string.Empty : attribute.Name[6..]] = attribute.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a namespace prefix the way the markup loader does, including its predeclared <c>x</c> prefix.
        /// </summary>
        /// <param name="element">The element the prefix is used on.</param>
        /// <param name="prefix">The prefix, or an empty string for the default namespace.</param>
        /// <returns>The namespace URI, or <see langword="null"/> when the prefix isn't declared.</returns>
        public string? ResolvePrefix(ElementSyntax element, string prefix)
        {
            if (GetNamespacesInScope(element, includeSelf: true).TryGetValue(prefix, out string? uri))
                return uri;

            return prefix == "x" ? MarkupNamespaces.Directives : null;
        }

        /// <summary>Finds the element whose name starts at <paramref name="offset"/>, where an XML reader reports its position.</summary>
        /// <param name="offset">The offset of the name's first character.</param>
        /// <returns>The element, or <see langword="null"/>.</returns>
        internal ElementSyntax? FindElementByNameStart(int offset) => elementsByNameStart.GetValueOrDefault(offset);

        /// <summary>Finds the attribute whose name starts at <paramref name="offset"/>, where an XML reader reports its position.</summary>
        /// <param name="offset">The offset of the name's first character.</param>
        /// <returns>The attribute, or <see langword="null"/>.</returns>
        internal AttributeSyntax? FindAttributeByNameStart(int offset) => attributesByNameStart.GetValueOrDefault(offset);
    }
}
