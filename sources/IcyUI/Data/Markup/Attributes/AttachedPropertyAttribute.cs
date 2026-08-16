// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup.Attributes
{
    /// <summary>
    /// Specifies that a class contains attached property accessors.
    /// This attribute allows the registration of attached properties by providing
    /// the names of the getter and setter methods.
    /// </summary>
    /// <remarks>
    /// The <see cref="AttachedPropertyAttribute"/> can be applied to a class that defines
    /// static methods for getting and setting attached properties. The attribute
    /// requires the names of the getter and setter methods, which will be used
    /// during the property registration process.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class AttachedPropertyAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AttachedPropertyAttribute"/> class.
        /// </summary>
        /// <param name="getterName">The name of the static method that gets the attached property value.</param>
        /// <param name="setterName">The name of the static method that sets the attached property value.</param>
        public AttachedPropertyAttribute(string getterName, string setterName)
        {
            GetterName = getterName;
            SetterName = setterName;
        }

        /// <summary>
        /// Gets the name of the static method that gets the attached property value.
        /// </summary>
        public string GetterName { get; }

        /// <summary>
        /// Gets the name of the static method that sets the attached property value.
        /// </summary>
        public string SetterName { get; }

        /// <summary>
        /// Gets or sets the name of the attached property.
        /// </summary>
        /// <remarks>
        /// This property can be used to specify a custom name for the attached property
        /// that may differ from the method names.
        /// </remarks>
        public string? PropertyName { get; set; }

        /// <summary>
        /// Gets or sets the name of the callback function for property validation.
        /// </summary>
        public string? ValidationCallback { get; set; }
    }
}