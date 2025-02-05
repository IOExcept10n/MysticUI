// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a listener for the text input events.
    /// </summary>
    public interface ITextEvents : IInputEventProvider, IStartRepeatEvents
    {
        /// <summary>
        /// Occurs when the user inputs any text in game.
        /// </summary>
        event EventHandler<GenericEventArgs<ITextInputEventInfo>>? TextInput;

        /// <summary>
        /// Occurs when user presses the <see langword="Copy"/> key combination.
        /// </summary>
        event EventHandler? CopyText;

        /// <summary>
        /// Occurs when user presses the <see langword="Cut"/> key combination.
        /// </summary>
        event EventHandler? CutText;

        /// <summary>
        /// Occurs when user presses the <see langword="Paste"/> key combination.
        /// </summary>
        event EventHandler? PasteText;

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
