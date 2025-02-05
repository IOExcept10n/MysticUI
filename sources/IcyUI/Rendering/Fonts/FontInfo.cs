using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icy.Rendering.Fonts
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
        Regular = 0,

        /// <summary>
        /// Bold text.
        /// </summary>
        Bold = 1,

        /// <summary>
        /// Italic text.
        /// </summary>
        Italic = 2,

        /// <summary>
        /// Underlined text.
        /// </summary>
        Underline = 4,

        /// <summary>
        /// Text with a line through the middle.
        /// </summary>
        Strikeout = 8,
    }

    /// <summary>
    /// Represents information about the font.
    /// </summary>
    /// <param name="FontFamily">The font family presented by this font.</param>
    /// <param name="FontSize">The size of the font.</param>
    /// <param name="Style">The style applied to the font.</param>
    public readonly record struct FontInfo(string FontFamily, float FontSize, FontStyle Style);
}
