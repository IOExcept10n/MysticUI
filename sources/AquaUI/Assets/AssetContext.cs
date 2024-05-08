namespace AquaUI.Assets
{
    /// <summary>
    /// Represents context to load assets.
    /// </summary>
    public record AssetContext(string RootPath) : IAssetContext;
}
