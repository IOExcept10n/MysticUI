using AquaUI.Data;

namespace AquaUI.Input.Devices
{
    /// <summary>
    /// Represents an interface for the keyboard input listener.
    /// </summary>
    public interface IKeyboardInput : IInputDeviceListener
    {
        /// <summary>
        /// Gets set of keys that are down during this frame.
        /// </summary>
        IEnumerable<Keys> KeysDown { get; }

        /// <summary>
        /// Occurs when the keyboard key is down. Passes a value of the pressed key.
        /// </summary>
        event EventHandler<GenericEventArgs<Keys>> KeyDown;

        /// <summary>
        /// Occurs when the keyboard key is up. Passes a value of the released key.
        /// </summary>
        event EventHandler<GenericEventArgs<Keys>> KeyUp;
    }
}