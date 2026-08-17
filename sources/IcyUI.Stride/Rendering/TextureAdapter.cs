// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Runtime.InteropServices;
using CommunityToolkit.Diagnostics;
using Icy.Rendering;
using Stride.Graphics;

namespace Icy.Stride.Rendering
{
    /// <summary>
    /// Represents a Stride wrapper for the <see cref="ITexture"/> interface.
    /// </summary>
    /// <param name="texture">An instance of the texture to use.</param>
    /// <param name="renderContext">
    /// The owning <see cref="Rendering.RenderContext"/>, if any, whose frame-scoped
    /// <see cref="Rendering.RenderContext.GraphicsContext"/> is reused for <see cref="GetTextureData{TColor}"/>/
    /// <see cref="SetTextureData{TColor}"/> - see their remarks for why this can't just create its own
    /// <see cref="CommandList"/>. <see langword="null"/> for textures that only ever get drawn, never
    /// read/written (e.g. imported image assets), which never need one.
    /// </param>
    internal class TextureAdapter(Texture texture, RenderContext? renderContext = null) : ITexture
    {
        /// <inheritdoc/>
        public Size Size => new(Texture.Width, Texture.Height);

        /// <summary>
        /// Gets the texture instance used to incapsulate into <see cref="ITexture"/>.
        /// </summary>
        public Texture Texture { get; private set; } = texture;

        /// <summary>
        /// Gets the current frame's shared <see cref="CommandList"/>, reused (never created here) because Stride's
        /// D3D11 backend permits only one <see cref="CommandList"/> per <see cref="GraphicsDevice"/> - minting a
        /// new one per texture upload (as this used to do via <c>CommandList.New</c>) throws
        /// <see cref="InvalidOperationException"/> ("Can't create multiple command lists with D3D11") as soon as a
        /// second upload happens within the same frame, e.g. rasterizing more than one dynamic-font glyph.
        /// </summary>
        private CommandList CommandList =>
            renderContext?.GraphicsContext.CommandList ??
            ThrowHelper.ThrowInvalidOperationException<CommandList>(
                $"This texture wasn't created with a {nameof(Rendering.RenderContext)} to source a {nameof(CommandList)} " +
                $"from, so its data can't be read or written.");

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
            var rawPixels = new byte[Texture.Width * Texture.Height * Marshal.SizeOf<TColor>()];
            Texture.GetData(CommandList, rawPixels);
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
            byte[] rawPixels = MemoryMarshal.AsBytes(buffer.AsSpan(startIndex, elementCount)).ToArray();
            ResourceRegion? resourceRegion = region is { } r
                ? new ResourceRegion(r.Left, r.Top, 0, r.Right, r.Bottom, 1)
                : null;
            Texture.SetData(CommandList, rawPixels, region: resourceRegion);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Texture.Dispose();
        }
    }
}
