// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a dynamic font that can render glyphs at any size at runtime.
    /// </summary>
    public class DynamicSpriteFont : SpriteFont
    {
        private readonly IGlyphRasterizer rasterizer;
        private DynamicFontAtlas atlas;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicSpriteFont"/> class.
        /// </summary>
        /// <param name="info">Information about the font.</param>
        /// <param name="rasterizer">The glyph rasterizer to use.</param>
        /// <param name="atlas">The texture atlas to store glyphs in.</param>
        /// <param name="resolver">An instance of the service that helps with getting glyphs that are not presented in this font.</param>
        public DynamicSpriteFont(FontInfo info, IGlyphRasterizer rasterizer, DynamicFontAtlas atlas, IFallbackFontResolver? resolver = null)
            : base(info, atlas, resolver)
        {
            Guard.IsNotNull(rasterizer, nameof(rasterizer));

            this.rasterizer = rasterizer;
            this.atlas = atlas;
            Metrics = rasterizer.GetFontMetrics(info.Size, info.Style);
        }

        /// <summary>
        /// Gets or sets an instance of the glyph sprite atlas used for this font instance.
        /// </summary>
        internal DynamicFontAtlas DynamicAtlas { get => atlas; set => atlas = value; }

        /// <summary>
        /// Gets an instance of the rasterizer used to construct this font instance.
        /// </summary>
        internal IGlyphRasterizer Rasterizer => rasterizer;

        /// <summary>
        /// Clears the glyph cache.
        /// </summary>
        public void ClearCache()
        {
            atlas.Clear();
        }

        /// <inheritdoc/>
        public override FontGlyph GetGlyph(int codepoint)
        {
            // Check atlas first
            if (atlas.GetGlyph(GetStyledGlyph(codepoint)) is FontGlyph glyph)
                return glyph;

            // Get glyph metrics and rasterize
            var metrics = rasterizer.GetGlyphMetrics(codepoint, Info.Size, Info.Style);
            if (metrics.IsEmpty)
            {
                if (metrics.Advance != 0)
                {
                    atlas.AddEmptyGlyph(metrics, Info);
                    return metrics;
                }

                return FontGlyph.None;
            }

            using var pixels = rasterizer.RasterizeGlyph(codepoint, Info.Size, Info.Style);
            if (pixels is null)
                return FontGlyph.None;

            return atlas.TryAddGlyph(metrics, pixels.Memory, Info);
        }

        /// <inheritdoc/>
        public override bool SupportsCharacter(int codepoint)
        {
            return atlas.Glyphs.ContainsKey(GetStyledGlyph(codepoint)) || rasterizer.ContainsGlyph(codepoint);
        }

        /// <inheritdoc/>
        protected override float GetKerning(FontGlyph current, FontGlyph previous)
        {
            // Get kerning from rasterizer
            return rasterizer.GetKerning(current.Codepoint, previous.Codepoint, Info.Size, Info.Style);
        }
    }
}