// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Markup.Attributes;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// The container a <see cref="Selector"/> realizes each item as - an <see cref="ItemContainer"/> with
    /// selection/highlight state layered on top.
    /// </summary>
    /// <remarks>
    /// <see cref="IsSelected"/>/<see cref="IsHighlighted"/> mirror <see cref="ToggleButton.IsChecked"/>'s own
    /// shape (set/clear a <see cref="ControlState"/> flag, no other side effects here) - <see cref="Selector"/>
    /// itself is what keeps them in sync with <see cref="SelectingItemsControl.SelectedIndex"/>/its internal highlighted
    /// index, including across a pool-and-reuse cycle (see <see cref="Selector.AttachContainer(ItemContainer, int)"/>).
    /// </remarks>
    public class SelectorItem : ItemContainer
    {
        private bool isHighlighted;
        private bool isSelected;

        /// <summary>
        /// Gets or sets a value indicating whether this item is the <see cref="Selector"/>'s current
        /// <see cref="SelectingItemsControl.SelectedItem"/>. Setting this sets/clears <see cref="ControlState.Selected"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (!SetProperty(ref isSelected, value))
                    return;
                ControlState = value ? ControlState | ControlState.Selected : ControlState & ~ControlState.Selected;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this item is the <see cref="Selector"/> popup's current
        /// keyboard/gamepad-highlighted item. Setting this sets/clears <see cref="ControlState.Highlighted"/>.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool IsHighlighted
        {
            get => isHighlighted;
            set
            {
                if (!SetProperty(ref isHighlighted, value))
                    return;
                ControlState = value ? ControlState | ControlState.Highlighted : ControlState & ~ControlState.Highlighted;
            }
        }

        /// <summary>
        /// Occurs when this item is tapped - raised from <see cref="OnTap"/>. Used by <see cref="WrapGrid"/>/
        /// <see cref="ListBox"/> for click-to-select, since their realized items live in the normal visual tree
        /// (unlike <see cref="Selector"/>'s popup items, which use their own separate mechanism - see
        /// <see cref="Selector"/>'s own remarks).
        /// </summary>
        public event EventHandler? Tapped;

        /// <inheritdoc/>
        /// <remarks>
        /// Skips raising <see cref="Tapped"/> while <see cref="UIElement.IsEnabled"/> is <see langword="false"/>.
        /// Normally redundant with <see cref="UIElement.IsEnabled"/>'s own effect on
        /// <see cref="UIElement.IsHitTestVisible"/> (a disabled item is never <see cref="Icy.UI.Canvas.HitTest(System.Drawing.Point)"/>'d
        /// in the first place, so <see cref="Icy.UI.Canvas.OnTap"/>'s normal dispatch never reaches this method
        /// for one) - guarded here too as well, defensively, so a caller invoking <see cref="OnTap"/> directly
        /// (bypassing hit-testing entirely) still can't click-to-select a disabled item.
        /// </remarks>
        protected internal override void OnTap()
        {
            base.OnTap();
            if (IsEnabled)
                Tapped?.Invoke(this, EventArgs.Empty);
        }
    }
}
