// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Configuration
{
    /// <summary>
    /// Represents a configuration builder instance with additional methods to build assets configuration.
    /// </summary>
    public interface IAssetConfigurationBuilder : IConfigurationBuilder
    {
        /// <summary>
        /// Gets an instance of the assets configuration to be used in application.
        /// </summary>
        IAssetConfiguration Assets { get; }
    }
}