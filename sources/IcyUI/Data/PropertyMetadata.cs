// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;

namespace Icy.Data
{
    /// <summary>
    /// Provides the annotated metadata for properties.
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
        public PropertyMetadata(object? defaultValue)
        {
            DefaultValue = defaultValue;
        }

        /// <summary>
        /// Gets the default metadata for any property.
        /// </summary>
        public static PropertyMetadata Default { get; } = new();

        /// <summary>
        /// Gets the default value for this property, if specified.
        /// </summary>
        public object? DefaultValue { get; }
    }
}
