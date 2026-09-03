// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Configuration
{
    /// <summary>
    /// Represents a configuration building service with additional methods to build theme configuration.
    /// </summary>
    public interface IThemeConfigurationBuilder : IConfigurationBuilder
    {
        /// <summary>
        /// Gets an instance of the <see cref="ThemeConfiguration"/> to be used in application.
        /// </summary>
        public ThemeConfiguration Theme { get; }
    }
}
