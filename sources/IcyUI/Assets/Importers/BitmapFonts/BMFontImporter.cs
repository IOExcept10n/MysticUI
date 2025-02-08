// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Net.Mime;
using Icy.Rendering.Fonts;

namespace Icy.Assets.Importers.BitmapFonts
{
    /// <summary>
    /// Represents an importer for the <c>AngelCode Bitmap Font</c> files.
    /// </summary>
    internal class BMFontImporter : IAssetImporter<StaticSpriteFont>
    {
        private const string BMFontFormat = "font/x-bmfont";

        /// <inheritdoc/>
        public bool CanRead(string? format) =>
            format == BMFontFormat ||
            format == MediaTypeNames.Application.Octet ||
            format == MediaTypeNames.Text.Xml ||
            string.IsNullOrEmpty(format);

        /// <inheritdoc/>
        public StaticSpriteFont Import(Stream stream, IImportContext importContext)
        {
            BitmapFont font = BitmapFont.Load(stream);
            return font.ToSpriteFont(importContext);
        }
    }
}