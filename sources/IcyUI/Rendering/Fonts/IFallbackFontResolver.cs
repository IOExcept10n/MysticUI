// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a service that helps with finding a font that can render specified glyph instance.
    /// </summary>
    public interface IFallbackFontResolver
    {
        /// <summary>
        /// Gets an instance of the font that can render a glyph with the specified parameters.
        /// </summary>
        /// <param name="glyph">Information about the rendered glyph that has not been found in a target font.</param>
        /// <returns>An instance of the font that can render a glyph with the specified data.</returns>
        IFont? GetFallbackFont(StyledGlyphDefinition glyph);
    }
}