namespace MysticUI.Extensions
{
    /// <summary>
    /// Indicates property to represent type of the instance of the object that should be created.
    /// </summary>
    /// <example>
    /// For example, if you have an abstract class but want to make it extensible to allow users make their own types to extend your abstract type,
    /// you can use this attribute on the property that should indicate custom type of created object.
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class InstanceTypeAttribute : Attribute
    {
    }

    /// <summary>
    /// Indicates the property that is used as a content for the control.
    /// </summary>
    /// <remarks>
    /// Any object can't hold more than one content property because all complex tags
    /// that doesn't detected as properties are saved into the content property.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ContentAttribute : Attribute
    {
    }
}