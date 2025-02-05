// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;

namespace Icy.Input.Events
{
    /// <summary>
    /// Represents a basic interface for all input events listeners.
    /// </summary>
    public interface IInputEventProvider : IUpdateableInput, IInitializable
    {
        /// <summary>
        /// Gets the input system instance for the current input event provider.
        /// </summary>
        public IInputSystem InputSystem { get; }
    }
}
