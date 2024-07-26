// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Input
{
    /// <summary>
    /// Represents an interface for the updateable input system component.
    /// </summary>
    public interface IUpdateableInput
    {
        /// <summary>
        /// Performs the update for the component.
        /// </summary>
        /// <param name="deltaTime">Time since the last frame.</param>
        void Update(TimeSpan deltaTime);
    }
}