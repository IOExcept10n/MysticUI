// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;
using Icy.UI;

namespace Icy.Diagnostics.Overlays
{
    /// <summary>
    /// The built-in <c>"Bounds"</c> debug overlay: draws an element's margin/border/padding/content boxes as
    /// nested outlines, in the same margin-orange/border-yellow/padding-green/content-blue convention browser
    /// devtools use.
    /// </summary>
    public class BoxModelOverlay : IDebugOverlay
    {
        /// <inheritdoc/>
        public string Name => "Bounds";

        /// <inheritdoc/>
        public void Render(UIElement element, IRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            BoxModel box = BoxModelResolver.Resolve(element);

            context.DrawRectangle(box.MarginBox, Color.Orange);
            context.DrawRectangle(box.BorderBox, Color.Gold);

            if (box.PaddingBox is { } paddingBox)
                context.DrawRectangle(paddingBox, Color.MediumSeaGreen);

            if (box.ContentBox is { } contentBox)
                context.DrawRectangle(contentBox, Color.DodgerBlue);
        }
    }
}
