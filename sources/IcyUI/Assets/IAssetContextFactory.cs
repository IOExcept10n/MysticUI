// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;

namespace Icy.Assets
{
    /// <summary>
    /// Represents a factory that helps with creation for the specialized <see cref="IAssetContext"/> instances.
    /// </summary>
    public interface IAssetContextFactory
    {
        /// <summary>
        /// Creates an asset context with specified <see cref="Uri"/> instance.
        /// </summary>
        /// <param name="uri">An <see cref="Uri"/> that points towards required asset context.</param>
        /// <returns>An instance of the <see cref="IAssetContext"/> that provides access to assets by specified <see cref="Uri"/>.</returns>
        IAssetContext Create(Uri uri);

        /// <summary>
        /// Tries to create an asset context with specified <see cref="Uri"/> instance.
        /// </summary>
        /// <param name="uri">An <see cref="Uri"/> that points towards required asset context.</param>
        /// <param name="assetContext">An instance of the <see cref="IAssetContext"/> that provides access to assets by specified <see cref="Uri"/> or <see langword="default"/> if the context can't be created.</param>
        /// <returns><see langword="true"/> if context was created successfully; otherwise <see langword="false"/>.</returns>
        bool TryCreate(Uri uri, [NotNullWhen(true)] out IAssetContext? assetContext);
    }
}