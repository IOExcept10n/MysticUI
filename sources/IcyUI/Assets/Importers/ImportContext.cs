using Icy.Configuration;

namespace Icy.Assets.Importers
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IImportContext"/> type.
    /// </summary>
    /// <param name="AssetResolver">An instance of the <see cref="IAssetResolver"/> to import assets.</param>
    /// <param name="DataFormat"><c>MIME</c>-type for the data format for the requested asset.</param>
    /// <param name="ImportSource">The context from which the asset is loaded.</param>
    /// <param name="ResourceName">Name of the loaded resource.</param>
    public record ImportContext(IAssetResolver AssetResolver, string? DataFormat, IAssetContext ImportSource, string? ResourceName) : IImportContext;
}
