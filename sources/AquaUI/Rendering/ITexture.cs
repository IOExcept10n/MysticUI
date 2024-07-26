// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Represents generalized interface for all texture types.
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

    /// <summary>
    /// Represents generic interface for textures with support of initialization and shared access.
    /// </summary>
    /// <typeparam name="TSelf">Type of the texture accessed by this interface.</typeparam>
    /// <typeparam name="TGraphics">Type of the graphics device used to create instance of this texture.</typeparam>
    public interface ITexture<out TSelf, in TGraphics> : ITexture
        where TSelf : class, ITexture<TSelf, TGraphics>
        where TGraphics : class
    {
        /// <summary>
        /// Gets shared white color texture used for utilitary rendering.
        /// </summary>
        /// <param name="graphics">An instance of the graphics device to initialize a texture.</param>
        /// <returns>Shared white texture with size 2px.</returns>
        static abstract TSelf GetWhite(TGraphics graphics);

        /// <summary>
        /// Creates new instance of <typeparamref name="TSelf"/> class.
        /// </summary>
        /// <typeparam name="TColor">Format of colors saved to the texture.</typeparam>
        /// <param name="graphics">An instance of the graphics device to initialize a texture.</param>
        /// <param name="width">Width of the texture to create.</param>
        /// <param name="height">Height of the texture to create.</param>
        /// <param name="data">Data to save to the texture. Leave this array empty to not to fill texture with data.</param>
        /// <returns>New instance of the <typeparamref name="TSelf"/> class.</returns>
        static abstract TSelf CreateTexture<TColor>(TGraphics graphics, int width, int height, TColor[] data)
            where TColor : unmanaged;
    }
}