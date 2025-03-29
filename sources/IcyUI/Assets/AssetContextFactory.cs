// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Assets
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IAssetContextFactory"/> interface.
    /// </summary>
    internal class AssetContextFactory : IAssetContextFactory
    {
        /// <inheritdoc/>
        public IAssetContext Create(Uri uri) => new AssetContext(uri);

        /// <inheritdoc/>
        public bool TryCreate(Uri uri, out IAssetContext assetContext) => (assetContext = new AssetContext(uri)) is { };
    }
}