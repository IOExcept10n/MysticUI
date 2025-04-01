// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace Icy.MonoGame.Rendering
{
    /// <summary>
    /// Represents a <see langword="MonoGame"/> wrapper for the <see cref="ITexture"/> interface.
    /// </summary>
    /// <param name="texture">An instance of the texture to use.</param>
    internal class TextureAdapter(Texture2D texture) : ITexture
    {
        private static Texture2D? White;
        private static TextureAdapter? WhiteWrapper;

        /// <summary>
        /// Gets the texture instance used to incapsulate into <see cref="ITexture"/>.
        /// </summary>
        public Texture2D Texture { get; private set; } = texture;

        /// <inheritdoc/>
        public Size Size => new(Texture.Bounds.Width, Texture.Bounds.Height);

        /// <inheritdoc/>
        public static TextureAdapter CreateTexture<TColor>(GraphicsDevice graphics, int width, int height, TColor[] data) where TColor : unmanaged
        {
            var texture = new Texture2D(graphics, width, height);
            texture.SetData(data);
            return texture;
        }

        /// <inheritdoc/>
        public static TextureAdapter GetWhite(GraphicsDevice graphics)
        {
            if (WhiteWrapper == null)
            {
                CreateWhiteTexture(graphics);
                WhiteWrapper = White;
            }
            return WhiteWrapper;
        }

        /// <inheritdoc/>
        [MemberNotNull(nameof(White))]
        private static void CreateWhiteTexture(GraphicsDevice graphics)
        {
            White = new(graphics, 2, 2);
            Microsoft.Xna.Framework.Color white = Color.White.AsEngineColor();
            // The most simple way to fill texture with white color
            White.SetData([white, white, white, white]);
        }

        /// <inheritdoc/>
        public void GetTextureData<TColor>(Rectangle? region, TColor[] buffer, int startIndex, int count)
            where TColor : struct
        {
            Texture.GetData(0, region?.AsEngineRectangle(), buffer, startIndex, count);
        }

        /// <inheritdoc/>
        public void SetTextureData<TColor>(Rectangle? region, TColor[] buffer, int startIndex, int count)
            where TColor : struct
        {
            Texture.SetData(0, region?.AsEngineRectangle(), buffer, startIndex, count);
        }

        public static implicit operator TextureAdapter(Texture2D texture) => new(texture);

        public static explicit operator Texture2D(TextureAdapter wrapper) => wrapper.Texture;
    }
}
