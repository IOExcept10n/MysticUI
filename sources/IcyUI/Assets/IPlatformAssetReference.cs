// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Assets
{
    /// <summary>
    /// Represents a reference to the engine-specified assets section that could be read with engine API.
    /// </summary>
    public interface IPlatformAssetReference
    {
        /// <summary>
        /// Loads an asset with the specified relative path.
        /// </summary>
        /// <typeparam name="T">Type of the asset to load.</typeparam>
        /// <param name="assetPath">Relative path to the asset to load from this section.</param>
        /// <returns>An instance of the loaded asset.</returns>
        public T Load<T>(string? assetPath = null);

        /// <summary>
        /// Unloads an asset with the specified relative path.
        /// </summary>
        /// <param name="assetPath">Relative path to the asset to unload from this section.</param>
        public void Unload(string? assetPath = null);
    }
}