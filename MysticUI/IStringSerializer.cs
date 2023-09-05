namespace MysticUI
{
    /// <summary>
    /// Provides the basic functionality to the classes that can parse different types from and to string.
    /// </summary>
    public interface IStringSerializer
    {
        /// <summary>
        /// Determines whether the serializer can convert the following type.
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns><see langword="true"/> if the serializer can serialize the given type.</returns>
        public bool CanParse(Type type);

        /// <summary>
        /// Parses the value from the string with converting it to the given type.
        /// </summary>
        /// <param name="targetType">A type to convert the string to.</param>
        /// <param name="value">The string value with an object contents.</param>
        /// <returns>A result of the value conversion.</returns>
        public object Parse(Type targetType, string value);

        /// <summary>
        /// Serializes the value into a string.
        /// </summary>
        /// <param name="value">A value to serialize to.</param>
        /// <returns>A string with object contents.</returns>
        public string Serialize(object value);
    }
}