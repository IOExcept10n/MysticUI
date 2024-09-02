// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Represents a service for texture rendering.
    /// </summary>
    /// <remarks>
    /// The <see cref="IRenderContext"/> implicitly uses <see cref="ITextureRenderer{TTexture, TGraphics}"/> to allow brushes draw textures.
    /// </remarks>
    /// <typeparam name="TTexture">Type of supported texture format.</typeparam>
    /// <typeparam name="TGraphics">Type of the graphics device used in specified engine.</typeparam>
    public interface ITextureRenderer<in TTexture, out TGraphics>
        where TTexture : class, ITexture<TTexture, TGraphics>
        where TGraphics : class
    {
        /// <summary>
        /// Gets the reference to a used graphics device.
        /// </summary>
        TGraphics GraphicsDevice { get; }

        /// <summary>
        /// Gets the current rendering context to apply effects and draw primitives.
        /// </summary>
        IRenderContext Context { get; }

        /// <summary>
        /// Draws a texture.
        /// </summary>
        /// <param name="texture">Texture to draw.</param>
        /// <param name="position">Position to draw into.</param>
        /// <param name="sourceRectangle">Source area from the texture.</param>
        /// <param name="color">Color to apply to a texture.</param>
        /// <param name="rotation">Rotation to apply to a texture.</param>
        /// <param name="origin">Origin of the rotation to apply.</param>
        /// <param name="scale">Scale to apply.</param>
        /// <param name="depth">Z-layer depth.</param>
        void Draw(TTexture texture, Vector2 position, Rectangle? sourceRectangle, Color color, float rotation, Vector2 origin, Vector2 scale, float depth = 0f);
    }
}