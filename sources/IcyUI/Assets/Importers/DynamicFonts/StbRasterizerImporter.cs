// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Net.Mime;
using Icy.Rendering.Fonts;

namespace Icy.Assets.Importers.DynamicFonts
{
    /// <summary>
    /// Represents an importer for the basic dynamic font rasterizer.
    /// </summary>
    internal class StbRasterizerImporter : IAssetImporter<IGlyphRasterizer>
    {
        /// <inheritdoc/>
        public bool CanRead(string? format) =>
            format == MediaTypeNames.Font.Ttf ||
            format == MediaTypeNames.Font.Otf ||
            format == MediaTypeNames.Application.Octet ||
            string.IsNullOrEmpty(format);

        /// <inheritdoc/>
        public IGlyphRasterizer Import(Stream stream, IImportContext importContext)
        {
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return new StbTrueTypeRasterizer(memory.ToArray());
        }
    }
}