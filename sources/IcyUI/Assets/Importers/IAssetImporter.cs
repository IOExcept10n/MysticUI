// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Assets.Importers
{
    /// <summary>
    /// Represents a service for the assets reading.
    /// </summary>
    /// <typeparam name="T">Type of the assets that can be loaded using this importer.</typeparam>
    public interface IAssetImporter<out T> : IFormatReadChecker
    {
        /// <summary>
        /// Performs asset loading from the specified data stream.
        /// </summary>
        /// <typeparam name="TContext">Type of the asset context passed to a method.</typeparam>
        /// <param name="stream">Data stream to read asset from.</param>
        /// <param name="importContext">Context to import the asset.</param>
        /// <returns>An instance of the asset of type <typeparamref name="T"/> loaded from the specified stream.</returns>
        T Import<TContext>(Stream stream, IImportContext<TContext> importContext)
            where TContext : IAssetContext<TContext>;
    }

    /// <summary>
    /// Represents an object that can tell whether it can read data in specific format.
    /// </summary>
    public interface IFormatReadChecker
    {
        /// <summary>
        /// Checks if the importer can read assets in the specified data format.
        /// </summary>
        /// <param name="format">A string with <see langword="MIME"/>-type for the format of the loaded asset.</param>
        /// <returns><see langword="true"/> if the asset can be loaded by this asset reader; otherwise <see langword="false"/>.</returns>
        bool CanRead(string? format);
    }
}