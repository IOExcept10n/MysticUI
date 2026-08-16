// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Runtime.InteropServices;
using Icy.Rendering;
using Stride.Graphics;

namespace Icy.Stride.Rendering
{
    /// <summary>
    /// Represents a Stride wrapper for the <see cref="ITexture"/> interface.
    /// </summary>
    /// <param name="texture">An instance of the texture to use.</param>
    internal class TextureAdapter(Texture texture) : ITexture
    {
        /// <inheritdoc/>
        public Size Size => new(Texture.Width, Texture.Height);

        /// <summary>
        /// Gets the texture instance used to incapsulate into <see cref="ITexture"/>.
        /// </summary>
        public Texture Texture { get; private set; } = texture;

        public static implicit operator TextureAdapter(Texture texture) => new(texture);

        public static explicit operator Texture(TextureAdapter wrapper) => wrapper.Texture;

        /// <inheritdoc/>
        public void GetTextureData<TColor>(Rectangle? region, TColor[] buffer, int startIndex, int elementCount)
            where TColor : struct
        {
            // ITexture only constrains TColor to `struct`, but Stride's Texture.GetData<TData> requires
            // `unmanaged`. Bridging through byte (which always satisfies `unmanaged`, regardless of what TColor
            // actually is) and reinterpreting via MemoryMarshal sidesteps that without narrowing this method's own
            // constraint, which C# doesn't allow on an interface implementation anyway.
            using CommandList commandList = CommandList.New(Texture.GraphicsDevice);
            var rawPixels = new byte[Texture.Width * Texture.Height * Marshal.SizeOf<TColor>()];
            Texture.GetData(commandList, rawPixels);
            ReadOnlySpan<TColor> allPixels = MemoryMarshal.Cast<byte, TColor>(rawPixels);

            if (region == null)
            {
                allPixels.Slice(0, elementCount).CopyTo(buffer.AsSpan(startIndex));
                return;
            }

            Rectangle r = region.Value;
            int i = startIndex;
            for (int y = r.Top; y < r.Bottom; y++)
            {
                for (int x = r.Left; x < r.Right; x++)
                {
                    buffer[i++] = allPixels[(y * Texture.Width) + x];
                }
            }
        }

        /// <inheritdoc/>
        public void SetTextureData<TColor>(Rectangle? region, TColor[] buffer, int startIndex, int elementCount)
            where TColor : struct
        {
            using CommandList commandList = CommandList.New(Texture.GraphicsDevice);
            byte[] rawPixels = MemoryMarshal.AsBytes(buffer.AsSpan(startIndex, elementCount)).ToArray();
            ResourceRegion? resourceRegion = region is { } r
                ? new ResourceRegion(r.Left, r.Top, 0, r.Right, r.Bottom, 1)
                : null;
            Texture.SetData(commandList, rawPixels, region: resourceRegion);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Texture.Dispose();
        }
    }
}
