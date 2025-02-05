// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Net.Mime;
using System.Numerics;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using Icy.Rendering.Brushes;
using Icy.Rendering.Fonts;

namespace Icy.Assets.Importers.BitmapFonts
{
    /// <summary>
    /// Represents an importer for the <c>XML BMFont</c> fonts format.
    /// </summary>
    internal class XmlFontImporter : IAssetImporter<StaticSpriteFont>
    {
        /// <inheritdoc/>
        public bool CanRead(string? format) => format == MediaTypeNames.Text.Xml;

        /// <inheritdoc/>
        public StaticSpriteFont Import<TContext>(Stream stream, IImportContext<TContext> importContext)
            where TContext : IAssetContext<TContext>
        {
            var document = XDocument.Load(stream);
            if (document.Root == null)
                return ThrowHelper.ThrowFormatException<StaticSpriteFont>("Couldn't find a root of the document. Impossible to import font.");
            FontInfo info = ParseInfo(document);
            var pages = ImportPages(document, importContext);
            var glyphs = ParseGlyphs(document, pages);
            var kernings = ParseKernings(document);

            // Maybe it shouldn't be used
            float lineGap = ReadSingle(document.Root.Element("common"), "lineHeight");

            return new(info, pages, kernings, glyphs, lineGap);
        }

        private static IReadOnlyDictionary<StaticSpriteFont.CodepointsPair, int> ParseKernings(XDocument document)
        {
            XElement? kernings = document.Root!.Element(nameof(kernings));
            int count = ReadInt(kernings, nameof(count));
            Dictionary<StaticSpriteFont.CodepointsPair, int> result = new(count);
            if (count == 0)
                return result;
            foreach (var kerning in kernings!.Elements("kerning"))
            {
                int first = ReadInt(kerning, nameof(first));
                int second = ReadInt(kerning, nameof(second));
                int amount = ReadInt(kerning, nameof(amount));

                result[new(first, second)] = amount;
            }

            return result;
        }

        private static List<FontGlyph> ParseGlyphs(XDocument document, IImage[] atlas)
        {
            XElement? chars = document.Root!.Element(nameof(chars));
            int count = ReadInt(chars, nameof(count));
            List<FontGlyph> result = new(count);
            if (count == 0)
                return result;
            int i = 0;
            foreach (var info in chars!.Elements("char"))
            {
                int codepoint = ReadInt(info, "id");
                int x = ReadInt(info, nameof(x));
                int y = ReadInt(info, nameof(y));
                int width = ReadInt(info, nameof(width));
                int height = ReadInt(info, nameof(height));
                float xoffset = ReadSingle(info, nameof(xoffset));
                float yoffset = ReadSingle(info, nameof(yoffset));
                float xadvance = ReadSingle(info, nameof(xadvance));
                int pageNumber = ReadInt(info, "page");

                var uv = new Vector4(
                    x / atlas[pageNumber].Size.Width,
                    y / atlas[pageNumber].Size.Height,
                    (x + width) / atlas[pageNumber].Size.Width,
                    (y + height) / atlas[pageNumber].Size.Height);

                var bearing = new Vector2(xoffset, yoffset);
                var size = new Size(width, height);

                var glyph = new FontGlyph(codepoint, (uint)i++, (uint)pageNumber, xadvance, bearing, size, uv);
                result.Add(glyph);
            }

            return result;
        }

        private static IImage[] ImportPages<TContext>(XDocument document, IImportContext<TContext> importContext)
            where TContext : IAssetContext<TContext>
        {
            int count = ReadInt(document.Root!.Element("common"), "pages");
            if (count == 0)
            {
                return ThrowHelper.ThrowFormatException<IImage[]>("Couldn't determine the number of pages to import the font.");
            }

            IImage[] result = new IImage[count];
            foreach (var pageDefinition in document.Root.Elements("page"))
            {
                int id = ReadInt(pageDefinition, "id");
                string? fileName = pageDefinition.Attribute("file")?.Value;
                if (fileName == null)
                {
                    return ThrowHelper.ThrowFormatException<IImage[]>("Couldn't find a file to load image from.");
                }

                result[id] = importContext.AssetResolver.LoadAsset<IImage>(importContext.ImportSource, fileName);
            }

            return result;
        }

        private static FontInfo ParseInfo(XDocument document)
        {
            var infoElement = document.Root?.Element("info");
            if (infoElement == null)
                return default;
            string fontFamily = infoElement.Attribute("face")?.Value ?? BitmapFontInfo.FallbackFontFamily;
            float fontSize = ReadSingle(infoElement, "size");
            FontStyle style = ParseFontStyle(infoElement);
            return new FontInfo(fontFamily, fontSize, style);
        }

        private static FontStyle ParseFontStyle(XElement infoElement)
        {
            FontStyle style = FontStyle.Regular;
            if (infoElement?.Attribute(nameof(FontStyle.Bold).ToLower())?.Value == "1")
                style |= FontStyle.Bold;
            if (infoElement?.Attribute(nameof(FontStyle.Italic).ToLower())?.Value == "1")
                style |= FontStyle.Italic;
            if (infoElement?.Attribute(nameof(FontStyle.Underline).ToLower())?.Value == "1")
                style |= FontStyle.Underline;
            if (infoElement?.Attribute(nameof(FontStyle.Strikeout).ToLower())?.Value == "1")
                style |= FontStyle.Strikeout;

            return style;
        }

        private static int ReadInt(XElement? element, XName attributeName)
        {
            return int.Parse(element?.Attribute(attributeName)?.Value ?? "0");
        }

        private static float ReadSingle(XElement? element, XName attributeName)
        {
            return float.Parse(element?.Attribute(attributeName)?.Value ?? "0");
        }
    }
}