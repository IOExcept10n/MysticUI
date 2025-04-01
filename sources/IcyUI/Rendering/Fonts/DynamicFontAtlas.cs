// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;
using System.Drawing;
using CommunityToolkit.Diagnostics;
using CommunityToolkit.HighPerformance;
using Icy.Configuration;
using Icy.Rendering.Brushes;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a dynamic texture atlas for storing font glyphs.
    /// </summary>
    public class DynamicFontAtlas : IFontAtlas
    {
        private readonly Dictionary<int, FontGlyph> glyphs;
        private readonly IRenderContext context;
        private readonly List<Page> pages;
        private readonly uint maxPages;
        private uint currentPageIndex;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicFontAtlas"/> class.
        /// </summary>
        /// <param name="context">An instance of the <see cref="IRenderContext"/> to create textures from.</param>
        /// <param name="maxPages">The maximum number of texture pages to create.</param>
        public DynamicFontAtlas(IRenderContext context, uint maxPages = 4)
        {
            Guard.IsGreaterThan(maxPages, 0u, nameof(maxPages));

            this.maxPages = maxPages;
            this.context = context;
            glyphs = new Dictionary<int, FontGlyph>();
            pages = new List<Page>();
            currentPageIndex = 0;
        }

        /// <inheritdoc/>
        public Size PageSize => new Size(1024, 1024); // Maximum page size

        /// <inheritdoc/>
        public int PageCount => pages.Count;

        /// <inheritdoc/>
        public IReadOnlyDictionary<int, FontGlyph> Glyphs => glyphs;

        /// <inheritdoc/>
        public IReadOnlyList<ITexture> Textures => pages.Select(p => p.Texture).ToList();

        /// <inheritdoc/>
        public FontGlyph? GetGlyph(int codepoint)
        {
            return glyphs.TryGetValue(codepoint, out var glyph) ? glyph : null;
        }

        /// <summary>
        /// Adds an empty glyph for rendering purposes.
        /// </summary>
        /// <remarks>
        /// This method can be used for non-rendered glyphs that represent some unique spacings or kernings.
        /// The most common example of an "empty" glyph is a whitespace.
        /// </remarks>
        /// <param name="glyph">An instance of the <see cref="FontGlyph"/> to add to a font.</param>
        public void AddEmptyGlyph(FontGlyph glyph)
        {
            glyphs[glyph.Codepoint] = glyph;
        }

        /// <inheritdoc/>
        public FontGlyph TryAddGlyph(FontGlyph glyph, Memory<byte> pixels)
        {
            Guard.IsNotNull(pixels, nameof(pixels));

            // Check if glyph already exists
            if (glyphs.TryGetValue(glyph.Codepoint, out var cached))
                return cached;

            // Get size range for the glyph
            var sizeRange = GetSizeRange(glyph.Size.Height);
            var page = GetOrCreatePage(sizeRange);

            // Update glyph with page index and texture region
            return glyphs[glyph.Codepoint] = page.TryAddGlyph(glyph, pixels);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            glyphs.Clear();
            foreach (var page in pages)
            {
                page.Dispose();
            }
            pages.Clear();
            currentPageIndex = 0;
        }

        private Page GetOrCreatePage(SizeRange sizeRange)
        {
            // Try to find an existing page for this size range
            foreach (var page in pages)
            {
                if (page.SizeRange == sizeRange && page.HasSpace)
                    return page;
            }

            // Create a new page if we haven't reached the maximum
            if (currentPageIndex < maxPages)
            {
                var page = new Page(context, sizeRange, currentPageIndex);
                pages.Add(page);
                currentPageIndex++;
                return page;
            }

            // If we've reached the maximum, reuse the oldest page
            var oldestPage = pages[0];
            pages.RemoveAt(0);
            pages.Add(oldestPage);
            return oldestPage;
        }

        private static SizeRange GetSizeRange(int height) => height switch
        {
            <= 8 => SizeRange.Small,
            <= 16 => SizeRange.Medium,
            <= 32 => SizeRange.Large,
            _ => SizeRange.ExtraLarge,
        };

        public ITexture GetGlyphPage(in FontGlyph glyph) => pages[(int)glyph.PageIndex].Texture;

        private enum SizeRange
        {
            Small,       // 1-8px
            Medium,      // 9-16px
            Large,       // 17-32px
            ExtraLarge,  // >32px
        }

        private class Page : IDisposable
        {
            private readonly IRenderContext context;
            private readonly ITexture texture;
            private readonly SizeRange sizeRange;
            private readonly uint pageIndex;
            private int currentX;
            private int currentY;
            private int rowHeight;

            public ITexture Texture => texture;

            public SizeRange SizeRange => sizeRange;

            public bool HasSpace => currentY + rowHeight < texture.Size.Height;

            public Page(IRenderContext context, SizeRange sizeRange, uint pageIndex)
            {
                this.context = context;
                this.sizeRange = sizeRange;
                this.pageIndex = pageIndex;

                // Create texture with size based on range
                int textureSize = sizeRange switch
                {
                    SizeRange.Small or SizeRange.Medium => 512,  // Small glyphs
                    SizeRange.Large or SizeRange.ExtraLarge => 1024,  // Large glyphs
                    _ => ThrowHelper.ThrowArgumentOutOfRangeException<int>(nameof(sizeRange), "Invalid size range"),
                };

                var arr = ArrayPool<Rgba32>.Shared.Rent(textureSize * textureSize);
                Array.Clear(arr);
                texture = context.CreateTexture(textureSize, textureSize, arr);
                ArrayPool<Rgba32>.Shared.Return(arr);
                Reset();
            }

            public FontGlyph TryAddGlyph(FontGlyph glyph, Memory<byte> pixels)
            {
                // Check if we have enough space
                if (!HasSpace || currentX + glyph.Size.Width > texture.Size.Width)
                {
                    // Move to next row
                    currentX = 0;
                    currentY += rowHeight;
                    rowHeight = 0;
                }

                if (currentY + glyph.Size.Height > texture.Size.Height)
                    return FontGlyph.None;

                // Update glyph with texture region
                var region = new Rectangle(currentX, currentY, glyph.Size.Width, glyph.Size.Height);
                CopyGlyphToAtlas(pixels, region);

                // Update position for next glyph
                currentX += glyph.Size.Width;
                rowHeight = Math.Max(rowHeight, glyph.Size.Height);

                return glyph with { PageIndex = pageIndex, TextureRegion = region };
            }

            private void Reset()
            {
                currentX = 0;
                currentY = 0;
                rowHeight = 0;
            }

            private void CopyGlyphToAtlas(Memory<byte> pixels, Rectangle region)
            {
                int square = region.Width * region.Height;
                // Convert grayscale pixels to RGBA32 colors without allocation
                var colors = ArrayPool<Rgba32>.Shared.Rent(square);
                try
                {
                    var pixelSpan = pixels.Span;

                    for (int i = 0; i < square; i++)
                    {
                        byte alpha = pixelSpan[i];
                        colors[i] = new Rgba32(255, 255, 255, alpha);
                    }

                    // Set texture data
                    texture.SetTextureData(region, colors[..square]);
                }
                finally
                {
                    ArrayPool<Rgba32>.Shared.Return(colors);
                }
            }

            public void Dispose()
            {
                if (texture is IDisposable disposable)
                    disposable.Dispose();
            }
        }
    }
}