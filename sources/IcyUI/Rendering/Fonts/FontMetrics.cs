// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Represents the metrics of the font.
    /// </summary>
    /// <param name="Ascent">The distance from the baseline to the top of the tallest glyph.</param>
    /// <param name="Descent">The distance from the baseline to the bottom of the lowest glyph.</param>
    /// <param name="LineGap">The recommended spacing between lines of text.</param>
    /// <param name="LowercaseHeight">The height of lowercase letters in the font.</param>
    /// <param name="CapitalHeight">The height of uppercase letters in the font.</param>
    public readonly record struct FontMetrics(float Ascent, float Descent, float LineGap, float LowercaseHeight, float CapitalHeight)
    {
    }
}
