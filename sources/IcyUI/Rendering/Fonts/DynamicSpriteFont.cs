// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using CommunityToolkit.Diagnostics;
using Icy.Rendering.Brushes;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a dynamic font that can render glyphs at any size at runtime.
    /// </summary>
    public class DynamicSpriteFont : SpriteFont
    {
        private readonly IGlyphRasterizer rasterizer;
        private readonly DynamicFontAtlas atlas;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicSpriteFont"/> class.
        /// </summary>
        /// <param name="info">Information about the font.</param>
        /// <param name="rasterizer">The glyph rasterizer to use.</param>
        /// <param name="atlas">The texture atlas to store glyphs in.</param>
        public DynamicSpriteFont(FontInfo info, IGlyphRasterizer rasterizer, DynamicFontAtlas atlas)
            : base(info, atlas)
        {
            Guard.IsNotNull(rasterizer, nameof(rasterizer));

            this.rasterizer = rasterizer;
            this.atlas = atlas;
            Metrics = rasterizer.GetFontMetrics(info.Size, info.Style);
        }

        /// <summary>
        /// Gets an instance of the rasterizer used to construct this font instance.
        /// </summary>
        internal IGlyphRasterizer Rasterizer => rasterizer;

        /// <summary>
        /// Gets an instance of the glyph sprite atlas used for this font instance.
        /// </summary>
        internal DynamicFontAtlas Atlas => atlas;

        /// <inheritdoc/>
        protected override FontGlyph GetGlyph(int codepoint, in FontRenderingOptions options)
        {
            // Check atlas first
            if (atlas.GetGlyph(codepoint) is FontGlyph glyph)
                return glyph;

            // Get glyph metrics and rasterize
            var metrics = rasterizer.GetGlyphMetrics(codepoint, Info.Size, Info.Style);
            if (metrics.IsEmpty)
            {
                if (metrics.Advance != 0)
                {
                    atlas.AddEmptyGlyph(metrics);
                    return metrics;
                }

                return FontGlyph.None;
            }

            using var pixels = rasterizer.RasterizeGlyph(codepoint, Info.Size, Info.Style);
            if (pixels is null)
                return FontGlyph.None;

            return atlas.TryAddGlyph(metrics, pixels.Memory);
        }

        /// <inheritdoc/>
        protected override float GetKerning(FontGlyph current, FontGlyph previous)
        {
            // Get kerning from rasterizer
            return rasterizer.GetKerning(current.Codepoint, previous.Codepoint, Info.Size, Info.Style);
        }

        /// <summary>
        /// Clears the glyph cache.
        /// </summary>
        public void ClearCache()
        {
            atlas.Clear();
        }
    }
} 