namespace Icy.UI
{
    /// <summary>
    /// Represents an interface for the items that can be focused.
    /// </summary>
    public interface INotifyFocusChanged
    {
        /// <summary>
        /// Occurs when the value of the <see cref="IsFocused"/> property changed.
        /// </summary>
        event EventHandler? FocusChanged;

        /// <summary>
        /// Gets a value indicating whether the element is focused.
        /// </summary>
        bool IsFocused { get; }
    }
}
