// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Assets
{
    /// <summary>
    /// Represents a service that performs the library assets management.
    /// </summary>
    /// <typeparam name="TContext">Type of the asset context used in target project.</typeparam>
    public interface IAssetConfiguration<TContext> : IDisposable
        where TContext : IAssetContext<TContext>
    {
        /// <summary>
        /// Gets or sets the root asset context for all the UI assets.
        /// </summary>
        TContext DefaultAssetContext { get; set; }

        /// <summary>
        /// Gets the used instance of the <see cref="IAssetResolver{TContext}"/>.
        /// </summary>
        IAssetResolver<TContext> AssetResolver { get; }

        /// <summary>
        /// Gets or sets the used localizer instance.
        /// </summary>
        ILocalizer Localizer { get; set; }

        /// <summary>
        /// Gets the UI resources registry instance.
        /// </summary>
        IResourceRegistry Resources { get; }
    }
}