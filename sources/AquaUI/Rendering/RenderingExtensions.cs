// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Provides extensions for the texture rendering.
    /// </summary>
    public static class RenderingExtensions
    {
#pragma warning disable CS1573 // To inherit documentation from overload

        /// <inheritdoc cref="ITextureRenderer{TTexture, TGraphics}.Draw(TTexture, Vector2, Rectangle?, Color, float, Vector2, Vector2, float)"/>
        /// <param name="destinationRectangle">Bounds to draw texture into.</param>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, float depth)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class
        {
            Vector2 size;
            if (sourceRectangle != null)
            {
                size = new Vector2(sourceRectangle.Value.Width, sourceRectangle.Value.Height);
            }
            else
            {
                size = new Vector2(texture.Size.Width, texture.Size.Height);
            }

            var pos = new Vector2(destinationRectangle.X, destinationRectangle.Y);
            var scale = new Vector2(destinationRectangle.Width / size.X, destinationRectangle.Height / size.Y);
            renderer.Draw(texture, pos, sourceRectangle, color, rotation, origin, scale, depth);
        }

#pragma warning restore

        /// <inheritdoc cref="Draw{TTexture, TGraphics}(ITextureRenderer{TTexture, TGraphics}, TTexture, Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class =>
            Draw(renderer, texture, destinationRectangle, sourceRectangle, color, rotation, origin, 0.0f);

        /// <inheritdoc cref="Draw{TTexture, TGraphics}(ITextureRenderer{TTexture, TGraphics}, TTexture, Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, Rectangle destinationRectangle, Rectangle? sourceRectangle, Color color)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class =>
            Draw(renderer, texture, destinationRectangle, sourceRectangle, color, 0, Vector2.Zero);

        /// <inheritdoc cref="Draw{TTexture, TGraphics}(ITextureRenderer{TTexture, TGraphics}, TTexture, Rectangle, Rectangle?, Color, float, Vector2, float)"/>
        public static void Draw<TTexture, TGraphics>(this ITextureRenderer<TTexture, TGraphics> renderer, TTexture texture, Rectangle destinationRectangle, Color color)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class =>
            Draw(renderer, texture, destinationRectangle, null, color, 0, Vector2.Zero);

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