using AquaUI.Input.Events;

namespace AquaUI.Input
{
    /// <summary>
    /// Represents the default event system implementation.
    /// It uses default handlers for all event types.
    /// </summary>
    internal class DefaultInputAggregator : IInputEventSystem, IUpdateable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultInputAggregator"/> class.
        /// </summary>
        /// <param name="inputSystem">An instance of the <see cref="IInputSystem"/> to initialize all internal event listeners.</param>
        public DefaultInputAggregator(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
            Devices = new DeviceEvents(inputSystem);
            var mouse = new MouseEvents(inputSystem);
            Drag = mouse;
            Navigation = new NavigationEvents(inputSystem);
            Scroll = mouse;
            Text = new TextEvents(inputSystem);
            Touch = mouse;
        }

        /// <inheritdoc/>
        public IDeviceEvents Devices { get; }

        /// <inheritdoc/>
        public IDragEvents Drag { get; }

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public INavigationEvents Navigation { get; }

        /// <inheritdoc/>
        public IScrollEvents Scroll { get; }

        /// <inheritdoc/>
        public ITextEvents Text { get; }

        /// <inheritdoc/>
        public ITouchEvents Touch { get; }

        /// <inheritdoc/>
        public void Update(TimeSpan elapsedTime)
        {
            (Drag as IUpdateable)?.Update(elapsedTime);
            (Touch as IUpdateable)?.Update(elapsedTime);
            (Text as IUpdateable)?.Update(elapsedTime);
            (Navigation as IUpdateable)?.Update(elapsedTime);
            (Scroll as IUpdateable)?.Update(elapsedTime);
            (Devices as IUpdateable)?.Update(elapsedTime);
        }
    }
}