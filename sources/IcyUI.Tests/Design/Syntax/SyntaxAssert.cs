// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Xunit;

namespace Icy.Tests.Design.Syntax
{
    /// <summary>
    /// Asserts the parser's core guarantee: every character of the text belongs to exactly one node, in order, so
    /// the tree reproduces the text byte for byte.
    /// </summary>
    internal static class SyntaxAssert
    {
        public static void Tiles(DocumentSyntax document)
        {
            AssertSequence(document.Nodes, 0, document.Text.Length);
            foreach (MarkupSyntaxNode node in document.Nodes)
                AssertElement(document.Text, node);
        }

        private static void AssertElement(string text, MarkupSyntaxNode node)
        {
            if (node is not ElementSyntax element)
                return;

            Assert.Equal(element.Span.Start + 1, element.NameSpan.Start);
            Assert.Equal(element.Name, text.Substring(element.NameSpan.Start, element.NameSpan.Length));

            int cursor = element.NameSpan.End;
            foreach (AttributeSyntax attribute in element.Attributes)
            {
                Assert.True(attribute.Span.Start >= cursor, $"Attribute '{attribute.Name}' starts before the previous one ends.");
                Assert.Equal(attribute.Name, text.Substring(attribute.NameSpan.Start, attribute.NameSpan.Length));
                Assert.Same(element, attribute.Parent);
                cursor = attribute.Span.End;
            }

            Assert.True(element.StartTagEnd >= cursor);
            int contentEnd = element.EndTagSpan?.Start ?? element.Span.End;
            AssertSequence(element.Content, element.StartTagEnd, contentEnd);
            Assert.Equal(element.Span.End, element.EndTagSpan?.End ?? contentEnd);

            foreach (MarkupSyntaxNode child in element.Content)
            {
                Assert.Same(element, child.Parent);
                AssertElement(text, child);
            }
        }

        private static void AssertSequence(IReadOnlyList<MarkupSyntaxNode> nodes, int start, int end)
        {
            int cursor = start;
            foreach (MarkupSyntaxNode node in nodes)
            {
                Assert.Equal(cursor, node.Span.Start);
                cursor = node.Span.End;
            }

            Assert.Equal(end, cursor);
        }
    }
}
