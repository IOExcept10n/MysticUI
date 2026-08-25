// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Diagnostics
{
    /// <summary>
    /// The nested rectangles a devtools-style layout overlay draws for one element - however many of them the
    /// element's own type actually exposes, per <see cref="BoxModelResolver"/>'s remarks.
    /// </summary>
    /// <remarks>
    /// Every rectangle is in the element's own local space - <c>(0,0)</c> at its top-left - matching the
    /// convention <see cref="Rendering.IRenderContext.Transform"/> already represents when
    /// <see cref="IDebugOverlay.Render"/> runs.
    /// </remarks>
    /// <param name="MarginBox">The element's border box expanded outward by its <c>Margin</c>.</param>
    /// <param name="BorderBox">The element's own local bounds - <c>(0,0)</c> sized to <c>ActualBounds</c>.</param>
    /// <param name="PaddingBox">
    /// The border box inset by <c>BorderThickness</c>, or <see langword="null"/> if the element's runtime type
    /// exposes no such property.
    /// </param>
    /// <param name="ContentBox">
    /// The element's <c>IContainerLayout.ContentBounds</c> translated into local space, or
    /// <see langword="null"/> if the element doesn't implement <c>IContainerLayout</c>.
    /// </param>
    public readonly record struct BoxModel(Rectangle MarginBox, Rectangle BorderBox, Rectangle? PaddingBox, Rectangle? ContentBox);
}
