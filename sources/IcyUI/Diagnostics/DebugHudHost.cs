// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Diagnostics
{
    /// <summary>
    /// Owns the screen-space debug HUD tree for one <see cref="Canvas"/> - one <see cref="StackPanel"/> per
    /// active <see cref="IDebugHudPanel"/>, docked to the top-right corner.
    /// </summary>
    /// <remarks>
    /// Attached via <c>UIElement.Canvas = canvas</c> directly rather than <see cref="Canvas.Add"/>, so it never
    /// enters <see cref="Canvas.HitTest"/>/focus traversal - a debug HUD that stole clicks or Tab-stops from the
    /// app it's inspecting would defeat its own purpose.
    /// </remarks>
    /// <param name="canvas">The canvas this host builds a HUD for.</param>
    internal sealed class DebugHudHost(Canvas canvas)
    {
        private readonly Dictionary<IDebugHudSection, UIElement> builtSections = [];
        private HashSet<string> builtForTools = [];
        private UIElement? root;

        /// <summary>
        /// Rebuilds the HUD tree if the active tool set changed since the last call, refreshes every section, and
        /// draws it across the canvas's full surface (see <see cref="Canvas.SurfaceSize"/>).
        /// </summary>
        /// <param name="context">The render context to draw with - its transform is reset to the canvas's surface transform and its scissor to the full viewport first.</param>
        /// <param name="elapsed">The previous frame's duration, passed to every section's <see cref="IDebugHudSection.Refresh"/>.</param>
        public void Render(IRenderContext context, TimeSpan elapsed)
        {
            DebugToolRegistry registry = canvas.Configuration.Types.Diagnostics;
            EnsureBuilt(registry);
            if (root == null)
                return;

            root.Arrange(new Rectangle(Point.Empty, canvas.SurfaceSize));

            var frame = new DebugFrameContext(canvas, elapsed);
            foreach ((IDebugHudSection section, UIElement built) in builtSections)
                section.Refresh(built, frame);

            // The HUD is a fixed overlay drawn in surface space (so it scales with the UI), not part of the scene it's
            // inspecting - drop the Canvas's own pan/rotate/scale that context.Transform still holds here, and keep
            // the scissor in physical pixels.
            context.Transform = canvas.SurfaceTransform;
            context.Options.Scissor = new Rectangle(Point.Empty, context.ViewportSize);

            root.Draw(context);
        }

        private void EnsureBuilt(DebugToolRegistry registry)
        {
            if (builtForTools.SetEquals(canvas.ActiveDebugTools))
                return;

            builtSections.Clear();
            var stack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(8),
            };

            foreach (string name in canvas.ActiveDebugTools)
            {
                if (!registry.Panels.TryGetValue(name, out IDebugHudPanel? panel))
                    continue;

                var panelStack = new StackPanel { Orientation = Orientation.Vertical };
                foreach (IDebugHudSection section in panel.Sections)
                {
                    UIElement built = section.Build();
                    builtSections[section] = built;
                    panelStack.Children.Add(built);
                }

                stack.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(200, 20, 20, 24)),
                    Padding = new Thickness(8),
                    Margin = new Thickness(0, 0, 0, 6),
                    Child = panelStack,
                });
            }

            root = stack.Children.Count > 0 ? stack : null;
            if (root != null)
                root.Canvas = canvas;
            builtForTools = new HashSet<string>(canvas.ActiveDebugTools, StringComparer.Ordinal);
        }
    }
}
