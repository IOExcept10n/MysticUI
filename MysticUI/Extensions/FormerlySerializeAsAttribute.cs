namespace MysticUI.Extensions
{
    /// <summary>
    /// Indicates that the property with this attribute was serialized using following name.
    /// </summary>
    /// <remarks>
    /// This could be needed if your API has changed but you still want to support older API versions.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class FormerlySerializeAsAttribute : Attribute
    {
        /// <summary>
        /// Previous serialized name for this property.
        /// </summary>
        public string OldName { get; }

        /// <summary>
        /// Indicates that the property with this attribute was serialized using following name.
        /// </summary>
        /// <param name="oldName">Old name of the property.</param>
        public FormerlySerializeAsAttribute(string oldName)
        {
            OldName = oldName;
        }
    }
}