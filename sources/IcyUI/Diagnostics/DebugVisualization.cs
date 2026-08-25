// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering;
using Icy.UI;

namespace Icy.Diagnostics
{
    /// <summary>
    /// Dispatches per-element debug overlays - the only entry point <see cref="UIElement.Draw(IRenderContext)"/>
    /// calls into.
    /// </summary>
    public static class DebugVisualization
    {
        /// <summary>
        /// Renders every <see cref="IDebugOverlay"/> active for <paramref name="element"/> - its
        /// <c>Debug.Visualization</c> override if it has one, otherwise <paramref name="canvas"/>'s
        /// <see cref="Canvas.ActiveDebugTools"/>.
        /// </summary>
        /// <param name="element">The element being drawn.</param>
        /// <param name="context">
        /// The render context, with <see cref="IRenderContext.Transform"/> already set to
        /// <paramref name="element"/>'s own screen transform.
        /// </param>
        /// <param name="canvas">The canvas <paramref name="element"/> belongs to.</param>
        public static void RenderElementOverlays(UIElement element, IRenderContext context, Canvas canvas)
        {
            ICollection<string> active = ResolveActive(element, canvas);
            if (active.Count == 0)
                return;

            DebugToolRegistry registry = canvas.Configuration.Types.Diagnostics;
            foreach (string name in active)
            {
                if (registry.Overlays.TryGetValue(name, out IDebugOverlay? overlay))
                    overlay.Render(element, context);
            }
        }

        private static ICollection<string> ResolveActive(UIElement element, Canvas canvas)
        {
            string? overrideValue = Debug.GetVisualization(element);
            if (overrideValue == null)
                return canvas.ActiveDebugTools;
            if (overrideValue.Length == 0)
                return Array.Empty<string>();

            return overrideValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }
}
