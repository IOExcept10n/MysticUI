// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;
using System.Globalization;
using System.Text;
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// Decodes and encodes attribute values exactly as an XML reader sees them.
    /// </summary>
    internal static class MarkupEscaping
    {
        private static readonly SearchValues<char> DecodeTriggers = SearchValues.Create("&\r\n\t");

        /// <summary>
        /// Decodes an attribute value the way <see cref="System.Xml.XmlReader"/> does: literal tabs and line breaks
        /// become spaces (a <c>\r\n</c> pair becomes one), then entity and character references are expanded.
        /// </summary>
        /// <param name="text">The whole markup text.</param>
        /// <param name="valueSpan">The value's range, quotes excluded.</param>
        /// <returns>The decoded value.</returns>
        public static string DecodeAttributeValue(string text, TextSpan valueSpan)
        {
            ReadOnlySpan<char> raw = text.AsSpan(valueSpan.Start, valueSpan.Length);
            if (raw.IndexOfAny(DecodeTriggers) < 0)
                return raw.ToString();

            var builder = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '\r')
                {
                    builder.Append(' ');
                    if (i + 1 < raw.Length && raw[i + 1] == '\n')
                        i++;
                    continue;
                }

                if (c is '\n' or '\t')
                {
                    builder.Append(' ');
                    continue;
                }

                if (c == '&')
                {
                    int semicolon = raw[i..].IndexOf(';');
                    if (semicolon > 1 && TryDecodeReference(raw.Slice(i + 1, semicolon - 1), out string? decoded))
                    {
                        builder.Append(decoded);
                        i += semicolon;
                        continue;
                    }
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Escapes a value for an attribute quoted with <paramref name="quote"/>, so that decoding it gives
        /// <paramref name="value"/> back exactly. Literal tabs and line breaks become character references, since
        /// a reader would otherwise normalize them to spaces.
        /// </summary>
        /// <param name="value">The value to escape.</param>
        /// <param name="quote">The quote character the value will be written between.</param>
        /// <returns>The escaped value.</returns>
        public static string EscapeAttributeValue(string value, char quote)
        {
            ArgumentNullException.ThrowIfNull(value);

            var builder = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': builder.Append("&amp;"); break;
                    case '<': builder.Append("&lt;"); break;
                    case '"' when quote == '"': builder.Append("&quot;"); break;
                    case '\'' when quote == '\'': builder.Append("&apos;"); break;
                    case '\n': builder.Append("&#10;"); break;
                    case '\r': builder.Append("&#13;"); break;
                    case '\t': builder.Append("&#9;"); break;
                    default: builder.Append(c); break;
                }
            }

            return builder.ToString();
        }

        private static bool TryDecodeReference(ReadOnlySpan<char> name, out string? value)
        {
            value = name switch
            {
                "lt" => "<",
                "gt" => ">",
                "amp" => "&",
                "quot" => "\"",
                "apos" => "'",
                _ => null,
            };
            if (value != null)
                return true;

            if (name.Length < 2 || name[0] != '#')
                return false;

            bool hex = name[1] is 'x' or 'X';
            ReadOnlySpan<char> digits = hex ? name[2..] : name[1..];
            NumberStyles style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
            if (!int.TryParse(digits, style, CultureInfo.InvariantCulture, out int code) || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF)
                return false;

            value = char.ConvertFromUtf32(code);
            return true;
        }
    }
}
