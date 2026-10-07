// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using Icy.Configuration;
using Icy.Design.Editor;
using Icy.Input.Devices;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;
// An alias, not the namespace: Stride Sample references WPF, which has its own KeyGesture there.
using ICommand = System.Windows.Input.ICommand;

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
    /// <item><description>
    /// The sidebar's footer button (or F4) toggles an editor scoped to the demo area: the sidebar keeps working with the
    /// pointer, and the editor stays on across demos.
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
            EditorFrame? editor = null;
            var cache = new Dictionary<SampleEntry, UIElement>();
            tree.SelectionChanged += (_, _) =>
            {
                if (tree.SelectedItem is TreeViewNode { Tag: SampleEntry entry })
                {
                    content.Content = GetOrBuild(entry);

                    // The editor stays on across demos; a selection on the page that left would act on nothing visible.
                    editor?.Session.Clear();
                }
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
                            // A long line of text would otherwise widen the whole page; demos scroll vertically only.
                            HorizontalScrollMode = ScrollMode.Disabled,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                        };
                    cache.Add(entry, shown);
                }

                return shown;
            }

            var editLabel = new TextBlock { Text = "Edit demo (F4)" };
            var editButton = new Button
            {
                Content = editLabel,
                Padding = new Thickness(12, 6),
                Margin = new Thickness(6),
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };
            var editStatus = new TextBlock { Margin = new Thickness(6, 0, 6, 6) };
            var footer = new StackPanel { Orientation = Orientation.Vertical };
            footer.Children.Add(editButton);
            footer.Children.Add(editStatus);
            Grid.SetRow(footer, 1);

            var sidebar = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            sidebar.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            sidebar.Children.Add(tree);
            sidebar.Children.Add(footer);

            var root = new SplitPane
            {
                First = sidebar,
                Second = content,
                SplitterPosition = 0.18f,
                MinFirstSize = 180,
                Background = new SolidColorBrush(Color.FromArgb(255, 25, 25, 30)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            // Arrows and the D-pad start in the sidebar.
            root.Attached += (_, _) => root.Canvas?.Focus(tree);

            void Step(int delta)
            {
                if (leaves.Count == 0)
                    return;

                int index = tree.SelectedItem is TreeViewNode node ? leaves.IndexOf(node) : -1;
                int next = index < 0
                    ? (delta > 0 ? 0 : leaves.Count - 1)
                    : (index + delta + leaves.Count) % leaves.Count;

                // Selecting reveals the leaf: its category expands and the sidebar scrolls to it.
                tree.SelectedItem = leaves[next];
            }

            configuration.Input.Events.RegisterCommand(new ShellCommand(() => Step(1)), new KeyGesture(Keys.PageDown));
            configuration.Input.Events.RegisterCommand(new ShellCommand(() => Step(-1)), new KeyGesture(Keys.PageUp));

            // The shell's own editor works on whichever demo is shown; the sidebar stays usable with the pointer.
            void ToggleEditor()
            {
                if (editor != null)
                {
                    editor.Dispose();
                    editor = null;
                    editLabel.Text = "Edit demo (F4)";
                    editStatus.Text = string.Empty;
                    return;
                }

                if (root.Canvas is not { } canvas)
                    return;
                if (EditorSession.FindAttached(canvas) != null)
                {
                    editStatus.Text = "The Editor demo's editor is on.";
                    return;
                }

                editor = EditorFrame.Attach(canvas, DesignDemo.SessionFor(configuration), content);
                editLabel.Text = "Stop editing (F4)";
                editStatus.Text = string.Empty;
            }

            editButton.Command = new ShellCommand(ToggleEditor);
            configuration.Input.Events.RegisterCommand(new ShellCommand(ToggleEditor), new KeyGesture(Keys.F4));

            if (leaves.Count > 0)
                tree.SelectedItem = leaves[0];
            return root;
        }

        private sealed class ShellCommand(Action execute) : ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute();
        }
    }
}
