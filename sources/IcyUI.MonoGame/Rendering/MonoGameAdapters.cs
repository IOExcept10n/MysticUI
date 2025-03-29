// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;
using Icy.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Icy.MonoGame.Rendering
{
    /// <summary>
    /// Represents extension methods to adapt MonoGame types to library interfaces.
    /// </summary>
    public static class MonoGameAdapters
    {
        /// <summary>
        /// Wraps an instance of the <see cref="Texture2D"/> into <see cref="ITexture"/> interface for library usage.
        /// </summary>
        /// <param name="texture">An instance of the <see cref="Texture2D"/> to wrap.</param>
        /// <returns>An instance of the <see cref="ITexture"/> ready for work with the library.</returns>
        public static ITexture Wrap(this Texture2D texture) => new TextureAdapter(texture);

        /// <summary>
        /// Tries to unwrap an instance of the texture that has been wrapped using <see cref="Wrap(Texture2D)"/>.
        /// </summary>
        /// <param name="texture">An instance of the <see cref="ITexture"/> that has been making by calling <see cref="Wrap(Texture2D)"/>.</param>
        /// <returns>An instance of the <see cref="Texture2D"/> that has been wrapped to create <paramref name="texture"/>.</returns>
        /// <exception cref="InvalidOperationException">Occurs when <paramref name="texture"/> wasn't wrapped using <see cref="Wrap(Texture2D)"/> before.</exception>
        public static Texture2D Unwrap(this ITexture texture) =>
            (texture as TextureAdapter)?.Texture ??
            ThrowHelper.ThrowInvalidOperationException<Texture2D>("Couldn't find wrapped texture instance.");

        /// <summary>
        /// Performs texture drawing with specified rendering options using an instance of the <see cref="SpriteBatch"/> class.
        /// </summary>
        /// <param name="spriteBatch">An instance of the <see cref="SpriteBatch"/> to draw texture with.</param>
        /// <param name="texture">An instance of the <see cref="Texture2D"/> to render.</param>
        /// <param name="options">Options for texture drawing.</param>
        public static void Draw(this SpriteBatch spriteBatch, Texture2D texture, in TextureRenderingOptions options)
        {
            Vector2 size;
            if (options.Source != null)
            {
                size = new Vector2(options.Source.Value.Width, options.Source.Value.Height);
            }
            else
            {
                size = new Vector2(texture.Width, texture.Height);
            }

            var pos = new Vector2(options.Destination.X, options.Destination.Y);
            var scale = new Vector2(options.Destination.Width / size.X, options.Destination.Height / size.Y);

            spriteBatch.Draw(
                texture,
                pos,
                options.Source?.AsEngineRectangle(),
                options.Color.AsEngineColor(),
                options.Rotation,
                options.Origin,
                scale,
                SpriteEffects.None,
                options.Depth);
        }
    }
}