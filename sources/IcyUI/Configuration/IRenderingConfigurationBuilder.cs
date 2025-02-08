// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents a configuration building service with additional methods to build rendering configuration.
    /// </summary>
    public interface IRenderingConfigurationBuilder : IConfigurationBuilder
    {
        /// <summary>
        /// Gets an instance of the rendering service to be used in application.
        /// </summary>
        IRenderContext RenderContext { get; }
    }
}