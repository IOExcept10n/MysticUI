using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.HighPerformance;
using Icy.Rendering.Fonts;

namespace Icy.Assets.Importers
{
    internal class BMFontImporter : IAssetImporter<StaticSpriteFont>
    {
        private const string BMFontFormat = "font/x-bmfont";
        private const uint HeaderMask = 0xFFFFFF00; // Mask to get first three bytes
        private const uint HeaderMagicNumber = 0x424D4600; // First three bytes are "BMF"

        public bool CanRead(string? format) => format == BMFontFormat;

        public StaticSpriteFont Import<TContext>(Stream stream, IImportContext<TContext> importContext)
            where TContext : IAssetContext<TContext>
        {
            uint header = stream.Read<uint>();
            if ((header & HeaderMask) == HeaderMagicNumber)
            {
                return ImportBinary(stream, importContext);
            }
            else
            {
                // Return to the beginning
                stream.Seek(-sizeof(uint), SeekOrigin.Current);
                return ImportText(stream, importContext);
            }
        }

        private StaticSpriteFont ImportText<TContext>(Stream stream, IImportContext<TContext> importContext) where TContext : IAssetContext<TContext>
        {
            throw new NotImplementedException();
        }

        private StaticSpriteFont ImportBinary<TContext>(Stream stream, IImportContext<TContext> importContext) where TContext : IAssetContext<TContext>
        {
            throw new NotImplementedException();
        }
    }
}
