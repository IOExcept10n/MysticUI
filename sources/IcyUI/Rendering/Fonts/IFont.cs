// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents an interface for the text fonts.
    /// </summary>
    public interface IFont
    {
        /// <summary>
        /// Gets the info about this font.
        /// </summary>
        FontInfo Info { get; }

        /// <summary>
        /// Gets the calculated font metrics.
        /// </summary>
        FontMetrics Metrics { get; }

        /// <summary>
        /// Gets the info about loaded font glyphs.
        /// </summary>
        IReadOnlyDictionary<int, FontGlyph> Glyphs { get; }

        /// <summary>
        /// Checks if a font supports specified codepoint instance.
        /// </summary>
        /// <param name="codepoint">The Unicode codepoint for the character to use.</param>
        /// <returns><see langword="true"/> if this font instance can render specified character; otherwise <see langword="false"/>.</returns>
        bool SupportsCharacter(int codepoint);

        /// <summary>
        /// Gets the glyph for the specified character codepoint.
        /// </summary>
        /// <param name="codepoint">Codepoint to get glyph for.</param>
        /// <returns>Glyph info to use with this font.</returns>
        /// <remarks>
        /// <para>
        /// If the requested codepoint is not found in the font, this method will attempt to return
        /// the glyph for default codepoint. If that also fails, it returns <see cref="FontGlyph.None"/>.
        /// </para>
        /// <para>
        /// We recommend you to use this method instead of calling <see cref="Glyphs"/> by indexer
        /// because this method can have additional logic for the glyphs preparation.
        /// For instance, the <see cref="DynamicSpriteFont"/> rasterizes glyphs only when this method is called.
        /// </para>
        /// </remarks>
        FontGlyph GetGlyph(int codepoint);

        /// <summary>
        /// Gets the space required to draw string with this font.
        /// </summary>
        /// <param name="text">The text to measure.</param>
        /// <param name="options">Options for the measure.</param>
        /// <returns>Maximal size in pixels to draw the string with current font parameters.</returns>
        Vector2 MeasureString(string text, in FontRenderingOptions options);

        /// <summary>
        /// Calculates estimated bounds for the string to render.
        /// </summary>
        /// <param name="text">Text to calculate bounds.</param>
        /// <param name="options">Options for the calculations.</param>
        /// <returns>Estimated bounds required for drawing.</returns>
        Rectangle CalculateBounds(string text, in FontRenderingOptions options);

        /// <summary>
        /// Enumerates glyphs to render for the specified text.
        /// </summary>
        /// <param name="text">Text to enumerate glyphs for.</param>
        /// <param name="options">Rendering options to apply.</param>
        /// <returns>An enumeration for all the rendered glyphs.</returns>
        List<RenderGlyph> GetRenderGlyphs(string text, in FontRenderingOptions options);

        /// <summary>
        /// Draws specified text using this font instance.
        /// </summary>
        /// <param name="context">An instance of the render context to draw font.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="options">Options for the rendering.</param>
        void DrawString(IRenderContext context, string text, in FontRenderingOptions options);
    }
}
