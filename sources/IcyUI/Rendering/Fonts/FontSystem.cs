using System.Collections.Frozen;
using CommunityToolkit.Diagnostics;
using Icy.Assets;
using Icy.Configuration;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Manages font loading and resolution.
    /// </summary>
    public class FontSystem
    {
        private static readonly string SystemFontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        private static readonly AssetContext SystemFontsAssetContext = new(SystemFontsPath + '/');
        private static readonly FrozenDictionary<VectorFontInfo, string> SystemFontFileNames = EnumerateSystemFonts()
                                                                                                .DistinctBy(x => x.Info)
                                                                                                .ToFrozenDictionary(x => x.Info, y => y.FileName);

        // Dynamic fonts with the same family and style share their rasterizer and atlas, so it them storing almost all their data in one place.
        private readonly Dictionary<VectorFontInfo, IGlyphRasterizer> rasterizers;
        private readonly Dictionary<FontInfo, IFont> fontsCache;
        private readonly IcyConfiguration config;

        /// <summary>
        /// Initializes a new instance of the <see cref="FontSystem"/> class.
        /// </summary>
        /// <param name="config">The asset configuration to use for loading font data.</param>
        public FontSystem(IcyConfiguration config)
        {
            Guard.IsNotNull(config);

            this.config = config;
            rasterizers = [];
            fontsCache = [];
        }

        public IFont FallbackFont { get; set; }

        /// <summary>
        /// Gets or loads a font with the specified information.
        /// </summary>
        /// <param name="info">Information about the font to get or load.</param>
        /// <returns>The font instance.</returns>
        public IFont GetOrLoad(FontInfo info)
        {
            // If already registered, return an existing instance.
            if (fontsCache.TryGetValue(info, out var font))
            {
                return font;
            }

            // If can create dynamic based on already loaded font, make it.
            if (rasterizers.TryGetValue(info, out var rasterizer))
            {
                return fontsCache[info] = new DynamicSpriteFont(info, rasterizer, new(config.RenderContext));
            }

            // If font is installed on target OS, try to load it.
            if (SystemFontFileNames.TryGetValue(info, out string? systemPath) && SystemFontsAssetContext.IsAvailable(systemPath))
            {
                ImportFont(info.Family, SystemFontsAssetContext, systemPath);

                // After importing this shared data should be available.
                rasterizer = rasterizers[info];
                return fontsCache[info] = new DynamicSpriteFont(info, rasterizer, new(config.RenderContext));
            }

            return FallbackFont;
        }

        /// <summary>
        /// Imports a font from a file.
        /// </summary>
        /// <typeparam name="TContext">Type of the context used to load font assets.</typeparam>
        /// <param name="family">The font family name.</param>
        /// <param name="assetContext">The asset context to load from.</param>
        /// <param name="filePath">Path to the font file.</param>
        /// <returns>
        /// An instance of the <see cref="IFont"/> loaded from the specified path.
        /// </returns>
        public IFont ImportFont<TContext>(string family, TContext assetContext, string filePath)
            where TContext : IAssetContext
        {
            Guard.IsNotNullOrEmpty(family, nameof(family));
            Guard.IsNotNullOrEmpty(filePath, nameof(filePath));

            var font = config.Assets.AssetResolver.LoadAsset<IFont>(assetContext, filePath);
            RegisterFont(font);
            return font;
        }

        /// <summary>
        /// Clears all font caches.
        /// </summary>
        public void Clear()
        {
            foreach (var rasterizer in rasterizers.Values)
            {
                rasterizer.Dispose();
            }

            rasterizers.Clear();
            fontsCache.Clear();
        }

        private static IEnumerable<(VectorFontInfo Info, string FileName)> EnumerateSystemFonts() => from filePath in Directory.EnumerateFiles(SystemFontsPath)
                                                                                                     let fileName = Path.GetFileName(filePath)
                                                                                                     where DynamicFontsHelper.IsFontFileName(fileName)
                                                                                                     let info = DynamicFontsHelper.GetFontInfo(filePath)
                                                                                                     select ((VectorFontInfo)info, fileName);

        private void RegisterFont(IFont font)
        {
            // Make shared storage for the specified font.
            if (font is DynamicSpriteFont dyn)
            {
                // Cache atlas and rasterizer used in this font to not reload fonts if available.
                rasterizers[font.Info] = dyn.Rasterizer;
            }

            fontsCache[font.Info] = font;
        }

        private readonly record struct SharedDynamicFontData(IGlyphRasterizer Rasterizer, DynamicFontAtlas Atlas);

        private readonly record struct VectorFontInfo(string Family, FontStyle Style)
        {
            public static implicit operator VectorFontInfo(FontInfo info) => new(info.Family, info.Style);
        }
    }
}