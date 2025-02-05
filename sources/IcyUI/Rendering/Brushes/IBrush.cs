// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// Represents an interface for all drawable UI tools.
    /// </summary>
    /// <remarks>
    /// Brushes incapsulate texture rendering process
    /// making it cross-platform by including
    /// texture type guard and basic visual effects code.
    /// </remarks>
    public interface IBrush
    {
        /// <summary>
        /// Draws a brush on the specified render surface.
        /// </summary>
        /// <typeparam name="TTexture">Type of textures used in specified engine.</typeparam>
        /// <typeparam name="TGraphics">Type of the graphics device used in specified engine.</typeparam>
        /// <param name="renderer">An instance of the texture renderer used for the texture rendering.</param>
        /// <param name="options">Options to draw the brush with.</param>
        void Draw<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics> renderer, in TextureRenderingOptions options)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class;
    }
}
