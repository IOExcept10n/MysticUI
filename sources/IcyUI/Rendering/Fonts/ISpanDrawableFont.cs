// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents a high-performant implementation of the <see cref="IFont"/> interface.
    /// </summary>
    /// <remarks>
    /// This implementation supports methods to handle text spans which can improve memory and time usage.
    /// </remarks>
    public interface ISpanDrawableFont : IFont
    {
        /// <inheritdoc cref="IFont.MeasureString(string, in FontRenderingOptions)"/>
        Vector2 MeasureString(ReadOnlySpan<char> text, in FontRenderingOptions options);

        /// <inheritdoc cref="IFont.CalculateBounds(string, in FontRenderingOptions)"/>
        Rectangle CalculateBounds(ReadOnlySpan<char> text, in FontRenderingOptions options);

        /// <inheritdoc cref="IFont.GetRenderGlyphs(string, in FontRenderingOptions)"/>
        List<RenderGlyph> GetRenderGlyphs(ReadOnlySpan<char> text, in FontRenderingOptions options);

        /// <inheritdoc cref="IFont.DrawString(IRenderContext, string, in FontRenderingOptions)"/>
        void DrawString(IRenderContext context, ReadOnlySpan<char> text, in FontRenderingOptions options);
    }
}
