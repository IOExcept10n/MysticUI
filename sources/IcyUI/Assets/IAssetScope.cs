// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Assets
{
    /// <summary>
    /// Represents a scope for caching all the assets related to the specified context.
    /// </summary>
    public interface IAssetScope : IDisposable
    {
        /// <summary>
        /// Gets the root context to cache all the assets inside it.
        /// </summary>
        IAssetContext RootContext { get; }

        /// <summary>
        /// Gets an instance of the assets resolver that owes this scope.
        /// </summary>
        IAssetResolver AssetResolver { get; }

        /// <summary>
        /// Checks if the specified asset is stored in cache of this scope.
        /// </summary>
        /// <param name="path">Relative path to the asset.</param>
        /// <returns><see langword="true"/> if the asset is stored in cache; otherwise <see langword="false"/>.</returns>
        bool IsCached(string path);

        /// <summary>
        /// Gets an asset for the specified path if it is cached.
        /// </summary>
        /// <typeparam name="T">Type of the requested asset.</typeparam>
        /// <param name="path">Relative path to the cached asset.</param>
        /// <returns>An instance of the cached asset.</returns>
        /// <exception cref="AssetNotFoundException">Occurs when the asset hasn't been loaded to the cache.</exception>
        public T GetAsset<T>(string path)
            where T : class;
    }
}