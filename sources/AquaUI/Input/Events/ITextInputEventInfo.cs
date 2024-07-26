// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Input.Events
{
    /// <summary>
    /// Defines a type of the text input event.
    /// </summary>
    public enum TextInputEventType
    {
        /// <summary>
        /// The text has been entered.
        /// </summary>
        Input = 0,

        /// <summary>
        /// The text has not yet been entered but is still being edited.
        /// </summary>
        Composition = 1,
    }

    /// <summary>
    /// Represents an interface for to handle events used for text input.
    /// </summary>
    public interface ITextInputEventInfo
    {
        /// <summary>
        /// Gets the part of the composition being edited.
        /// </summary>
        Range CompositionRange { get; }

        /// <summary>
        /// Gets the text that is entered.
        /// </summary>
        string Text { get; }

        /// <summary>
        /// Gets the type of the text input event.
        /// </summary>
        TextInputEventType Type { get; }
    }
}