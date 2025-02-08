// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets.Importers;
using Icy.Assets.Parsers;

namespace Icy.Assets
{
    /// <summary>
    /// Represents a service that performs assets loading.
    /// </summary>
    public interface IAssetResolver : IDisposable
    {
        /// <summary>
        /// Registers an instance of the <see cref="IAssetParser"/> to use when loading assets.
        /// </summary>
        /// <param name="parser">An instance of the <see cref="IAssetParser"/> to load assets.</param>
        void RegisterParser(IAssetParser parser);

        /// <summary>
        /// Registers an instance of the <see cref="IAssetImporter{T}"/> to use when loading assets of type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">Type of the assets supported by specified asset loader.</typeparam>
        /// <param name="importer">An instance of the <see cref="IAssetImporter{T}"/> to register.</param>
        void RegisterImporter<T>(IAssetImporter<T> importer);

        /// <summary>
        /// Creates a caching scope for the specified asset context.
        /// </summary>
        /// <param name="context">An instance of the <see cref="IAssetScope"/> that refers to the cached section.</param>
        /// <returns>A disposable scope for caching assets related to the specified asset context.</returns>
        IAssetScope CreateScope(IAssetContext context);

        /// <summary>
        /// Loads an asset from the specified location.
        /// </summary>
        /// <typeparam name="T">Type of the loaded asset.</typeparam>
        /// <param name="context">Context to load asset from.</param>
        /// <param name="path">Path to load asset from.</param>
        /// <returns>An instance of the loaded asset.</returns>
        T LoadAsset<T>(IAssetContext context, string path)
            where T : class;

        /// <summary>
        /// Loads the asset asynchronously if it's available. See more: <seealso cref="LoadAsset{T}(IAssetContext, string)"/>.
        /// </summary>
        /// <typeparam name="T">Type of the loaded asset.</typeparam>
        /// <param name="context">Context to load asset from.</param>
        /// <param name="path">Path to load asset from.</param>
        /// <returns>An instance of the loaded asset.</returns>
        ValueTask<T> LoadAssetAsync<T>(IAssetContext context, string path)
            where T : class;
    }
}