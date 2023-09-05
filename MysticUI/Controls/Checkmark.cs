using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents an image-like control that can show images according to its <see cref="IsChecked"/> state.
    /// </summary>
    public sealed class Checkmark : Image
    {
        private IImage? selectedForeground;
        private IImage? intermediateForeground;
        private bool? isChecked = false;

        /// <summary>
        /// Foreground image for the Checked state.
        /// </summary>
        [Category("Appearance")]
        public IImage? SelectedForeground
        {
            get => selectedForeground;
            set
            {
                if (value == selectedForeground)
                    return;
                selectedForeground = value;
                OnSelectedForegroundChanged();
                NotifyPropertyChanged(nameof(SelectedForeground));
            }
        }

        /// <summary>
        /// Foreground image for the Intermediate state.
        /// </summary>
        [Category("Appearance")]
        public IImage? IntermediateForeground
        {
            get => intermediateForeground;
            set
            {
                if (value == intermediateForeground)
                    return;
                intermediateForeground = value;
                OnIntermediateForegroundChanged();
                NotifyPropertyChanged(nameof(IntermediateForeground));
            }
        }

        /// <summary>
        /// Determines whether the <see cref="Checkmark"/> should present Checked state.
        /// </summary>
        [Category("Behavior")]
        public bool? IsChecked
        {
            get => isChecked;
            set
            {
                if (value == isChecked) return;
                isChecked = value;
                OnCheckedChanged();
                NotifyPropertyChanged(nameof(IsChecked));
            }
        }

        /// <summary>
        /// Occurs when the value of the <see cref="SelectedForeground"/> property changes.
        /// </summary>
        public event EventHandler? SelectedForegroundChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="IntermediateForeground"/> property changes.
        /// </summary>
        public event EventHandler? IntermediateForegroundChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="IsChecked"/> property changes.
        /// </summary>
        public event EventHandler? CheckedChanged;

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            var defaultForeground = foreground;
            if (defaultForeground == null) return;
            if (IsChecked == true)
            {
                foreground = selectedForeground;
            }
            else if (IsChecked == null)
            {
                foreground = intermediateForeground;
            }
            foreground ??= defaultForeground;
            base.RenderInternal(context);
            foreground = defaultForeground;
        }

        /// <summary>
        /// Raises the <see cref="SelectedForegroundChanged"/> event.
        /// </summary>
        internal void OnSelectedForegroundChanged()
        {
            SelectedForegroundChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the <see cref="IntermediateForegroundChanged"/> event.
        /// </summary>
        internal void OnIntermediateForegroundChanged()
        {
            IntermediateForegroundChanged?.Invoke(this, EventArgs.Empty);
        }

        internal void OnCheckedChanged()
        {
            CheckedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}