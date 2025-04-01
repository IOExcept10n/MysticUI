// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Collections.Frozen;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a read-only texture atlas for static fonts.
    /// </summary>
    public class StaticFontAtlas : IFontAtlas
    {
        private readonly FrozenDictionary<int, FontGlyph> glyphs;
        private readonly IReadOnlyList<ITexture> textures;
        private readonly Size pageSize;

        /// <summary>
        /// Initializes a new instance of the <see cref="StaticFontAtlas"/> class.
        /// </summary>
        /// <param name="pageSize">Size of each texture page.</param>
        /// <param name="textures">The pre-rendered texture pages.</param>
        /// <param name="glyphs">The glyphs to store in the atlas.</param>
        public StaticFontAtlas(Size pageSize, IReadOnlyList<ITexture> textures, FrozenDictionary<int, FontGlyph> glyphs)
        {
            this.pageSize = pageSize;
            this.textures = textures;
            this.glyphs = glyphs;
        }

        /// <inheritdoc/>
        public Size PageSize => pageSize;

        /// <inheritdoc/>
        public int PageCount => textures.Count;

        /// <inheritdoc/>
        public IReadOnlyDictionary<int, FontGlyph> Glyphs => glyphs;

        /// <inheritdoc/>
        public FontGlyph? GetGlyph(int codepoint)
        {
            return glyphs.TryGetValue(codepoint, out var glyph) ? glyph : null;
        }

        public ITexture GetGlyphPage(in FontGlyph glyph) => textures[(int)glyph.PageIndex];
    }
}