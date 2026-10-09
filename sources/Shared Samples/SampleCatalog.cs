// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Collections.Generic;
using Icy.Configuration;
using Icy.UI;

namespace Icy.SharedSamples
{
    /// <summary>
    /// One demo the <see cref="SampleShell"/> lists.
    /// </summary>
    /// <param name="Category">The sidebar category the demo appears under.</param>
    /// <param name="Name">The demo's sidebar label.</param>
    /// <param name="Build">Builds the demo's root element from the host's configuration and font family.</param>
    /// <param name="ScrollsItself">
    /// <see langword="true"/> when the demo brings its own full-size <see cref="UI.Controls.ScrollViewer"/>, so the shell
    /// must not wrap it in another one.
    /// </param>
    public sealed record SampleEntry(string Category, string Name, Func<IcyConfiguration, string, UIElement> Build, bool ScrollsItself = false);

    /// <summary>
    /// The demos both sample hosts show, in sidebar order. Adding a demo takes one line here.
    /// </summary>
    public static class SampleCatalog
    {
        /// <summary>
        /// Gets every demo in sidebar order. Each category's entries are contiguous.
        /// </summary>
        public static IReadOnlyList<SampleEntry> All { get; } =
        [
            new("Basics", "Controls", ControlsDemo.Build, ScrollsItself: true),
            new("Basics", "Styles", StylesDemo.Build, ScrollsItself: true),
            new("Basics", "Navigation", NavigationDemo.Build),
            new("Markup", "Markup", MarkupDemo.Build),
            new("Markup", "Markup Styles", MarkupStylesDemo.Build),
            new("Markup", "Control Templates", ControlTemplateDemo.Build),
            new("Layout", "Split Pane", SplitPaneDemo.Build),
            new("Layout", "Expander", ExpanderDemo.Build),
            new("Layout", "Wrap Grid", WrapGridDemo.Build),
            new("Layout", "Scaling", ScalingDemo.Build),
            new("Items", "Items Control", ItemsControlDemo.Build),
            new("Items", "List Box", ListBoxDemo.Build),
            new("Items", "Selector", SelectorDemo.Build),
            new("Items", "Tab Control", TabControlDemo.Build),
            new("Items", "Tree View", TreeViewDemo.Build),
            new("Dialogs & Pickers", "Dialog", DialogDemo.Build),
            new("Dialogs & Pickers", "Color Picker", ColorPickerDemo.Build),
            new("Design Tools", "Property Grid", PropertyGridDemo.Build),
            new("Design Tools", "Design", DesignDemo.Build),
            new("Design Tools", "Editor", EditorDemo.Build),
            new("Design Tools", "Editor Workspace", EditorWorkspaceDemo.Build, ScrollsItself: true),
        ];
    }
}
