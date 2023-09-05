using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a control similar to button, but its <see cref="ButtonBase.IsPressed"/> property value represents a state that toggles with every click.
    /// </summary>
    public class ToggleButton : ButtonBase
    {
        private bool? isChecked = false;

        /// <summary>
        /// Gets or sets a value indicating whether the <see cref="ToggleButton"/> is in checked state.
        /// </summary>
        /// <value>
        /// <see langword="true"/> if the <see cref="ToggleButton"/> is in checked state, <see langword="false"/> if in the disabled state, else <see langword="null"/>.
        /// </value>
        [Category("Behavior")]
        [DefaultValue(false)]
        public virtual bool? IsChecked
        {
            get => isChecked;
            set
            {
                if (value == isChecked)
                    return;
                isChecked = value;
                OnCheckedChanged();
                NotifyPropertyChanged(nameof(IsChecked));
            }
        }

        /// <summary>
        /// Occurs when the value of the <see cref="IsChecked"/> property changes.
        /// </summary>
        public event EventHandler? CheckedChanged;

        /// <inheritdoc/>
        protected internal override sealed void ToggleClick(bool isClicked)
        {
            return;
        }

        /// <summary>
        /// Raises the <see cref="CheckedChanged"/> event.
        /// </summary>
        protected internal virtual void OnCheckedChanged()
        {
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        protected internal override void OnClick()
        {
            base.OnClick();
            IsChecked = IsChecked == false;
        }
    }
}