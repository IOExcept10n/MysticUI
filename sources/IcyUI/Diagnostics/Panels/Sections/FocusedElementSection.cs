// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Diagnostics.Panels.Sections
{
    /// <summary>
    /// Shows the currently focused element's type and its ancestor chain up to its root - the "label that shows
    /// currently focused element names and hierarchies" example from the design discussion.
    /// </summary>
    public class FocusedElementSection : IDebugHudSection
    {
        /// <inheritdoc/>
        public string Name => "Focus";

        /// <inheritdoc/>
        public UIElement Build() => new TextBlock { Foreground = Color.WhiteSmoke, FontSize = 13 };

        /// <inheritdoc/>
        public void Refresh(UIElement built, in DebugFrameContext frame)
        {
            var text = (TextBlock)built;
            UIElement? focused = frame.Canvas.FocusedElement;
            text.Text = focused == null ? "Focused: (none)" : $"Focused: {DescribeChain(focused)}";
        }

        private static string DescribeChain(UIElement element)
        {
            List<string> chain = [];
            for (UIElement? current = element; current != null; current = current.Parent)
                chain.Add(current.Name is { Length: > 0 } name ? $"{current.GetType().Name}({name})" : current.GetType().Name);

            chain.Reverse();
            return string.Join(" > ", chain);
        }
    }
}
