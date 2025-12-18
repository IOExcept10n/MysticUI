// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Provides extensions for the property references system.
    /// </summary>
    public static class PropertyReferencesExtensions
    {
        /// <summary>
        /// Gets a value indicating whether this object contains registered reference to a specified property.
        /// </summary>
        /// <param name="dp">An object to test.</param>
        /// <param name="property">A property to check.</param>
        /// <returns><see langword="true"/> if the property is defined for either this type or its base types; <see langword="false"/> otherwise.</returns>
        public static bool ContainsProperty(this object dp, IPropertyReference property)
        {
            return dp.GetType().IsAssignableTo(property.OwnerType);
        }
    }
}
