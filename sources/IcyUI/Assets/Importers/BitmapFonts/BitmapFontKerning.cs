using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using Icy.Data;
using static Icy.Assets.Importers.BitmapFonts.BitmapFont;

namespace Icy.Assets.Importers.BitmapFonts
{
    /// <summary>
    /// Represents <b>AngelCode Bitmap Font</b> kerning info.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi, Pack = 1)]
    internal struct BitmapFontKerning : ISpanParsable<BitmapFontKerning>
    {
        /// <summary>
        /// Codepoint for the first character.
        /// </summary>
        public int First;

        /// <summary>
        /// Codepoint for the second character.
        /// </summary>
        public int Second;

        /// <summary>
        /// Amount of the kerning.
        /// </summary>
        public short Amount;

        /// <inheritdoc/>
        public static BitmapFontKerning Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return TryParse(s, provider, out var result) ? result : ThrowHelper.ThrowFormatException<BitmapFontKerning>("Invalid string format.");
        }

        /// <inheritdoc/>
        public static BitmapFontKerning Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        /// <summary>
        /// Reads the font kerning from the binary font data stream.
        /// </summary>
        /// <param name="stream">Stream to read data from.</param>
        /// <returns>An instance of the <see cref="BitmapFontKerning"/> struct.</returns>
        public static BitmapFontKerning Read(Stream stream) => stream.Read<BitmapFontKerning>();

        /// <summary>
        /// Reads font kerning info from an XML tag.
        /// </summary>
        /// <param name="tag">Tag with an info to read.</param>
        /// <returns>An instance of the <see cref="BitmapFontKerning"/> struct with data from the tag.</returns>
        public static BitmapFontKerning ReadXml(XElement tag)
        {
            BitmapFontKerning result = default;
            result.First = GetAttribute<int>(tag, "first");
            result.Second = GetAttribute<int>(tag, "second");
            result.Amount = GetAttribute<short>(tag, "amount");
            return result;
        }

        /// <inheritdoc/>
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BitmapFontKerning result)
        {
            result = default;

            foreach (var token in s.TokenizeWithBrackets(' '))
            {
                if (token is "kerning" or { IsEmpty: true }) continue;

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
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BitmapFontKerning result) =>
            TryParse(s.AsSpan(), provider, out result);

        private static bool ProcessToken(ReadOnlySpan<char> key, ReadOnlySpan<char> value, ref BitmapFontKerning result, IFormatProvider? provider) => key switch
        {
            "first" => int.TryParse(value, provider, out result.First),
            "second" => int.TryParse(value, provider, out result.Second),
            "amount" => short.TryParse(value, provider, out result.Amount),
            _ => true,
        };
    }
}
