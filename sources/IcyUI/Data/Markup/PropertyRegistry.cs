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

        private readonly object gate = new();

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
        /// Retrieves the property store for a specific type, creating and registering the default reflection-based
        /// store for it on first request if none has been registered yet.
        /// </summary>
        /// <param name="type">The type for which to retrieve the property store.</param>
        /// <returns>The property store associated with the specified type.</returns>
        /// <remarks>
        /// Call <see cref="RegisterPropertyStore(IPropertyStore)"/> ahead of time for a type if it needs a custom
        /// <see cref="IPropertyStore"/> implementation instead of the default reflection-based one.
        /// </remarks>
        public IPropertyStore GetPropertyStore(Type type)
        {
            lock (gate)
            {
                if (!Contains(type))
                {
                    var store = (IPropertyStore)Activator.CreateInstance(typeof(PropertyStore<>).MakeGenericType(type))!;
                    Add(store);
                }

                return this[type];
            }
        }

        /// <inheritdoc/>
        protected override Type GetKeyForItem(IPropertyStore item) => item.TargetType;
    }
}