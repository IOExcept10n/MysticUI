// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents a collection that stores property references for the types registered in application.
    /// </summary>
    public class PropertyRegistry : KeyedCollection<Type, IPropertyStore>
    {
        private static readonly Lazy<PropertyRegistry> InstanceValue = new(() => []);

        // Private constructor to prevent instantiation
        private PropertyRegistry()
        {
        }

        /// <summary>
        /// Gets the singleton instance of the <see cref="PropertyRegistry"/> class.
        /// </summary>
        public static PropertyRegistry Instance => InstanceValue.Value;

        /// <summary>
        /// Registers a property store for a specific type.
        /// </summary>
        /// <param name="propertyStore">The property store to register.</param>
        public void RegisterPropertyStore(IPropertyStore propertyStore)
        {
            ArgumentNullException.ThrowIfNull(propertyStore);
            Add(propertyStore);
        }

        /// <summary>
        /// Retrieves the property store for a specific type.
        /// </summary>
        /// <param name="type">The type for which to retrieve the property store.</param>
        /// <returns>The property store associated with the specified type, or <see langword="null"/> if not found.</returns>
        public IPropertyStore? GetPropertyStore(Type type)
        {
            return Contains(type) ? this[type] : null;
        }

        /// <inheritdoc/>
        protected override Type GetKeyForItem(IPropertyStore item) => item.TargetType;
    }
}