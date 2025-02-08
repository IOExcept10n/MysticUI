// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IAssetConfiguration"/> to use with <see cref="AssetContext"/>.
    /// </summary>
    internal sealed class AssetConfiguration : IAssetConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AssetConfiguration"/> class.
        /// </summary>
        /// <param name="defaultAssetContext">The root UI asset context.</param>
        public AssetConfiguration(AssetContext defaultAssetContext)
        {
            DefaultAssetContext = defaultAssetContext;
            SharedResources = new ResourceRegistry();
            Localizer = new ResourceLocalizer(SharedResources);
            AssetResolver = new AssetResolver();
        }

        /// <inheritdoc/>
        public IAssetContext DefaultAssetContext { get; set; }

        /// <inheritdoc/>
        public IAssetResolver AssetResolver { get; }

        /// <inheritdoc/>
        public ILocalizer Localizer { get; set; }

        /// <inheritdoc/>
        public IResourceRegistry SharedResources { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            AssetResolver.Dispose();
            foreach (var resource in SharedResources.Values)
            {
                if (resource is IDisposable disposable)
                    disposable.Dispose();
            }

            SharedResources.Clear();
            GC.SuppressFinalize(this);
        }
    }
}