namespace Icy.Assets
{
    /// <summary>
    /// Represents context to load assets.
    /// </summary>
    public record AssetContext(Uri RootPath) : IAssetContext<AssetContext>
    {
        /// <inheritdoc/>
        public static AssetContext Create(Uri targetPath) => new(targetPath);

        /// <inheritdoc/>
        public AssetContext Combine(string relativePath) => new(new Uri(RootPath, relativePath));

        /// <inheritdoc/>
        public bool IsAvailable(string? relativePath = null) => Path.Exists(GetAbsolutePath(relativePath));

        /// <inheritdoc/>
        public string GetAbsolutePath(string? relativePath = null) => new Uri(RootPath, relativePath).AbsolutePath;

        /// <inheritdoc/>
        public Stream OpenStream(string? relativePath = null)
        {
            return File.OpenRead(GetAbsolutePath(relativePath));
        }

        /// <inheritdoc/>
        public Task<Stream> OpenStreamAsync(string? relativePath = null)
        {
            return Task.FromResult(OpenStream(relativePath));
        }

        /// <inheritdoc/>
        public string? GetDataFormat(string? relativePath = null)
        {
            return MimeMapping.GetMimeType(Path.GetFileName(GetAbsolutePath(relativePath)));
        }
    }
}
