// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using Icy.Rendering.Brushes;
using Icy.Rendering.Fonts;

namespace Icy.Assets.Importers.BitmapFonts
{
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


        public StaticSpriteFont ToSpriteFont<TAssetContext>(IImportContext<TAssetContext> importContext)
            where TAssetContext : IAssetContext<TAssetContext>
        {
            FontInfo info = Info.ToFontInfo();
            IImage[] atlases = new IImage[Common.Pages];
            for (int i = 0; i < atlases.Length; i++)
            {
                atlases[i] = importContext.AssetResolver.LoadAsset<IImage>(importContext.ImportSource, Pages[i].FileName);
            }

            var kernings = GetKernings();
            var glyphs = GetGlyphs();
            return new(info, atlases, kernings, glyphs, 0);
        }

        private IEnumerable<FontGlyph> GetGlyphs()
        {
            int i = 0;
            foreach (var c in Chars)
            {
                var uv = new Vector4(
                    c.X / Common.PageWidth,
                    c.Y / Common.PageHeight,
                    (c.X + c.Width) / Common.PageWidth,
                    (c.Y + c.Height) / Common.PageHeight);

                var bearing = new Vector2(c.XOffset, c.YOffset);
                var size = new Size(c.Width, c.Height);

                yield return new(c.Codepoint, (uint)i++, c.Page, c.XAdvance, bearing, size, uv);
            }
        }

        private IReadOnlyDictionary<StaticSpriteFont.CodepointsPair, int> GetKernings() =>
            Kernings.ToDictionary(k => new StaticSpriteFont.CodepointsPair(k.First, k.Second), t => (int)t.Amount);

        public static BitmapFont Load(Stream stream, IFormatProvider? format = null) => DetectFormat(stream) switch
        {
            FontFormat.Binary => LoadBinary(stream),
            FontFormat.Text => Parse(stream, format ?? CultureInfo.InvariantCulture),
            FontFormat.Xml => ReadXml(stream),
            _ => ThrowHelper.ThrowFormatException<BitmapFont>("Cannot determine file format to read data from the stream."),
        };

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

        internal static T GetAttribute<T>(XElement element, XName name)
            where T : struct, IParsable<T> => T.TryParse(element.Attribute(name)?.Value ?? "0", null, out var result) ? result : default;

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