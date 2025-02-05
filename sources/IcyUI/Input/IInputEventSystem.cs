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
    }
}
