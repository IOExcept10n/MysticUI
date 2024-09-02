// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace AquaUI.MonoGame.Rendering
{
    /// <summary>
    /// Represents a <see langword="MonoGame"/> wrapper for the <see cref="ITexture"/> interface.
    /// </summary>
    /// <param name="texture">An instance of the texture to use.</param>
    public class TextureWrapper(Texture2D texture) : ITexture<TextureWrapper, GraphicsDevice>
    {
        private static Texture2D? White;
        private static TextureWrapper? WhiteWrapper;

        /// <summary>
        /// Gets the texture instance used to incapsulate into <see cref="ITexture"/>.
        /// </summary>
        public Texture2D Texture { get; private set; } = texture;

        /// <inheritdoc/>
        public Size Size => new(Texture.Bounds.Width, Texture.Bounds.Height);

        /// <inheritdoc/>
        public static TextureWrapper CreateTexture<TColor>(GraphicsDevice graphics, int width, int height, TColor[] data) where TColor : unmanaged
        {
            var texture = new Texture2D(graphics, width, height);
            texture.SetData(data);
            return texture;
        }

        /// <inheritdoc/>
        public static TextureWrapper GetWhite(GraphicsDevice graphics)
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
        public void GetTextureData<TColor>(TColor[] buffer) where TColor : unmanaged
        {
            Texture.GetData(buffer);
        }

        /// <inheritdoc/>
        public void SetTextureData<TColor>(TColor[] buffer) where TColor : unmanaged
        {
            Texture.SetData(buffer);
        }

        public static implicit operator TextureWrapper(Texture2D texture) => new(texture);

        public static explicit operator Texture2D(TextureWrapper wrapper) => wrapper.Texture;
    }
}