using Icy.Assets;
using Icy.Assets.Importers;
using Icy.Assets.Importers.BitmapFonts;
using Icy.Assets.Importers.DynamicFonts;
using Icy.Assets.Parsers;
using Icy.Data;
using Icy.Data.Markup;
using Icy.Diagnostics;
using Icy.Markup;
using Icy.Rendering.Fonts;

namespace Icy.Configuration
{
    /// <summary>
    /// Provides extension methods for configuring asset settings in the library.
    /// </summary>
    public static class BuildingExtensions
    {
        private const string DefaultThemeResourceName = "Icy.Resources.Themes.DefaultTheme.xml";

        /// <summary>
        /// Sets the localizer for the asset configuration.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <param name="localizer">The localizer instance to be used.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder WithLocalizer(this IAssetConfigurationBuilder builder, ILocalizer localizer)
        {
            builder.Assets.Localizer = localizer;
            return builder;
        }

        /// <summary>
        /// Sets the localizer for the asset configuration using a default instance of the specified type.
        /// </summary>
        /// <typeparam name="T">The type of the localizer to create.</typeparam>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder WithLocalizer<T>(this IAssetConfigurationBuilder builder)
            where T : ILocalizer, new()
        {
            builder.Assets.Localizer = new T();
            return builder;
        }

        /// <summary>
        /// Sets the default asset context for the asset configuration.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <param name="context">The asset context to be set as default.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder WithDefaultAssetContext(this IAssetConfigurationBuilder builder, IAssetContext context)
        {
            builder.Assets.DefaultAssetContext = context;
            return builder;
        }

        /// <summary>
        /// Sets the asset context factory for the asset configuration.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <param name="factory">The asset context factory to be set to the configuration.</param>
        /// <returns>THe current asset configuration builder to fluent configuration.</returns>
        public static IAssetConfigurationBuilder WithAssetContextFactory(this IAssetConfigurationBuilder builder, IAssetContextFactory factory)
        {
            builder.Assets.AssetContextFactory = factory;
            return builder;
        }

        /// <summary>
        /// Adds an asset importer to the asset configuration.
        /// </summary>
        /// <typeparam name="T">The type of the asset to be imported.</typeparam>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <param name="importer">The asset importer instance to be added.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder AddImporter<T>(this IAssetConfigurationBuilder builder, IAssetImporter<T> importer)
        {
            builder.Assets.AssetResolver.RegisterImporter(importer);
            return builder;
        }

        /// <summary>
        /// Adds an asset importer of the specified type to the asset configuration.
        /// </summary>
        /// <typeparam name="TAsset">The type of the asset to be imported.</typeparam>
        /// <typeparam name="TImporter">The type of the asset importer to be created.</typeparam>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder AddImporter<TAsset, TImporter>(this IAssetConfigurationBuilder builder)
            where TImporter : IAssetImporter<TAsset>, new()
        {
            builder.Assets.AssetResolver.RegisterImporter(new TImporter());
            return builder;
        }

        /// <summary>
        /// Adds an asset parser to the asset configuration.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <param name="parser">The asset parser instance to be added.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder AddParser(this IAssetConfigurationBuilder builder, IAssetParser parser)
        {
            builder.Assets.AssetResolver.RegisterParser(parser);
            return builder;
        }

        /// <summary>
        /// Adds an asset parser of the specified type to the asset configuration.
        /// </summary>
        /// <typeparam name="T">The type of the asset parser to be created.</typeparam>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder AddParser<T>(this IAssetConfigurationBuilder builder)
            where T : IAssetParser, new()
        {
            builder.Assets.AssetResolver.RegisterParser(new T());
            return builder;
        }

        /// <summary>
        /// Adds support for bitmap fonts to the asset configuration.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder AddBitmapFontSupport(this IAssetConfigurationBuilder builder) =>
            builder.AddImporter<IFont, BMFontImporter>();

        /// <summary>
        /// Adds support for dynamic TrueType fonts to the asset configuration.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        public static IAssetConfigurationBuilder AddDynamicFontSupport(this IAssetConfigurationBuilder builder) =>
            builder.AddImporter<IFont, DynamicFontImporter>()
                   .AddImporter<IGlyphRasterizer, StbRasterizerImporter>();

        /// <summary>
        /// Adds support for basic font types to the asset configuration.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        /// <remarks>
        /// This method adds support for both bitmap fonts and TrueType fonts.
        /// </remarks>
        public static IAssetConfigurationBuilder AddBasicFontSupport(this IAssetConfigurationBuilder builder) =>
            builder.AddBitmapFontSupport()
                  .AddDynamicFontSupport();

        /// <summary>
        /// Sets the assembly resolver for the reflection configuration.
        /// </summary>
        /// <param name="builder">The reflection configuration builder instance.</param>
        /// <param name="resolver">The assembly resolver instance to be used.</param>
        /// <returns>The current reflection configuration builder instance for fluent configuration.</returns>
        public static IReflectionConfigurationBuilder WithAssemblyResolver(this IReflectionConfigurationBuilder builder, IAssemblyResolver resolver)
        {
            builder.Types.AssemblyResolver = resolver;
            return builder;
        }

