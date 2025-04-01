// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace Icy.Rendering
{
    public static class RenderExtensions
    {
        /// <summary>
        /// Gets the colors stored in texture, reading data in format represented by <typeparamref name="TColor"/> struct.
        /// </summary>
        /// <typeparam name="TColor">Type of the color used to read texture data.</typeparam>
        /// <param name="texture">Texture instance to read data from.</param>
        /// <param name="region">The section of the texture where the data will be copied from. <see langword="null"/> indicates the data will be copied over the entire texture.</param>
        /// <param name="buffer">The array to receive texture data. If <paramref name="region"/> is null, the number of elements in the array must be equal to the <see cref="ITexture.Size"/>; otherwise, the number of elements in the array should be equal to the size of the rectangle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetTextureData<TColor>(this ITexture texture, Rectangle? region, TColor[] buffer)
            where TColor : struct => texture.GetTextureData(region, buffer, 0, buffer.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetTextureData<TColor>(this ITexture texture, TColor[] buffer)
            where TColor : struct => texture.GetTextureData(null, buffer, 0, buffer.Length);

        /// <summary>
        ///
        /// </summary>
        /// <typeparam name="TColor"></typeparam>
        /// <param name="texture"></param>
        /// <param name="region"></param>
        /// <param name="buffer"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureData<TColor>(this ITexture texture, Rectangle? region, TColor[] buffer)
            where TColor : struct => texture.SetTextureData(region, buffer, 0, buffer.Length);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureData<TColor>(this ITexture texture, TColor[] buffer)
            where TColor : struct => texture.SetTextureData(null, buffer, 0, buffer.Length);

        public static void Modify<TColor>(this ITexture texture, Rectangle? region, Func<TColor, TColor> modifier)
            where TColor : struct
        {
            int size = texture.Size.Width * texture.Size.Height;
            var buffer = ArrayPool<TColor>.Shared.Rent(size);
            try
            {
                texture.GetTextureData(buffer: buffer, region: region);
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = modifier(buffer[i]);
                texture.SetTextureData(buffer: buffer, region: region);
            }
            finally
            {
                ArrayPool<TColor>.Shared.Return(buffer);
            }
        }

        public static void Modify<TColor>(this ITexture texture, Func<TColor, TColor> modifier)
            where TColor : struct => Modify(texture, null, modifier);
    }
}