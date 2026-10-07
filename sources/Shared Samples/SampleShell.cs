// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using Icy.Configuration;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// The sample hosts' whole UI: a sidebar <see cref="TreeView"/> of the <see cref="SampleCatalog"/> demos, grouped by
    /// category, next to the selected demo.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>A demo is built the first time it's selected, then kept, so it keeps its state and scroll offset.</description></item>
    /// <item><description>Only the selected demo is attached to the canvas; the others get no layout, rendering or input.</description></item>
    /// <item><description>
    /// The shared design session attaches before any demo is built, so every demo page is tracked, whatever order you
    /// open them in.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class SampleShell
    {
        /// <summary>
        /// Builds the shell over <see cref="SampleCatalog.All"/> and selects the first demo.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The shell's root element, to add to a <see cref="Canvas"/>.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily) => Build(configuration, fontFamily, SampleCatalog.All);

        /// <summary>
        /// Builds the shell over <paramref name="entries"/>; tests pass their own.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <param name="entries">The demos to list, in sidebar order.</param>
        /// <returns>The shell's root element.</returns>
        internal static UIElement Build(IcyConfiguration configuration, string fontFamily, IReadOnlyList<SampleEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(entries);
            configuration.Fonts.DefaultFontFamily = fontFamily;

            // First, so every demo built from here on is tracked.
            DesignDemo.SessionFor(configuration);

            var tree = new TreeView();
            var categories = new Dictionary<string, TreeViewNode>();
            var leaves = new List<TreeViewNode>();
            foreach (SampleEntry entry in entries)
            {
                if (!categories.TryGetValue(entry.Category, out TreeViewNode? category))
                {
                    category = new TreeViewNode(entry.Category) { IsSelectable = false, IsExpanded = true };
                    categories.Add(entry.Category, category);
                    tree.Items.Add(category);
                }

                var leaf = new TreeViewNode(entry.Name) { Tag = entry };
                category.Children.Add(leaf);
                leaves.Add(leaf);
            }

            var content = new ContentControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            var cache = new Dictionary<SampleEntry, UIElement>();
            tree.SelectionChanged += (_, _) =>
            {
                if (tree.SelectedItem is TreeViewNode { Tag: SampleEntry entry })
                    content.Content = GetOrBuild(entry);
            };

            UIElement GetOrBuild(SampleEntry entry)
            {
                if (!cache.TryGetValue(entry, out UIElement? shown))
                {
                    UIElement demo = entry.Build(configuration, fontFamily);
                    shown = entry.ScrollsItself
                        ? demo
                        : new ScrollViewer
                        {
                            Content = demo,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                        };
                    cache.Add(entry, shown);
                }

                return shown;
            }

            var root = new SplitPane
            {
                First = tree,
                Second = content,
                SplitterPosition = 0.18f,
                MinFirstSize = 180,
                Background = new SolidColorBrush(Color.FromArgb(255, 25, 25, 30)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            // Arrows and the D-pad start in the sidebar.
            root.Attached += (_, _) => root.Canvas?.Focus(tree);

            if (leaves.Count > 0)
                tree.SelectedItem = leaves[0];
            return root;
        }
    }
}
