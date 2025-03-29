using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using Icy.Data;

namespace Icy.Assets.Importers.BitmapFonts
{
    /// <summary>
    /// Represents <b>AngelCode Bitmap Font</b> texture page info.
    /// </summary>
    /// <param name="Id">Page id used to import font.</param>
    /// <param name="FileName">Page file name to load texture from.</param>
    internal readonly record struct BitmapFontPage(int Id, string FileName) : ISpanParsable<BitmapFontPage>
    {
        // I guess this much will be enough, it can store a little book with 10Kb so I think there could be no fonts that use so many pages.
        private const int MaxPagesSize = 10240;

        /// <inheritdoc/>
        public static BitmapFontPage Parse(ReadOnlySpan<char> s, IFormatProvider? provider)
        {
            return TryParse(s, provider, out var result) ? result : ThrowHelper.ThrowFormatException<BitmapFontPage>("Incorrect string format.");
        }

        /// <inheritdoc/>
        public static BitmapFontPage Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

        /// <inheritdoc/>
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BitmapFontPage result)
        {
            result = default;

            foreach (var token in s.TokenizeWithBrackets(' '))
            {
                if (token is "page" or { IsEmpty: true }) continue;

                int splitterIndex = token.IndexOf('=');
                if (splitterIndex < 0)
                    return false;

                var key = token[..splitterIndex];
                var value = token[(splitterIndex + 1)..];

                if (key is "id" && int.TryParse(value, out var id))
                {
                    result = result with { Id = id };
                }
                else if (key is "file")
                {
                    result = result with { FileName = new(value.Trim('"')) };
                }
            }

            return true;
        }

        /// <inheritdoc/>
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BitmapFontPage result) =>
            TryParse(s.AsSpan(), provider, out result);

        /// <summary>
        /// Reads the font texture pages info from the data stream.
        /// </summary>
        /// <param name="stream">Font binary file data stream.</param>
        /// <param name="count">Count of pages in the font.</param>
        /// <param name="sectionSize">Size of the section.</param>
        /// <returns>An array of pages with all the info stored in the font.</returns>
        public static BitmapFontPage[] Read(Stream stream, int count, int sectionSize)
        {
            BitmapFontPage[] pages = new BitmapFontPage[count];
            if (sectionSize > MaxPagesSize)
                return ThrowHelper.ThrowArgumentException<BitmapFontPage[]>("Couldn't allocate enough memory to read all the pages.");
            Span<byte> buffer = stackalloc byte[sectionSize];
            for (int i = 0; i < count; i++)
            {
                int temp = 0;
                int size = 0;
                while ((temp = stream.ReadByte()) > 0)
                {
                    buffer[size++] = (byte)temp;
                }

                pages[i] = new(i, Encoding.ASCII.GetString(buffer[..size]));
            }

            return pages;
        }

        /// <summary>
        /// Reads font page info from an XML tag.
        /// </summary>
        /// <param name="tag">Tag with an info to read.</param>
        /// <returns>An instance of the <see cref="BitmapFontPage"/> struct with data from the tag.</returns>
        public static BitmapFontPage ReadXml(XElement tag)
        {
            int id = int.Parse(tag.Attribute("id")?.Value ?? "0");
            string fileName = tag.Attribute("file")?.Value ?? string.Empty;
            return new(id, fileName);
        }
    }
}
