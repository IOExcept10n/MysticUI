namespace AquaUI.Assets
{
    /// <summary>
    /// An interface which represents rooted path segment to access assets from.
    /// </summary>
    /// <typeparam name="TSelf">Type of the used asset context.</typeparam>
    public interface IAssetContext<TSelf>
        where TSelf : IAssetContext<TSelf>
    {
        /// <summary>
        /// Gets path of the root directory to accessing assets in.
        /// </summary>
        string RootPath { get; }

        /// <summary>
        /// Combines the asset context instance with the local path to produce new local asset context.
        /// </summary>
        /// <param name="localPath">A local path to make new context to.</param>
        /// <returns>New instance of the <typeparamref name="TSelf"/> that refers to the combined path.</returns>
        TSelf Combine(string localPath);

        /// <summary>
        /// Checks if the path represented by given path is exists.
        /// </summary>
        /// <param name="localPath">Local path part for this context.</param>
        /// <returns><see langword="true"/> if the path exists; <see langword="false"/> otherwise.</returns>
        bool Exists(string? localPath = null);

        /// <summary>
        /// Gets the absolute path to the given local path relative to this <typeparamref name="TSelf"/> instance.
        /// </summary>
        /// <param name="localPath">The local path to make absolute path for.</param>
        /// <returns>An absolute path for the specified path combination.</returns>
        string GetAbsolutePath(string? localPath = null);

        /// <summary>
        /// Opens a stream to the specified local path.
        /// </summary>
        /// <param name="localPath">Optional local path to open the stream to.</param>
        /// <returns>An instance of the <see cref="Stream"/> to the specified asset.</returns>
        Stream OpenStream(string? localPath = null);
    }
}