        /// <summary>
        /// Sets the type converter for the reflection configuration.
        /// </summary>
        /// <param name="builder">The reflection configuration builder instance.</param>
        /// <param name="converter">The type converter instance to be used.</param>
        /// <returns>The current reflection configuration builder instance for fluent configuration.</returns>
        public static IReflectionConfigurationBuilder WithTypeConverter(this IReflectionConfigurationBuilder builder, ITypeConverter converter)
        {
            builder.Types.TypeConverter = converter;
            builder.Types.PropertyRegistry.TypeConverter = converter;
            return builder;
        }

        /// <summary>
        /// Sets the property registry for the reflection configuration.
        /// </summary>
        /// <param name="builder">The reflection configuration builder instance.</param>
        /// <param name="registry">The property registry instance to be used.</param>
        /// <returns>The current reflection configuration builder instance for fluent configuration.</returns>
        /// <remarks>
        /// Only useful for isolating a configuration from the process-wide
        /// <see cref="Data.Markup.PropertyRegistry.Default"/> - tests being the usual reason. Elements must be
        /// constructed against the same registry, so build them inside a
        /// <see cref="Data.Markup.PropertyRegistry.UseScope"/> for <paramref name="registry"/>; see the remarks on
        /// <see cref="Data.Markup.PropertyRegistry"/> for why mixing registries breaks value precedence.
        /// </remarks>
        public static IReflectionConfigurationBuilder WithPropertyRegistry(this IReflectionConfigurationBuilder builder, PropertyRegistry registry)
        {
            builder.Types.PropertyRegistry = registry;
            return builder;
        }

        /// <summary>
        /// Configures the markup facet - the types markup may name, how they're constructed, and how bare text
        /// becomes objects.
        /// </summary>
        /// <param name="builder">The reflection configuration builder instance.</param>
        /// <param name="configure">The configuration action applied to <see cref="ReflectionConfiguration.Markup"/>.</param>
        /// <returns>The current reflection configuration builder instance for fluent configuration.</returns>
        /// <example>
        /// <code language="csharp">
        /// builder.ConfigureMarkup(m => m.RegisterShortName&lt;HealthBar&gt;());
        /// </code>
        /// </example>
        public static IReflectionConfigurationBuilder ConfigureMarkup(this IReflectionConfigurationBuilder builder, Action<MarkupConfiguration> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            configure(builder.Types.Markup);
            return builder;
        }

        /// <summary>
        /// Configures the catalog of registered debug overlays and HUD panels.
        /// </summary>
        /// <param name="builder">The reflection configuration builder instance.</param>
        /// <param name="configure">The configuration action applied to <see cref="ReflectionConfiguration.Diagnostics"/>.</param>
        /// <returns>The current reflection configuration builder instance for fluent configuration.</returns>
        /// <example>
        /// <code language="csharp">
        /// builder.ConfigureDiagnostics(d => d.RegisterOverlay&lt;MyCustomOverlay&gt;());
        /// </code>
        /// </example>
        public static IReflectionConfigurationBuilder ConfigureDiagnostics(this IReflectionConfigurationBuilder builder, Action<DebugToolRegistry> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            configure(builder.Types.Diagnostics);
            return builder;
        }

        /// <summary>
        /// Adds support for loading markup documents through the asset pipeline - both a <see cref="UI.UIElement"/>
        /// document (a <c>Page</c>, say) and a standalone <see cref="UI.ResourceDictionary"/> document (a theme
        /// file referenced via <c>Source=</c>), so either can be requested by path like any other asset.
        /// </summary>
        /// <param name="builder">The asset configuration builder instance.</param>
        /// <param name="configuration">The built configuration the loader resolves types and properties through.</param>
        /// <returns>The current asset configuration builder instance for fluent configuration.</returns>
        /// <remarks>
        /// Takes the finished <see cref="IcyConfiguration"/> rather than reading it off the builder because the
        /// loader needs the whole thing - assets, types, and fonts - which only exists once
        /// <see cref="IConfigurationBuilder.Build"/> has run. Call this after building.
        /// </remarks>
        public static IAssetConfigurationBuilder AddMarkupSupport(this IAssetConfigurationBuilder builder, IcyConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            return builder
                .AddImporter<UI.UIElement>(new MarkupImporter(configuration))
                .AddImporter<UI.ResourceDictionary>(new ResourceDictionaryImporter(configuration));
        }

