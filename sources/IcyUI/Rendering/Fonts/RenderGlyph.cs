// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents an info about rendered glyph.
    /// </summary>
    /// <remarks>
    /// This info can be used for debugging purposes, text selection or text customization.
    /// </remarks>
    /// <param name="Index">Index of the rendered glyph in the specified string.</param>
    /// <param name="Codepoint">Codepoint representing current glyph.</param>
    /// <param name="Bounds">Bounds of the rendered glyph.</param>
    /// <param name="Advance">Horizontal advance from the beginning of the string applied to the glyph.</param>
    public readonly record struct RenderGlyph(int Index, int Codepoint, Rectangle Bounds, int Advance)
    {
    }
}
