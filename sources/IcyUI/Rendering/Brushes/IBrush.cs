// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
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
        /// <param name="context">An instance of the render context to draw brush with..</param>
        /// <param name="options">Options to draw the brush with.</param>
        void Draw(IRenderContext context, in TextureRenderingOptions options);
    }
}
