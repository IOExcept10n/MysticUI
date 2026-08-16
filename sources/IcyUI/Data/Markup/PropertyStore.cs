// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Default <see cref="IPropertyStore{T}"/> implementation. Populates itself by scanning <typeparamref name="T"/>
    /// for <see cref="Attributes.RegisterReferenceAttribute"/>-marked properties and <see cref="Attributes.AttachedPropertyAttribute"/>-marked
    /// attached-property accessors the first time it is created, and walks <typeparamref name="T"/>'s base types for
    /// inherited-property lookups.
    /// </summary>
    /// <typeparam name="T">The type this store holds property references for.</typeparam>
    internal sealed class PropertyStore<T> : IPropertyStore<T>
    {
        private readonly Dictionary<string, IPropertyReference> properties;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyStore{T}"/> class, resolving <typeparamref name="T"/>'s
        /// own registered and attached properties immediately.
        /// </summary>
        public PropertyStore()
        {
            properties = PropertyReferencesRegistration.ResolveProperties(typeof(T)).ToDictionary(p => p.Name);
        }

        /// <inheritdoc/>
        public IEnumerable<IPropertyReference> EnumerateProperties(bool includeInherited = true)
        {
            foreach (IPropertyReference property in properties.Values)
                yield return property;

            if (!includeInherited)
                yield break;

            IPropertyStore? baseStore = GetBaseStore();
            if (baseStore == null)
                yield break;

            foreach (IPropertyReference property in baseStore.EnumerateProperties(true))
            {
                if (!properties.ContainsKey(property.Name))
                    yield return property;
            }
        }

        /// <inheritdoc/>
        public IPropertyReference GetProperty(string name, bool searchInherited = true) =>
            TryGetProperty(name, searchInherited, out IPropertyReference? property)
                ? property
                : throw new KeyNotFoundException($"Property '{name}' isn't registered for type '{typeof(T)}'.");

        /// <inheritdoc/>
        public IPropertyReference<TValue> GetProperty<TValue>(string name, bool searchInherited = true) =>
            TryGetProperty(name, searchInherited, out IPropertyReference<TValue>? property)
                ? property
                : throw new KeyNotFoundException($"Property '{name}' of type '{typeof(TValue)}' isn't registered for type '{typeof(T)}'.");

        /// <inheritdoc/>
        public IPropertyReference RegisterAttached(string name, Type propertyType, PropertyMetadata metadata, ValidateValueCallback? validationCallback = null)
        {
            Type referenceType = typeof(SyntheticPropertyReference<>).MakeGenericType(propertyType);
            var reference = (IPropertyReference)Activator.CreateInstance(
                referenceType,
                [MakeStorageKey(name), name, string.Empty, UIPropertyMetadata.CreateAttached(metadata), validationCallback])!;
            properties[name] = reference;
            return reference;
        }

        /// <inheritdoc/>
        public IPropertyReference<TValue> RegisterAttached<TValue>(string name, PropertyMetadata metadata, ValidateValueCallback? validationCallback = null)
        {
            var reference = new SyntheticPropertyReference<TValue>(MakeStorageKey(name), name, string.Empty, UIPropertyMetadata.CreateAttached(metadata), validationCallback);
            properties[name] = reference;
            return reference;
        }

        /// <inheritdoc/>
        public IPropertyReference<TValue> RegisterProperty<TValue>(string name, PropertyMetadata metadata, ValidateValueCallback? validationCallback = null)
        {
            var reference = new SyntheticPropertyReference<TValue>(MakeStorageKey(name), name, string.Empty, metadata, validationCallback);
            properties[name] = reference;
            return reference;
        }

        /// <inheritdoc/>
        public IPropertyReference RegisterProperty(string name, Type propertyType, PropertyMetadata metadata, ValidateValueCallback? validationCallback = null)
        {
            Type referenceType = typeof(SyntheticPropertyReference<>).MakeGenericType(propertyType);
            var reference = (IPropertyReference)Activator.CreateInstance(
                referenceType,
                [MakeStorageKey(name), name, string.Empty, metadata, validationCallback])!;
            properties[name] = reference;
            return reference;
        }

        /// <inheritdoc/>
        public bool TryGetProperty(string name, bool searchInherited, [NotNullWhen(true)] out IPropertyReference? property)
        {
            if (properties.TryGetValue(name, out property))
                return true;

            if (searchInherited && GetBaseStore() is IPropertyStore baseStore)
                return baseStore.TryGetProperty(name, true, out property);

            property = null;
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetProperty(string name, [NotNullWhen(true)] out IPropertyReference? property) =>
            TryGetProperty(name, true, out property);

        /// <inheritdoc/>
        public bool TryGetProperty<TValue>(string name, bool searchInherited, [NotNullWhen(true)] out IPropertyReference<TValue>? property)
        {
            if (TryGetProperty(name, searchInherited, out IPropertyReference? found) && found is IPropertyReference<TValue> typed)
            {
                property = typed;
                return true;
            }

            property = null;
            return false;
        }

        /// <inheritdoc/>
        public bool TryGetProperty<TValue>(string name, [NotNullWhen(true)] out IPropertyReference<TValue>? property) =>
            TryGetProperty(name, true, out property);

        private static string MakeStorageKey(string name) => $"{typeof(T).FullName}.{name}";

        private static IPropertyStore? GetBaseStore()
        {
            Type? baseType = typeof(T).BaseType;
            return baseType == null ? null : PropertyRegistry.Instance.GetPropertyStore(baseType);
        }
    }
}
