// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;
using Icy.Configuration;
using Icy.MonoGame.Assets;
using Icy.MonoGame.Assets.Importers;
using Icy.MonoGame.Input;
using Icy.MonoGame.Rendering;
using Microsoft.Xna.Framework;

namespace Icy.MonoGame.Configuration
{
    /// <summary>
    /// Provides extension methods for configuring the IcyUI in MonoGame applications.
    /// </summary>
    public static class MonoGameBuildingExtensions
    {
        /// <summary>
        /// Configures the specified <see cref="Game"/> instance to use the IcyUI with default settings.
        /// </summary>
        /// <param name="game">The <see cref="Game"/> instance to configure.</param>
        /// <returns>The configured <see cref="Game"/> instance.</returns>
        public static Game UseIcyUI(this Game game) =>
            game.UseIcyUI(builder => builder.WithDefaultMonoGameConfiguration(game));

        /// <summary>
        /// Configures the specified <see cref="Game"/> instance to use the IcyUI with custom settings.
        /// </summary>
        /// <param name="game">The <see cref="Game"/> instance to configure.</param>
        /// <param name="configure">An action to configure the <see cref="IConfigurationBuilder"/>.</param>
        /// <returns>The configured <see cref="Game"/> instance.</returns>
        public static Game UseIcyUI(this Game game, Action<IConfigurationBuilder> configure)
        {
            var builder = new IcyConfigurationBuilder();
            configure(builder);
            var renderer = new MonoGameIcyRenderer(game, builder.Build());
            game.Components.Add(renderer);
            return game;
        }

        /// <summary>
        /// Gets UI configuration with which the game IcyUI library has been initialized.
        /// </summary>
        /// <param name="game">An instance of the <see cref="Game"/> to read UI configuration from.</param>
        /// <returns>An instance of the <see cref="IcyConfiguration"/> that runs UI in specified game.</returns>
        /// <exception cref="InvalidOperationException">Occurs when UI not has not been initialized.</exception>
        public static IcyConfiguration GetIcyConfiguration(this Game game)
        {
            var renderer = game.Components.OfType<MonoGameIcyRenderer>().FirstOrDefault();
            if (renderer == null)
                return ThrowHelper.ThrowInvalidOperationException<IcyConfiguration>("Couldn't access UI configuration. UI node has not been added to the game.");
            return renderer.Configuration;
        }

        /// <summary>
        /// Configures the default settings for MonoGame in the provided configuration builder.
        /// </summary>
        /// <param name="builder">The configuration builder instance to configure.</param>
        /// <param name="game">The <see cref="Game"/> instance used for configuration.</param>
        /// <returns>The updated configuration builder instance for fluent configuration.</returns>
        public static IConfigurationBuilder WithDefaultMonoGameConfiguration(this IConfigurationBuilder builder, Game game) =>
            builder.ConfigureRendering(new RenderContext(game.GraphicsDevice))
                   .ConfigureInput(new InputSystem(game))
                   .ConfigureTypes()
                   .ConfigureAssets()
                   .WithAssetContextFactory(new MonoGameAssetContextFactory(game))
                   .AddBasicFontSupport()
                   .UseMonoGameImporters(game);


        /// <summary>
        /// Adds MonoGame-specific importers to the asset configuration builder.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance to configure.</param>
        /// <param name="game">The <see cref="Game"/> instance used for configuration.</param>
        /// <returns>The updated asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder UseMonoGameImporters(this IAssetConfigurationBuilder builder, Game game) =>
            builder.AddImporter(new TextureImporter(game));
    }

}