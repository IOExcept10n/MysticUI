// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup.Attributes;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Adds single-item selection on top of <see cref="ItemsControl"/> - the shared base for
    /// <see cref="Selector"/> (adds a popup on top of this) and <see cref="WrapGrid"/>/<see cref="ListBox"/>
    /// (always-visible, no popup).
    /// </summary>
    /// <remarks>
    /// Realizes <see cref="SelectorItem"/>s (see <see cref="CreateContainer(DataTemplate, object)"/>) instead
    /// of bare <see cref="ItemContainer"/>s, and keeps each realized item's <see cref="SelectorItem.IsSelected"/>
    /// in sync with <see cref="SelectedIndex"/> - including across a pool-and-reuse cycle, since a selected
    /// item's container can be de-realized and rebuilt later by virtualization.
    /// </remarks>
    public abstract class SelectingItemsControl : ItemsControl
    {
        /// <summary>
        /// Reverse lookup from a realized <see cref="SelectorItem"/> back to the item index it's currently
        /// showing, so <see cref="Container_Tapped"/> can turn a tap on the container into a
        /// <see cref="SelectedIndex"/> assignment. Kept in sync by <see cref="AttachContainer(ItemContainer, int)"/>/
        /// <see cref="DetachContainer(ItemContainer)"/>. <c>protected</c> rather than <see langword="private"/> so
        /// <see cref="Selector"/> - whose own <c>AttachContainer</c>/<c>DetachContainer</c> override never calls
        /// this base implementation - can reuse the same map for its own (differently-triggered) tap handling
        /// instead of keeping a second, identically-shaped duplicate.
        /// </summary>
        protected readonly Dictionary<SelectorItem, int> containerIndices = [];

        private int selectedIndex = -1;
        private object? selectedItem;

        /// <summary>
        /// Occurs when <see cref="SelectedIndex"/>/<see cref="SelectedItem"/> changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Gets or sets the currently selected item's index, or <c>-1</c> for no selection.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than <c>-1</c> or greater than or equal to the item count.</exception>
        [Category("Behavior")]
        [DefaultValue(-1)]
        [RegisterReference]
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                Guard.IsGreaterThanOrEqualTo(value, -1);
                if (value != -1)
                    Guard.IsLessThan(value, ItemCount);
                if (selectedIndex == value)
                    return;

                if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? oldContainer))
                    ((SelectorItem)oldContainer).IsSelected = false;

                selectedIndex = value;
                selectedItem = selectedIndex == -1 ? null : GetItemAt(selectedIndex);

                OnSelectionChanged();

                if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? newContainer))
                    ((SelectorItem)newContainer).IsSelected = true;

                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the currently selected item, or <see langword="null"/> for no selection. Setting an item
        /// not present in <see cref="ItemsControl.ItemsSource"/> clears selection instead of throwing.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? SelectedItem
        {
            get => selectedItem;
            set => SelectedIndex = IndexOfItem(value);
        }

        /// <inheritdoc/>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
            => new SelectorItem { Content = template.Build(item) };

        /// <summary>
        /// Stamps a freshly realized <paramref name="container"/>'s <see cref="SelectorItem.IsSelected"/> from
        /// <see cref="SelectedIndex"/>, on top of the base <see cref="ItemsControl"/> wiring - the case the
        /// <see cref="SelectedIndex"/> setter's own stamp (see its own <c>realizedContainers</c> lookup) can't
        /// reach: <paramref name="index"/> selected before it's ever realized, or re-realized later after a
        /// pool-and-reuse cycle. <see cref="Selector"/> overrides this again itself, to route into its popup
        /// host instead of this base's default parenting.
        /// </summary>
        /// <remarks>
        /// Also wires up click-to-select: subscribes <see cref="SelectorItem.Tapped"/> (handled by
        /// <see cref="Container_Tapped"/>) and records <paramref name="index"/> in <see cref="containerIndices"/>
        /// so the handler can look it back up. This is inert for <see cref="Selector"/> and its subclasses
        /// (<see cref="Dropdown"/>, <see cref="ComboBox"/>), since their own <c>AttachContainer</c> override
        /// doesn't call this base implementation.
        /// </remarks>
        /// <param name="container">The freshly realized container.</param>
        /// <param name="index">The item index <paramref name="container"/> was realized for.</param>
        protected override void AttachContainer(ItemContainer container, int index)
        {
            base.AttachContainer(container, index);
            var item = (SelectorItem)container;
            item.IsSelected = index == SelectedIndex;
            item.Tapped += Container_Tapped;
            containerIndices[item] = index;
        }

        /// <summary>
        /// Exact inverse of <see cref="AttachContainer(ItemContainer, int)"/>'s click-to-select wiring -
        /// unsubscribes <see cref="SelectorItem.Tapped"/> and forgets <paramref name="container"/>'s
        /// <see cref="containerIndices"/> entry before deferring to the base detach.
        /// </summary>
        /// <param name="container">The container being de-realized.</param>
        protected override void DetachContainer(ItemContainer container)
        {
            var item = (SelectorItem)container;
            item.Tapped -= Container_Tapped;
            containerIndices.Remove(item);
            base.DetachContainer(container);
        }

        /// <summary>
        /// Called after <see cref="SelectedIndex"/>/<see cref="SelectedItem"/> have been updated, before the
        /// realized-container stamp and the public <see cref="SelectionChanged"/> event - empty by default. A
        /// subclass with derived state of its own that depends on the new selection (e.g. <see cref="Selector"/>'s
        /// <see cref="Selector.SelectedValue"/>) overrides this to refresh it, rather than reacting to its own
        /// <see cref="SelectionChanged"/> subscription (which would depend on subscriber-ordering relative to any
        /// external subscriber this control's own consumer adds).
        /// </summary>
        protected virtual void OnSelectionChanged()
        {
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Clears <see cref="SelectedIndex"/> (and <see cref="SelectedItem"/> with it) when the item it pointed to
        /// no longer exists because the live <see cref="ItemsControl.ItemsSource"/> shrank out from under it - the
        /// setter's own <see cref="ArgumentOutOfRangeException"/> guard only catches a caller assigning an
        /// out-of-range value directly, not a bound collection later removing the selected item.
        /// </remarks>
        protected override void OnItemsChanged()
        {
            if (SelectedIndex >= ItemCount)
                SelectedIndex = -1;
        }

        /// <summary>
        /// Called when a realized container is tapped. Selects <paramref name="index"/> by default.
        /// </summary>
        /// <param name="index">The tapped container's item index.</param>
        /// <remarks>
        /// A subclass whose items aren't all selectable (e.g. a tree whose category rows only expand) overrides this to
        /// decide what a tap does.
        /// </remarks>
        protected virtual void OnContainerTapped(int index) => SelectedIndex = index;

        /// <summary>
        /// Handles a realized <see cref="SelectorItem"/>'s <see cref="SelectorItem.Tapped"/> event by passing the item's
        /// current index, looked up via <see cref="containerIndices"/>, to <see cref="OnContainerTapped(int)"/>.
        /// </summary>
        /// <param name="sender">The tapped <see cref="SelectorItem"/>.</param>
        /// <param name="e">Unused.</param>
        private void Container_Tapped(object? sender, EventArgs e)
        {
            if (sender is SelectorItem item && containerIndices.TryGetValue(item, out int index))
                OnContainerTapped(index);
        }
    }
}
