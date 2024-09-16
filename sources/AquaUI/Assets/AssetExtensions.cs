// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;

namespace AquaUI.Assets
{
    /// <summary>
    /// Provides some extension methods for the assets management.
    /// </summary>
    public static class AssetExtensions
    {
        /// <summary>
        /// Tries to get the resource from the specified resource registry.
        /// </summary>
        /// <param name="resources">An instance of the <see cref="IResourceRegistry"/> to access the specified resource.</param>
        /// <param name="key">A key to access the specified resource.</param>
        /// <param name="result">An instance of the requested resource..</param>
        /// <param name="targetType">Optional type constraint for the requested resource.</param>
        /// <returns><see langword="true"/> if the resource have been found and returned successfully; otherwise <see langword="false"/>.</returns>
        public static bool TryGetResource(this IResourceRegistry resources, string key, [NotNullWhen(true)] out object? result, Type? targetType = null)
        {
            if (resources.ContainsKey(key))
            {
                result = resources.GetResource(key, targetType);
                return true;
            }

            result = null;
            return false;
        }

        /// <summary>
        /// Tries to get the resource from the specified resource registry.
        /// </summary>
        /// <typeparam name="T">Type of the requested resource.</typeparam>
        /// <param name="resources">An instance of the <see cref="IResourceRegistry"/> to access the specified resource.</param>
        /// <param name="key">A key to access the specified resource.</param>
        /// <param name="result">An instance of the requested resource..</param>
        /// <returns><see langword="true"/> if the resource have been found and returned successfully; otherwise <see langword="false"/>.</returns>
        public static bool TryGetResource<T>(this IResourceRegistry resources, string key, [NotNullWhen(true)] out T? result)
            where T : class
        {
            if (resources.ContainsKey(key))
            {
                result = resources.GetResource<T>(key);
                return true;
            }

            result = null;
            return false;
        }
    }
}