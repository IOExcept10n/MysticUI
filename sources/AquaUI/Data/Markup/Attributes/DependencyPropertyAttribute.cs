// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Data.Markup.Attributes
{
    /// <summary>
    /// Represents an attribute for the property that can be used in a markup dependencies system.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class DependencyPropertyAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the validation callback method. Method signature should match the <see cref="ValidateValueCallback"/> delegate.
        /// </summary>
        public string? ValidationCallback { get; set; }

        /// <summary>
        /// Gets or sets the name of the property update callback method.
        /// </summary>
        public string? UpdateCallback { get; set; }
    }
}