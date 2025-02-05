// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;
using Icy.Controls;
using Icy.Rendering.Fonts;
using static Icy.Assets.Importers.BitmapFonts.BitmapFont;

namespace Icy.Assets.Importers.BitmapFonts
{
    /// <summary>
    /// Represents <b>AngelCode Bitmap Font</b> info tag.
    /// </summary>
    public readonly struct BitmapFontInfo : ISpanParsable<BitmapFontInfo>
    {
        /// <summary>
        /// Defines a fallback font family value.
        /// </summary>
        internal const string FallbackFontFamily = "DefaultFont";

        private readonly InfoFields fields;
        private readonly string? fontFamily;

        private BitmapFontInfo(InfoFields fields, string? fontFamily)
        {
            this.fields = fields;
            this.fontFamily = fontFamily;
        }

        [Flags]
        private enum Bits : byte
        {
            None,
            Smooth = 0b1000_0000,
            Unicode = 0b0100_0000,
            Italic = 0b0010_0000,
            Bold = 0b0001_0000,
            FixedHeight = 0b0000_1000,

            // NOTE: these two values are not documented officially, they're actually reserved. I use them mostly to my library's purposes.
            Underline = 0b0000_0100,

            Strikeout = 0b0000_0010,
        }

        /// <summary>
        /// Gets the name of the font family for the current font.
        /// </summary>
        public string? FontFamily => fontFamily;

        /// <summary>
        /// Gets the size of the font.
        /// </summary>
        public ushort FontSize => fields.FontSize;

        /// <summary>
        /// Gets the font style info.
        /// </summary>
        public FontStyle FontStyle
        {
            get
            {
                FontStyle style = FontStyle.Regular;
                if ((fields.BitField & Bits.Bold) == Bits.Bold) style |= FontStyle.Bold;
                if ((fields.BitField & Bits.Italic) == Bits.Italic) style |= FontStyle.Italic;
                if ((fields.BitField & Bits.Underline) == Bits.Underline) style |= FontStyle.Underline;
                if ((fields.BitField & Bits.Strikeout) == Bits.Strikeout) style |= FontStyle.Strikeout;

                return style;
            }
        }

        /// <summary>
        /// Parses <see cref="BitmapFontInfo"/> from the specified info tag string.
        /// </summary>
        /// <param name="s">A string with encoded info bitmap font tag.</param>
        /// <returns>An instance of the <see cref="BitmapFontInfo"/> struct with the info from the string.</returns>
        public static BitmapFontInfo Parse(string s) => Parse(s, null);

        /// <inheritdoc/>
        public static BitmapFontInfo Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        /// <inheritdoc/>
        public static BitmapFontInfo Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
            TryParse(s, provider, out var result) ? result : ThrowHelper.ThrowFormatException<BitmapFontInfo>("Invalid string format to read data from.");

        /// <summary>
        /// Reads the font info from the data stream.
        /// </summary>
        /// <param name="stream">Stream with the font data.</param>
        /// <param name="sectionSize">Size of the info section in a file.</param>
        /// <returns>An instance of the <see cref="BitmapFontInfo"/> struct with data from the stream.</returns>
        public static BitmapFontInfo Read(Stream stream, int sectionSize)
        {
            var fields = stream.Read<InfoFields>();
            string fontFamily;
            int requestedSize = sectionSize - Unsafe.SizeOf<InfoFields>();
            if (requestedSize < Environment.SystemPageSize)
            {
                Span<byte> buffer = stackalloc byte[requestedSize];
                fontFamily = ReadFontFamily(stream, buffer);
            }
            else
            {
                using var owner = MemoryOwner<byte>.Allocate(requestedSize);
                fontFamily = ReadFontFamily(stream, owner.Span);
            }

            return new(fields, fontFamily);

            static string ReadFontFamily(Stream stream, Span<byte> buffer)
            {
                stream.ReadExactly(buffer);
                var trimmed = buffer[..^1]; // Remove trailing null-terminator.
                string fontFamily = Encoding.ASCII.GetString(trimmed);
                return fontFamily;
            }
        }

        /// <summary>
        /// Reads font info from an XML tag.
        /// </summary>
        /// <param name="tag">Tag with an info to read.</param>
        /// <returns>An instance of the <see cref="BitmapFontInfo"/> struct with data from the tag.</returns>
        public static BitmapFontInfo ReadXml(XElement tag)
        {
            InfoFields fields = default;
            fields.AA = GetAttribute<byte>(tag, "aa");
            foreach (var value in Enum.GetValues<Bits>())
            {
                if (GetAttribute<byte>(tag, value.ToString().ToLower()) == 1)
                    fields.BitField |= value;
            }

            fields.Charset = GetAttribute<byte>(tag, "charset");
            fields.FontSize = GetAttribute<ushort>(tag, "size");
            fields.Outline = GetAttribute<byte>(tag, "outline");

            // HACK: here I use my own paddings type and its format is little different.
            // So, my paddings have format "left,top,right,bottom"
            // While paddings here have format "top,right,bottom,left"
            // So I need to rotate it a little bit.
            var padding = GetAttribute<Thickness>(tag, "padding");
            fields.PaddingUp = (byte)padding.Left;
            fields.PaddingRight = (byte)padding.Top;
            fields.PaddingDown = (byte)padding.Right;
            fields.PaddingLeft = (byte)padding.Bottom;

            // Here I use my paddings as well.
            // It's just a shortcut for spacings.
            var spacing = GetAttribute<Thickness>(tag, "spacing");
            fields.SpacingHorizontal = (byte)spacing.Left;
            fields.SpacingVertical = (byte)spacing.Right;

            fields.StretchH = GetAttribute<byte>(tag, "stretchH");

            string fontFamily = tag.Attribute("face")?.Value ?? FallbackFontFamily;
            return new(fields, fontFamily);
        }

        /// <inheritdoc/>
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BitmapFontInfo result)
        {
            if (string.IsNullOrWhiteSpace(s))
            {
                result = default;
                return false;
            }

            return TryParse(s.AsSpan(), provider, out result);
        }

