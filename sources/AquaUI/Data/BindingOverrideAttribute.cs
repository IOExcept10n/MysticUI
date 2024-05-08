namespace AquaUI.Data
{
    /// <summary>
    /// Indicates that the property can override binding parameters according to its behavior.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class BindingOverrideAttribute : Attribute
    {
        /// <summary>
        /// Get the binding mode that should be used instead of custom when handle this property.
        /// </summary>
        public BindingMode ModeOverride { get; }

        /// <summary>
        /// Indicates that the property can override binding parameters according to its behavior.
        /// </summary>
        /// <param name="mode">The binding mode that should be used instead of custom when handle this property.</param>
        public BindingOverrideAttribute(BindingMode mode)
        {
            ModeOverride = mode;
        }
    }
}