// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Data.Bindings;

namespace AquaUI.Data.Markup
{
    /// <summary>
    /// Represents an interface for the objects that take part in <see cref="IDependencyProperty"/> system.
    /// </summary>
    public interface IDependencyObject : IBindingTarget
    {
        /// <summary>
        /// Clears the local value of a property. The property to be cleared is specified by a <see cref="IDependencyProperty"/> identifier.
        /// </summary>
        /// <param name="property">A property whose value is to be cleared.</param>
        void ClearValue(IDependencyProperty property);

        /// <summary>
        /// Enumerates all the local values set to this object.
        /// </summary>
        /// <returns>Enumeration of pairs with properties and their local values.</returns>
        IEnumerable<KeyValuePair<IDependencyProperty, object?>> EnumerateLocalValues();

        /// <summary>
        /// Gets the value of the specified dependency property.
        /// </summary>
        /// <param name="property">The property to get value from.</param>
        /// <returns>The value of the property.</returns>
        object? GetValue(IDependencyProperty property);

        /// <summary>
        /// Sets the value to the specified property.
        /// </summary>
        /// <param name="property">The property to set value to.</param>
        /// <param name="value">The value to set.</param>
        void SetValue(IDependencyProperty property, object? value);
    }
}