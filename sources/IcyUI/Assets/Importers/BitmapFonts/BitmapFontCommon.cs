// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using static Icy.Assets.Importers.BitmapFonts.BitmapFont;

namespace Icy.Assets.Importers.BitmapFonts
{
    /// <summary>
    /// Represents <b>AngelCode Bitmap Font</b> common tag.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    internal struct BitmapFontCommon : ISpanParsable<BitmapFontCommon>
    {
        private ushort lineHeight;
        private ushort baseline;
        private ushort scaleWidth;
        private ushort scaleHeight;
        private ushort pages;
        private Bits bits;
        private byte alphaChnl;
        private byte redChnl;
        private byte greenChnl;
        private byte blueChnl;

        [Flags]
        private enum Bits : byte
        {
            None = 0,
            Packed = 1,
        }

        /// <summary>
        /// Gets the number of pages in the font.
        /// </summary>
        public readonly int Pages => pages;

        /// <summary>
        /// Gets the width of the page atlas in a font.
        /// </summary>
        public readonly ushort PageWidth => scaleWidth;

        /// <summary>
        /// Gets the height of the page atlas in a font.
        /// </summary>
        public readonly ushort PageHeight => scaleHeight;

        /// <summary>
        /// Parses <see cref="BitmapFontCommon"/> from the specified info tag string.
        /// </summary>
        /// <param name="s">A string with encoded info bitmap font tag.</param>
        /// <returns>An instance of the <see cref="BitmapFontCommon"/> struct with the info from the string.</returns>
        public static BitmapFontCommon Parse(string s) => Parse(s, null);

        /// <inheritdoc/>
        public static BitmapFontCommon Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return TryParse(s, provider, out var result) ? result : ThrowHelper.ThrowFormatException<BitmapFontCommon>("Incorrect string format.");
        }

        /// <inheritdoc/>
        public static BitmapFontCommon Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        /// <inheritdoc/>
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BitmapFontCommon result)
        {
            result = default;

            foreach (var token in s.Tokenize(' '))
            {
                if (token is "common" or { IsEmpty: true }) continue;

                int splitterIndex = token.IndexOf('=');
                if (splitterIndex < 0)
                    return false;

                var key = token[..splitterIndex];
                var value = token[(splitterIndex + 1)..];

                if (!ProcessToken(key, value, ref result, provider))
                    return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BitmapFontCommon result) => TryParse(s.AsSpan(), provider, out result);

        /// <summary>
        /// Reads the font common from the data stream.
        /// </summary>
        /// <param name="stream">Stream with the font data.</param>
        /// <returns>An instance of the <see cref="BitmapFontCommon"/> struct with data from the stream.</returns>
        public static BitmapFontCommon Read(Stream stream)
        {
            return stream.Read<BitmapFontCommon>();
        }

        /// <summary>
        /// Reads font common data from an XML tag.
        /// </summary>
        /// <param name="tag">Tag with an info to read.</param>
        /// <returns>An instance of the <see cref="BitmapFontCommon"/> struct with data from the tag.</returns>
        public static BitmapFontCommon ReadXml(XElement tag)
        {
            BitmapFontCommon common = default;
            common.baseline = GetAttribute<ushort>(tag, "base");
            if (GetAttribute<byte>(tag, "packed") == 1)
                common.bits |= Bits.Packed;
            common.lineHeight = GetAttribute<ushort>(tag, nameof(lineHeight));
            common.pages = GetAttribute<ushort>(tag, nameof(pages));
            common.scaleWidth = GetAttribute<ushort>(tag, "scaleW");
            common.scaleHeight = GetAttribute<ushort>(tag, "scaleH");
            common.alphaChnl = GetAttribute<byte>(tag, nameof(alphaChnl));
            common.blueChnl = GetAttribute<byte>(tag, nameof(blueChnl));
            common.greenChnl = GetAttribute<byte>(tag, nameof(greenChnl));
            common.redChnl = GetAttribute<byte>(tag, nameof(redChnl));

            return common;
        }

        private static bool ProcessToken(ReadOnlySpan<char> key, ReadOnlySpan<char> value, ref BitmapFontCommon result, IFormatProvider? provider) => key switch
        {
            "lineHeight" => ushort.TryParse(value, provider, out result.lineHeight),
            "base" => ushort.TryParse(value, provider, out result.baseline),
            "scaleW" => ushort.TryParse(value, provider, out result.scaleWidth),
            "scaleHeight" => ushort.TryParse(value, provider, out result.scaleHeight),
            "pages" => ushort.TryParse(value, provider, out result.pages),
            "packed" when value is "1" => (result.bits |= Bits.Packed) is { },
            "alphaChnl" => byte.TryParse(value, provider, out result.alphaChnl),
            "redChnl" => byte.TryParse(value, provider, out result.redChnl),
            "greenChnl" => byte.TryParse(value, provider, out result.greenChnl),
            "blueChnl" => byte.TryParse(value, provider, out result.blueChnl),
            _ => true,
        };
    }
}