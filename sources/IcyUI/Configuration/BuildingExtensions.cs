using Icy.Assets;
using Icy.Assets.Importers;
using Icy.Assets.Importers.BitmapFonts;
using Icy.Assets.Importers.DynamicFonts;
using Icy.Assets.Parsers;
using Icy.Data;
using Icy.Data.Markup;
using Icy.Rendering.Fonts;

namespace Icy.Configuration
{
    /// <summary>
    /// Provides extension methods for configuring asset settings in the library.
    /// </summary>
    public static class BuildingExtensions
    {
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
