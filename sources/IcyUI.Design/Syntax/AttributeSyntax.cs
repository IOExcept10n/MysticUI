// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// An attribute in an element's start tag: <c>Name="Value"</c>.
    /// </summary>
    public sealed class AttributeSyntax : MarkupSyntaxNode
    {
        /// <summary>Initializes a new instance of the <see cref="AttributeSyntax"/> class. Only the parser creates them.</summary>
        /// <param name="span">The whole attribute.</param>
        /// <param name="name">The name as written.</param>
        /// <param name="nameSpan">The range of the name.</param>
        /// <param name="valueSpan">The range of the value, quotes excluded.</param>
        /// <param name="quote">The quote character, or <c>\0</c>.</param>
        /// <param name="value">The decoded value.</param>
        /// <param name="isMissingValue">Whether the value is missing.</param>
        internal AttributeSyntax(TextSpan span, string name, TextSpan nameSpan, TextSpan valueSpan, char quote, string value, bool isMissingValue)
            : base(span)
        {
            Name = name;
            NameSpan = nameSpan;
            ValueSpan = valueSpan;
            Quote = quote;
            Value = value;
            IsMissingValue = isMissingValue;
        }

        /// <summary>
        /// Gets the attribute's name as written, prefix included (<c>x:Name</c>, <c>Grid.Row</c>).
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
        /// Gets the range of the name.
        /// </summary>
        public TextSpan NameSpan { get; }

        /// <summary>
        /// Gets the range of the value, quotes excluded. Empty, right after the name, when the value is missing.
        /// </summary>
        public TextSpan ValueSpan { get; }

        /// <summary>
        /// Gets the quote character around the value: <c>"</c>, <c>'</c>, or <c>\0</c> when the value is unquoted
        /// or missing.
        /// </summary>
        public char Quote { get; }

        /// <summary>
        /// Gets the value as an XML reader would report it: entities and character references decoded, and literal
        /// tabs and line breaks normalized to spaces.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets a value indicating whether the attribute has no <c>=value</c> part at all.
        /// </summary>
        public bool IsMissingValue { get; }

        /// <summary>
        /// Gets a value indicating whether this is an <c>xmlns</c> or <c>xmlns:prefix</c> declaration.
        /// </summary>
        public bool IsNamespaceDeclaration => Name == "xmlns" || Name.StartsWith("xmlns:", StringComparison.Ordinal);
    }
}
