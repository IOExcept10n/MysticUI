// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents a storage for indexed property references.
    /// </summary>
    public interface IPropertyStore
    {
        /// <summary>
        /// Gets the type associated with the property store.
        /// </summary>
        Type TargetType { get; }

        /// <summary>
        /// Enumerates all property references stored in the property store.
        /// </summary>
        /// <param name="includeInherited">Indicates whether to include inherited properties.</param>
        /// <returns>An enumerable collection of property references.</returns>
        IEnumerable<IPropertyReference> EnumerateProperties(bool includeInherited = true);

        /// <summary>
        /// Retrieves a property reference by name.
        /// </summary>
        /// <param name="name">The name of the property to retrieve.</param>
        /// <param name="searchInherited">Indicates whether to search inherited properties.</param>
        /// <returns>The property reference associated with the specified name, or <see langword="null"/> if not found.</returns>
        IPropertyReference GetProperty(string name, bool searchInherited = true);

        /// <summary>
        /// Retrieves a strongly-typed property reference by name.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value.</typeparam>
        /// <param name="name">The name of the property to retrieve.</param>
        /// <param name="searchInherited">Indicates whether to search inherited properties.</param>
        /// <returns>The strongly-typed property reference associated with the specified name, or <see langword="null"/> if not found.</returns>
        IPropertyReference<TValue> GetProperty<TValue>(string name, bool searchInherited = true);

        /// <summary>
        /// Registers an attached property with the specified name and type.
        /// </summary>
        /// <param name="name">The name of the attached property.</param>
        /// <param name="propertyType">The type of the attached property.</param>
        /// <param name="metadata">Metadata associated with the property.</param>
        /// <param name="validationCallback">An optional callback for validating property values.</param>
        /// <returns>The registered property reference.</returns>
        IPropertyReference RegisterAttached(string name, Type propertyType, PropertyMetadata metadata, ValidateValueCallback? validationCallback = null);

        /// <summary>
        /// Registers a strongly-typed attached property with the specified name.
        /// </summary>
        /// <typeparam name="TValue">The type of the attached property value.</typeparam>
        /// <param name="name">The name of the attached property.</param>
        /// <param name="metadata">Metadata associated with the property.</param>
        /// <param name="validationCallback">An optional callback for validating property values.</param>
        /// <returns>The registered strongly-typed property reference.</returns>
        IPropertyReference<TValue> RegisterAttached<TValue>(string name, PropertyMetadata metadata, ValidateValueCallback? validationCallback = null);

        /// <summary>
        /// Registers a property with the specified name and type.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value.</typeparam>
        /// <param name="name">The name of the property.</param>
        /// <param name="metadata">Metadata associated with the property.</param>
        /// <param name="validationCallback">An optional callback for validating property values.</param>
        /// <returns>The registered strongly-typed property reference.</returns>
        IPropertyReference<TValue> RegisterProperty<TValue>(string name, PropertyMetadata metadata, ValidateValueCallback? validationCallback = null);

        /// <summary>
        /// Registers a property with the specified name and type.
        /// </summary>
        /// <param name="name">The name of the property.</param>
        /// <param name="propertyType">The type of the property.</param>
        /// <param name="metadata">Metadata associated with the property.</param>
        /// <param name="validationCallback">An optional callback for validating property values.</param>
        /// <returns>The registered property reference.</returns>
        IPropertyReference RegisterProperty(string name, Type propertyType, PropertyMetadata metadata, ValidateValueCallback? validationCallback = null);

        /// <summary>
        /// Attempts to retrieve a property reference by name.
        /// </summary>
        /// <param name="name">The name of the property to retrieve.</param>
        /// <param name="searchInherited">Indicates whether to search inherited properties.</param>
        /// <param name="property">When this method returns, contains the property reference associated with the specified name, or <see langword="null"/> if not found.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        bool TryGetProperty(string name, bool searchInherited, [NotNullWhen(true)] out IPropertyReference? property);

        /// <summary>
        /// Attempts to retrieve a property reference by name.
        /// </summary>
        /// <param name="name">The name of the property to retrieve.</param>
        /// <param name="property">When this method returns, contains the property reference associated with the specified name, or <see langword="null"/> if not found.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        bool TryGetProperty(string name, [NotNullWhen(true)] out IPropertyReference? property);

        /// <summary>
        /// Attempts to retrieve a strongly-typed property reference by name.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value.</typeparam>
        /// <param name="name">The name of the property to retrieve.</param>
        /// <param name="searchInherited">Indicates whether to search inherited properties.</param>
        /// <param name="property">When this method returns, contains the strongly-typed property reference associated with the specified name, or <see langword="null"/> if not found.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        bool TryGetProperty<TValue>(string name, bool searchInherited, [NotNullWhen(true)] out IPropertyReference<TValue>? property);

        /// <summary>
        /// Attempts to retrieve a strongly-typed property reference by name.
        /// </summary>
        /// <typeparam name="TValue">The type of the property value.</typeparam>
        /// <param name="name">The name of the property to retrieve.</param>
        /// <param name="property">When this method returns, contains the strongly-typed property reference associated with the specified name, or <see langword="null"/> if not found.</param>
        /// <returns><see langword="true"/> if the property was found; otherwise, <see langword="false"/>.</returns>
        bool TryGetProperty<TValue>(string name, [NotNullWhen(true)] out IPropertyReference<TValue>? property);
    }

    /// <summary>
    /// Represents a storage for indexed property references for a specified type.
    /// </summary>
    /// <typeparam name="T">The type for which properties are stored.</typeparam>
    public interface IPropertyStore<T> : IPropertyStore
    {
        /// <summary>
        /// Gets the type associated with the property store.
        /// </summary>
        Type IPropertyStore.TargetType => typeof(T);
    }
}