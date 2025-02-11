// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering
{
    /// <summary>
    /// Represents generalized interface for platform texture type adapters.
    /// </summary>
    public interface ITexture
    {
        /// <summary>
        /// Gets the size of a texture.
        /// </summary>
        Size Size { get; }

        /// <summary>
        /// Gets the colors stored in texture, reading data in format represented by <typeparamref name="TColor"/> struct.
        /// </summary>
        /// <typeparam name="TColor">Format of colors stored in the texture.</typeparam>
        /// <param name="buffer">Data buffer to save data to.</param>
        void GetTextureData<TColor>(TColor[] buffer)
            where TColor : unmanaged;

        /// <summary>
        /// Sets the colors data to the specified bounds in texture.
        /// </summary>
        /// <typeparam name="TColor">Format of colors stored in the texture.</typeparam>
        /// <param name="buffer">Buffer to read data from.</param>
        void SetTextureData<TColor>(TColor[] buffer)
            where TColor : unmanaged;
    }
}
