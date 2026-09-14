// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Markup.Attributes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// One tab within a <see cref="TabControl"/> - <see cref="ContentControl.Content"/> is the tab's own body,
    /// shown only while this item is the <see cref="TabControl"/>'s <see cref="SelectingItemsControl.SelectedItem"/>;
    /// <see cref="Header"/>/<see cref="Icon"/> describe how this tab's clickable header row looks.
    /// </summary>
    /// <remarks>
    /// A plain data item, not itself realized directly - <see cref="TabControl"/> builds each header from
    /// <see cref="Header"/>/<see cref="Icon"/> via its own default <c>ItemTemplate</c>, wrapped in a
    /// <see cref="SelectorItem"/>. Disabling a tab is just <see cref="UIElement.IsEnabled"/> - no
    /// <see cref="TabItem"/>-specific plumbing needed.
    /// </remarks>
    public class TabItem : ContentControl
    {
        private object? header;
        private Icon? icon;

        /// <summary>
        /// Gets or sets the content shown in this tab's clickable header row - typically plain text, but any
        /// object <see cref="TabControl"/>'s header template can display.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? Header
        {
            get => header;
            set => SetProperty(ref header, value);
        }

        /// <summary>
        /// Gets or sets a small glyph shown alongside <see cref="Header"/> in this tab's header row, or
        /// <see langword="null"/> for none.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public Icon? Icon
        {
            get => icon;
            set => SetProperty(ref icon, value);
        }
    }
}
