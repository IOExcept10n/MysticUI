// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// A per-glyph effect for text rendering, passed through <see cref="FontRenderingOptions.Effect"/>.
    /// </summary>
    /// <remarks>
    /// Reserved for a future text-effects feature: the built-in font renderers don't call <see cref="ApplyEffect"/> yet,
    /// so setting <see cref="FontRenderingOptions.Effect"/> currently has no visible result.
    /// </remarks>
    public interface ICustomFontEffect
    {
        /// <summary>
        /// Applies the effect to one glyph as it's drawn.
        /// </summary>
        /// <param name="glyph">The glyph being drawn.</param>
        /// <param name="renderContext">The context the glyph is drawn with.</param>
        public void ApplyEffect(RenderGlyph glyph, IRenderContext renderContext);
    }
}
