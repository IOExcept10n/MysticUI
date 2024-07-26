// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Data.Bindings.Attributes
{
    /// <summary>
    /// Indicates that the property can override binding parameters according to its behavior.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class BindingOverrideAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BindingOverrideAttribute"/> class.
        /// </summary>
        /// <param name="mode">The binding mode that should be used instead of custom when handle this property.</param>
        public BindingOverrideAttribute(BindingMode mode) => ModeOverride = mode;

        /// <summary>
        /// Gets the binding mode that should be used instead of custom when handle this property.
        /// </summary>
        public BindingMode ModeOverride { get; }
    }
}