// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// One color stop within a <see cref="GradientBrush"/>.
    /// </summary>
    /// <param name="Offset">Position along the gradient, from <c>0</c> to <c>1</c>.</param>
    /// <param name="Color">The color at this stop.</param>
    public readonly record struct GradientStop(float Offset, Color Color);
}
