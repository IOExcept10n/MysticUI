using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Deinfes an info about the style applied to the text.
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
        /// Gets the styles for this font.
        /// </summary>
        FontStyle Style { get; }

        /// <summary>
        /// Gets the space required to draw string with this font.
        /// </summary>
        /// <param name="text">The text to measure.</param>
        /// <returns>Maximal size in pixels to draw the string with current font parameters.</returns>
        Vector2 MeasureString(string text);
    }
}
