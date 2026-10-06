// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// The chevron of a <see cref="TreeViewItem"/>: an <see cref="Icon"/> that reports its own taps, so a tap on it opens
    /// or closes the row without selecting it.
    /// </summary>
    /// <remarks>
    /// Taps bubble to every ancestor with no "handled" flag (see <see cref="UIElement.OnTap"/>). This part raises
    /// <see cref="Tapped"/> first, and its <see cref="TreeViewItem"/> then skips its own tap handling for that tap. Name
    /// it <see cref="TreeViewItem.ExpanderPartName"/> in a <see cref="TreeViewItem"/> template.
    /// </remarks>
    public class TreeViewExpander : Icon
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TreeViewExpander"/> class, showing <see cref="IconKind.ChevronRight"/>.
        /// </summary>
        public TreeViewExpander()
        {
            Kind = IconKind.ChevronRight;
        }

        /// <summary>
        /// Occurs when the chevron is tapped while enabled.
        /// </summary>
        public event EventHandler? Tapped;

        /// <inheritdoc/>
        protected internal override void OnTap()
        {
            base.OnTap();
            if (IsEnabled)
                Tapped?.Invoke(this, EventArgs.Empty);
        }
    }
}
