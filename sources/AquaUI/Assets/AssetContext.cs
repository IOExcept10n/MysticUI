namespace AquaUI.Assets
{
    /// <summary>
    /// Represents context to load assets.
    /// </summary>
    public record AssetContext(string RootPath) : IAssetContext<AssetContext>
    {
        /// <inheritdoc/>
        public AssetContext Combine(string localPath) => new(Path.Combine(RootPath, localPath));

        /// <inheritdoc/>
        public bool Exists(string? localPath = null)
        {
            if (localPath == null)
                return Path.Exists(RootPath);
            else return Path.Exists(Path.Combine(RootPath, localPath));
        }

        /// <inheritdoc/>
        public string GetAbsolutePath(string? localPath = null)
        {
            if (localPath == null)
                return Path.GetFullPath(RootPath);
            return Path.GetFullPath(Path.Combine(RootPath, localPath));
        }
    }
}
