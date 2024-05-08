using AquaUI.Data;

namespace AquaUI.Input.Devices
{
    /// <summary>
    /// Represents an interface for the mouse input listener.
    /// </summary>
    public interface IMouseInput
    {
        /// <summary>
        /// Gets the current mouse state.
        /// </summary>
        MouseInfo MouseInfo { get; }

        /// <summary>
        /// Occurs when the mouse button is pressed. Passes the pressed button info.
        /// </summary>
        event EventHandler<GenericEventArgs<MouseButtons>> MouseButtonPressed;

        /// <summary>
        /// Occurs then the mouse button is released. Passes the released button info.
        /// </summary>
        event EventHandler<GenericEventArgs<MouseButtons>> MouseButtonReleased;
    }
}