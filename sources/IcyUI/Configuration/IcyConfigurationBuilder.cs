// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Icy.Assets;
using Icy.Input;
using Icy.Rendering;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents a builder that allows fluent configuration for the <c>IcyUI</c> library.
    /// </summary>
    public class IcyConfigurationBuilder :
        IAssetConfigurationBuilder,
        IInputConfigurationBuilder,
        IRenderingConfigurationBuilder,
        IReflectionConfigurationBuilder
    {
        /// <inheritdoc/>
        [NotNull]
        public IAssetConfiguration? Assets { get; private set; }

        /// <inheritdoc/>
        [NotNull]
        public IInputSystem? InputSystem { get; private set; }

        /// <inheritdoc/>
        [NotNull]
        public IRenderContext? RenderContext { get; private set; }

        /// <inheritdoc/>
        [NotNull]
        public IReflectionConfiguration? Types { get; private set; }

        /// <inheritdoc/>
        public IcyConfiguration Build()
        {
            Guard.IsNotNull(Assets);
            Guard.IsNotNull(InputSystem);
            Guard.IsNotNull(RenderContext);
            Guard.IsNotNull(Types);
            return new IcyConfiguration(InputSystem, Assets, RenderContext, Types);
        }

        /// <inheritdoc/>
        public IAssetConfigurationBuilder ConfigureAssets(IAssetConfiguration assetConfiguration)
        {
            Assets = assetConfiguration;
            return this;
        }

        /// <inheritdoc/>
        public IAssetConfigurationBuilder ConfigureAssets()
        {
            Assets ??= new AssetConfiguration(AssetContext.ApplicationContext);
            return this;
        }

        /// <inheritdoc/>
        public IInputConfigurationBuilder ConfigureInput(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
            return this;
        }

        /// <inheritdoc/>
        public IInputConfigurationBuilder ConfigureInput()
        {
            Guard.IsNotNull(InputSystem);
            return this;
        }

        /// <inheritdoc/>
        public IRenderingConfigurationBuilder ConfigureRendering(IRenderContext renderContext)
        {
            RenderContext = renderContext;
            return this;
        }

        /// <inheritdoc/>
        public IRenderingConfigurationBuilder ConfigureRendering()
        {
            Guard.IsNotNull(RenderContext);
            return this;
        }

        /// <inheritdoc/>
        public IReflectionConfigurationBuilder ConfigureTypes(IReflectionConfiguration typesConfig)
        {
            Types = typesConfig;
            return this;
        }

        /// <inheritdoc/>
        public IReflectionConfigurationBuilder ConfigureTypes()
        {
            Types ??= new ReflectionConfiguration();
            return this;
        }
    }
}