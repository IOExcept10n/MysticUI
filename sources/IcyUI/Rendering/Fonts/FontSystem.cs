// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using CommunityToolkit.Diagnostics;
using Icy.Assets;
using Icy.Configuration;

namespace Icy.Rendering.Fonts
{
    /// <summary>
    /// Manages font loading and resolution.
    /// </summary>
    public class FontSystem : IDisposable
    {
        private static readonly string SystemFontsPath = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        private static readonly AssetContext SystemFontsAssetContext = new(SystemFontsPath + '/');

        private readonly IcyConfiguration config;
        private readonly Dictionary<FontInfo, IFont> fontsCache;

        // Dynamic fonts with the same family and style share their rasterizer and atlas, so store almost all their data in one place.
        private readonly Dictionary<VectorFontInfo, SharedDynamicFontData> rasterizationData;

        private FrozenDictionary<VectorFontInfo, string>? systemFontFileNames;

        /// <summary>
        /// Initializes a new instance of the <see cref="FontSystem"/> class.
        /// </summary>
        /// <param name="config">The asset configuration to use for loading font data.</param>
        public FontSystem(IcyConfiguration config)
        {
            Guard.IsNotNull(config);

            this.config = config;
            rasterizationData = [];
            fontsCache = [];
            FontResolver = new FallbackResolver();
        }

        /// <summary>
        /// Gets or sets the maximal number of texture pages to use in atlas of a single dynamic font family.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When imported, dynamic fonts get an expandable texture atlas for rasterized glyphs.
        /// Each font family in a single font system has its own atlas instance shared across style and size variations of this font.
        /// Dynamic font atlas has support for multiple pages in case of large font sizes or inefficient glyphs placement.
        /// By default the number of pages is equal to <c>4</c>.
        /// </para>
        /// <para>
        /// It is recommended to have at least 4 pages limit because an atlas groups glyphs by size.
        /// Each glyph size category should have its own atlas page for efficient search and placement.
        /// </para>
        /// </remarks>
        public uint AtlasPageLimit { get; set; } = 4;

