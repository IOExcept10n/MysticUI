using MysticUI.Extensions;
using Stride.Input;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents basic functionality for al button-like controls.
    /// </summary>
    public class ButtonBase : ContentControl
    {
        internal bool releaseOnTouchLeft;
        private bool isPressed;
        private bool isClicked;

        /// <summary>
        /// Determines whether the button accepts clicking by Escape shortcut.
        /// </summary>
        [Category("Behavior")]
        public bool IsCancel { get; set; }

        /// <summary>
        /// Determines whether the button is pressed during the frame.
        /// </summary>
        [XmlIgnore, JsonIgnore, Browsable(false)]
        public bool IsPressed
        {
            get => isPressed;
            set
            {
                if (value == isPressed)
                    return;
                isPressed = value;
                OnPressedChanged();
                NotifyPropertyChanged(nameof(IsPressed));
            }
        }

        /// <summary>
        /// Handled when button is clicked.
        /// </summary>
        public event EventHandler<GenericEventArgs<MouseInfo>>? Click;

        /// <summary>
        /// Occurs when the value of <see cref="IsPressed"/> property changes.
        /// </summary>
        public event EventHandler? PressedChanged;

        /// <summary>
        /// Performs a click without actually clicking a button.
        /// </summary>
        public void PerformClick()
        {
            OnTouchDown();
            OnTouchUp();
        }

        /// <inheritdoc/>
        protected internal override void OnTouchLeft()
        {
            base.OnTouchLeft();

            if (releaseOnTouchLeft)
            {
                ToggleClick(false);
            }
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchUp()
        {
            base.OnTouchUp();

            if (!Enabled) return false;

            ToggleClick(false);

            if (isClicked)
            {
                OnClick();
                isClicked = false;
            }
            return true;
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchDown()
        {
            base.OnTouchDown();

            if (!Enabled) return false;

            ToggleClick(true);
            isClicked = true;

            return true;
        }

        /// <inheritdoc/>
        protected internal override void OnKeyDown(Keys key)
        {
            base.OnKeyDown(key);
            if (!Enabled) return;
            if (IsCancel && key == Keys.Escape || HasFocus && key == Keys.Enter)
            {
                PerformClick();
            }
        }

        /// <inheritdoc/>
        protected internal override void OnActiveChanged()
        {
            base.OnActiveChanged();

            if (!IsActive && IsPressed)
            {
                IsPressed = false;
            }
        }

        /// <summary>
        /// Tries to set new click value to the button if it supports this action.
        /// </summary>
        /// <param name="isClicked">A new value to set.</param>
        protected internal virtual void ToggleClick(bool isClicked)
        {
            if (isClicked != IsPressed && Enabled)
            {
                IsPressed = isClicked;
            }
        }

        /// <summary>
        /// Raises <see cref="Click"/> event.
        /// </summary>
        protected internal virtual void OnClick()
        {
            Click?.Invoke(this, Desktop?.InputManager?.Mouse ?? default);
        }

        /// <summary>
        /// Occurs when the <see cref="IsPressed"/> property changes.
        /// </summary>
        protected internal virtual void OnPressedChanged()
        {
            PressedChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}