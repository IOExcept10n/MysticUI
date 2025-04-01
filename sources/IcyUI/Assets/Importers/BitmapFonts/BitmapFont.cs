// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.Rendering.Fonts;

namespace Icy.Assets.Importers.BitmapFonts
{
    /// <summary>
    /// Represents an <c>AngelCode Bitmap Font</c> asset instance.
    /// </summary>
    /// <param name="Info">An info tag for the font.</param>
    /// <param name="Common">Common data section for the font.</param>
    /// <param name="Pages">Font atlas texture pages array.</param>
    /// <param name="Chars">Array of characters stored in a font.</param>
    /// <param name="Kernings">Array of character kernings defined in a font.</param>
    internal record BitmapFont(BitmapFontInfo Info, BitmapFontCommon Common, BitmapFontPage[] Pages, BitmapFontChar[] Chars, BitmapFontKerning[] Kernings)
    {
        // Each file contains up to 5 blocks (kernings block is optional).
        private const int BlocksCount = 5;

        private enum FontFormat
        {
            Unknown,
            Binary,
            Text,
            Xml,
        }

        // Use static readonly properties because of assembly optimization for them.
        private static ReadOnlySpan<byte> BinaryFormatHeader => [(byte)'B', (byte)'M', (byte)'F', 3];

        private static ReadOnlySpan<byte> TextFormatHeader => [(byte)'i', (byte)'n', (byte)'f', (byte)'o'];

        private static ReadOnlySpan<byte> XmlFormatHeader => [(byte)'<'];

        private static ReadOnlySpan<byte> XmlWithBOMFormatHeader => [0xEF, 0xBB, 0xBF, (byte)'<'];

        /// <summary>
        /// Imports an instance of the <see cref="StaticSpriteFont"/> with info from this font definition.
        /// </summary>
        /// <param name="importContext">Context to import texture atlases for the font.</param>
        /// <returns>An instance of the <see cref="StaticSpriteFont"/> ready to draw.</returns>
        public StaticSpriteFont ToSpriteFont(IImportContext importContext) => new(Info.ToFontInfo(), GetAtlases(importContext), GetKernings(), GetGlyphs(), 0);

        /// <summary>
        /// Loads a <see cref="BitmapFont"/> from the data stream.
        /// </summary>
        /// <remarks>
        /// Allowed formats are <see langword="Binary"/>, <see langword="Text"/> and <see langword="XML"/>.
        /// </remarks>
        /// <param name="stream">Stream with encoded font data.</param>
        /// <param name="numbersFormat">An instance of the numbers format provider to load font with.</param>
        /// <returns>An instance of the <see cref="BitmapFont"/> with info from the file stream.</returns>
        public static BitmapFont Load(Stream stream, IFormatProvider? numbersFormat = null) => DetectFormat(stream) switch
        {
            FontFormat.Binary => LoadBinary(stream),
            FontFormat.Text => Parse(stream, numbersFormat ?? CultureInfo.InvariantCulture),
            FontFormat.Xml => ReadXml(stream),
            _ => ThrowHelper.ThrowFormatException<BitmapFont>("Cannot determine file format to read data from the stream."),
        };

        /// <summary>
        /// Reads bitmap font data from an XML file stream.
        /// </summary>
        /// <param name="stream">Stream to the XML file with font data.</param>
        /// <returns>An instance of the <see cref="BitmapFont"/> with data from the file.</returns>
        public static BitmapFont ReadXml(Stream stream)
        {
            XDocument document = XDocument.Load(stream);
            if (document.Root == null)
                return ThrowHelper.ThrowFormatException<BitmapFont>("Document doesn't have the root component, parsing is unavailable.");
            BitmapFontInfo info = default;
            BitmapFontCommon common = default;
            BitmapFontPage[] pages = [];
            BitmapFontChar[] chars = [];
            BitmapFontKerning[] kernings = [];
            foreach (var element in document.Root.Elements())
            {
                if (element.Name == "info")
                {
                    info = BitmapFontInfo.ReadXml(element);
                }
                else if (element.Name == "common")
                {
                    common = BitmapFontCommon.ReadXml(element);
                    pages = new BitmapFontPage[common.Pages];
                }
                else if (element.Name == "page")
                {
                    var page = BitmapFontPage.ReadXml(element);
                    pages[page.Id] = page;
                }
                else if (element.Name == "chars")
                {
                    int count = GetAttribute<int>(element, "count");
                    if (count == 0)
                        count = element.Elements().Count();
                    chars = new BitmapFontChar[count];
                    int i = 0;
                    foreach (var tag in element.Elements())
                    {
                        chars[i++] = BitmapFontChar.ReadXml(tag);
                    }
                }
                else if (element.Name == "kernings")
                {
                    int count = GetAttribute<int>(element, "count");
                    if (count == 0)
                        count = element.Elements().Count();
                    kernings = new BitmapFontKerning[count];
                    int i = 0;
                    foreach (var tag in element.Elements())
                    {
                        kernings[i++] = BitmapFontKerning.ReadXml(tag);
                    }
                }
            }

            return new(info, common, pages, chars, kernings);
        }

