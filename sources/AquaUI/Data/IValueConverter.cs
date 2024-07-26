using CommunityToolkit.Diagnostics;

namespace AquaUI.Data
{
    /// <summary>
    /// Represents an interface for the custom abstract converters that can convert values from the one type to another.
    /// </summary>
    public interface IValueConverter
    {
        /// <summary>
        /// Converts the value from the one type to another.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The result of conversion.</returns>
        object Convert(object value);
    }

    /// <inheritdoc cref="IValueConverter"/>
    /// <typeparam name="TSource">The source type to convert value from.</typeparam>
    /// <typeparam name="TTarget">The target type to convert value to.</typeparam>
    public interface IValueConverter<TSource, TTarget> : IValueConverter
    {
        /// <inheritdoc cref="IValueConverter.Convert(object)"/>
        TTarget Convert(TSource value);

        /// <inheritdoc/>
        object IValueConverter.Convert(object value)
        {
            if (value is TSource source)
                return Convert(source)!;
            return ThrowHelper.ThrowArgumentException<object>(nameof(value), "Argument type doesn't match the provided type.");
        }
    }
}
