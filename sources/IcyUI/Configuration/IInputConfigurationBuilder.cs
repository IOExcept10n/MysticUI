// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Input;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents a configuration building service with additional methods to build input configuration.
    /// </summary>
    public interface IInputConfigurationBuilder : IConfigurationBuilder
    {
        /// <summary>
        /// Gets an instance of the <see cref="IInputSystem"/> to be used in application.
        /// </summary>
        IInputSystem InputSystem { get; }
    }
}