        /// <summary>
        /// Reads bitmap font data from a text file stream.
        /// </summary>
        /// <param name="stream">Stream to the text file with font data.</param>
        /// <param name="provider">Numbers format provider to parse data with.</param>
        /// <returns>An instance of the <see cref="BitmapFont"/> with data from the file.</returns>
        public static BitmapFont Parse(Stream stream, IFormatProvider? provider)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            BitmapFontInfo info = default;
            BitmapFontCommon common = default;
            BitmapFontPage[] pages = [];
            BitmapFontChar[] chars = [];
            BitmapFontKerning[] kernings = [];
            string? line;
            while (!reader.EndOfStream)
            {
                line = reader.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                if (line.StartsWith("info"))
                {
                    info = BitmapFontInfo.Parse(line, provider);
                }
                else if (line.StartsWith("common"))
                {
                    common = BitmapFontCommon.Parse(line, provider);
                    pages = new BitmapFontPage[common.Pages];
                }
                else if (line.StartsWith("page"))
                {
                    var page = BitmapFontPage.Parse(line, provider);
                    pages[page.Id] = page;
                }
                else if (line.StartsWith("chars"))
                {
                    int count = int.Parse(line.Replace("chars count=", string.Empty).Trim());
                    chars = new BitmapFontChar[count];
                    for (int i = 0; i < count; i++)
                    {
                        line = reader.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            break;
                        chars[i] = BitmapFontChar.Parse(line, provider);
                    }
                }
                else if (line.StartsWith("kernings"))
                {
                    int count = int.Parse(line.Replace("kernings count=", string.Empty).Trim());
                    kernings = new BitmapFontKerning[count];
                    for (int i = 0; i < count; i++)
                    {
                        line = reader.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            break;
                        kernings[i] = BitmapFontKerning.Parse(line, provider);
                    }
                }
            }

            return new(info, common, pages, chars, kernings);
        }

        /// <summary>
        /// Reads bitmap font info from a binary file stream.
        /// </summary>
        /// <param name="stream">Stream to the binary file with font data.</param>
        /// <returns>An instance of the <see cref="BitmapFont"/> with data from the file.</returns>
        public static BitmapFont LoadBinary(Stream stream)
        {
            BitmapFontInfo info = default;
            BitmapFontCommon common = default;
            BitmapFontPage[] pages = [];
            BitmapFontChar[] chars = [];
            BitmapFontKerning[] kernings = [];
            stream.Read<int>(); // Skip header
            for (int i = 0; i < BlocksCount; i++)
            {
                int block = stream.ReadByte();

                // EOF case
                if (block == -1)
                    break;
                int blockSize = stream.Read<int>();
                switch (block)
                {
                    case 1:
                        info = BitmapFontInfo.Read(stream, blockSize); break;
                    case 2:
                        common = BitmapFontCommon.Read(stream); break;
                    case 3:
                        pages = BitmapFontPage.Read(stream, common.Pages, blockSize); break;
                    case 4:
                        int charsCount = blockSize / Unsafe.SizeOf<BitmapFontChar>();
                        chars = new BitmapFontChar[charsCount];
                        for (int j = 0; j < charsCount; j++)
                        {
                            chars[j] = BitmapFontChar.Read(stream);
                        }

                        break;

                    case 5:
                        int kerningsCount = blockSize / Unsafe.SizeOf<BitmapFontKerning>();
                        kernings = new BitmapFontKerning[kerningsCount];
                        for (int j = 0; j < kerningsCount; j++)
                        {
                            kernings[j] = BitmapFontKerning.Read(stream);
                        }

                        break;
                }
            }

            return new(info, common, pages, chars, kernings);
        }

        /// <summary>
        /// Gets data from an <see cref="XAttribute"/> of the specified <see cref="XElement"/>.
        /// </summary>
        /// <typeparam name="T">Type of the requested data.</typeparam>
        /// <param name="element">An <see cref="XElement"/> to search attributes in.</param>
        /// <param name="name">Name of the attribute to get.</param>
        /// <returns>An instance of the <typeparamref name="T"/> with data from an attribute.</returns>
        internal static T GetAttribute<T>(XElement element, XName name)
            where T : struct, IParsable<T> => T.TryParse(element.Attribute(name)?.Value ?? "0", null, out var result) ? result : default;

        private ITexture[] GetAtlases(IImportContext importContext)
        {
            ITexture[] atlases = new ITexture[Common.Pages];
            for (int i = 0; i < atlases.Length; i++)
            {
                atlases[i] = importContext.AssetResolver.LoadAsset<ITexture>(importContext.ImportSource, Pages[i].FileName);

                // HACK: little note, for now I found that texture atlases for my fonts somehow use black background for characters.
                // My rendering system requires transparent characters to work with. This way I need to restore alpha channel from my picture.
                PremultiplyAlpha(atlases[i]);
            }

            return atlases;
        }

        private static void PremultiplyAlpha(ITexture texture) => texture.Modify<Rgba32>(x => x with { A = x.GetIntensity() });

        private IEnumerable<FontGlyph> GetGlyphs()
        {
            int i = 0;
            foreach (var c in Chars)
            {
                var bounds = new Rectangle(c.X, c.Y, c.Width, c.Height);

                var bearing = new Vector2(c.XOffset, c.YOffset);
                var size = new Size(c.Width, c.Height);

                yield return new(c.Codepoint, (uint)i++, c.Page, c.XAdvance, bearing, size, bounds);
            }
        }

        private Dictionary<StaticSpriteFont.CodepointsPair, int> GetKernings() =>
            Kernings.ToDictionary(k => new StaticSpriteFont.CodepointsPair(k.First, k.Second), t => (int)t.Amount);

        private static FontFormat DetectFormat(Stream stream)
        {
            Span<byte> header = stackalloc byte[4];
            stream.ReadExactly(header);
            stream.Seek(-header.Length, SeekOrigin.Current); // Return stream to the beginning
            if (header.StartsWith(BinaryFormatHeader))
                return FontFormat.Binary;
            if (header.StartsWith(TextFormatHeader))
                return FontFormat.Text;
            if (header.StartsWith(XmlFormatHeader) || header.StartsWith(XmlWithBOMFormatHeader))
                return FontFormat.Xml;
            return FontFormat.Unknown;
        }
    }
}