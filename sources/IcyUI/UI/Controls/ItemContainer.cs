// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// The minimal container <c>ItemsControl</c> wraps each realized data item's built visual tree in.
    /// </summary>
    /// <remarks>
    /// A bare <see cref="ContentControl"/> with no selection state - exists as its own type only so
    /// <c>ItemsControl</c>'s pooling (keyed per <c>DataTemplate</c>) and theming
    /// (<c>&lt;Style TargetType="ItemContainer"&gt;</c>) have something distinct from a generic
    /// <see cref="ContentControl"/> to target. A future <c>Selector</c>/<c>ListBox</c> introduces a real
    /// <c>ListBoxItem</c> with selection state on top of this mechanism, rather than adding selection here - see
    /// the Phase 2 design spec's container-tiering decision.
    /// </remarks>
    public class ItemContainer : ContentControl
    {
    }
}
