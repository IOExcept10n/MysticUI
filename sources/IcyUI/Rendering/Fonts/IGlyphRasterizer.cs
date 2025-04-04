// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a glyph rasterizer that can convert font glyphs to textures.
    /// </summary>
    public interface IGlyphRasterizer : IDisposable
    {
        /// <summary>
        /// Rasterizes a glyph at the specified size and style.
        /// </summary>
        /// <param name="codepoint">The Unicode codepoint of the glyph to rasterize.</param>
        /// <param name="size">The size of the glyph in pixels.</param>
        /// <param name="style">The style of the glyph.</param>
        /// <returns>The rasterized glyph pixels byte sequence.</returns>
        IMemoryOwner<byte>? RasterizeGlyph(int codepoint, float size, FontStyle style);

        /// <summary>
        /// Gets metrics for the font with the specified font size.
        /// </summary>
        /// <param name="fontSize">Expected font size in points to calculate scaled metrics for.</param>
        /// <param name="style">The style of the font to use.</param>
        /// <returns>An instance of the <see cref="FontMetrics"/> struct with font calculated metrics.</returns>
        FontMetrics GetFontMetrics(float fontSize, FontStyle style);

        /// <summary>
        /// Gets the metrics for a glyph at the specified size and style.
        /// </summary>
        /// <param name="codepoint">The Unicode codepoint of the glyph.</param>
        /// <param name="size">The size of the glyph in pixels.</param>
        /// <param name="style">The style of the font.</param>
        /// <returns>The metrics for the glyph.</returns>
        FontGlyph GetGlyphMetrics(int codepoint, float size, FontStyle style);

        /// <summary>
        /// Gets the kerning between two glyphs.
        /// </summary>
        /// <param name="first">First glyph codepoint.</param>
        /// <param name="second">Second glyph codepoint.</param>
        /// <param name="size">Font size.</param>
        /// <param name="style">Font style.</param>
        /// <returns>The kerning value in pixels.</returns>
        float GetKerning(int first, int second, float size, FontStyle style);

        /// <summary>
        /// Checks if the character is supported in this font.
        /// </summary>
        /// <param name="codepoint">Unicode character codepoint to check.</param>
        /// <returns><see langword="true"/> if the character with the specified codepoint is supported by this font instance; otherwise <see langword="false"/>.</returns>
        bool ContainsGlyph(int codepoint);
    }
}