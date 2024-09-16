// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace AquaUI.Rendering.Fonts
{
    /// <summary>
    /// Represents a high-performant implementation of the <see cref="IFont"/> interface.
    /// </summary>
    /// <remarks>
    /// This implementation supports methods to handle text spans which can improve memory and time usage.
    /// </remarks>
    public interface IUtf8DrawableFont : IFont
    {
        /// <inheritdoc cref="IFont.MeasureString(string, in FontRenderingOptions)"/>
        Vector2 MeasureString(ReadOnlySpan<char> text, in FontRenderingOptions options);

        /// <inheritdoc cref="IFont.CalculateBounds(string, in FontRenderingOptions)"/>
        Rectangle CalculateBounds(ReadOnlySpan<char> text, in FontRenderingOptions options);

        /// <inheritdoc cref="IFont.Draw{TTexture, TGraphics}(ITextureRenderer{TTexture, TGraphics}, string, in FontRenderingOptions)"/>
        void Draw<TTexture, TGraphics>(ITextureRenderer<TTexture, TGraphics>, ReadOnlySpan<char> text, in FontRenderingOptions options)
            where TTexture : class, ITexture<TTexture, TGraphics>
            where TGraphics : class;
    }
}