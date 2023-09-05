namespace MysticUI.Extensions.Content
{
    /// <summary>
    /// Represents context to load assets.
    /// </summary>
    public record AssetContext(string RootPath) : IAssetContext;
}