// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Defines the interface for font texture atlases.
    /// </summary>
    public interface IFontAtlas
    {
        /// <summary>
        /// Gets the size of each texture page.
        /// </summary>
        Size PageSize { get; }

        /// <summary>
        /// Gets the number of texture pages.
        /// </summary>
        int PageCount { get; }

        /// <summary>
        /// Gets all glyphs stored in the atlas.
        /// </summary>
        IReadOnlyDictionary<int, FontGlyph> Glyphs { get; }

        /// <summary>
        /// Gets a page for the glyph with the specified codepoint.
        /// </summary>
        /// <param name="glyph">Glyph to get atlas page for.</param>
        /// <returns>A texture page for the specified glyph.</returns>
        ITexture GetGlyphPage(in FontGlyph glyph);

        /// <summary>
        /// Gets the glyph for a codepoint.
        /// </summary>
        /// <param name="codepoint">The Unicode codepoint of the glyph.</param>
        /// <returns>The glyph if it exists; otherwise, <see langword="null"/>.</returns>
        FontGlyph? GetGlyph(int codepoint);
    }
}