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
    /// Represents information about a font.
    /// </summary>
    /// <param name="Family">The font family name.</param>
    /// <param name="Size">The font size in pixels.</param>
    /// <param name="Style">The font style.</param>
    public readonly record struct FontInfo(string Family, float Size, FontStyle Style)
    {
        /// <summary>
        /// Gets a value indicating whether this font is dynamic.
        /// </summary>
        public bool IsDynamic => Size <= 0;

        /// <summary>
        /// Creates a new instance of <see cref="FontInfo"/> with the specified size.
        /// </summary>
        /// <param name="size">The new size.</param>
        /// <returns>A new instance with the specified size.</returns>
        public FontInfo WithSize(float size) => this with { Size = size };
    }
}
