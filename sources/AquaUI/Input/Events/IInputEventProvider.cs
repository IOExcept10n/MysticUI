namespace AquaUI.Input.Events
{
    /// <summary>
    /// Represents a basic interface for all input events listeners.
    /// </summary>
    public interface IInputEventProvider
    {
        /// <summary>
        /// Provides the input system instance for the current input event provider.
        /// </summary>
        public IInputSystem InputSystem { get; }
    }
}