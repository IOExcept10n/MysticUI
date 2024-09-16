namespace AquaUI.Assets
{
    /// <summary>
    /// Represents a service that performs assets loading.
    /// </summary>
    /// <typeparam name="TContext">Type of the context used in the asset loader.</typeparam>
    public interface IAssetResolver<in TContext> : IDisposable
        where TContext : IAssetContext<TContext>
    {
        /// <summary>
        /// Loads an asset from the specified location.
        /// </summary>
        /// <typeparam name="T">Type of the loaded asset.</typeparam>
        /// <param name="context">Context to load asset from.</param>
        /// <param name="path">Path to load asset from.</param>
        /// <param name="keepInCache">
        /// <see langword="true"/> if you want to cache the loaded asset to not reload it later.
        /// If the <see langword="false"/> is selected, system will load the asset as new whenever it is loaded or not.
        /// </param>
        /// <returns>An instance of the loaded asset.</returns>
        public T LoadAsset<T>(TContext context, string path, bool keepInCache = true)
            where T : class;

        /// <summary>
        /// Loads the asset asynchronously if it's available. See more: <seealso cref="LoadAsset{T}(TContext, string, bool)"/>.
        /// </summary>
        /// <typeparam name="T">Type of the loaded asset.</typeparam>
        /// <param name="context">Context to load asset from.</param>
        /// <param name="path">Path to load asset from.</param>
        /// <param name="keepInCache">
        /// <see langword="true"/> if you want to cache the loaded asset to not reload it later.
        /// If the <see langword="false"/> is selected, system will load the asset as new whenever it is loaded or not.
        /// </param>
        /// <returns>An instance of the loaded asset.</returns>
        public ValueTask<T> LoadAssetAsync<T>(TContext context, string path, bool keepInCache = true)
            where T : class;

        /// <summary>
        /// Detects if the asset was already loaded to the cache.
        /// </summary>
        /// <param name="context">Context to load the asset.</param>
        /// <param name="path">Relative path to the asset.</param>
        /// <returns><see langword="true"/> if the asset is stored in cache, <see langword="false"/> otherwise.</returns>
        public bool IsCached(TContext context, string path);

        /// <summary>
        /// Unloads and disposes all content from the given context.
        /// </summary>
        /// <param name="context">Context to unload.</param>
        public void UnloadContext(TContext context);

        /// <summary>
        /// Opens a file and reads its content.
        /// </summary>
        /// <param name="context">Context to get file from.</param>
        /// <param name="path">Path to the file.</param>
        /// <returns>Contents of the text file with specified path.</returns>
        public string ReadFile(TContext context, string path);

        /// <summary>
        /// Unloads and disposes all content from the given context asynchronously if it's available.
        /// </summary>
        /// <param name="context">Context to unload.</param>
        /// <returns>Task to await if the operation can be performed asynchronously.</returns>
        public Task UnloadContextAsync(TContext context);
    }
}
