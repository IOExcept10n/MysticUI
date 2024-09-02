// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace AquaUI.Rendering.Brushes
{
    /// <summary>
    /// Represents an interface for all drawable UI tools.
    /// </summary>
    /// <remarks>
    /// Brushes incapsulate texture rendering process
    /// making it crossplatform by including
    /// texture type guard and basic visual effects code.
    /// </remarks>
    public interface IBrush
    {
        /// <summary>
        /// Draws a brush on the specified render surface.
        /// </summary>
        /// <typeparam name="TTexture">Type of textures used in specified engine.</typeparam>
        /// <typeparam name="TGraphics">Type of the graphics device used in specified engine.</typeparam>
        /// <param name="renderer">Instance of the texture renderer to be used for texture rendering.</param>
        /// <param name="destination">Target bounds to render brush into.</param>
        /// <param name="source">Source part of the brush (if available).</param>
        /// <param name="color">Color filter to apply for a brush.</param>
        /// <param name="rotation">Rotation to apply for the rendered texture.</param>
        /// <param name="origin">Rotation origin to apply.</param>
        /// <param name="depth">Depth layer.</param>
        void Draw<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics> renderer, Rectangle destination, Rectangle? source, Color color, float rotation, Vector2 origin, float depth = 0.0f)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class;
    }
}