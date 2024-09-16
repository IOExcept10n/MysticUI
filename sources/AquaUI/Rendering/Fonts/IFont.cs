// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace AquaUI.Rendering.Fonts
{
    /// <summary>
    /// Defines an info about the style applied to the text.
    /// </summary>
    /// <remarks>
    /// This enum supports a bitwise combination of its members values.
    /// </remarks>
    [Flags]
    public enum FontStyle
    {
        /// <summary>
        /// Standard text.
        /// </summary>
        Regular,

        /// <summary>
        /// Bold text.
        /// </summary>
        Bold,

        /// <summary>
        /// Italic text.
        /// </summary>
        Italic,

        /// <summary>
        /// Underlined text.
        /// </summary>
        Underline,

        /// <summary>
        /// Text with a line through the middle.
        /// </summary>
        Strikeout,
    }

    /// <summary>
    /// Represents an interface for the text fonts.
    /// </summary>
    public interface IFont
    {
        /// <summary>
        /// Gets the name of the font family.
        /// </summary>
        string FontFamily { get; }

        /// <summary>
        /// Gets the size of the font (pts).
        /// </summary>
        float FontSize { get; }

        /// <summary>
        /// Gets the height of the font line.
        /// </summary>
        int LineHeight { get; }

        /// <summary>
        /// Gets the styles for this font.
        /// </summary>
        FontStyle Style { get; }

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
        /// Draws a font with given texture renderer instance.
        /// </summary>
        /// <typeparam name="TTexture">Type of the textures used in the renderer.</typeparam>
        /// <typeparam name="TGraphics">Type of the graphics device used in the renderer.</typeparam>
        /// <param name="renderer">Renderer to draw textures.</param>
        /// <param name="text">Text to write.</param>
        /// <param name="options">Options for the renderer.</param>
        void Draw<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics> renderer, string text, in FontRenderingOptions options)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class;
    }
}