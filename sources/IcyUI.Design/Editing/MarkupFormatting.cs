// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Text;
using Icy.Design.Syntax;
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Produces text changes that look like a person wrote them: matching the document's line breaks, indentation
    /// and quote style.
    /// </summary>
    internal static class MarkupFormatting
    {
        private static readonly string[] LineBreaks = ["\r\n", "\n", "\r"];

        public static int GetLineStart(string text, int position)
        {
            int start = position;
            while (start > 0 && text[start - 1] is not ('\n' or '\r'))
                start--;
            return start;
        }

        /// <summary>
        /// Determines whether only spaces and tabs come before <paramref name="position"/> on its line.
        /// </summary>
        public static bool StartsLine(string text, int position)
        {
            for (int i = GetLineStart(text, position); i < position; i++)
            {
                if (text[i] is not (' ' or '\t'))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the leading spaces and tabs of the line <paramref name="position"/> is on.
        /// </summary>
        public static string GetIndentation(string text, int position)
        {
            int start = GetLineStart(text, position);
            int end = start;
            while (end < text.Length && text[end] is ' ' or '\t')
                end++;
            return text[start..end];
        }

        /// <summary>
        /// Gets the line break the document uses: its first <c>\r\n</c>, <c>\n</c> or <c>\r</c>, or <c>\n</c> for a
        /// single-line document.
        /// </summary>
        public static string DetectNewLine(string text)
        {
            int index = text.AsSpan().IndexOfAny('\r', '\n');
            if (index < 0)
                return "\n";
            if (text[index] == '\n')
                return "\n";
            return index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";
        }

        /// <summary>
        /// Finds one level of indentation: what the first indented child adds to its parent's indentation. Two
        /// spaces when the document has no example.
        /// </summary>
        public static string DetectIndentUnit(DocumentSyntax syntax)
        {
            string text = syntax.Text;
            foreach (ElementSyntax element in syntax.Elements)
            {
                if (element.Parent is not { } parent || !StartsLine(text, element.Span.Start) || !StartsLine(text, parent.Span.Start))
                    continue;

                string own = GetIndentation(text, element.Span.Start);
                string outer = GetIndentation(text, parent.Span.Start);
                if (own.Length > outer.Length && own.StartsWith(outer, StringComparison.Ordinal))
                    return own[outer.Length..];
            }

            return "  ";
        }

        /// <summary>
        /// Re-indents every line of <paramref name="fragment"/> after the first: strips <paramref name="stripIndent"/>
        /// (the fragment's old indentation, for moved text) and prefixes <paramref name="indent"/>.
        /// </summary>
        public static string Reindent(string fragment, string indent, string newLine, string? stripIndent = null)
        {
            string[] lines = fragment.Split(LineBreaks, StringSplitOptions.None);
            var builder = new StringBuilder(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!string.IsNullOrEmpty(stripIndent) && line.StartsWith(stripIndent, StringComparison.Ordinal))
                    line = line[stripIndent.Length..];
                builder.Append(newLine).Append(indent).Append(line);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets what to delete to remove <paramref name="element"/>: its whole line(s), line break included, when it
        /// has them to itself; otherwise just its own span.
        /// </summary>
        public static TextSpan GetRemovalSpan(string text, ElementSyntax element)
        {
            if (!StartsLine(text, element.Span.Start))
                return element.Span;

            int end = element.Span.End;
            while (end < text.Length && text[end] is ' ' or '\t')
                end++;

            if (end < text.Length && text[end] is not ('\r' or '\n'))
                return element.Span;

            if (end < text.Length && text[end] == '\r')
                end++;
            if (end < text.Length && text[end] == '\n')
                end++;

            return TextSpan.FromBounds(GetLineStart(text, element.Span.Start), end);
        }

        /// <summary>
        /// Creates the change that sets attribute <paramref name="name"/> to <paramref name="value"/>: the value in
        /// place when it exists, otherwise a new attribute after the last one (on its own line when the attributes
        /// are written one per line), quoted like its neighbours.
        /// </summary>
        public static TextChange CreateAttributeChange(string text, ElementSyntax element, string name, string value)
        {
            AttributeSyntax? existing = element.FindAttribute(name);
            if (existing is { IsMissingValue: false } && existing.Quote != '\0')
                return new TextChange(existing.ValueSpan, MarkupEscaping.EscapeAttributeValue(value, existing.Quote));

            IReadOnlyList<AttributeSyntax> attributes = element.Attributes;
            char quote = attributes.Count > 0 && attributes[^1].Quote != '\0' ? attributes[^1].Quote : '"';
            string attributeText = $"{name}={quote}{MarkupEscaping.EscapeAttributeValue(value, quote)}{quote}";

            // A broken attribute of the same name (no value, or unquoted) is replaced whole.
            if (existing != null)
                return new TextChange(existing.Span, attributeText);

            if (attributes.Count == 0)
                return new TextChange(new TextSpan(element.NameSpan.End, 0), " " + attributeText);

            AttributeSyntax last = attributes[^1];
            bool onePerLine = StartsLine(text, last.Span.Start) && GetLineStart(text, last.Span.Start) != GetLineStart(text, element.Span.Start);
            string separator = onePerLine ? DetectNewLine(text) + GetIndentation(text, last.Span.Start) : " ";
            return new TextChange(new TextSpan(last.Span.End, 0), separator + attributeText);
        }

        /// <summary>
        /// Gets what to delete to remove an attribute: the attribute and the whitespace before it.
        /// </summary>
        public static TextSpan GetAttributeRemovalSpan(string text, AttributeSyntax attribute)
        {
            int start = attribute.Span.Start;
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
                start--;
            return TextSpan.FromBounds(start, attribute.Span.End);
        }

        /// <summary>
        /// Creates the insertion of <paramref name="fragment"/> as content child number <paramref name="index"/> of
        /// <paramref name="parent"/>.
        /// </summary>
        /// <param name="syntax">The current tree.</param>
        /// <param name="parent">The element to insert into.</param>
        /// <param name="contentChildren">The parent's content children to count <paramref name="index"/> against.</param>
        /// <param name="index">The position among <paramref name="contentChildren"/>.</param>
        /// <param name="fragment">The element's markup.</param>
        /// <param name="stripIndent">The fragment's old indentation, when it's moved text; otherwise <see langword="null"/>.</param>
        /// <returns>The change, and the offset where the inserted element's <c>&lt;</c> lands, in the same coordinates.</returns>
        public static (TextChange Change, int ElementOffset) CreateInsertion(
            DocumentSyntax syntax,
            ElementSyntax parent,
            IReadOnlyList<ElementSyntax> contentChildren,
            int index,
            string fragment,
            string? stripIndent)
        {
            string text = syntax.Text;
            string newLine = DetectNewLine(text);

            if (parent.IsSelfClosing)
            {
                string parentIndent = StartsLine(text, parent.Span.Start) ? GetIndentation(text, parent.Span.Start) : string.Empty;
                string childIndent = parentIndent + DetectIndentUnit(syntax);
                string prefix = ">" + newLine + childIndent;
                string body = Reindent(fragment, childIndent, newLine, stripIndent);
                int slash = parent.StartTagEnd - 2;
                string replacement = prefix + body + newLine + parentIndent + "</" + parent.Name + ">";
                return (new TextChange(new TextSpan(slash, 2), replacement), slash + prefix.Length);
            }

            ElementSyntax? anchor;
            bool before;
            if (contentChildren.Count > 0)
            {
                before = index < contentChildren.Count;
                anchor = before ? contentChildren[index] : contentChildren[^1];
            }
            else
            {
                // No content yet, but maybe property elements: go after the last of them.
                anchor = parent.Elements.LastOrDefault();
                before = false;
            }

            if (anchor == null)
            {
                // Only whitespace between the tags (the caller rejects elements that hold text).
                string parentIndent = StartsLine(text, parent.Span.Start) ? GetIndentation(text, parent.Span.Start) : string.Empty;
                string childIndent = parentIndent + DetectIndentUnit(syntax);
                string prefix = newLine + childIndent;
                string body = Reindent(fragment, childIndent, newLine, stripIndent);
                var inner = TextSpan.FromBounds(parent.StartTagEnd, parent.EndTagSpan!.Value.Start);
                return (new TextChange(inner, prefix + body + newLine + parentIndent), inner.Start + prefix.Length);
            }

            bool lineStyle = StartsLine(text, anchor.Span.Start);
            string indent = lineStyle ? GetIndentation(text, anchor.Span.Start) : string.Empty;
            string content = Reindent(fragment, indent, newLine, stripIndent);
            if (before)
            {
                string suffix = lineStyle ? newLine + indent : string.Empty;
                return (new TextChange(new TextSpan(anchor.Span.Start, 0), content + suffix), anchor.Span.Start);
            }

            string separator = lineStyle ? newLine + indent : string.Empty;
            return (new TextChange(new TextSpan(anchor.Span.End, 0), separator + content), anchor.Span.End + separator.Length);
        }
    }
}
