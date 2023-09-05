using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a control similar to <seealso cref="Button"/>, but
    /// the <see cref="ButtonBase.Click"/> event occurs every frame while the button is pressed.
    /// </summary>
    public class RepeatButton : ButtonBase
    {
        /// <summary>
        /// Determines whether to perform a click at any time when the mouse hovers over the button.
        /// </summary>
        [Category("Behavior")]
        public bool ClickOnHover { get; set; }

        /// <inheritdoc/>
        protected internal override void OnMouseMove()
        {
            base.OnMouseMove();
            if (IsPressed || ClickOnHover) PerformClick();
        }
    }
}