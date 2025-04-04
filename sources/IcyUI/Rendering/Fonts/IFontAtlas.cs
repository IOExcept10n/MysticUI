// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Defines the interface for font texture atlases.
    /// </summary>
    public interface IFontAtlas
    {
        /// <summary>
        /// Gets the number of texture pages.
        /// </summary>
        int PageCount { get; }

        /// <summary>
        /// Gets all glyphs stored in the atlas.
        /// </summary>
        IReadOnlyDictionary<StyledGlyphDefinition, FontGlyph> Glyphs { get; }

        /// <summary>
        /// Gets the font textures.
        /// </summary>
        IReadOnlyCollection<ITexture> Textures { get; }

        /// <summary>
        /// Gets a page for the glyph with the specified codepoint.
        /// </summary>
        /// <param name="glyph">Glyph to get atlas page for.</param>
        /// <returns>A texture page for the specified glyph.</returns>
        ITexture GetGlyphPage(in FontGlyph glyph);

        /// <summary>
        /// Gets the glyph for a codepoint.
        /// </summary>
        /// <param name="glyphDefinition">Info about glyph codepoint and style to get glyph details for.</param>
        /// <returns>The glyph if it exists; otherwise, <see langword="null"/>.</returns>
        FontGlyph? GetGlyph(StyledGlyphDefinition glyphDefinition);
    }

    /// <summary>
    /// Represents an identification data for the glyph for the search in font atlas.
    /// </summary>
    /// <param name="Codepoint">The Unicode codepoint of the glyph.</param>
    /// <param name="FontSize">Glyph font size in points.</param>
    /// <param name="Style">Glyph font style info.</param>
    public readonly record struct StyledGlyphDefinition(int Codepoint, float FontSize, FontStyle Style);
}