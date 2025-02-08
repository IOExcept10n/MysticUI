// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;

namespace Icy.Data
{
    /// <summary>
    /// Represents a service that helps with generalized type conversion between all .NET types.
    /// </summary>
    public interface ITypeConverter
    {
        /// <summary>
        /// Converts specified value to the specified target type.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="targetType">Type to convert to.</param>
        /// <returns>Converted value.</returns>
        /// <exception cref="InvalidCastException">Occurs when the type conversion between <paramref name="value"/> type and <paramref name="targetType"/> is not supported.</exception>
        [return: NotNullIfNotNull(nameof(value))]
        object? Convert(object? value, Type targetType);

        /// <summary>
        /// Registers the custom type converter to handle custom types conversion.
        /// </summary>
        /// <typeparam name="TSource">Source type to convert from.</typeparam>
        /// <typeparam name="TTarget">Target type to convert to.</typeparam>
        /// <param name="converter">The converter instance to register.</param>
        void RegisterConverter<TSource, TTarget>(IValueConverter<TSource, TTarget> converter);

        /// <summary>
        /// Registers the type converter using runtime types definitions.
        /// </summary>
        /// <param name="from">Source type to convert from.</param>
        /// <param name="to">Target type to convert to.</param>
        /// <param name="converter">The converter instance to register.</param>
        void RegisterConverter(Type from, Type to, IValueConverter converter);
    }
}