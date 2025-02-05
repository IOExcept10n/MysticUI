// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering
{
    /// <summary>
    /// Represents an options for the texture and brush rendering.
    /// </summary>
    /// <param name="Destination">Target bounds to render brush into.</param>
    /// <param name="Source">Source part of the brush (if available).</param>
    /// <param name="Color">Color filter to apply for a brush.</param>
    /// <param name="Rotation">Rotation to apply for the rendered texture.</param>
    /// <param name="Origin">Rotation origin to apply.</param>
    /// <param name="Depth">Depth layer.</param>
    public readonly record struct TextureRenderingOptions(Rectangle Destination, Rectangle? Source, Color Color, float Rotation, Vector2 Origin, float Depth)
    {
#pragma warning disable SA1313
        /// <inheritdoc cref="TextureRenderingOptions(Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public TextureRenderingOptions(Rectangle Destination, Rectangle? Source, Color Color, float Rotation, Vector2 Origin)
            : this(Destination, Source, Color, Rotation, Origin, 0.0f)
        {
        }

        /// <inheritdoc cref="TextureRenderingOptions(Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public TextureRenderingOptions(Rectangle Destination, Rectangle? Source, Color Color)
            : this(Destination, Source, Color, 0, Vector2.Zero, 0.0f)
        {
        }

        /// <inheritdoc cref="TextureRenderingOptions(Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public TextureRenderingOptions(Rectangle Destination, Rectangle? Source)
            : this(Destination, Source, Color.White, 0, Vector2.Zero, 0.0f)
        {
        }

        /// <inheritdoc cref="TextureRenderingOptions(Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public TextureRenderingOptions(Rectangle Destination)
            : this(Destination, null, Color.White, 0, Vector2.Zero, 0.0f)
        {
        }
#pragma warning restore
    }
}
