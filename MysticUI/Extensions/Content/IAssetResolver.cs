namespace MysticUI.Extensions.Content
{
    /// <summary>
    /// An interface for the object which provides assets loading for the library.
    /// </summary>
    /// <typeparam name="TContext">Type of the context used in the asset loader.</typeparam>
    public interface IAssetResolver<in TContext> : IDisposable
        where TContext : IAssetContext
    {
        /// <summary>
        /// Loads an asset from the given location.
        /// </summary>
        /// <typeparam name="T">Type of the loaded asset.</typeparam>
        /// <param name="context">Context in which asset will be loaded.</param>
        /// <param name="path">Path from which the asset is loaded.</param>
        /// <param name="keepInCache">
        /// <see langword="true"/> if you want to cache the loaded asset to not reload it later.
        /// If the <see langword="false"/> is selected, system will load the asset as new whenever it is loaded or not.
        /// <list type="bullet"><item>
        /// Please note that the cached assets will be stored as weak references so if you
        /// remove a reference to it in another place, the cached resource may be cleared as well
        /// </item></list>
        /// </param>
        /// <returns>The loaded asset instance.</returns>
        public T LoadAsset<T>(TContext context, string path, bool keepInCache = true) where T : class;

        /// <summary>
        /// Loads the asset asynchronously if it's available. See more: <seealso cref="LoadAsset{T}(TContext, string, bool)"/>
        /// </summary>
        /// <typeparam name="T">Type of the loaded asset.</typeparam>
        /// <param name="context">Context in which asset will be loaded.</param>
        /// <param name="path">Path from which the asset is loaded.</param>
        /// <param name="keepInCache">
        /// <see langword="true"/> if you want to cache the loaded asset to not reload it later.
        /// If the <see langword="false"/> is selected, system will load the asset as new whenever it is loaded or not.
        /// <list type="bullet"><item>
        /// Please note that the cached assets will be stored as weak references so if you
        /// remove a reference to it in another place, the cached resource may be cleared as well
        /// </item></list>
        /// </param>
        /// <returns>The loaded asset instance.</returns>
        public ValueTask<T> LoadAssetAsync<T>(TContext context, string path, bool keepInCache = true) where T : class;

        /// <summary>
        /// Detects if the asset was already loaded to the cache.
        /// </summary>
        /// <param name="context">Context in which asset is loaded.</param>
        /// <param name="path">Relative path to the asset.</param>
        /// <returns><see langword="true"/> if the asset is stored in cache, <see langword="false"/> otherwise.</returns>
        public bool IsStoredInCache(TContext context, string path);

        /// <summary>
        /// Unloads and disposes all content from the given context.
        /// </summary>
        /// <param name="context">Context to unload.</param>
        public void UnloadContext(TContext context);

        /// <summary>
        /// Resolves a file and reads its content.
        /// </summary>
        /// <param name="context">Context to load from.</param>
        /// <param name="path">Path to the file.</param>
        /// <returns></returns>
        public string ReadFile(TContext context, string path);

        /// <summary>
        /// Unloads and disposes all content from the given context asynchronously if it's available.
        /// </summary>
        /// <param name="context">Context to unload.</param>
        /// <returns>Task to await if the operation can be performed asynchronously.</returns>
        public Task UnloadContextAsync(TContext context);
    }
}