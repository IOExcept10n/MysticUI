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
    /// <see cref="SelectingItemsControl"/> completely unchanged. Click-to-select is also inherited unchanged,
    /// via <see cref="SelectorItem.Tapped"/> (see <see cref="SelectingItemsControl.AttachContainer(ItemContainer, int)"/>/
    /// <see cref="SelectingItemsControl.DetachContainer(ItemContainer)"/>) - unlike <see cref="Selector"/>'s popup
    /// items, this control's realized items live in the normal visual tree, so <see cref="Canvas.OnTap"/>'s
    /// existing per-element dispatch reaches them directly.
    /// </remarks>
    public class ListBox : SelectingItemsControl
    {
    }
}
