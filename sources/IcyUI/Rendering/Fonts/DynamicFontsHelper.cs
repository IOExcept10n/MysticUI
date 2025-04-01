using System.Buffers;
using System.Text;
using CommunityToolkit.Diagnostics;
using Icy.Data;

namespace Icy.Rendering.Fonts
{
    public class DynamicFontsHelper
    {
        // The 'name' in HEX BE code.
        private const uint NameTableTag = 0x6E616D65;

        private enum NameId : ushort
        {
            Copyright = 0,
            Family = 1,
            Subfamily = 2,
            FullName = 4,
            Version = 5,
        }

        /// <summary>
        /// Checks if the file name is a correct dynamic font file name.
        /// </summary>
        /// <remarks>
        /// Correct font file name assumes that the file name has TTF or OTF extension.
        /// </remarks>
        /// <param name="fileName">The file name to check.</param>
        /// <returns><see langword="true"/> if the file name is a font file name; otherwise <see langword="false"/>.</returns>
        public static bool IsFontFileName(string? fileName) =>
            !string.IsNullOrEmpty(fileName) &&
            (fileName.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) ||
            fileName.EndsWith(".otf", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Gets the font info for a file specified by <paramref name="fontPath"/>.
        /// </summary>
        /// <param name="fontPath">Path to a font file to read info from.</param>
        /// <returns>Information about the font.</returns>
        public static FontInfo GetFontInfo(string fontPath)
        {
            using var fontStream = File.OpenRead(fontPath);
            return GetFontInfo(fontStream);
        }

        /// <summary>
        /// Gets the font info for the specified font file stream.
        /// </summary>
        /// <param name="fontStream">Zero-placed TTF/OTF font file stream.</param>
        /// <returns>Information about the font.</returns>
        public static FontInfo GetFontInfo(Stream fontStream)
        {
            Guard.CanRead(fontStream);
            Guard.CanSeek(fontStream);
            uint version = fontStream.ReadBigEndian<uint>();
            ushort numTables = fontStream.ReadBigEndian<ushort>();
            ushort searchRange = fontStream.ReadBigEndian<ushort>();
            ushort entrySelector = fontStream.ReadBigEndian<ushort>();
            ushort rangeShift = fontStream.ReadBigEndian<ushort>();

            if (!TryFindTable(fontStream, numTables, out uint offset, out uint length))
                return ThrowHelper.ThrowFormatException<FontInfo>("Input file doesn't contain font info to read from.");

            return ReadNameTable(fontStream, offset, length);
        }

        private static FontInfo ReadNameTable(Stream fontStream, uint offset, uint length)
        {
            fontStream.Position = offset;
            ushort format = fontStream.ReadBigEndian<ushort>();
            ushort count = fontStream.ReadBigEndian<ushort>();
            ushort stringOffset = fontStream.ReadBigEndian<ushort>();

            string? family = null, style = null, fullName = null, version = null, copyright = null;

            for (int i = 0; i < count; i++)
            {
                ushort platformId = fontStream.ReadBigEndian<ushort>();
                ushort encodingId = fontStream.ReadBigEndian<ushort>();
                ushort languageId = fontStream.ReadBigEndian<ushort>();
                NameId nameId = fontStream.ReadBigEndian<NameId>();
                ushort locLength = fontStream.ReadBigEndian<ushort>();
                ushort locOffset = fontStream.ReadBigEndian<ushort>();

                long pos = fontStream.Position;
                fontStream.Position = offset + stringOffset + locOffset;
                using var memory = MemoryPool<byte>.Shared.Rent(locLength);
                var buffer = memory.Memory.Span[..locLength];
                fontStream.ReadExactly(buffer);
                var value = ReadString(buffer, platformId, encodingId);
                fontStream.Position = pos;

                switch (nameId)
                {
                    case NameId.Copyright:
                        copyright = value; break;
                    case NameId.Family:
                        family = value; break;
                    case NameId.Subfamily:
                        style = value; break;
                    case NameId.FullName:
                        fullName = value; break;
                    case NameId.Version:
                        version = value; break;
                }
            }

            if (!Enum.TryParse(style, out FontStyle styleCode)) styleCode = FontStyle.Regular;
            return new FontInfo(family ?? fullName ?? string.Empty, 0, styleCode);
        }

        private static string ReadString(Span<byte> bytes, ushort platformID, ushort encodingID) => platformID switch
        {
            1 => Encoding.ASCII.GetString(bytes), // Mac
            3 when encodingID == 1 => Encoding.BigEndianUnicode.GetString(bytes), // Windows, Unicode BMP
            3 when encodingID == 10 => Encoding.UTF32.GetString(bytes), // Windows, Unicode full
            _ => Encoding.ASCII.GetString(bytes), // Default, ASCII
        };

        private static bool TryFindTable(Stream fontStream, uint numTables, out uint offset, out uint length)
        {
            for (int i = 0; i < numTables; i++)
            {
                uint currentTag = fontStream.ReadBigEndian<uint>();
                uint checksum = fontStream.ReadBigEndian<uint>();
                offset = fontStream.ReadBigEndian<uint>();
                length = fontStream.ReadBigEndian<uint>();

                if (currentTag == NameTableTag)
                    return true;
            }

            offset = length = 0;
            return false;
        }
    }
}
