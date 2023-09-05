namespace MysticUI.Controls
{
    /// <summary>
    /// An interface for focusable objects.
    /// </summary>
    public interface INotifyFocusChanged
    {
        /// <summary>
        /// Determines whether the element has keyboard focus on itself.
        /// </summary>
        public bool HasFocus { get; }

        /// <summary>
        /// Occurs when the value of the <see cref="HasFocus"/> property changed.
        /// </summary>
        public event EventHandler? FocusChanged;
    }
}