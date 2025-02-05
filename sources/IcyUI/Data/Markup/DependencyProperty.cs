// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents the default implementation of the <see cref="IDependencyProperty"/> interface.
    /// This class provides information about registered dependency properties.
    /// </summary>
    internal sealed class DependencyProperty : IDependencyProperty
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DependencyProperty"/> class.
        /// </summary>
        /// <param name="ownerType">The type that owes this property.</param>
        /// <param name="name">The name of the defined property.</param>
        /// <param name="propertyType">The type of the property values.</param>
        public DependencyProperty(Type ownerType, string name, Type propertyType)
        {
            OwnerType = ownerType;
            Name = name;
            PropertyType = propertyType;
        }

        /// <summary>
        /// Gets the property metadata provided when the property was registered.
        /// </summary>
        public PropertyMetadata Metadata { get; init; } = PropertyMetadata.Default;

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type that owes this property.
        /// </summary>
        public Type OwnerType { get; }

        /// <summary>
        /// Gets the type of the property values.
        /// </summary>
        public Type PropertyType { get; }

        /// <summary>
        /// Gets the method that is executed when property value is about to change to test if it is valid or not.
        /// </summary>
        public ValidateValueCallback? ValidationCallback { get; init; }
    }
}
