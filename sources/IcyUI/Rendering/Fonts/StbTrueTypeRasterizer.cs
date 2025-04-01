// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;
using System.Drawing;
using System.Numerics;
using static StbTrueTypeSharp.StbTrueType;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a glyph rasterizer that uses StbTrueTypeSharp for TrueType font rendering.
    /// </summary>
    public class StbTrueTypeRasterizer : IGlyphRasterizer
    {
        private readonly byte[] fontData;
        private readonly stbtt_fontinfo fontInfo;
        private readonly Dictionary<(int First, int Second, float Size, FontStyle Style), float> kerningCache;
        private bool disposedValue;

        /// <summary>
        /// Initializes a new instance of the <see cref="StbTrueTypeRasterizer"/> class.
        /// </summary>
        /// <param name="fontData">The font data.</param>
        public StbTrueTypeRasterizer(byte[] fontData)
        {
            this.fontData = fontData;
            fontInfo = new stbtt_fontinfo();
            kerningCache = new Dictionary<(int First, int Second, float Size, FontStyle Style), float>();

            unsafe
            {
                fixed (byte* ptr = fontData)
                {
                    if (stbtt_InitFont(fontInfo, ptr, 0) == 0)
                        throw new InvalidOperationException("Failed to initialize font.");
                }
            }
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
                int paddedWidth = width + padding * 2;
                int paddedHeight = height + padding * 2;
                IMemoryOwner<byte> bitmap = MemoryPool<byte>.Shared.Rent(paddedWidth * paddedHeight);

                fixed (byte* ptr = bitmap.Memory.Span)
                {
                    stbtt_MakeGlyphBitmap(fontInfo, ptr, paddedWidth, paddedHeight, paddedWidth, scale, scale, index);
                }

                return bitmap;
            }
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

                // Add padding for hinting at small sizes
                int padding = size <= 16 ? 1 : 0;
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    kerningCache.Clear();
                }

                fontInfo.Dispose();
                disposedValue = true;
            }
        }

        ~StbTrueTypeRasterizer()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
} 