// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents configuration for the library assets.
    /// </summary>
    public sealed class AssetConfiguration : IDisposable
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
            AssetContextFactory = new AssetContextFactory();
        }

        /// <summary>
        /// Gets or sets the default context to use with assets loading.
        /// </summary>
        public IAssetContext DefaultAssetContext { get; set; }

        /// <summary>
        /// Gets or sets an instance of the asset resolver used in the library.
        /// </summary>
        public IAssetResolver AssetResolver { get; set; }

        /// <summary>
        /// Gets or sets the localization service instance.
        /// </summary>
        public ILocalizer Localizer { get; set; }

        /// <summary>
        /// Gets or sets resources registry used in the library.
        /// </summary>
        public IResourceRegistry SharedResources { get; set; }

        /// <summary>
        /// Gets or sets an instance of the asset context factory to use with current configuration.
        /// </summary>
        public IAssetContextFactory AssetContextFactory { get; set; }

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