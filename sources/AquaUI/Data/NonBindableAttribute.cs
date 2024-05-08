namespace AquaUI.Data
{
    /// <summary>
    /// Indicates that the property can't use property bindings.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class NonBindableAttribute : Attribute
    {
        /// <summary>
        /// Determines whether the property shouldn't be used in the source object for binding.
        /// </summary>
        public bool AsSource { get; set; } = true;

        /// <summary>
        /// Determines whether the property shouldn't be used in the target object for binding.
        /// </summary>
        public bool AsTarget { get; set; } = true;
    }
}