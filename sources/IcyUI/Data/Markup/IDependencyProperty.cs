// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel.DataAnnotations;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Provides the delegate for the methods that perform <see cref="IDependencyProperty"/> validation.
    /// </summary>
    /// <param name="value">Value to validate.</param>
    /// <returns>Result of the validation. If the property is valid, <see cref="ValidationResult.Success"/> is returned.</returns>
    public delegate ValidationResult? ValidateValueCallback(object value);

    /// <summary>
    /// Represents an interface for the properties that can be set using specialized UI framework methods.
    /// </summary>
    public interface IDependencyProperty
    {
        /// <summary>
        /// Gets the metadata for the property.
        /// </summary>
        PropertyMetadata Metadata { get; }

        /// <summary>
        /// Gets the name of the property.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the owner type of the property.
        /// </summary>
        Type OwnerType { get; }

        /// <summary>
        /// Gets the type of the property value.
        /// </summary>
        Type PropertyType { get; }

        /// <summary>
        /// Gets the registered callback for the property value validation.
        /// This property returns <see langword="null"/> for all the properties that doesn't have validation methods regietered.
        /// </summary>
        ValidateValueCallback? ValidationCallback { get; }
    }
}
