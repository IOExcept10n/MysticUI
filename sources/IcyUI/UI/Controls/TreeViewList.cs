// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// The virtualizing list inside a <see cref="TreeView"/>: shows the tree's visible rows (<see cref="FlatRow"/>s) as
    /// <see cref="TreeViewItem"/>s and hands taps back to the tree.
    /// </summary>
    /// <remarks>
    /// Its items are rows, but each row's content is built from and bound to the row's <em>data item</em>, so templates
    /// and <c>{Binding}</c> see the same object they would in a <see cref="ListBox"/>.
    /// </remarks>
    internal sealed class TreeViewList : SelectingItemsControl
    {
        private readonly TreeView owner;
        private readonly Dictionary<TreeViewItem, FlatRow> rowsByContainer = [];
        private int currentIndex = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeViewList"/> class.
        /// </summary>
        /// <param name="owner">The tree this list belongs to.</param>
        public TreeViewList(TreeView owner)
        {
            this.owner = owner;
            ItemTemplateSelector = row => owner.ResolveTemplate(((FlatRow)row).Item);
        }

        /// <summary>Gets the realized containers by row index.</summary>
        internal IReadOnlyDictionary<int, ItemContainer> Realized => RealizedContainers;

        /// <summary>
        /// Gets or sets the index of the keyboard-current row, shown through <see cref="SelectorItem.IsHighlighted"/>;
        /// <c>-1</c> for none.
        /// </summary>
        internal int CurrentIndex
        {
            get => currentIndex;
            set
            {
                if (currentIndex == value)
                    return;

                if (RealizedContainers.TryGetValue(currentIndex, out ItemContainer? old))
                    ((SelectorItem)old).IsHighlighted = false;
                currentIndex = value;
                if (RealizedContainers.TryGetValue(currentIndex, out ItemContainer? current))
                    ((SelectorItem)current).IsHighlighted = true;
            }
        }

        /// <summary>Re-stamps the realized container of <paramref name="row"/>, if there is one.</summary>
        /// <param name="row">The row whose state changed.</param>
        internal void Restamp(FlatRow row)
        {
            foreach ((TreeViewItem container, FlatRow shown) in rowsByContainer)
            {
                if (ReferenceEquals(shown, row))
                    owner.Stamp(container, shown);
            }
        }

        /// <summary>Re-stamps every realized container.</summary>
        internal void RestampAll()
        {
            foreach ((TreeViewItem container, FlatRow shown) in rowsByContainer)
                owner.Stamp(container, shown);
        }

        /// <inheritdoc/>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
            => new TreeViewItem { Content = template.Build(((FlatRow)item).Item) };

        /// <inheritdoc/>
        /// <remarks>
        /// Re-stamps everything row-dependent: a pooled container arrives with its content bound to the
        /// <see cref="FlatRow"/> (the base only rebinds <see cref="UIElement.DataContext"/> to the list item) and with the
        /// previous row's depth, chevron and highlight.
        /// </remarks>
        protected override void AttachContainer(ItemContainer container, int index)
        {
            base.AttachContainer(container, index);
            var item = (TreeViewItem)container;
            var row = (FlatRow)GetItemAt(index);
            if (item.Content != null)
                item.Content.DataContext = row.Item;
            rowsByContainer[item] = row;
            item.ExpanderTapped += Item_ExpanderTapped;
            item.IsHighlighted = index == currentIndex;
            owner.Stamp(item, row);
        }

        /// <inheritdoc/>
        protected override void DetachContainer(ItemContainer container)
        {
            var item = (TreeViewItem)container;
            item.ExpanderTapped -= Item_ExpanderTapped;
            rowsByContainer.Remove(item);
            base.DetachContainer(container);
        }

        /// <inheritdoc/>
        protected override void OnContainerTapped(int index) => owner.OnRowTapped((FlatRow)GetItemAt(index));

        /// <inheritdoc/>
        /// <remarks>The tree owns selection by data item and maps it back to a row index after every change.</remarks>
        protected override void OnItemsChanged() => owner.OnRowsChanged();

        private void Item_ExpanderTapped(object? sender, EventArgs e)
        {
            if (sender is TreeViewItem item && rowsByContainer.TryGetValue(item, out FlatRow? row))
                owner.OnExpanderTapped(row);
        }
    }
}
