// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;

namespace Icy.Configuration
{
    /// <summary>
    /// Represents a service that performs the library assets management.
    /// </summary>
    public interface IAssetConfiguration : IDisposable
    {
        /// <summary>
        /// Gets or sets the root asset context for all the UI assets.
        /// </summary>
        IAssetContext DefaultAssetContext { get; set; }

        /// <summary>
        /// Gets the used instance of the <see cref="IAssetResolver"/>.
        /// </summary>
        IAssetResolver AssetResolver { get; }

        /// <summary>
        /// Gets or sets the used localizer instance.
        /// </summary>
        ILocalizer Localizer { get; set; }

        /// <summary>
        /// Gets the UI resources registry instance.
        /// </summary>
        IResourceRegistry SharedResources { get; }
    }
}
