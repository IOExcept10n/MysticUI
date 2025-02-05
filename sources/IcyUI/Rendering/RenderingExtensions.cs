// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering
{
    /// <summary>
    /// Provides extensions for the texture rendering.
    /// </summary>
    public static class RenderingExtensions
    {
        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <typeparam name="TTexture">Supported texture format.</typeparam>
        /// <typeparam name="TGraphics">Supported graphics device type.</typeparam>
        /// <param name="renderer">Instance of the renderer to draw a texture.</param>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="options">Options to apply for a draw.</param>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, in TextureRenderingOptions options)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class
        {
            Vector2 size;
            if (options.Source != null)
            {
                size = new Vector2(options.Source.Value.Width, options.Source.Value.Height);
            }
            else
            {
                size = new Vector2(texture.Size.Width, texture.Size.Height);
            }

            var pos = new Vector2(options.Destination.X, options.Destination.Y);
            var scale = new Vector2(options.Destination.Width / size.X, options.Destination.Height / size.Y);
            renderer.Draw(texture, pos, options.Source, options.Color, options.Rotation, options.Origin, scale, options.Depth);
        }

        /// <inheritdoc cref="ITextureRenderer{TTexture, TGraphics}.Draw(TTexture, Vector2, Rectangle?, Color, float, Vector2, Vector2, float)"/>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, Vector2 position, Color color, Vector2 scale, float rotation, Vector2 origin)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class =>
            renderer.Draw(texture, position, null, color, rotation, origin, scale);

        /// <inheritdoc cref="ITextureRenderer{TTexture, TGraphics}.Draw(TTexture, Vector2, Rectangle?, Color, float, Vector2, Vector2, float)"/>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class =>
            renderer.Draw(texture, position, sourceRectangle, color, rotation, origin, Vector2.One);

        /// <inheritdoc cref="ITextureRenderer{TTexture, TGraphics}.Draw(TTexture, Vector2, Rectangle?, Color, float, Vector2, Vector2, float)"/>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, Vector2 position, Rectangle? sourceRectangle, Color color)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class =>
            renderer.Draw(texture, position, sourceRectangle, color, 0, Vector2.Zero, Vector2.One);

        /// <inheritdoc cref="ITextureRenderer{TTexture, TGraphics}.Draw(TTexture, Vector2, Rectangle?, Color, float, Vector2, Vector2, float)"/>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, Vector2 position, Color color)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class =>
            renderer.Draw(texture, position, null, color, 0, Vector2.Zero, Vector2.One);
    }
}
