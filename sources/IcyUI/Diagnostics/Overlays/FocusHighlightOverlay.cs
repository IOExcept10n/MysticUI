// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;
using Icy.UI;
using Icy.UI.Styles;

namespace Icy.Diagnostics.Overlays
{
    /// <summary>
    /// The built-in <c>"Focus"</c> debug overlay: outlines an element while it's
    /// <see cref="ControlState.Focused"/>, <see cref="ControlState.Hovered"/>, or <see cref="ControlState.Pressed"/>,
    /// reading <see cref="UIElement.ControlState"/> directly rather than any separate hit-test bookkeeping.
    /// </summary>
    public class FocusHighlightOverlay : IDebugOverlay
    {
        /// <inheritdoc/>
        public string Name => "Focus";

        /// <inheritdoc/>
        public void Render(UIElement element, IRenderContext context)
        {
            ArgumentNullException.ThrowIfNull(element);
            ArgumentNullException.ThrowIfNull(context);

            Rectangle bounds = new(Point.Empty, element.ActualBounds.Size);
            ControlState state = element.ControlState;

            // Drawn in priority order so the most specific state's outline paints last (on top) when more than
            // one applies at once (e.g. a focused element the pointer also happens to be over).
            if ((state & ControlState.Hovered) != 0)
                context.DrawRectangle(bounds, Color.Silver, 2f);
            if ((state & ControlState.Focused) != 0)
                context.DrawRectangle(bounds, Color.Cyan, 2f);
            if ((state & ControlState.Pressed) != 0)
                context.DrawRectangle(bounds, Color.Red, 2f);
        }
    }
}
