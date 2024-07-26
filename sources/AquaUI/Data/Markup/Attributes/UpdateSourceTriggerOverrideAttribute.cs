// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Data.Markup.Attributes
{
    /// <summary>
    /// Provides information about default update source trigger for the specified property.
    /// </summary>
    public class UpdateSourceTriggerOverrideAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateSourceTriggerOverrideAttribute"/> class.
        /// </summary>
        /// <param name="updateSourceTrigger">The default update source trigger for the property.</param>
        public UpdateSourceTriggerOverrideAttribute(UpdateSourceTrigger updateSourceTrigger)
        {
            UpdateSourceTrigger = updateSourceTrigger;
        }

        /// <summary>
        /// Gets the default update source trigger for the property.
        /// </summary>
        public UpdateSourceTrigger UpdateSourceTrigger { get; }
    }
}