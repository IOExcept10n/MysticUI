// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Frozen;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a read-only texture atlas for static fonts.
    /// </summary>
    public class StaticFontAtlas : IFontAtlas
    {
        private readonly FrozenDictionary<StyledGlyphDefinition, FontGlyph> glyphs;
        private readonly IReadOnlyList<ITexture> textures;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticFontAtlas"/> class.
        /// </summary>
        /// <param name="textures">The pre-rendered texture pages.</param>
        /// <param name="glyphs">The glyphs to store in the atlas.</param>
        public StaticFontAtlas(IReadOnlyList<ITexture> textures, FrozenDictionary<StyledGlyphDefinition, FontGlyph> glyphs)
        {
            this.textures = textures;
            this.glyphs = glyphs;
        }

        /// <inheritdoc/>
        public int PageCount => textures.Count;

        /// <inheritdoc/>
        public IReadOnlyDictionary<StyledGlyphDefinition, FontGlyph> Glyphs => glyphs;

        /// <inheritdoc/>
        public IReadOnlyCollection<ITexture> Textures => textures;

        /// <inheritdoc/>
        public FontGlyph? GetGlyph(StyledGlyphDefinition glyphDefinition) => glyphs.TryGetValue(glyphDefinition, out var glyph) ? glyph : null;

        /// <inheritdoc/>
        public ITexture GetGlyphPage(in FontGlyph glyph) => textures[(int)glyph.PageIndex];
    }
}