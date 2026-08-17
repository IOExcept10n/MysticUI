// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;
using System.Drawing;
using System.Numerics;
using System.Runtime.InteropServices;
using static StbTrueTypeSharp.StbTrueType;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a glyph rasterizer that uses StbTrueTypeSharp for TrueType font rendering.
    /// </summary>
    public unsafe class StbTrueTypeRasterizer : IGlyphRasterizer
    {
        private readonly byte[] fontData;
        private readonly stbtt_fontinfo fontInfo;
        private readonly Dictionary<(int First, int Second, float Size, FontStyle Style), float> kerningCache;
        private readonly GCHandle dataHandle;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="StbTrueTypeRasterizer"/> class.
        /// </summary>
        /// <param name="fontData">The font data.</param>
        public StbTrueTypeRasterizer(byte[] fontData)
        {
            this.fontData = fontData;
            fontInfo = new stbtt_fontinfo();
            kerningCache = [];
            dataHandle = GCHandle.Alloc(fontData, GCHandleType.Pinned);

            unsafe
            {
                fixed (byte* ptr = fontData)
                {
                    if (stbtt_GetNumberOfFonts(ptr) > 0)
                    {
                        int offset = stbtt_GetFontOffsetForIndex(ptr, 0);
                        if (stbtt_InitFont(fontInfo, ptr, offset) == 0)
                            throw new InvalidOperationException("Failed to initialize font.");
                    }
                }
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="StbTrueTypeRasterizer"/> class.
        /// </summary>
        /// <remarks>
        /// This rasterizer uses C <see langword="STB_TrueType"/> library which needs manual font data release.
        /// </remarks>
        ~StbTrueTypeRasterizer()
        {
            Dispose(disposing: false);
        }

        /// <inheritdoc/>
        public bool ContainsGlyph(int codepoint) => stbtt_FindGlyphIndex(fontInfo, codepoint) != 0;

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc/>
        public unsafe FontMetrics GetFontMetrics(float fontSize, FontStyle style)
        {
            // For uninitialized or template fonts metrics are unavailable.
            if (fontSize == 0 || float.IsNaN(fontSize))
                return default;
            float scaled = stbtt_ScaleForPixelHeight(fontInfo, fontSize);
            int ascent, descent, lineGap;
            stbtt_GetFontVMetrics(fontInfo, &ascent, &descent, &lineGap);
            var capitalMetric = GetGlyphMetrics('X', fontSize, style);
            var lowercaseMetric = GetGlyphMetrics('x', fontSize, style);
            return new FontMetrics(ascent * scaled, descent * scaled, lineGap * scaled, lowercaseMetric.Size.Height, capitalMetric.Size.Height);
        }

        /// <inheritdoc/>
        public FontGlyph GetGlyphMetrics(int codepoint, float size, FontStyle style)
        {
            unsafe
            {
                int index = stbtt_FindGlyphIndex(fontInfo, codepoint);

                // Get glyph metrics
                int x0 = 0, y0 = 0, x1 = 0, y1 = 0;
                float scaled = stbtt_ScaleForPixelHeight(fontInfo, size);
                stbtt_GetGlyphBitmapBox(fontInfo, index, scaled, scaled, &x0, &y0, &x1, &y1);

                // Get advance width and left side bearing
                int advanceWidth = 0, leftSideBearing = 0;
                stbtt_GetGlyphHMetrics(fontInfo, index, &advanceWidth, &leftSideBearing);
                advanceWidth = (int)(advanceWidth * scaled);
                leftSideBearing = (int)(leftSideBearing * scaled);

                // Add padding for hinting at small sizes - but only for glyphs that actually have ink. A glyph
                // with no bitmap box (e.g. space) must stay reported as zero-sized here, matching the unpadded
                // box RasterizeGlyph computes: DynamicSpriteFont.GetGlyph relies on FontGlyph.IsEmpty to decide
                // between "spacing-only glyph, just use Advance" and "has a bitmap, rasterize it" - inflating an
                // inkless glyph's size with padding made it look non-empty, so GetGlyph tried to rasterize it,
                // RasterizeGlyph correctly found nothing to draw and returned null, and GetGlyph gave up and
                // returned FontGlyph.None instead - silently dropping the glyph's Advance entirely. For space,
                // that meant every space character contributed zero width and disappeared from the layout.
                bool hasInk = x1 > x0 && y1 > y0;
                int padding = hasInk && size <= 16 ? 1 : 0;
                int width = x1 - x0 + (padding * 2),
                    height = y1 - y0 + (padding * 2);
                return new FontGlyph(
                    codepoint,
                    0,
                    0,
                    advanceWidth,
                    new Vector2(leftSideBearing + padding, y0 + padding),
                    new Size(width, height),
                    Rectangle.Empty);
            }
        }

        /// <inheritdoc/>
        public float GetKerning(int first, int second, float size, FontStyle style)
        {
            var key = (first, second, size, style);
            if (kerningCache.TryGetValue(key, out float kerning))
                return kerning;

            unsafe
            {
                int index1 = stbtt_FindGlyphIndex(fontInfo, first),
                    index2 = stbtt_FindGlyphIndex(fontInfo, second);

                // Get kerning from font
                kerning = stbtt_GetGlyphKernAdvance(fontInfo, index1, index2) * stbtt_ScaleForPixelHeight(fontInfo, size);
            }

            kerningCache[key] = kerning;
            return kerning;
        }

        /// <inheritdoc/>
        public IMemoryOwner<byte>? RasterizeGlyph(int codepoint, float size, FontStyle style)
        {
            unsafe
            {
                float scale = stbtt_ScaleForPixelHeight(fontInfo, size);
                int index = stbtt_FindGlyphIndex(fontInfo, codepoint);

                // Get glyph metrics
                int x0 = 0, y0 = 0, x1 = 0, y1 = 0;
                stbtt_GetGlyphBitmapBox(fontInfo, index, scale, scale, &x0, &y0, &x1, &y1);

                // Calculate dimensions
                int width = x1 - x0;
                int height = y1 - y0;
                if (width <= 0 || height <= 0)
                    return null;

                // Allocate bitmap with padding for hinting
                int padding = size <= 16 ? 1 : 0;
                int paddedWidth = width + (padding * 2);
                int paddedHeight = height + (padding * 2);
                IMemoryOwner<byte> bitmap = MemoryPool<byte>.Shared.Rent(paddedWidth * paddedHeight);

                fixed (byte* ptr = bitmap.Memory.Span)
                {
                    stbtt_MakeGlyphBitmap(fontInfo, ptr, paddedWidth, paddedHeight, paddedWidth, scale, scale, index);
                }

                return bitmap;
            }
        }

        /// <summary>
        /// Performs internal resources clean-up operations.
        /// </summary>
        /// <param name="disposing">Indicates whether the disposing was initialized by user or finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    kerningCache.Clear();
                }

                if (dataHandle.IsAllocated)
                {
                    dataHandle.Free();
                }

                fontInfo.Dispose();
                disposedValue = true;
            }
        }
    }
}