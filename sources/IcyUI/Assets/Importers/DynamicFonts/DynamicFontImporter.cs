// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Net.Mime;
using Icy.Configuration;
using Icy.Rendering;
using Icy.Rendering.Fonts;

namespace Icy.Assets.Importers.DynamicFonts
{
    /// <summary>
    /// Represents an importer for dynamic TrueType fonts.
    /// </summary>
    internal class DynamicFontImporter : IAssetImporter<IFont>
    {
        /// <inheritdoc/>
        public bool CanRead(string? format) =>
            format == MediaTypeNames.Font.Ttf ||
            format == MediaTypeNames.Font.Otf ||
            format == MediaTypeNames.Application.Octet ||
            string.IsNullOrEmpty(format);

        /// <inheritdoc/>
        public IFont Import(Stream stream, IImportContext importContext)
        {
            var rasterizer = importContext.AssetResolver.LoadAsset<IGlyphRasterizer>(importContext.ImportSource, importContext.ResourceName ?? string.Empty);

            // The imported font is invalid because it can't provide correct glyphs.
            // Dynamic fonts are just wrappers around an IRasterizer
            // where each font instance has its own size to rasterize
            // Also each font has its own glyph atlas which is generated at initialization
            // An imported font is just a template for creating other instances based on the same rasterizer.
            // We recommend using FontSystem class for fonts manipulation, instead of creating font instances manually.
            return new DynamicSpriteFont(DynamicFontsHelper.GetFontInfo(stream), rasterizer, null!);
        }
    }
}