// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.Markup;
using Icy.SharedSamples;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Design.Syntax
{
    public class MarkupParserTests
    {
        public static TheoryData<string, string> SampleDocuments => new()
        {
            { nameof(MarkupDemo), MarkupDemo.Markup },
            { nameof(ControlTemplateDemo), ControlTemplateDemo.Markup },
            { "DefaultTheme", ReadDefaultTheme() },
        };

        public static TheoryData<string, string> PositionDocuments
        {
            get
            {
                TheoryData<string, string> data = SampleDocuments;
                data.Add("CrlfAndTabs", "<StackPanel\r\n\tOrientation=\"Horizontal\">\r\n\t<Border\r\n\t\tWidth=\"1\"\r\n\t\tx:Name=\"b\"/>\r\n</StackPanel>");
                return data;
            }
        }

        public static TheoryData<string> MalformedDocuments => new()
        {
            "<A>",
            "<A><B></A>",
            "<A></B></A>",
            "<A x=\"1></A>",
            "<A x></A>",
            "<A x=1/>",
            "<A>a < b</A>",
            "<A><!-- open",
            "<A/><B/>",
            string.Empty,
            "<!DOCTYPE A><A/>",
            "<A",
            "</A>",
        };

        [Fact]
        public void Parse_RecordsElementAndAttributeSpans()
        {
            const string text = "<Border Width=\"10\" x:Name='b'>\n  <TextBlock>Hi</TextBlock>\n</Border>";

            DocumentSyntax document = DocumentSyntax.Parse(text);

            Assert.Empty(document.Diagnostics);
            ElementSyntax root = Assert.IsType<ElementSyntax>(document.Root);
            Assert.Equal("Border", root.Name);
            Assert.Equal(new TextSpan(1, 6), root.NameSpan);

            AttributeSyntax width = root.Attributes[0];
            Assert.Equal("Width", width.Name);
            Assert.Equal(new TextSpan(15, 2), width.ValueSpan);
            Assert.Equal('"', width.Quote);
            Assert.Equal("10", width.Value);

            AttributeSyntax name = root.Attributes[1];
            Assert.Equal("x", name.Prefix);
            Assert.Equal("Name", name.LocalName);
            Assert.Equal('\'', name.Quote);
            Assert.Equal("b", name.Value);

            Assert.Equal(30, root.StartTagEnd);
            ElementSyntax child = Assert.Single(root.Elements);
            Assert.Equal(33, child.Span.Start);
            Assert.Same(child, document.FindElementAt(33));
            Assert.Equal(text.Length, root.EndTagSpan!.Value.End);
            SyntaxAssert.Tiles(document);
        }

        [Fact]
        public void Parse_DecodesEntitiesAndNormalizesWhitespaceInValues()
        {
            DocumentSyntax document = DocumentSyntax.Parse("<A T=\"a &amp; b&#10;c&#x41;\td\"/>");

            Assert.Equal("a & b\ncA d", document.Root!.Attributes[0].Value);
        }

        [Fact]
        public void Parse_KeepsCommentsCDataAndProcessingInstructionsAsNodes()
        {
            DocumentSyntax document = DocumentSyntax.Parse("<?xml-stylesheet x?><!-- c --><A><![CDATA[<b>]]></A>");

            Assert.Empty(document.Diagnostics);
            Assert.IsType<ProcessingInstructionSyntax>(document.Nodes[0]);
            Assert.IsType<CommentSyntax>(document.Nodes[1]);
            Assert.IsType<CDataSyntax>(Assert.Single(document.Root!.Content));
            SyntaxAssert.Tiles(document);
        }

        [Theory]
        [MemberData(nameof(SampleDocuments))]
        public void RoundTrip_SampleDocumentsParseCleanlyAndTile(string name, string text)
        {
            DocumentSyntax document = DocumentSyntax.Parse(text);

            Assert.True(document.Diagnostics.Count == 0, $"{name}: {string.Join("; ", document.Diagnostics)}");
            SyntaxAssert.Tiles(document);
        }

        // Pins the convention the design layer's correlation relies on: XmlReader reports an element's and an
        // attribute's (line, column) at the first character of its name.
        [Theory]
        [MemberData(nameof(PositionDocuments))]
        public void Positions_MatchXmlReaderLineInfo(string name, string text)
        {
            DocumentSyntax document = DocumentSyntax.Parse(text);
            var lines = new LineMap(text);
            XDocument xml = ParseLikeTheLoader(text);

            foreach (XElement element in xml.Root!.DescendantsAndSelf())
            {
                var info = (IXmlLineInfo)element;
                ElementSyntax? found = document.FindElementByNameStart(lines.ToOffset(info.LineNumber, info.LinePosition));
                Assert.True(found != null, $"{name}: no element at {info.LineNumber}:{info.LinePosition}");
                Assert.Equal(element.Name.LocalName, found!.LocalName);

                foreach (XAttribute attribute in element.Attributes())
                {
                    var attributeInfo = (IXmlLineInfo)attribute;
                    AttributeSyntax? match = document.FindAttributeByNameStart(lines.ToOffset(attributeInfo.LineNumber, attributeInfo.LinePosition));
                    Assert.True(match != null, $"{name}: no attribute at {attributeInfo.LineNumber}:{attributeInfo.LinePosition}");
                    Assert.Equal(attribute.Name.LocalName, match!.LocalName);
                }
            }
        }

        [Theory]
        [MemberData(nameof(MalformedDocuments))]
        public void Malformed_NeverThrowsReportsAndStillTiles(string text)
        {
            DocumentSyntax document = DocumentSyntax.Parse(text);

            Assert.NotEmpty(document.Diagnostics);
            SyntaxAssert.Tiles(document);
        }

        [Fact]
        public void GetNamespacesInScope_CollectsAncestorDeclarationsInnermostFirst()
        {
            DocumentSyntax document = DocumentSyntax.Parse("<A xmlns:ui=\"u1\" xmlns=\"d\"><B xmlns:ui=\"u2\"><C xmlns:z=\"z\"/></B></A>");
            ElementSyntax c = document.Elements.Single(x => x.Name == "C");

            IReadOnlyDictionary<string, string> inherited = document.GetNamespacesInScope(c);
            IReadOnlyDictionary<string, string> all = document.GetNamespacesInScope(c, includeSelf: true);

            Assert.Equal("u2", inherited["ui"]);
            Assert.Equal("d", inherited[string.Empty]);
            Assert.False(inherited.ContainsKey("z"));
            Assert.Equal("z", all["z"]);
            Assert.Equal(MarkupNamespaces.Directives, document.ResolvePrefix(c, "x"));
        }

        [Theory]
        [InlineData('"')]
        [InlineData('\'')]
        public void EscapeAttributeValue_RoundTripsThroughTheParser(char quote)
        {
            const string value = "it's \"q\" <b> & c\n\t";
            string escaped = MarkupEscaping.EscapeAttributeValue(value, quote);

            DocumentSyntax document = DocumentSyntax.Parse($"<A T={quote}{escaped}{quote}/>");

            Assert.Empty(document.Diagnostics);
            Assert.Equal(value, document.Root!.Attributes[0].Value);
        }

        private static XDocument ParseLikeTheLoader(string text)
        {
            var nameTable = new NameTable();
            var namespaces = new XmlNamespaceManager(nameTable);
            namespaces.AddNamespace("x", MarkupNamespaces.Directives);
            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };
            using XmlReader reader = XmlReader.Create(new StringReader(text), settings, new XmlParserContext(nameTable, namespaces, null, XmlSpace.None));
            return XDocument.Load(reader, LoadOptions.SetLineInfo);
        }

        private static string ReadDefaultTheme()
        {
            Assembly assembly = typeof(UIElement).Assembly;
            string resource = assembly.GetManifestResourceNames().Single(x => x.EndsWith("DefaultTheme.xml", StringComparison.Ordinal));
            using Stream stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
