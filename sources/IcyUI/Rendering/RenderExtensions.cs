// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace Icy.Rendering
{
    /// <summary>
    /// Provides extension methods for rendering operations on textures.
    /// </summary>
    public static class RenderExtensions
    {
        /// <summary>
        /// Gets the colors stored in the texture, reading data in the format represented by the <typeparamref name="TColor"/> struct.
        /// </summary>
        /// <typeparam name="TColor">The type of the color used to read texture data. It must be a value type.</typeparam>
        /// <param name="texture">The texture instance to read data from.</param>
        /// <param name="region">The section of the texture from which the data will be copied.
        /// <see langword="null"/> indicates that the data will be copied from the entire texture.</param>
        /// <param name="buffer">The array to receive texture data. If <paramref name="region"/> is null,
        /// the number of elements in the array must be equal to the <see cref="ITexture.Size"/>;
        /// otherwise, the number of elements in the array should be equal to the size of the specified rectangle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetTextureData<TColor>(this ITexture texture, Rectangle? region, TColor[] buffer)
            where TColor : struct => texture.GetTextureData(region, buffer, 0, buffer.Length);

        /// <summary>
        /// Gets the colors stored in the entire texture, reading data in the format represented by the <typeparamref name="TColor"/> struct.
        /// </summary>
        /// <typeparam name="TColor">The type of the color used to read texture data. It must be a value type.</typeparam>
        /// <param name="texture">The texture instance to read data from.</param>
        /// <param name="buffer">The array to receive texture data. The number of elements in the array must be equal to the <see cref="ITexture.Size"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void GetTextureData<TColor>(this ITexture texture, TColor[] buffer)
            where TColor : struct => texture.GetTextureData(null, buffer, 0, buffer.Length);

        /// <summary>
        /// Sets the colors in the texture, writing data in the format represented by the <typeparamref name="TColor"/> struct.
        /// </summary>
        /// <typeparam name="TColor">The type of the color used to write texture data. It must be a value type.</typeparam>
        /// <param name="texture">The texture instance to write data to.</param>
        /// <param name="region">The section of the texture where the data will be written.
        /// <see langword="null"/> indicates that the data will be written to the entire texture.</param>
        /// <param name="buffer">The array containing the texture data to write. If <paramref name="region"/> is null,
        /// the number of elements in the array must be equal to the <see cref="ITexture.Size"/>;
        /// otherwise, the number of elements in the array should be equal to the size of the specified rectangle.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureData<TColor>(this ITexture texture, Rectangle? region, TColor[] buffer)
            where TColor : struct => texture.SetTextureData(region, buffer, 0, buffer.Length);

        /// <summary>
        /// Sets the colors in the entire texture, writing data in the format represented by the <typeparamref name="TColor"/> struct.
        /// </summary>
        /// <typeparam name="TColor">The type of the color used to write texture data. It must be a value type.</typeparam>
        /// <param name="texture">The texture instance to write data to.</param>
        /// <param name="buffer">The array containing the texture data to write. The number of elements in the array must be equal to the <see cref="ITexture.Size"/>.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetTextureData<TColor>(this ITexture texture, TColor[] buffer)
            where TColor : struct => texture.SetTextureData(null, buffer, 0, buffer.Length);

        /// <summary>
        /// Modifies the colors in the texture using a specified modifier function, applying it to a specified region of the texture.
        /// </summary>
        /// <typeparam name="TColor">The type of the color used to modify texture data. It must be a value type.</typeparam>
        /// <param name="texture">The texture instance to modify.</param>
        /// <param name="region">The section of the texture to modify.
        /// <see langword="null"/> indicates that the entire texture will be modified.</param>
        /// <param name="modifier">A function that takes a color of type <typeparamref name="TColor"/> and returns a modified color of the same type.</param>
        /// <remarks>
        /// This method retrieves the current texture data into a temporary buffer, applies the modifier function to each color,
        /// and then writes the modified data back to the specified region of the texture.
        /// </remarks>
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

        /// <summary>
        /// Modifies the colors in the entire texture using a specified modifier function.
        /// </summary>
        /// <typeparam name="TColor">The type of the color used to modify texture data. It must be a value type.</typeparam>
        /// <param name="texture">The texture instance to modify.</param>
        /// <param name="modifier">A function that takes a color of type <typeparamref name="TColor"/> and returns a modified color of the same type.</param>
        /// <remarks>
        /// This method retrieves the current texture data into a temporary buffer, applies the modifier function to each color,
        /// and then writes the modified data back to the entire texture.
        /// </remarks>
        public static void Modify<TColor>(this ITexture texture, Func<TColor, TColor> modifier)
            where TColor : struct => Modify(texture, null, modifier);
    }
}