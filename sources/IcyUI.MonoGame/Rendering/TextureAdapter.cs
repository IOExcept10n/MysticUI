// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace Icy.MonoGame.Rendering
{
    /// <summary>
    /// Represents a <see langword="MonoGame"/> wrapper for the <see cref="ITexture"/> interface.
    /// </summary>
    /// <param name="texture">An instance of the texture to use.</param>
    internal class TextureAdapter(Texture2D texture) : ITexture
    {
        /// <inheritdoc/>
        public Size Size => new(Texture.Bounds.Width, Texture.Bounds.Height);

        /// <summary>
        /// Gets the texture instance used to incapsulate into <see cref="ITexture"/>.
        /// </summary>
        public Texture2D Texture { get; private set; } = texture;

        public static implicit operator TextureAdapter(Texture2D texture) => new(texture);

        public static explicit operator Texture2D(TextureAdapter wrapper) => wrapper.Texture;

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

        /// <inheritdoc/>
        public void Dispose()
        {
            Texture.Dispose();
        }
    }
}