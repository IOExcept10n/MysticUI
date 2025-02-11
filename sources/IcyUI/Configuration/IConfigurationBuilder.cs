// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Input;
using Icy.Rendering;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents a service that simplifies library configuration building.
    /// </summary>
    public interface IConfigurationBuilder
    {
        /// <summary>
        /// Builds the final configuration object.
        /// </summary>
        /// <returns>The configured <see cref="IcyConfiguration"/> instance.</returns>
        IcyConfiguration Build();

        /// <summary>
        /// Sets the asset configuration to be used by the library.
        /// </summary>
        /// <param name="assetConfiguration">The asset configuration instance.</param>
        /// <returns>The current builder instance for fluent configuration.</returns>
        IAssetConfigurationBuilder ConfigureAssets(IAssetConfiguration assetConfiguration);

        /// <summary>
        /// Configures the asset management system with additional settings.
        /// </summary>
        /// <returns>An instance of <see cref="IAssetConfigurationBuilder"/> to further configure the asset management system.</returns>
        /// <remarks>
        /// This method allows for more detailed configuration of the asset system,
        /// such as setting up asset paths, localizers, and other related settings.
        /// </remarks>
        IAssetConfigurationBuilder ConfigureAssets();

        /// <summary>
        /// Sets the input system to be used by the library.
        /// </summary>
        /// <param name="inputSystem">The input system instance.</param>
        /// <returns>The current builder instance for fluent configuration.</returns>
        IInputConfigurationBuilder ConfigureInput(IInputSystem inputSystem);

        /// <summary>
        /// Configures the input system with additional settings.
        /// </summary>
        /// <returns>An instance of <see cref="IInputConfigurationBuilder"/> to further configure the input system.</returns>
        /// <remarks>
        /// This method allows for more detailed configuration of the input system,
        /// such as setting up key bindings, input events, and other related settings.
        /// </remarks>
        IInputConfigurationBuilder ConfigureInput();

        /// <summary>
        /// Sets the rendering context to be used by the library.
        /// </summary>
        /// <param name="renderContext">The render context instance.</param>
        /// <returns>The current builder instance for fluent configuration.</returns>
        IRenderingConfigurationBuilder ConfigureRendering(IRenderContext renderContext);

        /// <summary>
        /// Configures the rendering system with additional settings.
        /// </summary>
        /// <returns>An instance of <see cref="IRenderingConfigurationBuilder"/> to further configure the rendering system.</returns>
        /// <remarks>
        /// This method allows for more detailed configuration of the rendering system,
        /// such as setting up rendering options, shaders, and other related settings.
        /// </remarks>
        IRenderingConfigurationBuilder ConfigureRendering();

        /// <summary>
        /// Sets the reflection-related settings to be used by the library.
        /// </summary>
        /// <param name="typesConfig">The reflection configuration instance.</param>
        /// <returns>The current builder instance for fluent configuration.</returns>
        IReflectionConfigurationBuilder ConfigureTypes(ReflectionConfiguration typesConfig);

        /// <summary>
        /// Configures the reflection settings with additional settings.
        /// </summary>
        /// <returns>An instance of <see cref="IRenderingConfigurationBuilder"/> to further configure the rendering system.</returns>
        /// <remarks>
        /// This method allows for more detailed configuration of the reflection configuration,
        /// such as setting up assemblies resolving, types conversion and other related settings.
        /// </remarks>
        IReflectionConfigurationBuilder ConfigureTypes();
    }
}