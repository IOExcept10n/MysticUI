// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup.Attributes
{
    /// <summary>
    /// Represents an attribute for the property that should automatically generate <see cref="IPropertyReference"/> at the type initialization.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class RegisterReferenceAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets the name of the validation callback method. Method signature should match the <see cref="ValidateValueCallback"/> delegate.
        /// </summary>
        public string? ValidationCallback { get; set; }
    }
}
