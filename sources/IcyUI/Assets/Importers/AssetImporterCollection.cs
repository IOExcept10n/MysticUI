// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace Icy.Assets.Importers
{
    /// <summary>
    /// Represents a collection of asset importers optimized for searching importers ready to read assets in specified.
    /// </summary>
    internal class AssetImporterCollection : Collection<IFormatReadChecker>
    {
        /// <summary>
        /// Tries to find an importer that can read specified asset.
        /// </summary>
        /// <typeparam name="T">Requested type of asset to load.</typeparam>
        /// <param name="format">Format of the asset file to read.</param>
        /// <param name="importer">An instance of importer that can read an asset in specified format or <see langword="null"/> if not found.</param>
        /// <returns><see langword="true"/> if an importer has been found; otherwise <see langword="false"/>.</returns>
        public bool TryFindImporter<T>(string? format, [NotNullWhen(true)] out IAssetImporter<T>? importer)
            where T : class => (importer = this.OfType<IAssetImporter<T>>().FirstOrDefault(x => x.CanRead(format))) != null;
    }
}