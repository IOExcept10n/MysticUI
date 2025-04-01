using System.Web;

namespace Icy.Assets
{
    /// <summary>
    /// Represents context to load assets.
    /// </summary>
    public record AssetContext(Uri RootPath) : IAssetContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssetContext"/> class.
        /// </summary>
        /// <param name="rootPath">Root path to create context to.</param>
        public AssetContext(string rootPath)
            : this(new Uri(rootPath.Replace('\\', '/')))
        {
        }

        /// <summary>
        /// Gets an instance of the asset context that locates application domain directory.
        /// </summary>
        public static AssetContext ApplicationContext => new(AppDomain.CurrentDomain.BaseDirectory);

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
        public async Task<Stream> OpenStreamAsync(string? relativePath = null)
        {
            string path = GetAbsolutePath(relativePath);
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            return await Task.FromResult(fileStream);
        }

        /// <inheritdoc/>
        public string? GetDataFormat(string? relativePath = null)
        {
            return MimeMapping.GetMimeType(Path.GetFileName(GetAbsolutePath(relativePath)));
        }
    }
}
