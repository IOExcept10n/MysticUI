// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Globalization;

namespace Icy.Data.Bindings
{
    /// <summary>
    /// Gets the parameters for the binding value conversion.
    /// </summary>
    public class BindingConverterParameters
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BindingConverterParameters"/> class.
        /// </summary>
        /// <param name="converter">The converter to convert binding transferred data.</param>
        /// <param name="culture">The culture for the conversion.</param>
        public BindingConverterParameters(TypeConverter converter, CultureInfo? culture = null)
        {
            Converter = converter;
            Culture = culture ?? CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// Gets the converter instance to convert binding transferred data.
        /// </summary>
        public TypeConverter Converter { get; init; }

        /// <summary>
        /// Gets the culture for the conversion.
        /// </summary>
        public CultureInfo Culture { get; init; }
    }
}
