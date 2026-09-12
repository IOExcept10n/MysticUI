// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// A plain, always-visible, virtualizing, single-selectable list - the most direct realization of
    /// "an <see cref="ItemsControl"/> with click-to-select."
    /// </summary>
    /// <remarks>
    /// Overrides no virtualization geometry - a plain vertical list of selectable items is exactly
    /// <see cref="ItemsControl"/>'s own default single-column variable-height virtualization, inherited via
    /// <see cref="SelectingItemsControl"/> completely unchanged. Only adds click-to-select, via
    /// <see cref="SelectorItem.Tapped"/> (see <see cref="AttachContainer(ItemContainer, int)"/>/
    /// <see cref="DetachContainer(ItemContainer)"/>) - unlike <see cref="Selector"/>'s popup items, this
    /// control's realized items live in the normal visual tree, so <see cref="Canvas.OnTap"/>'s existing
    /// per-element dispatch reaches them directly.
    /// </remarks>
    public class ListBox : SelectingItemsControl
    {
        private readonly Dictionary<SelectorItem, int> containerIndices = [];

        /// <inheritdoc/>
        protected override void AttachContainer(ItemContainer container, int index)
        {
            base.AttachContainer(container, index);
            var item = (SelectorItem)container;
            item.IsSelected = index == SelectedIndex;
            item.Tapped += Container_Tapped;
            containerIndices[item] = index;
        }

        /// <inheritdoc/>
        protected override void DetachContainer(ItemContainer container)
        {
            var item = (SelectorItem)container;
            item.Tapped -= Container_Tapped;
            containerIndices.Remove(item);
            base.DetachContainer(container);
        }

        private void Container_Tapped(object? sender, EventArgs e)
        {
            if (sender is SelectorItem item && containerIndices.TryGetValue(item, out int index))
                SelectedIndex = index;
        }
    }
}
