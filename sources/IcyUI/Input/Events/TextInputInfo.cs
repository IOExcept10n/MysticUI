// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a default implementation for the <see cref="ITextInputEventInfo"/> interface.
    /// </summary>
    /// <param name="CompositionRange"><inheritdoc cref="ITextInputEventInfo.CompositionRange"/></param>
    /// <param name="Text"><inheritdoc cref="ITextInputEventInfo.Text"/></param>
    /// <param name="Type"><inheritdoc cref="ITextInputEventInfo.Type"/></param>
    public record TextInputInfo(Range CompositionRange, string Text, TextInputEventType Type) : ITextInputEventInfo;
}
