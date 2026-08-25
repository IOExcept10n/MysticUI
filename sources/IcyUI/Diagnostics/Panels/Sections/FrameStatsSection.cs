// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Diagnostics.Panels.Sections
{
    /// <summary>
    /// Shows the previous frame's duration and derived FPS.
    /// </summary>
    public class FrameStatsSection : IDebugHudSection
    {
        /// <inheritdoc/>
        public string Name => "Frame";

        /// <inheritdoc/>
        public UIElement Build() => new TextBlock { Foreground = Color.WhiteSmoke, FontSize = 13 };

        /// <inheritdoc/>
        public void Refresh(UIElement built, in DebugFrameContext frame)
        {
            var text = (TextBlock)built;
            double seconds = frame.Elapsed.TotalSeconds;
            double fps = seconds > 0 ? 1 / seconds : 0;
            text.Text = $"{fps:F0} FPS ({frame.Elapsed.TotalMilliseconds:F1} ms)";
        }
    }
}
