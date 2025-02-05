// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering.Brushes;
using Icy.Rendering.Fonts;

namespace Icy.Rendering
{
    /// <summary>
    /// Represents a service for the brushes rendering.
    /// </summary>
    /// <remarks>
    /// The <see cref="IRenderContext"/> is used by UI components to draw brushes.
    /// Internally, it redirects draw calls to the engine-specific <see cref="ITextureRenderer{TTexture, TGraphics}"/> instance.
    /// </remarks>
    public interface IRenderContext : IDisposable
    {
        /// <summary>
        /// Gets the current rendering options.
        /// </summary>
        IRenderOptions Options { get; }

        /// <summary>
        /// Begins rendering process.
        /// </summary>
        void Begin();

        /// <summary>
        /// Ends rendering process.
        /// </summary>
        void End();

        /// <summary>
        /// Clears rendering queue with making context ready for new render.
        /// </summary>
        void Flush();

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
        /// <param name="options">Options to draw the brush with.</param>
        void Draw(IBrush brush, in TextureRenderingOptions options);

        /// <summary>
        /// Draws a text string.
        /// </summary>
        /// <param name="font">Font instance to draw text with. Note that the font should be compatible with specified instance of the <see cref="IRenderContext"/>.</param>
        /// <param name="text">Text to draw.</param>
        /// <param name="options">Options to draw the text with.</param>
        /// <exception cref="NotSupportedException">Occurs when the specified font type is not supported for the current rendering context.</exception>
        void DrawString(IFont font, string text, in FontRenderingOptions options);
    }
}
