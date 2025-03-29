using System.Web;

namespace Icy.Assets
{
    /// <summary>
    /// Represents context to load assets.
    /// </summary>
    public record AssetContext(Uri RootPath) : IAssetContext
    {
        /// <summary>
        /// Gets an instance of the asset context that locates application domain directory.
        /// </summary>
        public static AssetContext ApplicationContext => new(new Uri(AppDomain.CurrentDomain.BaseDirectory));

        /// <inheritdoc/>
        public IAssetContext Combine(string relativePath) => new AssetContext(new Uri(RootPath, relativePath));

        /// <inheritdoc/>
        public bool IsAvailable(string? relativePath = null) => Path.Exists(GetAbsolutePath(relativePath));

        /// <inheritdoc/>
        public string GetAbsolutePath(string? relativePath = null) => new Uri(RootPath, relativePath).LocalPath;

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
