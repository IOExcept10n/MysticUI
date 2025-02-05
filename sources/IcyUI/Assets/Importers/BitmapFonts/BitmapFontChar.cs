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
    /// Represents <b>AngelCode Bitmap Font</b> glyph info.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    internal struct BitmapFontChar : ISpanParsable<BitmapFontChar>
    {
        /// <summary>
        /// Character codepoint.
        /// </summary>
        public int Codepoint;

        /// <summary>
        /// The left position of the character image in the texture.
        /// </summary>
        public ushort X;

        /// <summary>
        /// The top position of the character image in the texture.
        /// </summary>
        public ushort Y;

        /// <summary>
        /// The width of the character image in the texture.
        /// </summary>
        public ushort Width;

        /// <summary>
        /// The height of the character image in the texture.
        /// </summary>
        public ushort Height;

        /// <summary>
        /// How much the current position should be offset when copying the image from the texture to the screen.
        /// </summary>
        public short XOffset;

        /// <summary>
        /// How much the current position should be offset when copying the image from the texture to the screen.
        /// </summary>
        public short YOffset;

        /// <summary>
        /// How much the current position should be advanced after drawing the character.
        /// </summary>
        public short XAdvance;

        /// <summary>
        /// The texture page where the character image is found.
        /// </summary>
        public byte Page;

        /// <summary>
        /// The texture channel where the character image is found.
        /// </summary>
        public ColorChannel Channel;

        /// <summary>
        /// Defines packed color channel used to find characters inside image texture.
        /// </summary>
        [Flags]
        public enum ColorChannel : byte
        {
            /// <summary>
            /// There are no texture channels to access the character.
            /// </summary>
            None = 0,

            /// <summary>
            /// Use blue texture channel to access the character.
            /// </summary>
            Blue = 1,

            /// <summary>
            /// Use green texture channel to access the character.
            /// </summary>
            Green = 2,

            /// <summary>
            /// Use red texture channel to access the character.
            /// </summary>
            Red = 4,

            /// <summary>
            /// Use texture alpha channel to access the character.
            /// </summary>
            Alpha = 8,

            /// <summary>
            /// Use all texture channels to access the character.
            /// </summary>
            All = 15,
        }

        /// <summary>
        /// Reads the font glyph from the binary font data stream.
        /// </summary>
        /// <param name="stream">Stream to read data from.</param>
        /// <returns>An instance of the <see cref="BitmapFontChar"/> struct.</returns>
        public static BitmapFontChar Read(Stream stream) => stream.Read<BitmapFontChar>();

        /// <summary>
        /// Reads font glyph info from an XML tag.
        /// </summary>
        /// <param name="tag">Tag with an info to read.</param>
        /// <returns>An instance of the <see cref="BitmapFontChar"/> struct with data from the tag.</returns>
        public static BitmapFontChar ReadXml(XElement tag)
        {
            BitmapFontChar result = default;
            result.Codepoint = GetAttribute<int>(tag, "id");
            result.Channel = (ColorChannel)GetAttribute<byte>(tag, "chnl");
            result.Height = GetAttribute<ushort>(tag, "height");
            result.Width = GetAttribute<ushort>(tag, "width");
            result.X = GetAttribute<ushort>(tag, "x");
            result.Y = GetAttribute<ushort>(tag, "y");
            result.Page = GetAttribute<byte>(tag, "page");
            result.XOffset = GetAttribute<short>(tag, "xoffset");
            result.YOffset = GetAttribute<short>(tag, "yoffset");
            result.XAdvance = GetAttribute<short>(tag, "xadvance");
            return result;
        }

        /// <inheritdoc/>
        public static BitmapFontChar Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return TryParse(s, provider, out var result) ? result : ThrowHelper.ThrowFormatException<BitmapFontChar>("Incorrect string format.");
        }

        /// <inheritdoc/>
        public static BitmapFontChar Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        /// <inheritdoc/>
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BitmapFontChar result)
        {
            result = default;

            foreach (var token in s.Tokenize(' '))
            {
                if (token is "char" or { IsEmpty: true }) continue;

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
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BitmapFontChar result) =>
            TryParse(s.AsSpan(), provider, out result);

        private static bool ProcessToken(ReadOnlySpan<char> key, ReadOnlySpan<char> value, ref BitmapFontChar result, IFormatProvider? provider) => key switch
        {
            "id" => int.TryParse(value, provider, out result.Codepoint),
            "x" => ushort.TryParse(value, provider, out result.X),
            "y" => ushort.TryParse(value, provider, out result.Y),
            "width" => ushort.TryParse(value, provider, out result.Width),
            "height" => ushort.TryParse(value, provider, out result.Height),
            "xoffset" => short.TryParse(value, provider, out result.XOffset),
            "yoffset" => short.TryParse(value, provider, out result.YOffset),
            "xadvance" => short.TryParse(value, provider, out result.XAdvance),
            "page" => byte.TryParse(value, provider, out result.Page),
            "chnl" => byte.TryParse(value, provider, out byte temp) && (result.Channel = (ColorChannel)temp) is { },
            _ => true,
        };
    }
}