namespace Icy.Assets
{
    /// <summary>
    /// Represents a generalized source to access assets from.
    /// </summary>
    /// <typeparam name="TSelf">Type of the used asset context.</typeparam>
    public interface IAssetContext<TSelf>
        where TSelf : IAssetContext<TSelf>
    {
        /// <summary>
        /// Gets the path of the root directory for accessing assets.
        /// </summary>
        Uri RootPath { get; }

        /// <summary>
        /// Initializes a new instance of the <typeparamref name="TSelf"/> using the specified target path.
        /// </summary>
        /// <param name="targetPath">Uri instance to initialize the context.</param>
        /// <returns>An instance of the <typeparamref name="TSelf"/>.</returns>
        static abstract TSelf Create(Uri targetPath);

        /// <summary>
        /// Combines the asset context instance with a relative path to produce a new local asset context.
        /// </summary>
        /// <param name="relativePath">A relative path to create a new context for.</param>
        /// <returns>New instance of the <typeparamref name="TSelf"/> that refers to the combined path.</returns>
        TSelf Combine(string relativePath);

        /// <summary>
        /// Checks if the path represented by the given relative path exists.
        /// </summary>
        /// <param name="relativePath">Relative path part for this context.</param>
        /// <returns><see langword="true"/> if the path exists; <see langword="false"/> otherwise.</returns>
        bool IsAvailable(string? relativePath = null);

        /// <summary>
        /// Gets the absolute path to the given relative path relative to this <typeparamref name="TSelf"/> instance.
        /// </summary>
        /// <param name="relativePath">A relative path to make absolute path for.</param>
        /// <returns>An absolute path for the specified path combination.</returns>
        string GetAbsolutePath(string? relativePath = null);

        /// <summary>
        /// Tries to get data format of the resource specified by current asset context state.
        /// </summary>
        /// <param name="relativePath">A relative path to get the format for.</param>
        /// <returns>A string representing <see langword="MIME"/>-type for the specified <see cref="Uri"/> or <see langword="null"/> in case when format cannot be determined.</returns>
        string? GetDataFormat(string? relativePath = null);

        /// <summary>
        /// Opens a stream to the specified relative path.
        /// </summary>
        /// <param name="relativePath">Optional relative path to open the stream to.</param>
        /// <returns>An instance of the <see cref="Stream"/> to the specified asset.</returns>
        Stream OpenStream(string? relativePath = null);

        /// <summary>
        /// Asynchronously opens a stream to the specified relative path.
        /// </summary>
        /// <param name="relativePath">Optional relative path to open the stream to.</param>
        /// <returns>An asynchronous task that results to an instance of the <see cref="Stream"/> to the specified asset.</returns>
        Task<Stream> OpenStreamAsync(string? relativePath = null);
    }
}
