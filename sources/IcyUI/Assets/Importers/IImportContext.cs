// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;

namespace Icy.Assets.Importers
{
    /// <summary>
    /// Represents a context for importing a specified resource.
    /// </summary>
    public interface IImportContext
    {
        /// <summary>
        /// Gets an instance of the asset resolving service that called import.
        /// </summary>
        IAssetResolver AssetResolver { get; }

        /// <summary>
        /// Gets the <see langword="MIME"/>-type for the format of the imported data.
        /// </summary>
        string? DataFormat { get; }

        /// <summary>
        /// Gets the context to import items from.
        /// </summary>
        IAssetContext ImportSource { get; }

        /// <summary>
        /// Gets the name of the requested resource for the specified context.
        /// </summary>
        string? ResourceName { get; }
    }
}