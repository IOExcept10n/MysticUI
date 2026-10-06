using System.Windows.Input;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Input.Gestures;

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
        /// Gets the gestures recognized from touch contacts and mouse buttons. They update before every other event provider,
        /// and the touch and drag providers are built on top of them.
        /// </summary>
        IGestureEvents Gestures { get; }

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
        /// Registers a command to invoke when the specified key combination is pressed.
        /// </summary>
        /// <param name="command">Command instance to register.</param>
        /// <param name="gesture">Keys combination to trigger the command.</param>
        /// <param name="argument">Argument to pass to the command when the gesture is pressed.</param>
        /// <param name="handlesGesture">
        /// <see langword="true"/> to let this command take the gesture over: commands registered this way are tried before
        /// the others, the most recently registered first, and the first whose <see cref="ICommand.CanExecute(object?)"/>
        /// returns <see langword="true"/> runs alone. When none of them can execute, every other command for the gesture
        /// runs as usual. <see langword="false"/> (the default) runs the command together with the gesture's other ordinary
        /// commands.
        /// </param>
        void RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null, bool handlesGesture = false);

        /// <summary>
        /// Unregisters every command bound to the specified key combination.
        /// </summary>
        /// <param name="gesture">Keyboard combination to remove the commands for.</param>
        void UnregisterCommand(KeyGesture gesture);

        /// <summary>
        /// Unregisters one command from the specified key combination, leaving the gesture's other commands in place.
        /// </summary>
        /// <param name="command">The command to remove.</param>
        /// <param name="gesture">The key combination it was registered for.</param>
        void UnregisterCommand(ICommand command, KeyGesture gesture);
    }
}
