// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Rendering;
using Stride.Graphics;
using System.Drawing;

namespace AquaUI
{
    public class TextureWrapper : ITexture<TextureWrapper, GraphicsDevice>
    {
        public TextureWrapper(Texture texture)
        {
            Texture = texture;
        }

        public Size Size => new(Texture.Size.Width, Texture.Size.Height);
        public Texture Texture { get; }

        public static TextureWrapper CreateTexture<TColor>(GraphicsDevice graphics, int width, int height, TColor[] data) where TColor : unmanaged
        {
            // TODO: check different color types etc.
            return Texture.New2D(graphics, width, height, PixelFormat.R8G8B8A8_UNorm, data);
        }

        public static TextureWrapper GetWhite(GraphicsDevice graphics)
        {
            return graphics.GetSharedWhiteTexture();
        }

        public void GetTextureData<TColor>(TColor[] buffer) where TColor : unmanaged
        {
            using CommandList cl = CommandList.New(Texture.GraphicsDevice);
            Texture.GetData(cl, buffer);
        }

        public void SetTextureData<TColor>(TColor[] buffer) where TColor : unmanaged
        {
            using CommandList cl = CommandList.New(Texture.GraphicsDevice);
            Texture.SetData(cl, buffer);
        }

        public static explicit operator Texture(TextureWrapper wrapper) => wrapper.Texture;

        public static implicit operator TextureWrapper(Texture texture) => new(texture);
    }
}