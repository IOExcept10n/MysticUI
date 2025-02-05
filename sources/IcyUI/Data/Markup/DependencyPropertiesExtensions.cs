// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Provides extensions for the dependency properties system.
    /// </summary>
    public static class DependencyPropertiesExtensions
    {
        /// <summary>
        /// Gets a value indicating whether this dependency object contains specified property.
        /// </summary>
        /// <param name="dp">An object to test.</param>
        /// <param name="property">A property to check.</param>
        /// <returns><see langword="true"/> if the property is defined for either this type or its base types; <see langword="false"/> otherwise.</returns>
        public static bool ContainsProperty(this IDependencyObject dp, IDependencyProperty property)
        {
            return dp.GetType().IsAssignableTo(property.OwnerType);
        }

        /// <summary>
        /// Gets the value of the dependency object for specified property by its name.
        /// </summary>
        /// <param name="dp">The dependency object to access properties for.</param>
        /// <param name="propertyName">Name of the property to access.</param>
        /// <returns>The value of the specified property.</returns>
        public static object? GetValue(this IDependencyObject dp, string propertyName)
        {
            var property = DependencyPropertyRegistry.GetProperty(dp.GetType(), propertyName, true);
            return dp.GetValue(property);
        }

        /// <summary>
        /// Sets the value of the dependency object for the specified property by its name.
        /// </summary>
        /// <param name="dp">The dependency object to set value to.</param>
        /// <param name="propertyName">Name of the property to access.</param>
        /// <param name="value">Value to set.</param>
        public static void SetValue(this IDependencyObject dp, string propertyName, object? value)
        {
            var property = DependencyPropertyRegistry.GetProperty(dp.GetType(), propertyName, true);
            dp.SetValue(property, value);
        }
    }
}
