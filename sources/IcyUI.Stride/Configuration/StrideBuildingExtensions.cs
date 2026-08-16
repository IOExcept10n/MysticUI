// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;
using Icy.Configuration;
using Icy.Stride.Assets;
using Icy.Stride.Assets.Importers;
using Stride.Engine;
using Stride.Rendering.Compositing;

namespace Icy.Stride.Configuration
{
    /// <summary>
    /// Provides extension methods for configuring IcyUI in Stride applications.
    /// </summary>
    public static class StrideBuildingExtensions
    {
        /// <summary>
        /// Configures the specified <see cref="Game"/> instance to use IcyUI with default settings.
        /// </summary>
        /// <param name="game">The <see cref="Game"/> instance to configure.</param>
        /// <returns>The <see cref="IcyUISceneRenderer"/> render stage - add <see cref="Icy.UI.Canvas"/> instances
        /// to its <see cref="IcyUISceneRenderer.Canvases"/> collection to have them drawn every frame.</returns>
        public static IcyUISceneRenderer UseIcyUI(this Game game) =>
            game.UseIcyUI(builder => builder.WithDefaultStrideConfiguration(game));

        /// <summary>
        /// Configures the specified <see cref="Game"/> instance to use IcyUI with custom settings.
        /// </summary>
        /// <param name="game">The <see cref="Game"/> instance to configure.</param>
        /// <param name="configure">An action to configure the <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>The <see cref="IcyUISceneRenderer"/> render stage - add <see cref="Icy.UI.Canvas"/> instances
        /// to its <see cref="IcyUISceneRenderer.Canvases"/> collection to have them drawn every frame.</returns>
        public static IcyUISceneRenderer UseIcyUI(this Game game, Action<IConfigurationBuilder> configure)
        {
            var builder = new IcyConfigurationBuilder();
            configure(builder);
            IcyConfiguration configuration = builder.Build();

            var gameSystem = new IcyUIGameSystem(game.Services, configuration);
            game.GameSystems.Add(gameSystem);

            var overlay = new IcyUISceneRenderer { Configuration = configuration };
            GraphicsCompositor compositor = game.SceneSystem.GraphicsCompositor;
            compositor.Game = new CompositeSceneRenderer(compositor.Game, overlay);

            return overlay;
        }

        /// <summary>
        /// Gets the UI configuration with which the game IcyUI library has been initialized.
        /// </summary>
        /// <param name="game">An instance of the <see cref="Game"/> to read UI configuration from.</param>
        /// <returns>An instance of the <see cref="IcyConfiguration"/> that runs UI in the specified game.</returns>
        /// <exception cref="InvalidOperationException">Occurs when UI has not been initialized.</exception>
        public static IcyConfiguration GetIcyConfiguration(this Game game)
        {
            var system = game.GameSystems.OfType<IcyUIGameSystem>().FirstOrDefault();
            if (system == null)
                return ThrowHelper.ThrowInvalidOperationException<IcyConfiguration>("Couldn't access UI configuration. UI has not been added to the game.");
            return system.Configuration;
        }

        /// <summary>
        /// Configures the default settings for Stride in the provided configuration builder.
        /// </summary>
        /// <param name="builder">The configuration builder instance to configure.</param>
        /// <param name="game">The <see cref="Game"/> instance used for configuration.</param>
        /// <returns>The updated configuration builder instance for fluent configuration.</returns>
        public static IConfigurationBuilder WithDefaultStrideConfiguration(this IConfigurationBuilder builder, Game game) =>
            builder.ConfigureRendering(new Rendering.RenderContext(game.GraphicsDevice))
                   .ConfigureInput(new Input.InputSystem(game.Services.GetService<global::Stride.Input.InputManager>()))
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .WithAssetContextFactory(new StrideAssetContextFactory(game))
                   .AddBasicFontSupport()
                   .UseStrideImporters(game);

        /// <summary>
        /// Adds Stride-specific importers to the asset configuration builder.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance to configure.</param>
        /// <param name="game">The <see cref="Game"/> instance used for configuration.</param>
        /// <returns>The updated asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder UseStrideImporters(this IAssetConfigurationBuilder builder, Game game) =>
            builder.AddImporter(new TextureImporter(game));
    }
}
