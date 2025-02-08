// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;

namespace Icy.Assets
{
    /// <summary>
    /// Represents a service for the UI resources storage.
    /// </summary>
    public interface IResourceRegistry : IDictionary<string, object>
    {
        /// <summary>
        /// Gets the preferred culture of the loaded resources.
        /// </summary>
        CultureInfo PreferredCulture { get; }

        /// <summary>
        /// Gets the resource related to the specified <paramref name="key"/>.
        /// </summary>
        /// <param name="key">Key to access the specified resource.</param>
        /// <param name="targetType">Type of the requested resource.</param>
        /// <returns>The requested resource.</returns>
        /// <exception cref="KeyNotFoundException">Occurs when the requested resource not found.</exception>
        /// <exception cref="ArgumentException">Occurs when the requested resource type doesn't match the <paramref name="targetType"/>.</exception>
        object GetResource(string key, Type? targetType = null);

        /// <summary>
        /// Gets the resource related to the specified <paramref name="key"/>.
        /// </summary>
        /// <typeparam name="T">Type of the requested resource.</typeparam>
        /// <param name="key">Key to access the specified resource.</param>
        /// <returns>The requested resource instance.</returns>
        /// <exception cref="KeyNotFoundException">Occurs when the requested resource not found.</exception>
        /// <exception cref="ArgumentException">Occurs when the requested resource type doesn't match the <typeparamref name="T"/> type.</exception>
        T GetResource<T>(string key)
            where T : class;

        /// <summary>
        /// Gets an instance of the resource to be used in case when according resource couldn't be found.
        /// </summary>
        /// <typeparam name="T">Type of the requested resource.</typeparam>
        /// <returns>An instance of <typeparamref name="T"/> to be used as a fallback resource for type <typeparamref name="T"/>.</returns>
        T? GetFallbackResource<T>()
            where T : class;

        /// <summary>
        /// Gets an instance of the resource to be used in case when according resource couldn't be found.
        /// </summary>
        /// <param name="requestedType">Type of the requested resource.</param>
        /// <returns>An instance of <paramref name="requestedType"/> to be used as a fallback resource for type <paramref name="requestedType"/>.</returns>
        object? GetFallbackResource(Type requestedType);

        /// <summary>
        /// Sets an instance of the resource of type <typeparamref name="T"/> for the case when according resource couldn't be found.
        /// </summary>
        /// <param name="instance">An instance of <typeparamref name="T"/> to be used as a fallback resource for type <typeparamref name="T"/>.</param>
        /// <typeparam name="T">Type of the requested resource.</typeparam>
        void SetFallbackResource<T>(T instance)
            where T : class;
    }
}
