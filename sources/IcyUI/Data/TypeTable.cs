// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Icy.Data
{
    /// <summary>
    /// Represents a hash table with type keys that can search best type in hierarchy to access the value.
    /// </summary>
    /// <typeparam name="T">Type of the values stored in a table.</typeparam>
    public class TypeTable<T> : IDictionary<Type, T>
    {
        private readonly Dictionary<Type, T> items = [];

        /// <inheritdoc/>
        public int Count => items.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => ((ICollection<KeyValuePair<Type, T>>)items).IsReadOnly;

        /// <inheritdoc/>
        public ICollection<Type> Keys => items.Keys;

        /// <inheritdoc/>
        public ICollection<T> Values => items.Values;

        /// <inheritdoc/>
        public T this[Type key]
        {
            get
            {
                if (TryGetValue(key, out T? value))
                    return value;
                return ThrowKeyNotFoundException($"No value found for type {key}");
            }

            set
            {
                items[key] = value;
            }
        }

        /// <inheritdoc/>
        public void Add(Type key, T value)
        {
            items.Add(key, value);
        }

        /// <inheritdoc/>
        public void Add(KeyValuePair<Type, T> item)
        {
            ((ICollection<KeyValuePair<Type, T>>)items).Add(item);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            items.Clear();
        }

        /// <inheritdoc/>
        public bool Contains(KeyValuePair<Type, T> item)
        {
            return items.Contains(item);
        }

        /// <inheritdoc/>
        public bool ContainsKey(Type key)
        {
            return items.ContainsKey(key);
        }

        /// <inheritdoc/>
        public void CopyTo(KeyValuePair<Type, T>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<Type, T>>)items).CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<Type, T>> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        /// <inheritdoc/>
        public bool Remove(Type key)
        {
            return items.Remove(key);
        }

        /// <inheritdoc/>
        public bool Remove(KeyValuePair<Type, T> item)
        {
            return ((ICollection<KeyValuePair<Type, T>>)items).Remove(item);
        }

        /// <inheritdoc/>
        public bool TryGetValue(Type key, [MaybeNullWhen(false)] out T value)
        {
            // Check for exact match
            if (items.TryGetValue(key, out value))
            {
                return true;
            }

            // If not, find in hierarchy
            Type? baseType = key.BaseType;
            while (baseType != null)
            {
                if (items.TryGetValue(baseType, out value))
                {
                    return true;
                }

                baseType = baseType.BaseType;
            }

            // If not found, search through interfaces
            foreach (var iface in key.GetInterfaces())
            {
                if (items.TryGetValue(iface, out value))
                {
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)items).GetEnumerator();
        }

        private static T ThrowKeyNotFoundException(string message) => throw new KeyNotFoundException(message);
    }
}