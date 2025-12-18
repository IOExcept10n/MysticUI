// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering
{
    /// <summary>
    /// Represents a service that is used by UI components to draw brushes.
    /// </summary>
    /// <remarks>
    /// At the moment this library doesn't support engine font assets rendering because it doesn't have API for render glyphs access.
    /// </remarks>
    public interface IRenderContext : IDisposable
    {
        /// <summary>
        /// Occurs when size of the viewport changes.
        /// </summary>
        event EventHandler? ViewportResize;

        /// <summary>
        /// Gets the current rendering options.
        /// </summary>
        IRenderOptions Options { get; }

        /// <summary>
        /// Gets an instance of the shared white <see cref="ITexture"/> for auxiliary drawing.
        /// </summary>
        ITexture WhiteTexture { get; }

        /// <summary>
        /// Gets or sets the transform used for rendering.
        /// </summary>
        Transform2D Transform { get; set; }

        /// <summary>
        /// Gets the actual size of the viewport to render into.
        /// </summary>
        /// <remarks>
        /// Typically, the viewport size represent the actual size of the graphics device back buffer.
        /// </remarks>
        Size ViewportSize { get; }

        /// <summary>
        /// Applies an effect to the next draw calls.
        /// </summary>
        /// <param name="effect">Effect instance to apply.</param>
        void ApplyEffect(IEffect effect);

        /// <summary>
        /// Begins rendering process.
        /// </summary>
        void Begin();

        /// <summary>
        /// Remove applied effect instance by setting default effect instance.
        /// </summary>
        void ClearEffects();

        /// <summary>
        /// Creates an instance of the texture with specified size and colors data.
        /// </summary>
        /// <typeparam name="TColor">Format of the pixel colors to set data to the texture.</typeparam>
        /// <param name="width">Width of the texture to create.</param>
        /// <param name="height">Height of the texture to create.</param>
        /// <param name="data">Colors data to set to the texture.</param>
        /// <returns>An instance of the created texture with colors data set.</returns>
        ITexture CreateTexture<TColor>(int width, int height, TColor[] data)
            where TColor : unmanaged;

        /// <summary>
        /// Draws a texture into the rendering surface.
        /// </summary>
        /// <param name="texture">A texture to render.</param>
        /// <param name="options">Options to draw the brush with.</param>
        void Draw(ITexture texture, in TextureRenderingOptions options);

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
        /// <param name="code">The code to identify built-in effect.</param>
        /// <returns>An instance of the <see cref="IEffect"/> for the specified effect code.</returns>
        IEffect GetBuiltInEffect(EffectCode code);
    }
}