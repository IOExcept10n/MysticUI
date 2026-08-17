// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;
using Icy.Rendering;
using Stride.Core.Mathematics;
using Stride.Graphics;

namespace Icy.Stride.Rendering
{
    /// <summary>
    /// Represents extension methods to adapt Stride types to library interfaces.
    /// </summary>
    public static class StrideAdapters
    {
        /// <summary>
        /// Wraps an instance of the <see cref="Texture"/> into <see cref="ITexture"/> interface for library usage.
        /// </summary>
        /// <param name="texture">An instance of the <see cref="Texture"/> to wrap.</param>
        /// <param name="renderContext">
        /// The owning <see cref="RenderContext"/>, if any, whose frame-scoped <see cref="Stride.Graphics.GraphicsContext"/>
        /// the wrapped texture reuses for reading/writing its data (see <see cref="TextureAdapter"/>'s remarks).
        /// Pass this whenever <paramref name="texture"/> may have its data read or written later (e.g. a dynamic
        /// font atlas page); omit it for textures that are only ever drawn.
        /// </param>
        /// <returns>An instance of the <see cref="ITexture"/> ready for work with the library.</returns>
        public static ITexture Wrap(this Texture texture, RenderContext? renderContext = null) => new TextureAdapter(texture, renderContext);

        /// <summary>
        /// Tries to unwrap an instance of the texture that has been wrapped using <see cref="Wrap(Texture)"/>.
        /// </summary>
        /// <param name="texture">An instance of the <see cref="ITexture"/> that has been made by calling <see cref="Wrap(Texture)"/>.</param>
        /// <returns>An instance of the <see cref="Texture"/> that has been wrapped to create <paramref name="texture"/>.</returns>
        /// <exception cref="InvalidOperationException">Occurs when <paramref name="texture"/> wasn't wrapped using <see cref="Wrap(Texture)"/> before.</exception>
        public static Texture Unwrap(this ITexture texture) =>
            (texture as TextureAdapter)?.Texture ??
            ThrowHelper.ThrowInvalidOperationException<Texture>("Couldn't find wrapped texture instance.");

        /// <summary>
        /// Performs texture drawing with specified rendering options using an instance of the <see cref="SpriteBatch"/> class.
        /// </summary>
        /// <param name="spriteBatch">An instance of the <see cref="SpriteBatch"/> to draw texture with.</param>
        /// <param name="texture">An instance of the <see cref="Texture"/> to render.</param>
        /// <param name="options">Options for texture drawing.</param>
        public static void Draw(this SpriteBatch spriteBatch, Texture texture, in TextureRenderingOptions options)
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
                Vector2.Zero,
                scale,
                SpriteEffects.None,
                ImageOrientation.AsIs,
                options.Depth);
        }
    }
}
