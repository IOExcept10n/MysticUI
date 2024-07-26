// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using AquaUI.Rendering.Brushes;

namespace AquaUI.Rendering
{
    /// <summary>
    /// Represents a service for engine-dependent renderer implementation.
    /// </summary>
    public interface IRenderContext : IDisposable
    {
        /// <summary>
        /// Gets the rendering options.
        /// </summary>
        IRenderOptions Options { get; }

        /// <summary>
        /// Begins rendering process.
        /// </summary>
        void Begin();

        /// <summary>
        /// Ends rendeing process.
        /// </summary>
        void End();

        /// <summary>
        /// Clears rendering queue making context ready for new render.
        /// </summary>
        void Flush();

        /// <summary>
        /// Applies a texture mask to the current render context.
        /// </summary>
        /// <remarks>
        /// Note that now only one mask at the time can be used.
        /// </remarks>
        /// <param name="texture">Texture to use as a mask.</param>
        /// <param name="transform">Transform to apply to the mask.</param>
        void ApplyMask(ITexture texture, Transform2D transform);

        /// <summary>
        /// Removes the current texture mask.
        /// </summary>
        void ClearMask();

        /// <summary>
        /// Gets the built-in effect instance by its <see cref="EffectCode"/>.
        /// </summary>
        /// <remarks>
        /// Built-in effects needed for some UI components and features.
        /// </remarks>
        /// <param name="code">The code to identify built-in effect.</param>
        /// <returns>An instance of the <see cref="IEffect"/> for the specified effect code.</returns>
        IEffect GetBuiltInEffect(EffectCode code);

        /// <summary>
        /// Remove applied effect instance by setting default effect instance.
        /// </summary>
        void ClearEffects();

        /// <summary>
        /// Applies an effect to the next draw calls.
        /// </summary>
        /// <remarks>
        /// Depending on implementation, this method could implicitly call <see cref="Flush"/> method.
        /// </remarks>
        /// <param name="effect">Effect instance to apply.</param>
        void ApplyEffect(IEffect effect);

        /// <summary>
        /// Draws a brush into the rendering surface.
        /// </summary>
        /// <param name="brush">A brush to render.</param>
        /// <param name="destination">Drawing location rectangle.</param>
        /// <param name="sourceRectangle">Source area to get brush part from.</param>
        /// <param name="color">Color filter to apply to brush.</param>
        /// <param name="rotation">Rotation to apply for a brush.</param>
        /// <param name="depth">Depth layer to draw textures with.</param>
        void Draw(IBrush brush, Rectangle destination, Rectangle? sourceRectangle, Color color, float rotation, float depth = 0.0f);

        /// <summary>
        /// Draws a text string.
        /// </summary>
        /// <param name="font">Font instance to draw text with. Note that the font should be compatible with specified instance of the <see cref="IRenderContext"/>.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="position">Drawing location on screen.</param>
        /// <param name="color">A color filter to apply.</param>
        /// <param name="scale">A scaling for specified text.</param>
        /// <param name="rotation">A rotation for specified text in radians.</param>
        /// <param name="layerDepth">A depth of the layer for specified string to render.</param>
        /// <exception cref="NotSupportedException">Occurs when the specified font type is not supported for the current rendering context.</exception>
        void DrawString(IFont font, string text, Vector2 position, Color color, Vector2 scale, float rotation, float layerDepth = 0f);
    }
}