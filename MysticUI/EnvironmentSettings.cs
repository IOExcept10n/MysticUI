using CommunityToolkit.Diagnostics;
using MysticUI.Extensions.Content;
using Stride.Games;

namespace MysticUI
{
    /// <summary>
    /// Default implementation of the environment settings.
    /// </summary>
    public class EnvironmentSettings : IEnvironmentSettings
    {
        private IGame? game;
        private EnvironmentDebugOptions? debugOptions;
        private IAssetResolver<AssetContext>? defaultAssetResolver;
        private UIAssets? defaultAssets;

        /// <inheritdoc/>
        public IGame Game
        {
            get
            {
                Guard.IsNotNull(game);
                return game;
            }

            set => game = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc/>
        public EnvironmentDebugOptions DebugOptions
        {
            get => debugOptions ??= new EnvironmentDebugOptions();
            set => debugOptions = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc/>
        public bool SmoothText { get; set; }

        /// <inheritdoc/>
        public Version Version => GetType().Assembly.GetName().Version!;

        /// <inheritdoc/>
        public IAssetResolver<AssetContext> DefaultAssetsResolver
        {
            get
            {
                Guard.IsNotNull(defaultAssetResolver);
                return defaultAssetResolver;
            }
            set => defaultAssetResolver = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc/>
        public UIAssets DefaultAssets
        {
            get => defaultAssets ??= new UIAssets();
            set => defaultAssets = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <inheritdoc/>
        public ILocalizationProvider? LocalizationProvider { get; set; }

        /// <inheritdoc/>
        public string? InternalClipboard { get; set; }

        /// <inheritdoc/>
        public void Reset()
        {
            DefaultAssets.Dispose();
        }
    }

    /// <summary>
    /// Provides access to a selected environment settings.
    /// </summary>
    public static class EnvironmentSettingsProvider
    {
        private static IEnvironmentSettings? environmentSettings;

        /// <summary>
        /// Environment settings for the UI system.
        /// </summary>
        public static IEnvironmentSettings EnvironmentSettings
        {
            get => environmentSettings ??= new EnvironmentSettings();
            set => environmentSettings = value;
        }
    }
}