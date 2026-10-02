// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
        IReflectionConfigurationBuilder,
        IThemeConfigurationBuilder,
        IScalingConfigurationBuilder
    {
        /// <inheritdoc/>
        [NotNull]
        public AssetConfiguration? Assets { get; private set; }

        /// <inheritdoc/>
        [NotNull]
        public IInputSystem? InputSystem { get; private set; }

        /// <inheritdoc/>
        [NotNull]
        public IRenderContext? RenderContext { get; private set; }

        /// <inheritdoc/>
        [NotNull]
        public ReflectionConfiguration? Types { get; private set; }

        /// <inheritdoc/>
        [NotNull]
        public ThemeConfiguration? Theme { get; private set; }

        /// <inheritdoc/>
        [NotNull]
        public ScalingConfiguration? Scaling { get; private set; }

        /// <inheritdoc/>
        public IcyConfiguration Build()
        {
            Guard.IsNotNull(InputSystem);
            Guard.IsNotNull(RenderContext);
            ConfigureTypes();
            ConfigureAssets();
            ConfigureTheme();
            ConfigureScaling();
            return new IcyConfiguration(InputSystem, Assets, RenderContext, Types, Theme, Scaling);
        }

        /// <inheritdoc/>
        public IAssetConfigurationBuilder ConfigureAssets(AssetConfiguration assetConfiguration)
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
        public IReflectionConfigurationBuilder ConfigureTypes(ReflectionConfiguration typesConfig)
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

        /// <inheritdoc/>
        public IThemeConfigurationBuilder ConfigureTheme(ThemeConfiguration themeConfiguration)
        {
            Theme = themeConfiguration;
            return this;
        }

        /// <inheritdoc/>
        public IThemeConfigurationBuilder ConfigureTheme()
        {
            Theme ??= new ThemeConfiguration();
            return this;
        }

        /// <inheritdoc/>
        public IScalingConfigurationBuilder ConfigureScaling(ScalingConfiguration scalingConfiguration)
        {
            Scaling = scalingConfiguration;
            return this;
        }

        /// <inheritdoc/>
        public IScalingConfigurationBuilder ConfigureScaling()
        {
            Scaling ??= new ScalingConfiguration();
            return this;
        }
    }
}