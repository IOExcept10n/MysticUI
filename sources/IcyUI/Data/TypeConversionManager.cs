// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Icy.Data
{
    /// <summary>
    /// Represents a unified type conversion helper.
    /// </summary>
    public class TypeConversionManager : ITypeConverter
    {
        private readonly Dictionary<(Type SourceType, Type TargetType), Func<object, object>> convertersCache = [];
        private readonly Dictionary<(Type SourceType, Type TargetType), IValueConverter> customConverters = [];

        /// <summary>
        /// Registers the custom type converter to handle custom types conversion.
        /// </summary>
        /// <typeparam name="TSource">Source type to convert from.</typeparam>
        /// <typeparam name="TTarget">Target type to convert to.</typeparam>
        /// <param name="converter">The converter instance to register.</param>
        public void RegisterConverter<TSource, TTarget>(IValueConverter<TSource, TTarget> converter)
        {
            RegisterConverter(typeof(TSource), typeof(TTarget), converter);
        }

        /// <summary>
        /// Registers the type converter using runtime types definitions.
        /// </summary>
        /// <param name="from">Source type to convert from.</param>
        /// <param name="to">Target type to convert to.</param>
        /// <param name="converter">The converter instance to register.</param>
        public void RegisterConverter(Type from, Type to, IValueConverter converter)
        {
            customConverters[(from, to)] = converter;
        }

        /// <summary>
        /// Converts specified value to the specified target type.
        /// </summary>
        /// <param name="value">Value to convert.</param>
        /// <param name="targetType">Type to convert to.</param>
        /// <returns>Converted value.</returns>
        /// <exception cref="InvalidCastException">Occurs when the type conversion between <paramref name="value"/> type and <paramref name="targetType"/> is not supported.</exception>
        [return: NotNullIfNotNull(nameof(value))]
        public object? Convert(object? value, Type targetType)
        {
            if (value == null)
            {
                if (targetType.IsValueType && !targetType.IsNullable())
                    throw new InvalidCastException("Cannot convert null to a value type.");
                return null;
            }

            var sourceType = value.GetType();
            var cacheKey = (sourceType, targetType);

            if (convertersCache.TryGetValue(cacheKey, out var converter))
            {
                return converter(value);
            }

            converter = CreateConverter(sourceType, targetType);
            convertersCache[cacheKey] = converter;
            return converter(value);
        }

        private static bool IsCastMethod(MethodInfo method, Type sourceType, Type targetType)
        {
            return (method.Name == "op_Implicit" || method.Name == "op_Explicit") &&
                method.ReturnType.IsAssignableTo(targetType) &&
                method.GetParameters()[0].ParameterType.IsAssignableFrom(sourceType);
        }

        private static bool TryGetCastMethod(Type sourceType, Type targetType, [NotNullWhen(true)] out Func<object, object>? castMethod)
        {
            var method = sourceType.GetMethods(BindingFlags.Public | BindingFlags.Static).FirstOrDefault(x => IsCastMethod(x, sourceType, targetType));
            if (method != null)
            {
                var paramExpression = Expression.Parameter(typeof(object), "value");
                var castExpression = Expression.TypeAs(paramExpression, sourceType);
                var operatorExpression = Expression.Call(method, castExpression);
                castMethod = Expression.Lambda<Func<object, object>>(operatorExpression, paramExpression).Compile();
                return true;
            }

            castMethod = null;
            return false;
        }

        private static bool TryGetParseMethod(Type targetType, [NotNullWhen(true)] out Func<object, object>? parseMethod)
        {
            // Special case for enums
            if (targetType.IsEnum)
            {
                parseMethod = value => Enum.Parse(targetType, value.ToString()!);
                return true;
            }

            var method = targetType.GetMethod("Parse", [typeof(string), typeof(IFormatProvider)])!;
            if (method == null)
            {
                parseMethod = null;
                return false;
            }

            var paramExpression = Expression.Parameter(typeof(object), "value");
            var castExpression = Expression.TypeAs(paramExpression, typeof(string));
            var cultureInfo = Expression.Property(null, typeof(CultureInfo).GetProperty(nameof(CultureInfo.InvariantCulture))!);
            var parseExpression = Expression.Call(method, castExpression, cultureInfo);
            var convertExpression = Expression.TypeAs(parseExpression, typeof(object));
            parseMethod = Expression.Lambda<Func<object, object>>(convertExpression, paramExpression).Compile();
            return true;
        }

        /// <summary>
        /// Creates a converter function that supports conversion between <paramref name="sourceType"/> and <paramref name="targetType"/>.
        /// </summary>
        /// <remarks>
        /// The conversion priority is following:
        /// <list type="number">
        /// <item><b>Direct conversion</b> – supported between objects of the same type or from child type to parent.</item>
        /// <item><b><see cref="IValueConverter"/> registered conversion</b> – user can add his own way to convert between specified types.</item>
        /// <item><b>Cast conversion</b> – Tries to convert using defined cast methods, either explicit or implicit.</item>
        /// <item>
        /// <b>Primitive type conversion</b> – if value implements <see cref="IConvertible"/> interface and target type allows it,
        /// performs conversion using <see cref="Convert.ChangeType(object?, Type)"/>.
        /// </item>
        /// <item><b>To string conversion</b> – Performs <see cref="object.ToString"/> call if the target type is string.</item>
        /// <item>
        /// <b>Parsing</b> – if value implements <see cref="IParsable{TSelf}"/> interface
        /// or have <see cref="IParsable{TSelf}.Parse(string, IFormatProvider?)"/> method, performs parsing.
        /// </item>
        /// <item><b><see cref="TypeConverter"/> conversion</b> – gets the appropriate <see cref="TypeConverter"/> and tries conversion with it.</item>
        /// </list>
        /// The selected conversion way is cached and then used for each conversion between these two types (in only one way).
        /// </remarks>
        /// <param name="sourceType">The source type to convert value from.</param>
        /// <param name="targetType">The target type to convert value to.</param>
        /// <returns>A delegate that takes value of <paramref name="sourceType"/> as an argument and returns value of the <paramref name="targetType"/> as a result.</returns>
        /// <exception cref="InvalidCastException">Occurs when the conversion between types is not supported.</exception>
        private Func<object, object> CreateConverter(Type sourceType, Type targetType)
        {
            try
            {
                if (targetType.IsNullable())
                {
                    Type underlyingType = targetType.GenericTypeArguments[0];
                    if (sourceType == typeof(string))
                    {
                        return value =>
                        {
                            if (value is string s && string.Equals(s, "null", StringComparison.InvariantCultureIgnoreCase))
                            {
                                return null!;
                            }

                            return Convert(value, underlyingType);
                        };
                    }

                    targetType = underlyingType;
                }

                if (targetType.IsAssignableFrom(sourceType))
                {
                    return value => value;
                }

                if (customConverters.TryGetValue((sourceType, targetType), out var converter))
                {
                    return converter.Convert;
                }

                if (TryGetCastMethod(sourceType, targetType, out var castMethod))
                {
                    return castMethod;
                }

                if (targetType.GetInterface(nameof(IConvertible)) != null && targetType.IsPrimitive)
                {
                    return value => System.Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                }

                if (targetType == typeof(string))
                {
                    return value => value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value.ToString()!;
                }

                if (sourceType == typeof(string) && TryGetParseMethod(targetType, out var parseMethod))
                {
                    return parseMethod;
                }

                var typeConverter = TypeDescriptor.GetConverter(targetType);
                if (typeConverter.CanConvertFrom(sourceType))
                {
                    return value => typeConverter.ConvertFrom(null, CultureInfo.InvariantCulture, value)!;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidCastException($"Can't convert from {sourceType} to {targetType}.", ex);
            }

            throw new InvalidCastException($"Can't find any converters between {sourceType} and {targetType}.");
        }
    }
}
