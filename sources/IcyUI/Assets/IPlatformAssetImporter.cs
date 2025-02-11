using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Icy.Assets
{
    /// <summary>
    /// Represents an interface for objects that perform customized asset import for the platform-related asset reference.
    /// </summary>
    /// <remarks>
    /// This interface is used by asset adapters that perform loading underlying assets from the specified asset references.
    /// </remarks>
    /// <typeparam name="T">Requested type for import.</typeparam>
    public interface IPlatformAssetImporter<out T>
    {
        /// <summary>
        /// Loads and wraps a platform asset using related adapter type.
        /// </summary>
        /// <param name="assetReference">An instance of the asset reference to use for assets import.</param>
        /// <param name="assetPath">Path to asset to import.</param>
        /// <returns>An instance of <typeparamref name="T"/> for library to work with.</returns>
        T LoadAsset(IPlatformAssetReference assetReference, string? assetPath = null);

        /// <summary>
        /// Unloads related asset from the specified asset reference using its underlying platform type.
        /// </summary>
        /// <param name="assetReference">A reference to unload asset for.</param>
        /// <param name="assetPath">Path to asset to unload.</param>
        void UnloadAsset(IPlatformAssetReference assetReference, string? assetPath = null);
    }
}
