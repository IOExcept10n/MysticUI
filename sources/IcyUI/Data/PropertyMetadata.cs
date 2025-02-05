// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;

namespace Icy.Data
{
    /// <summary>
    /// Provides the default metadata for any kind of properties.
    /// </summary>
    public class PropertyMetadata
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMetadata"/> class.
        /// </summary>
        public PropertyMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyMetadata"/> class.
        /// </summary>
        /// <param name="defaultValue">Default property value.</param>
        /// <param name="propertyChangedCallback">The callback on the property value update.</param>
        public PropertyMetadata(object? defaultValue, PropertyChangedEventHandler? propertyChangedCallback)
        {
            DefaultValue = defaultValue;
            PropertyChangedCallback = propertyChangedCallback;
        }

        /// <summary>
        /// Gets the dedault metadata for any property.
        /// </summary>
        public static PropertyMetadata Default { get; } = new();

        /// <summary>
        /// Gets the default value for this property, if specified.
        /// </summary>
        public object? DefaultValue { get; }

        /// <summary>
        /// Gets the event callback on property change.
        /// </summary>
        public PropertyChangedEventHandler? PropertyChangedCallback { get; }
    }
}
