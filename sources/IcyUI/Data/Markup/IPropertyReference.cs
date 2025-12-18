// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel.DataAnnotations;

namespace Icy.Data.Markup
{
    /// <summary>
    /// Provides the delegate for the methods that perform <see cref="IPropertyReference"/> validation.
    /// </summary>
    /// <param name="value">Value to validate.</param>
    /// <returns>Result of the validation. If the property is valid, <see cref="ValidationResult.Success"/> is returned.</returns>
    public delegate ValidationResult? ValidateValueCallback(object value);

    /// <summary>
    /// Represents a reference to a property, providing metadata and access methods for getting, setting, and clearing the property value.
    /// </summary>
    public interface IPropertyReference
    {
        /// <summary>
        /// Gets the name of the category for the referenced property.
        /// </summary>
        string Category { get; }

        /// <summary>
        /// Gets the metadata for the referenced property.
        /// </summary>
        /// <remarks>
        /// This property provides access to additional information about the
        /// property, such as its attributes, default value, and other
        /// characteristics that may be relevant for reflection or
        /// property manipulation.
        /// </remarks>
        PropertyMetadata Metadata { get; }

        /// <summary>
        /// Gets the name of the referenced property.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the owner type of the referenced property.
        /// </summary>
        Type OwnerType { get; }

        /// <summary>
        /// Gets the type of the referenced property value.
        /// </summary>
        Type PropertyType { get; }

        /// <summary>
        /// Gets the registered callback for validating the value of the property reference.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property returns <see langword="null"/> for properties that do not have any validation methods registered.
        /// </para>
        /// <para>
        /// The validation callback should be invoked before setting a value to the property reference
        /// to ensure that the value is valid. Note that these validation callbacks are not called during
        /// property access; they are specifically intended for validating values set through property references.
        /// </para>
        /// </remarks>
        ValidateValueCallback? ValidationCallback { get; }

        /// <summary>
        /// Gets the value of the referenced property from the specified target object
        /// as an untyped object.
        /// </summary>
        /// <param name="target">The object from which to retrieve the property value.</param>
        /// <returns>The value of the referenced property as an object.</returns>
        object? GetRawValue(object target);

        /// <summary>
        /// Sets the value of the referenced property on the specified target from an untyped object.
        /// </summary>
        /// <param name="target">The object on which to set the property value.</param>
        /// <param name="value">The new value to assign to the property as an object.</param>
        void SetRawValue(object target, object? value);

        /// <summary>
        /// Clears the value of the referenced property on the specified target object.
        /// </summary>
        /// <param name="target">The object from which to clear the property value.</param>
        /// <remarks>
        /// <para>
        /// This method assumes that the <see cref="PropertyMetadata.DefaultValue"/> for the <see cref="Metadata"/>
        /// contains value possible to assign to the referenced property.
        /// </para>
        /// <para>
        /// This method removes the value of the property for the given instance of
        /// the owner type, effectively resetting it to its default state.
        /// </para>
        /// </remarks>
        void RawClearValue(object target) => SetRawValue(target, Metadata.DefaultValue);
    }

    /// <summary>
    /// Represents a strongly-typed reference to a property, providing methods
    /// for getting, setting, and clearing the property value for a specific type.
    /// </summary>
    /// <typeparam name="T">The type of the property value.</typeparam>
    public interface IPropertyReference<T> : IPropertyReference
    {
        /// <summary>
        /// Gets the value of the referenced property from the specified target object.
        /// </summary>
        /// <param name="target">The object from which to retrieve the property value.</param>
        /// <returns>The value of the referenced property.</returns>
        /// <remarks>
        /// This method retrieves the current value of the property for the given instance of the owner type.
        /// </remarks>
        T GetValue(object target);

        /// <summary>
        /// Sets the value of the referenced property on the specified target object.
        /// </summary>
        /// <param name="target">The object on which to set the property value.</param>
        /// <param name="value">The new value to assign to the property.</param>
        /// <remarks>
        /// This method updates the property value for the given instance of the owner type with the specified value.
        /// </remarks>
        void SetValue(object target, T value);

        /// <summary>
        /// Clears the value of the referenced property on the specified target object.
        /// </summary>
        /// <param name="target">The object from which to clear the property value.</param>
        /// <remarks>
        /// This method removes the value of the property for the given instance of
        /// the owner type, effectively resetting it to its default state.
        /// </remarks>
        void ClearValue(object target) => SetValue(target, (T)(Metadata.DefaultValue ?? default)!);
    }
}