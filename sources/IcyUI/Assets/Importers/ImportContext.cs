namespace Icy.Assets.Importers
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IImportContext{TContext}"/> type.
    /// </summary>
    /// <typeparam name="TContext">Type of the asset context used to import assets.</typeparam>
    /// <param name="AssetResolver">An instance of the <see cref="IAssetResolver{TContext}"/> to import assets.</param>
    /// <param name="DataFormat"><c>MIME</c>-type for the data format for the requested asset.</param>
    /// <param name="ImportSource">The context from which the asset is loaded.</param>
    /// <param name="ResourceName">Name of the loaded resource.</param>
    public record ImportContext<TContext>(IAssetResolver<TContext> AssetResolver, string? DataFormat, TContext ImportSource, string? ResourceName) : IImportContext<TContext>
        where TContext : IAssetContext<TContext>;
}
