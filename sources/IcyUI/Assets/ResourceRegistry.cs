// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CommunityToolkit.Diagnostics;

namespace Icy.Assets
{
    /// <summary>
    /// Represents a default implementation of the <see cref="IResourceRegistry"/> interface.
    /// </summary>
    /// <param name="preferredCulture">The preferred culture to use resources with.</param>
    internal class ResourceRegistry(CultureInfo? preferredCulture = null) : IResourceRegistry
    {
        private readonly Dictionary<string, object> resources = [];
        private readonly Dictionary<Type, object> fallbackResources = [];

        /// <inheritdoc/>
        public int Count => resources.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => ((IDictionary)resources).IsReadOnly;

        /// <inheritdoc/>
        public ICollection<string> Keys => resources.Keys;

        /// <inheritdoc/>
        public CultureInfo PreferredCulture { get; } = preferredCulture ?? CultureInfo.CurrentUICulture;

        /// <inheritdoc/>
        public ICollection<object> Values => resources.Values;

        /// <inheritdoc/>
        public object this[string key] { get => resources[key]; set => resources[key] = value; }

        /// <inheritdoc/>
        public void Add(string key, object value) => resources.TryAdd(key, value);

        /// <inheritdoc/>
        public void Add(KeyValuePair<string, object> item) => resources.TryAdd(item.Key, item.Value);

        /// <inheritdoc/>
        public void Clear() => resources.Clear();

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<string, object> item) => resources.Contains(item);

        /// <inheritdoc/>
        public bool ContainsKey(string key) => resources.ContainsKey(key);

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => ((IDictionary)resources).CopyTo(array, arrayIndex);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => resources.GetEnumerator();

        /// <inheritdoc/>
        public object GetResource(string key, Type? targetType = null)
        {
            if (resources.TryGetValue(key, out var value))
            {
                if (!value.GetType().IsAssignableTo(targetType))
                {
                    return ThrowHelper.ThrowArgumentException<object>("Found value does not match the specified type.");
                }

                return value;
            }

            if (targetType != null)
            {
                var fallback = GetFallbackResource(targetType);
                if (fallback != null) return fallback;
            }

            return ThrowKeyNotFoundException<object>("Value with the specified key not found.");
        }

        /// <inheritdoc/>
        public T GetResource<T>(string key)
            where T : class
        {
            if (resources.TryGetValue(key, out var value))
            {
                return value as T ?? GetFallbackResource<T>() ?? ThrowHelper.ThrowArgumentException<T>("Found value does not match the specified type.");
            }

            return ThrowKeyNotFoundException<T>("Value with the specified key not found.");
        }

        /// <inheritdoc/>
        public T? GetFallbackResource<T>()
            where T : class => fallbackResources.TryGetValue(typeof(T), out var result) ? (T)result : null;

        /// <inheritdoc/>
        public object? GetFallbackResource(Type requestedType) => fallbackResources.TryGetValue(requestedType, out var result) ? result : null;

        /// <inheritdoc/>
        public void SetFallbackResource<T>(T instance)
            where T : class => fallbackResources.Add(typeof(T), instance);

        /// <inheritdoc/>
        public bool Remove(string key) => resources.Remove(key, out _);

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<string, object> item) => resources.Remove(item.Key);

        /// <inheritdoc/>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out object value) => resources.TryGetValue(key, out value);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)resources).GetEnumerator();

        [DoesNotReturn]
        private static T ThrowKeyNotFoundException<T>(string message) => throw new KeyNotFoundException(message);
    }
}