        /// <summary>
        /// Gets or sets the font family used by text elements that don't name one of their own.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A <see cref="UI.Controls.TextBlock"/> with an empty <see cref="UI.Controls.TextBlock.FontFamily"/>
        /// resolves this family instead, at its own size and style. This is what lets markup declare bare text -
        /// <c>&lt;Button&gt;Click Me&lt;/Button&gt;</c> creates a <see cref="UI.Controls.TextBlock"/> that never had
        /// a family assigned - without silently rendering nothing.
        /// </para>
        /// <para>
        /// Leave empty to skip straight to <see cref="FallbackFont"/>. Setting a family that can't be loaded is
        /// harmless: <see cref="GetOrLoad(FontInfo)"/> falls back the same way any other miss does.
        /// </para>
        /// </remarks>
        public string DefaultFontFamily { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a font to be used when an instance of the requested font couldn't be found or created.
        /// </summary>
        /// <remarks>
        /// The last resort, below <see cref="DefaultFontFamily"/>: returned by <see cref="GetOrLoad(FontInfo)"/>
        /// whenever the requested font can't be resolved, at whatever size and style it was itself loaded with.
        /// </remarks>
        public IFont? FallbackFont { get; set; }

        /// <summary>
        /// Gets or sets an instance of the <see cref="IFallbackFontResolver"/> to get fonts that should be used as fallback ones if characters are not supported.
        /// </summary>
        /// <remarks>
        /// If any font can't find a character to write, it asks <see cref="FontResolver"/> to provide an instance of the font that can write it.
        /// </remarks>
        public IFallbackFontResolver FontResolver { get; set; }

        /// <summary>
        /// Gets a value indicating whether the system fonts are enabled and ready for import.
        /// </summary>
        [MemberNotNullWhen(true, nameof(systemFontFileNames))]
        public bool AreSystemFontsEnabled => systemFontFileNames != null;

        /// <summary>
        /// Clears all font caches.
        /// </summary>
        public void Clear()
        {
            foreach (var (rasterizer, atlas) in rasterizationData.Values)
            {
                rasterizer.Dispose();
                atlas.Clear();
            }

            rasterizationData.Clear();
            fontsCache.Clear();
            (FontResolver as FallbackResolver)?.Clear();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Clear();
        }

        /// <summary>
        /// Gets or loads a font with the specified information.
        /// </summary>
        /// <param name="info">Information about the font to get or load.</param>
        /// <returns>The font instance.</returns>
        public IFont? GetOrLoad(FontInfo info)
        {
            // If already registered, return an existing instance.
            if (fontsCache.TryGetValue(info, out var font))
            {
                return font;
            }

            // If can create dynamic based on already loaded font, make it.
            if (TryGetRasterizationData(info, out var pair))
            {
                return ReuseFont(info, pair);
            }

            if (AreSystemFontsEnabled)
            {
                try
                {
                    font = ImportSystemFont(info);
                }
                catch
                {
                }
            }

            return font ?? FallbackFont;
        }

        /// <summary>
        /// Imports a font from a file.
        /// </summary>
        /// <typeparam name="TContext">Type of the context used to load font assets.</typeparam>
        /// <param name="assetContext">The asset context to load from.</param>
        /// <param name="filePath">Path to the font file.</param>
        /// <returns>
        /// An instance of the <see cref="IFont"/> loaded from the specified path.
        /// </returns>
        public IFont ImportFont<TContext>(TContext assetContext, string filePath)
            where TContext : IAssetContext
        {
            Guard.IsNotNullOrEmpty(filePath, nameof(filePath));

            var font = config.Assets.AssetResolver.LoadAsset<IFont>(assetContext, filePath);
            RegisterFont(font);
            return font;
        }

        /// <summary>
        /// Imports a new instance of the font with the specified family, style and size from the system fonts storage.
        /// </summary>
        /// <param name="info">Font info to get an instance of the <see cref="IFont"/> for.</param>
        /// <returns>An instance of the <see cref="IFont"/> with the specified family, size and style.</returns>
        public IFont ImportSystemFont(FontInfo info)
        {
            if (!AreSystemFontsEnabled)
                return ThrowHelper.ThrowInvalidOperationException<IFont>($"Couldn't import a system fonts when system fonts support is disabled. Try calling {nameof(EnableSystemFonts)} first.");

            if (!systemFontFileNames.TryGetValue(info, out string? systemPath))
            {
                systemPath = info.Family;
            }

            // If font is installed on target OS, try to load it.
            if (SystemFontsAssetContext.IsAvailable(systemPath))
            {
                ImportFont(SystemFontsAssetContext, systemPath);

                // After importing this shared data should be available.
                if (!TryGetRasterizationData(info, out var data))
                    return ThrowHelper.ThrowInvalidOperationException<IFont>("Font was imported but its rasterization data couldn't be found.");
                return ReuseFont(info, data);
            }

            return ThrowHelper.ThrowArgumentException<IFont>("Couldn't find system font with the specified font info.");
        }

        /// <summary>
        /// Looks up shared rasterization data (a rasterizer + atlas) for <paramref name="info"/>.
        /// </summary>
        /// <remarks>
        /// Vector (dynamic) fonts are registered under a wildcard <see cref="FontInfo"/> with <see cref="FontInfo.Size"/>
        /// set to <c>0</c> (see <see cref="Assets.Importers.DynamicFonts.DynamicFontImporter"/>/
        /// <see cref="DynamicFontsHelper"/>) - a font file has no inherent pixel size, it can rasterize at any size
        /// requested. A caller asking for a specific size should still find that template rather than missing
        /// entirely just because the exact (family, size, style) triple was never registered.
        /// </remarks>
        /// <param name="info">The requested font info (typically with a real, non-zero size).</param>
        /// <param name="data">The found rasterization data, if any.</param>
        /// <returns><see langword="true"/> if matching rasterization data was found; otherwise <see langword="false"/>.</returns>
        private bool TryGetRasterizationData(FontInfo info, out SharedDynamicFontData data)
        {
            if (rasterizationData.TryGetValue(info, out data))
                return true;
            return !info.IsDynamic && rasterizationData.TryGetValue(info with { Size = 0 }, out data);
        }

        /// <summary>
        /// Enables support for importing system installed fonts by their family names and styles.
        /// </summary>
        /// <remarks>
        /// This method indexes system fonts directory so make sure an app has access to it (by default it has).
        /// </remarks>
        public void EnableSystemFonts() =>
            systemFontFileNames = EnumerateSystemFonts()
                                  .DistinctBy(x => x.Info)
                                  .ToFrozenDictionary(x => x.Info, y => y.FileName);

        private static IEnumerable<(VectorFontInfo Info, string FileName)> EnumerateSystemFonts() => from filePath in Directory.EnumerateFiles(SystemFontsPath)
                                                                                                     let fileName = Path.GetFileName(filePath)
                                                                                                     where DynamicFontsHelper.IsFontFileName(fileName)
                                                                                                     let info = DynamicFontsHelper.GetFontInfo(filePath)[0]
                                                                                                     select ((VectorFontInfo)info, fileName);

        private DynamicSpriteFont ReuseFont(FontInfo info, SharedDynamicFontData data)
        {
            var font = new DynamicSpriteFont(info, data.Rasterizer, data.Atlas, FontResolver);
            fontsCache[info] = font;
            (FontResolver as FallbackResolver)?.AddFont(font);
            return font;
        }

        private void RegisterFont(IFont font)
        {
            // Make shared storage for the specified font.
            if (font is DynamicSpriteFont dyn)
            {
                // Cache atlas and rasterizer used in this font to not reload fonts if available.
                // If the font has an atlas, use it, instead create new one.
                rasterizationData[font.Info] = new(dyn.Rasterizer, dyn.DynamicAtlas ??= new(config.RenderContext, AtlasPageLimit));
            }

            fontsCache[font.Info] = font;
        }

        private readonly record struct SharedDynamicFontData(IGlyphRasterizer Rasterizer, DynamicFontAtlas Atlas);

        private readonly record struct FontStyleInfo(float FontSize, FontStyle Style)
        {
            public static implicit operator FontStyleInfo(FontInfo info) => new(info.Size, info.Style);

            public static implicit operator FontStyleInfo(StyledGlyphDefinition glyph) => new(glyph.FontSize, glyph.Style);
        }

        private readonly record struct VectorFontInfo(string Family, FontStyle Style)
        {
            public static implicit operator VectorFontInfo(FontInfo info) => new(info.Family, info.Style);
        }

        private class FallbackResolver : IFallbackFontResolver
        {
            private readonly Dictionary<FontStyleInfo, List<IFont>> styleGroups = [];

            public void AddFont(IFont font)
            {
                if (!styleGroups.TryGetValue(font.Info, out var group))
                {
                    styleGroups[font.Info] = group = [];
                }

                group.Add(font);
            }

            public void Clear()
            {
                styleGroups.Clear();
            }

            public IFont? GetFallbackFont(StyledGlyphDefinition glyph)
            {
                if (styleGroups.TryGetValue(glyph, out var group))
                {
                    return group.FirstOrDefault(x => x.SupportsCharacter(glyph.Codepoint));
                }

                return null;
            }
        }
    }
}