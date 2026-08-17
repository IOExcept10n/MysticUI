// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data.Markup.Attributes;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A <see cref="Button"/> that toggles <see cref="IsChecked"/> each time it's tapped/activated, and reflects
    /// it via <see cref="Styles.ControlState.Checked"/> for styling.
    /// </summary>
    public class ToggleButton : Button
    {
        private bool isChecked;

        /// <summary>
        /// Occurs when <see cref="IsChecked"/> changes.
        /// </summary>
        public event EventHandler? IsCheckedChanged;

        /// <summary>
        /// Gets or sets a value indicating whether this element is checked/on.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(false)]
        [RegisterReference]
        public bool IsChecked
        {
            get => isChecked;
            set
            {
                if (!SetProperty(ref isChecked, value))
                    return;
                ControlState = value ? ControlState | ControlState.Checked : ControlState & ~ControlState.Checked;
                IsCheckedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        protected override void OnClick()
        {
            IsChecked = !IsChecked;
            base.OnClick();
        }
    }
}
