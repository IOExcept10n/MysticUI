// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.UI;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// An element a <see cref="ToolboxPanel"/> can insert.
    /// </summary>
    /// <param name="DisplayName">The name the toolbox shows.</param>
    /// <param name="Category">The group the toolbox lists it under.</param>
    /// <param name="Snippet">The markup inserted, a single element such as <c>&lt;Button&gt;Button&lt;/Button&gt;</c>.</param>
    public sealed record ToolboxItem(string DisplayName, string Category, string Snippet)
    {
        /// <summary>
        /// Types that are public and constructible but don't belong in a toolbox: bare base types, parts the controls create
        /// for themselves, and roots that host whole pages.
        /// </summary>
        private static readonly HashSet<string> NotInsertable = new(StringComparer.Ordinal)
        {
            "UIElement",
            "Control",
            "Panel",
            "ContentPresenter",
            "ExpanderHeader",
            "TreeViewExpander",
            "Window",
            "ItemContainer",
            "TreeViewItem",
            "SelectorItem",
            "TabItem",
            "ListBoxItem",
            "ComboBoxItem",
            "Page",
            "Frame",
            "Dialog",
        };

        /// <summary>
        /// Lists every built-in element a page can contain: each public, non-abstract <see cref="UIElement"/> with a
        /// parameterless constructor in one of the configuration's built-in markup namespaces.
        /// </summary>
        /// <param name="configuration">The configuration whose <see cref="Icy.Markup.MarkupConfiguration.BuiltInNamespaces"/> to use.</param>
        /// <returns>
        /// The items, sorted by category and name. Most snippets are a bare element (<c>&lt;Slider/&gt;</c>); buttons,
        /// text and borders get a minimal content or size so they're visible once inserted.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
        public static IReadOnlyList<ToolboxItem> CreateDefaults(IcyConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            var namespaces = new HashSet<string>(configuration.Types.Markup.BuiltInNamespaces, StringComparer.Ordinal);
            return [.. typeof(UIElement).Assembly.GetExportedTypes()
                .Where(t => t.Namespace != null && namespaces.Contains(t.Namespace)
                    && typeof(UIElement).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition
                    && t.GetConstructor(Type.EmptyTypes) != null && !NotInsertable.Contains(t.Name))
                .Select(t => new ToolboxItem(t.Name, CategoryOf(t), SnippetFor(t)))
                .OrderBy(x => x.Category, StringComparer.Ordinal).ThenBy(x => x.DisplayName, StringComparer.Ordinal)];
        }

        private static string CategoryOf(Type type) =>
            typeof(Icy.UI.Controls.ItemsControl).IsAssignableFrom(type) || typeof(Panel).IsAssignableFrom(type) || type.Name is "SplitPane" or "ScrollViewer"
                ? "Layout & Items"
                : "Controls";

        private static string SnippetFor(Type type) => type.Name switch
        {
            "Button" or "ToggleButton" or "CheckBox" or "RadioButton" => $"<{type.Name}>{type.Name}</{type.Name}>",
            "TextBlock" => "<TextBlock Text=\"Text\"/>",
            "TextBox" => "<TextBox Width=\"120\"/>",
            "Border" => "<Border Width=\"100\" Height=\"60\"/>",
            _ => $"<{type.Name}/>",
        };
    }
}
