// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a rasterized glyph from a font.
    /// </summary>
    /// <param name="Codepoint">The Unicode character codepoint that represents this glyph.</param>
    /// <param name="GlyphIndex">The index of the glyph within the font.</param>
    /// <param name="PageNumber">Number of the texture page that stores current glyph.</param>
    /// <param name="Advance">The horizontal advance width of the glyph.</param>
    /// <param name="Bearing">The offsets of the glyph's bitmap relative to the origin.</param>
    /// <param name="Size">The dimensions of the glyph's bounding box.</param>
    /// <param name="UVCoordinates">The texture coordinates of the glyph within the font's texture atlas.</param>
    public readonly record struct FontGlyph(int Codepoint, uint GlyphIndex, uint PageNumber, float Advance, Vector2 Bearing, Size Size, Vector4 UVCoordinates)
    {
        /// <summary>
        /// Gets a value representing an empty glyph that should not be rendered.
        /// </summary>
        public static FontGlyph None { get; } = default;

        /// <summary>
        /// Gets a value indicating whether the glyph is empty-sized so can't be rendered.
        /// </summary>
        public bool IsEmpty => Size.Width == 0 || Size.Height == 0;

        /// <summary>
        /// Gets a rectangle source inside texture atlas for this glyph.
        /// </summary>
        /// <param name="textureSize">The size of the texture atlas.</param>
        /// <returns>A rectangle representing the source area in the texture atlas.</returns>
        public Rectangle GetTextureRectangle(Size textureSize)
        {
            int left = (int)(UVCoordinates.X * textureSize.Width);
            int top = (int)(UVCoordinates.Y * textureSize.Height);
            int right = (int)(UVCoordinates.Z * textureSize.Width);
            int bottom = (int)(UVCoordinates.W * textureSize.Height);

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        /// <summary>
        /// Gets a rectangle that represents local render glyph bounds.
        /// </summary>
        /// <param name="position">The position where the glyph will be rendered.</param>
        /// <returns>A rectangle representing the render bounds of the glyph.</returns>
        public Rectangle GetRenderRectangle(Vector2 position)
        {
            int x = (int)(position.X + Bearing.X);
            int y = (int)(position.Y - Bearing.Y);

            return new Rectangle(x, y, Size.Width, Size.Height);
        }
    }
}