        /// <inheritdoc/>
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BitmapFontInfo result)
        {
            InfoFields fields = default;
            string? fontFamily = null;

            foreach (var token in s.Tokenize(' '))
            {
                // Interesting fact: "==" doesn't work while "is" works!
                if (token is "info" or { IsEmpty: true }) continue;

                int splitterIndex = token.IndexOf('=');
                if (splitterIndex == -1)
                {
                    result = default;
                    return false;
                }

                var key = token[..splitterIndex];
                var value = token[(splitterIndex + 1)..];

                if (!ProcessToken(key, value, ref fields, ref fontFamily, provider))
                {
                    result = default;
                    return false;
                }
            }

            result = new(fields, fontFamily);
            return true;
        }

        /// <summary>
        /// Gets the <see cref="FontInfo"/> with all the data for the static sprite font.
        /// </summary>
        /// <returns>An instance of the <see cref="FontInfo"/> struct with the data for the <see cref="StaticSpriteFont"/> from this bitmap font.</returns>
        public FontInfo ToFontInfo() => new(FontFamily ?? FallbackFontFamily, FontSize, FontStyle);

        private static bool ProcessPadding(ReadOnlySpan<char> value, ref InfoFields fields, IFormatProvider? provider)
        {
            var paddingTokens = value.Tokenize(',');
            int i = 0;

            foreach (var token in paddingTokens)
            {
                if (i >= 4) return false;

                if (byte.TryParse(token, provider, out byte pad))
                {
                    switch (i++)
                    {
                        case 0: fields.PaddingUp = pad; break;
                        case 1: fields.PaddingRight = pad; break;
                        case 2: fields.PaddingDown = pad; break;
                        case 3: fields.PaddingLeft = pad; break;
                    }
                }
                else
                {
                    return false;
                }
            }

            return i == 4; // Make sure there's exactly 4 tokens
        }

        private static bool ProcessSpacing(ReadOnlySpan<char> value, ref InfoFields fields, IFormatProvider? provider)
        {
            var spacingTokens = value.Tokenize(',');
            int i = 0;

            foreach (var token in spacingTokens)
            {
                if (i >= 2) return false;

                if (byte.TryParse(token, provider, out byte space))
                {
                    switch (i++)
                    {
                        case 0: fields.SpacingHorizontal = space; break;
                        case 1: fields.SpacingVertical = space; break;
                    }
                }
                else
                {
                    return false;
                }
            }

            return i == 2; // Make sure there's exactly 2 tokens
        }

        private static bool ProcessToken(ReadOnlySpan<char> key, ReadOnlySpan<char> value, ref InfoFields fields, ref string? fontFamily, IFormatProvider? provider) => key switch
        {
            "size" => ushort.TryParse(value, provider, out fields.FontSize),
            "bold" => TrySetBitField(value, ref fields.BitField, Bits.Bold, provider),
            "italic" => TrySetBitField(value, ref fields.BitField, Bits.Italic, provider),
            "unicode" => TrySetBitField(value, ref fields.BitField, Bits.Unicode, provider),
            "stretchH" => ushort.TryParse(value, provider, out fields.StretchH),
            "aa" => byte.TryParse(value, provider, out fields.AA),
            "padding" => ProcessPadding(value, ref fields, provider),
            "spacing" => ProcessSpacing(value, ref fields, provider),
            "outline" => byte.TryParse(value, provider, out fields.Outline),
            "face" => (fontFamily = new(value.Trim('"'))) is { },
            "underline" => TrySetBitField(value, ref fields.BitField, Bits.Underline, provider),
            "strikeout" => TrySetBitField(value, ref fields.BitField, Bits.Strikeout, provider),
            "charset" when value.IsEmpty || value is "\"\"" => (fields.Charset = 0) is { },
            "charset" => byte.TryParse(value, provider, out fields.Charset),
            _ => true,
        };

        private static bool TrySetBitField(ReadOnlySpan<char> value, ref Bits bitField, Bits bit, IFormatProvider? provider)
        {
            if (byte.TryParse(value, provider, out var result))
            {
                if (result == 1)
                    bitField |= bit;
                return true;
            }

            return false;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
        private struct InfoFields
        {
            public ushort FontSize;
            public Bits BitField;
            public byte Charset;
            public ushort StretchH;
            public byte AA;
            public byte PaddingUp;
            public byte PaddingRight;
            public byte PaddingDown;
            public byte PaddingLeft;
            public byte SpacingHorizontal;
            public byte SpacingVertical;
            public byte Outline;
        }
    }
}