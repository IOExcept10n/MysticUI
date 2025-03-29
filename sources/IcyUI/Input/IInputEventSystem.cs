using System.Windows.Input;
using Icy.Input.Devices;
using Icy.Input.Events;

namespace Icy.Input
{
    /// <summary>
    /// Represents the core class for the input event sources.
    /// </summary>
    public interface IInputEventSystem : IInputEventProvider
    {
        /// <summary>
        /// Gets the listener for the drag and drop events.
        /// </summary>
        IDragEvents Drag { get; }

        /// <summary>
        /// Gets the listener for the touch events.
        /// </summary>
        ITouchEvents Touch { get; }

        /// <summary>
        /// Gets the listener for the text input events.
        /// </summary>
        ITextEvents Text { get; }

        /// <summary>
        /// Gets the listener for the navigation events.
        /// </summary>
        INavigationEvents Navigation { get; }

        /// <summary>
        /// Gets the listener for the scroll events.
        /// </summary>
        IScrollEvents Scroll { get; }

        /// <summary>
        /// Gets the listener for the input devices' related events.
        /// </summary>
        IDeviceEvents Devices { get; }

        /// <summary>
        /// Registers command to invoke when specified keyboard keys combination is pressed.
        /// </summary>
        /// <param name="command">Command instance to register.</param>
        /// <param name="gesture">Keys combination to trigger the command.</param>
        /// <param name="argument">Argument to pass to the command when event is raised.</param>
        void RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null);

        /// <summary>
        /// Unregisters command bent to the specified keyboard combination.
        /// </summary>
        /// <param name="gesture">Keyboard combination to remove command for.</param>
        void UnregisterCommand(KeyGesture gesture);
    }
}
