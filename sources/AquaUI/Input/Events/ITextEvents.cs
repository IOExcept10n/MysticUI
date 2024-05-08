using AquaUI.Data;

namespace AquaUI.Input.Events
{
    /// <summary>
    /// Represents a listener for the text input events.
    /// </summary>
    public interface ITextEvents : IInputEventProvider
    {
        /// <summary>
        /// Occurs when the user inputs any text in game.
        /// </summary>
        event EventHandler<GenericEventArgs<ITextInputEvent>> TextInput;

        /// <summary>
        /// Enables text input mode.
        /// </summary>
        /// <remarks>
        /// If the game is running on mobile platforms, this method should enable virtual keyboard.
        /// If an IME is available, it should be turned on.
        /// </remarks>
        void EnableTextInput();

        /// <summary>
        /// Disables the text input mode.
        /// </summary>
        /// <remarks>
        /// If the game is running on mobile platforms, this method should disable virtual keyboard.
        /// If an IME is available, it should be hidden.
        /// </remarks>
        void DisableTextInput();
    }
}