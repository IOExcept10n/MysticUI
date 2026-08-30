// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Icy.Animations
{
    /// <summary>
    /// Converts the name of one of <see cref="Easing"/>'s standard functions, written as text, into the matching
    /// <see cref="EasingFunction"/>, so easing-valued properties can be set from a string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attached to <see cref="EasingFunction"/> itself, which is what lets <see cref="Data.TypeConversionManager"/> -
    /// and therefore markup - handle <c>Easing="EaseOutCubic"</c> without registering anything. The name is looked
    /// up, case-insensitively, against <see cref="Easing"/>'s public static properties (<c>Linear</c>,
    /// <c>EaseInQuad</c>, <c>EaseOutQuad</c>, <c>EaseInOutQuad</c>, <c>EaseInCubic</c>, <c>EaseOutCubic</c>,
    /// <c>EaseInOutCubic</c>).
    /// </para>
    /// <para>
    /// Only the standard named functions declared on <see cref="Easing"/> are covered. A custom
    /// <see cref="EasingFunction"/> needs to be assigned from code instead, since an arbitrary function has no
    /// textual representation.
    /// </para>
    /// </remarks>
    public class EasingFunctionTypeConverter : TypeConverter
    {
        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            if (value is string text)
            {
                PropertyInfo? property = typeof(Easing).GetProperty(
                    text,
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
                if (property != null && property.PropertyType == typeof(EasingFunction))
                {
                    return (EasingFunction?)property.GetValue(null);
                }
            }

            return base.ConvertFrom(context, culture, value);
        }
    }
}
