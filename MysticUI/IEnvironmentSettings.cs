using FontStashSharp;
using MysticUI.Brushes.TextureBrushes;
using MysticUI.Controls;
using MysticUI.Extensions.Content;
using Stride.Games;
using Stride.Graphics;

namespace MysticUI
{
    /// <summary>
    /// An interface for UI library environment settings.
    /// </summary>
    public interface IEnvironmentSettings
    {
        /// <summary>
        /// Game instance to work with.
        /// </summary>
        public IGame Game { get; set; }

        /// <summary>
        /// Debug options for the UI elements.
        /// </summary>
        public GraphicsDevice GraphicsDevice => Game.GraphicsDevice;

        /// <summary>
        /// Default assets resolver. This should be set until start of use.
        /// </summary>
        public IAssetResolver<AssetContext> DefaultAssetsResolver { get; set; }

        /// <summary>
        /// Debug options.
        /// </summary>
        public EnvironmentDebugOptions DebugOptions { get; set; }

        /// <summary>
        /// Default assets for current settings.
        /// </summary>
        public UIAssets DefaultAssets { get; set; }

        /// <summary>
        /// Version of the library.
        /// </summary>
        public Version Version { get; }

        /// <summary>
        /// Localization provider for the library.
        /// </summary>
        public ILocalizationProvider? LocalizationProvider { get; set; }

        /// <summary>
        /// Set it to <see langword="true"/> to make text more smooth.
        /// </summary>
        public bool SmoothText { get; }

        /// <summary>
        /// A clipboard substitution for occasions when the system clipboard is unavailable.
        /// </summary>
        public string? InternalClipboard { get; set; }

        /// <summary>
        /// Resets all resources to their defaults.
        /// </summary>
        public void Reset();
    }

    /// <summary>
    /// Represents a class to provide debug options for the library.
    /// </summary>
    public class EnvironmentDebugOptions
    {
        /// <summary>
        /// Indicates if control frames should be rendered.
        /// </summary>
        public bool DrawControlFrames { get; set; }

        /// <summary>
        /// Indicates if the keyboard focused control frame should be rendered.
        /// </summary>
        public bool DrawFocusFrame { get; set; }

        /// <summary>
        /// Indicates if the mouse hovered control frame should be rendered.
        /// </summary>
        public bool DrawMouseHoverFrame { get; set; }

        /// <summary>
        /// Indicates if text glyphs should be rendered.
        /// </summary>
        public bool DrawTextGlyphFrames { get; set; }

        /// <summary>
        /// Set it to disable renderer clipping.
        /// </summary>
        public bool DisableClipping { get; set; }
    }

    /// <summary>
    /// Represents set of assets which are used by default.
    /// </summary>
    public class UIAssets : IDisposable
    {
        private bool disposedValue;

        /// <summary>
        /// Game instance to work with.
        /// </summary>
        public static IGame Game => EnvironmentSettingsProvider.EnvironmentSettings.Game;

        /// <summary>
        /// Shared white texture instance to help with drawing primitives.
        /// </summary>
        public static Texture WhiteTexture => Game.GraphicsDevice.GetSharedWhiteTexture();

        /// <summary>
        /// Default stylesheet instance for the game UI.
        /// </summary>
        public Stylesheet? DefaultStylesheet { get; set; }

        /// <summary>
        /// Gets the set of texture atlases.
        /// </summary>
        public TextureAtlasCollection TextureAtlases { get; } = new();

        /// <summary>
        /// Gets the set of used UI fonts.
        /// </summary>
        public FontCollection Fonts { get; } = new();

        /// <summary>
        /// Gets the dictionary of the custom resources that can be used for UI bindings.
        /// </summary>
        /// <remarks>
        /// The keyword to use binding to resource in this dictionary is "{<see langword="Resource"/> key}". <br/>
        /// Note that resource processor will try to find the font in <see cref="Fonts"/> if the property has the <see cref="SpriteFontBase"/> type,
        /// or try to find the texture in <see cref="TextureAtlases"/> if the property can store textures.
        /// </remarks>
        public Dictionary<string, object> StaticResources { get; } = new();

        /// <summary>
        /// Provides the default context to load new assets. It's used by the library loaders.
        /// </summary>
        public AssetContext? DefaultAssetContext { get; set; }

        /// <summary>
        /// Gets the most relevant resource for the given property by its key.
        /// </summary>
        /// <param name="resourceTag">The key of the resource to load.</param>
        /// <param name="targetType">Type of the property to get the resource for.</param>
        /// <param name="required">Determines whether to throw an exception of the resource is not found.</param>
        /// <returns>The most relevant resource with the requested key.</returns>
        /// <exception cref="ArgumentException"></exception>
        public object? GetStaticResource(string resourceTag, Type? targetType = null, bool required = false)
        {
            if (targetType?.IsAssignableTo(typeof(IBrush)) == true)
            {
                var image = TextureAtlases[resourceTag];
                if (image != null) return image;
                else if (StaticResources.TryGetValue(resourceTag, out var textureTest))
                {
                    if (textureTest is IBrush)
                    {
                        return textureTest;
                    }
                    else if (textureTest is Texture texture)
                    {
                        return new ImageBrush(texture);
                    }
                }
            }
            else if (targetType == typeof(SpriteFontBase))
            {
                return Fonts[resourceTag] ?? (required ? throw new ArgumentException("The required resource by given key is not found.", nameof(resourceTag)) : null);
            }
            else if (StaticResources.TryGetValue(resourceTag, out var result)) return result;
            return required ? throw new ArgumentException("The required resource by given key is not found.", nameof(resourceTag)) : null;
        }

        /// <summary>
        /// Gets the most relevant resource for the given property by its key.
        /// </summary>
        /// <typeparam name="T">Type of the property to get the resource for.</typeparam>
        /// <param name="tag">The key of the resource to load.</param>
        /// <param name="required">Determines whether to throw an exception of the resource is not found.</param>
        /// <returns>The most relevant resource with the requested key.</returns>
        /// <exception cref="ArgumentException"></exception>
        public T? GetStaticResource<T>(string tag, bool required = false) where T : class
        {
            return GetStaticResource(tag, typeof(T), required) as T;
        }

        /// <summary>
        /// Disposes all assets.
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    StaticResources.Clear();
                    DefaultStylesheet?.Unload();
                    Fonts?.Clear();
                }
                disposedValue = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}