        /// <summary>
        /// Configures the default-theme facet - the <see cref="UI.ResourceDictionary"/> a <see cref="UI.Canvas"/>
        /// exposes to every element attached to it (see <see cref="UI.Canvas.Resources"/>).
        /// </summary>
        /// <param name="builder">The theme configuration builder instance.</param>
        /// <param name="configure">The configuration action applied to <see cref="ReflectionConfiguration"/>-sibling <see cref="ThemeConfiguration"/>.</param>
        /// <returns>The current theme configuration builder instance for fluent configuration.</returns>
        /// <example>
        /// <code language="csharp">
        /// builder.ConfigureTheme(t => t.Theme = myCustomThemeDictionary);
        /// </code>
        /// </example>
        public static IThemeConfigurationBuilder ConfigureTheme(this IThemeConfigurationBuilder builder, Action<ThemeConfiguration> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            configure(builder.Theme);
            return builder;
        }

        /// <summary>
        /// Configures the UI scaling facet - the defaults every <see cref="UI.Canvas"/> uses to compute
        /// <see cref="UI.Canvas.EffectiveScale"/>.
        /// </summary>
        /// <param name="builder">The scaling configuration builder instance.</param>
        /// <param name="configure">The configuration action applied to <see cref="ScalingConfiguration"/>.</param>
        /// <returns>The current scaling configuration builder instance for fluent configuration.</returns>
        /// <example>
        /// <code language="csharp">
        /// builder.ConfigureScaling().ConfigureScaling(s => s.Mode = UIScaleMode.ReferenceResolution);
        /// </code>
        /// </example>
        public static IScalingConfigurationBuilder ConfigureScaling(this IScalingConfigurationBuilder builder, Action<ScalingConfiguration> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);
            configure(builder.Scaling);
            return builder;
        }

        /// <summary>
        /// Loads IcyUI's own bundled default theme and assigns it to <see cref="IcyConfiguration.Theme"/>'s
        /// <see cref="ThemeConfiguration.Theme"/> property, so a <see cref="UI.Canvas"/> built against
        /// <paramref name="configuration"/> gives every Tier-1 control (<see cref="UI.Controls.Button"/>,
        /// <see cref="UI.Controls.CheckBox"/>, etc.) a real default look with no further setup.
        /// </summary>
        /// <param name="configuration">The built configuration to load the theme through and assign it to.</param>
        /// <returns><paramref name="configuration"/>, for fluent chaining.</returns>
        /// <exception cref="InvalidOperationException">The bundled default-theme resource is missing.</exception>
        /// <remarks>
        /// Unlike every other facet configured through <see cref="IcyConfigurationBuilder"/>, this must run
        /// <em>after</em> <see cref="IConfigurationBuilder.Build"/> - loading the theme markup needs a fully-built
        /// <see cref="IcyConfiguration"/> (a real <see cref="Markup.MarkupLoader"/>, type resolution, and property
        /// registration), the same reason <see cref="AddMarkupSupport(IAssetConfigurationBuilder, IcyConfiguration)"/>
        /// is itself a post-build call.
        /// </remarks>
        public static IcyConfiguration UseDefaultTheme(this IcyConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            using Stream stream = typeof(BuildingExtensions).Assembly.GetManifestResourceStream(DefaultThemeResourceName)
                ?? throw new InvalidOperationException($"The bundled default theme resource '{DefaultThemeResourceName}' is missing.");
            var loader = new MarkupLoader(configuration);

            // Loaded as a throwaway UIElement (see DefaultTheme.xml's own remarks on why it isn't a bare
            // <ResourceDictionary> document) purely to reach its .Resources - the element itself is discarded.
            UI.UIElement host = loader.Load(stream, "DefaultTheme.xml");
            configuration.Theme.Theme = host.Resources;
            return configuration;
        }

        /// <summary>
        /// Registers a type converter for converting values from one type to another.
        /// </summary>
        /// <param name="builder">The reflection configuration builder instance.</param>
        /// <param name="from">The source type for the conversion.</param>
        /// <param name="to">The target type for the conversion.</param>
        /// <param name="converter">The value converter instance to be registered.</param>
        /// <returns>The current reflection configuration builder instance for fluent configuration.</returns>
        public static IReflectionConfigurationBuilder AddTypeConverter(this IReflectionConfigurationBuilder builder, Type from, Type to, IValueConverter converter)
        {
            builder.Types.TypeConverter.RegisterConverter(from, to, converter);
            return builder;
        }

        /// <summary>
        /// Registers a type converter for converting values from a specified source type to a target type.
        /// </summary>
        /// <typeparam name="TSource">The source type for the conversion.</typeparam>
        /// <typeparam name="TTarget">The target type for the conversion.</typeparam>
        /// <param name="builder">The reflection configuration builder instance.</param>
        /// <param name="converter">The value converter instance to be registered.</param>
        /// <returns>The current reflection configuration builder instance for fluent configuration.</returns>
        public static IReflectionConfigurationBuilder AddTypeConverter<TSource, TTarget>(this IReflectionConfigurationBuilder builder, IValueConverter<TSource, TTarget> converter)
        {
            builder.Types.TypeConverter.RegisterConverter(converter);
            return builder;
        }
    }
}
