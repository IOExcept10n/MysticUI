// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A span-preserving, error-tolerant parser for the XML subset IcyUI markup uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every character of the input ends up inside exactly one top-level node's span, so the tree always
    /// reproduces the text byte for byte. Malformed input never throws: the parser reports a
    /// <see cref="Diagnostic"/>, keeps the offending characters as text, and carries on.
    /// </para>
    /// <para>
    /// The parser only builds syntax. Namespace prefixes are resolved later, with the loader's rules, by
    /// <see cref="DocumentSyntax.ResolvePrefix"/>. Tokens are offsets into the text; strings are only created for
    /// the names and values the tree exposes.
    /// </para>
    /// </remarks>
    internal sealed class MarkupParser
    {
        private readonly string text;
        private readonly List<Diagnostic> diagnostics = [];
        private readonly List<ElementSyntax> openElements = [];
        private readonly Dictionary<int, ElementSyntax> elementsByStart = [];
        private readonly Dictionary<int, ElementSyntax> elementsByNameStart = [];
        private readonly Dictionary<int, AttributeSyntax> attributesByNameStart = [];
        private int position;

        private MarkupParser(string text)
        {
            this.text = text;
        }

        /// <summary>Parses <paramref name="text"/>.</summary>
        /// <param name="text">The markup text.</param>
        /// <returns>The syntax tree.</returns>
        public static DocumentSyntax Parse(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var parser = new MarkupParser(text);
            List<MarkupSyntaxNode> nodes = parser.ParseContent(null);

            ElementSyntax? root = null;
            foreach (MarkupSyntaxNode node in nodes)
            {
                if (node is not ElementSyntax element)
                    continue;

                if (root == null)
                    root = element;
                else
                    parser.Report(element.NameSpan, "A document can have only one root element.");
            }

            if (root == null)
                parser.Report(new TextSpan(text.Length, 0), "The document has no root element.");

            return new DocumentSyntax(text, nodes, root, parser.diagnostics, parser.elementsByStart, parser.elementsByNameStart, parser.attributesByNameStart);
        }

        /// <summary>Determines whether <paramref name="c"/> can start a name.</summary>
        /// <param name="c">The character.</param>
        /// <returns><see langword="true"/> when it can.</returns>
        public static bool IsNameStartChar(char c) => char.IsLetter(c) || c is '_' or ':';

        /// <summary>Determines whether <paramref name="c"/> can appear in a name.</summary>
        /// <param name="c">The character.</param>
        /// <returns><see langword="true"/> when it can.</returns>
        public static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or ':' or '.' or '-';

        /// <summary>Determines whether <paramref name="name"/> is a valid element or attribute name.</summary>
        /// <param name="name">The name.</param>
        /// <returns><see langword="true"/> when it is.</returns>
        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || !IsNameStartChar(name[0]))
                return false;

            foreach (char c in name)
            {
                if (!IsNameChar(c))
                    return false;
            }

            return true;
        }

        private List<MarkupSyntaxNode> ParseContent(ElementSyntax? parent)
        {
            var nodes = new List<MarkupSyntaxNode>();
            while (position < text.Length)
            {
                MarkupSyntaxNode node;
                if (text[position] != '<')
                {
                    node = ParseText();
                }
                else if (StartsWith("</"))
                {
                    // Inside an element, the caller handles its own end tag (or decides this one isn't it).
                    if (parent != null)
                        break;
                    node = ParseStrayEndTag();
                }
                else if (StartsWith("<!--"))
                {
                    node = new CommentSyntax(ScanDelimited("<!--", "-->", "comment"));
                }
                else if (StartsWith("<![CDATA["))
                {
                    node = new CDataSyntax(ScanDelimited("<![CDATA[", "]]>", "CDATA section"));
                }
                else if (StartsWith("<?"))
                {
                    node = new ProcessingInstructionSyntax(ScanDelimited("<?", "?>", "processing instruction"));
                }
                else if (StartsWith("<!"))
                {
                    node = ParseDocumentType();
                }
                else if (position + 1 < text.Length && IsNameStartChar(text[position + 1]))
                {
                    node = ParseElement(parent);
                }
                else
                {
                    var span = new TextSpan(position, 1);
                    position++;
                    Report(span, "'<' must start a tag. Escape it as '&lt;' in text.");
                    node = new TextSyntax(span);
                }

                node.Parent = parent;
                nodes.Add(node);
            }

            return nodes;
        }

        private TextSyntax ParseText()
        {
            int start = position;
            int next = text.IndexOf('<', position);
            position = next < 0 ? text.Length : next;
            return new TextSyntax(TextSpan.FromBounds(start, position));
        }

        private TextSyntax ParseStrayEndTag()
        {
            int start = position;
            position += 2;
            string name = ReadName();
            SkipRestOfTag();
            var span = TextSpan.FromBounds(start, position);
            Report(span, name.Length > 0 ? $"Unexpected end tag '{name}'." : "Unexpected end tag.");
            return new TextSyntax(span);
        }

        private ProcessingInstructionSyntax ParseDocumentType()
        {
            int start = position;
            position += 2;
            SkipRestOfTag();
            var span = TextSpan.FromBounds(start, position);
            Report(span, "Document type declarations aren't supported.");
            return new ProcessingInstructionSyntax(span);
        }

        private TextSpan ScanDelimited(string open, string close, string what)
        {
            int start = position;
            int end = text.IndexOf(close, position + open.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                position = text.Length;
                var span = TextSpan.FromBounds(start, position);
                Report(span, $"The {what} isn't closed with '{close}'.");
                return span;
            }

            position = end + close.Length;
            return TextSpan.FromBounds(start, position);
        }

        private ElementSyntax ParseElement(ElementSyntax? parent)
        {
            int start = position++;
            int nameStart = position;
            string name = ReadName();
            var element = new ElementSyntax(start, name, new TextSpan(nameStart, name.Length)) { Parent = parent };
            elementsByStart[start] = element;
            elementsByNameStart[nameStart] = element;

            var attributes = new List<AttributeSyntax>();
            bool closed = false;
            while (position < text.Length)
            {
                SkipWhitespace();
                if (position >= text.Length)
                    break;

                char c = text[position];
                if (c == '>')
                {
                    position++;
                    closed = true;
                    break;
                }

                if (c == '/' && position + 1 < text.Length && text[position + 1] == '>')
                {
                    position += 2;
                    element.IsSelfClosing = true;
                    closed = true;
                    break;
                }

                if (c == '<')
                    break;

                if (IsNameStartChar(c))
                {
                    attributes.Add(ParseAttribute(element));
                    continue;
                }

                Report(new TextSpan(position, 1), $"Unexpected character '{c}' in the start tag of '{name}'.");
                position++;
            }

            element.StartTagEnd = position;
            element.SetAttributes(attributes);

            if (!closed)
            {
                Report(TextSpan.FromBounds(start, position), $"The start tag of '{name}' isn't closed.");
                element.IsMissingEndTag = true;
                element.Span = TextSpan.FromBounds(start, position);
                return element;
            }

            if (element.IsSelfClosing)
            {
                element.Span = TextSpan.FromBounds(start, position);
                return element;
            }

            openElements.Add(element);
            var content = new List<MarkupSyntaxNode>();
            while (true)
            {
                content.AddRange(ParseContent(element));
                if (position >= text.Length)
                {
                    Report(element.NameSpan, $"'{name}' has no end tag.");
                    element.IsMissingEndTag = true;
                    break;
                }

                int endStart = position;
                position += 2;
                string closing = ReadName();
                if (string.Equals(closing, name, StringComparison.Ordinal))
                {
                    SkipWhitespace();
                    if (position < text.Length && text[position] == '>')
                        position++;
                    else
                        Report(TextSpan.FromBounds(endStart, position), $"The end tag of '{name}' isn't closed.");

                    element.EndTagSpan = TextSpan.FromBounds(endStart, position);
                    break;
                }

                if (IsOpenAncestor(closing))
                {
                    // It closes an ancestor: this element simply never got its own end tag.
                    position = endStart;
                    Report(element.NameSpan, $"'{name}' has no end tag.");
                    element.IsMissingEndTag = true;
                    break;
                }

                // It matches nothing that is open: keep it as text so the round trip holds, and carry on.
                SkipRestOfTag();
                var stray = new TextSyntax(TextSpan.FromBounds(endStart, position)) { Parent = element };
                Report(stray.Span, $"Unexpected end tag '{closing}'.");
                content.Add(stray);
            }

            openElements.RemoveAt(openElements.Count - 1);
            element.SetContent(content);
            element.Span = TextSpan.FromBounds(start, position);
            return element;
        }

        private AttributeSyntax ParseAttribute(ElementSyntax owner)
        {
            int start = position;
            string name = ReadName();
            var nameSpan = new TextSpan(start, name.Length);
            int afterName = position;

            SkipWhitespace();
            if (position >= text.Length || text[position] != '=')
            {
                position = afterName;
                Report(nameSpan, $"Attribute '{name}' has no value.");
                return Register(new AttributeSyntax(nameSpan, name, nameSpan, new TextSpan(afterName, 0), '\0', string.Empty, isMissingValue: true), owner);
            }

            position++;
            SkipWhitespace();

            TextSpan valueSpan;
            char quote = '\0';
            if (position < text.Length && text[position] is '"' or '\'')
            {
                quote = text[position];
                int valueStart = position + 1;
                int close = text.IndexOf(quote, valueStart);
                int nextTag = text.IndexOf('<', valueStart);
                if (close < 0 || (nextTag >= 0 && nextTag < close))
                {
                    // '<' can never appear in a valid value, so it is a safe place to stop an unclosed one.
                    int valueEnd = nextTag >= 0 ? nextTag : text.Length;
                    valueSpan = TextSpan.FromBounds(valueStart, valueEnd);
                    position = valueEnd;
                    Report(TextSpan.FromBounds(start, valueEnd), $"The value of '{name}' isn't closed with {quote}.");
                }
                else
                {
                    valueSpan = TextSpan.FromBounds(valueStart, close);
                    position = close + 1;
                }
            }
            else
            {
                int valueStart = position;
                while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not ('>' or '<') && !StartsWith("/>"))
                    position++;

                valueSpan = TextSpan.FromBounds(valueStart, position);
                Report(TextSpan.FromBounds(start, position), $"The value of '{name}' must be quoted.");
            }

            string value = MarkupEscaping.DecodeAttributeValue(text, valueSpan);
            return Register(new AttributeSyntax(TextSpan.FromBounds(start, position), name, nameSpan, valueSpan, quote, value, isMissingValue: false), owner);
        }

        private AttributeSyntax Register(AttributeSyntax attribute, ElementSyntax owner)
        {
            attribute.Parent = owner;
            attributesByNameStart[attribute.NameSpan.Start] = attribute;
            return attribute;
        }

        private bool IsOpenAncestor(string name)
        {
            for (int i = openElements.Count - 2; i >= 0; i--)
            {
                if (string.Equals(openElements[i].Name, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private string ReadName()
        {
            int start = position;
            while (position < text.Length && IsNameChar(text[position]))
                position++;

            return text[start..position];
        }

        private void SkipWhitespace()
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
                position++;
        }

        private void SkipRestOfTag()
        {
            while (position < text.Length && text[position] is not ('>' or '<'))
                position++;

            if (position < text.Length && text[position] == '>')
                position++;
        }

        private bool StartsWith(string value) => text.AsSpan(position).StartsWith(value, StringComparison.Ordinal);

        private void Report(TextSpan span, string message) => diagnostics.Add(new Diagnostic(span, message));
    }
}
