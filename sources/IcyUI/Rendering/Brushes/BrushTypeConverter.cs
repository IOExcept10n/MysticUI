// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Globalization;

namespace Icy.Rendering.Brushes
{
    /// <summary>
    /// Converts a colour written as text into a <see cref="SolidColorBrush"/>, so brush-valued properties can be set
    /// from a string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attached to <see cref="IBrush"/> itself, which is what lets
    /// <see cref="Data.TypeConversionManager"/> - and therefore markup - handle
    /// <c>Background="#FF262628"</c> without registering anything. The colour text is whatever
    /// <see cref="System.Drawing.ColorConverter"/> accepts: a named colour (<c>WhiteSmoke</c>), <c>#RGB</c>,
    /// <c>#RRGGBB</c>, or <c>#AARRGGBB</c>.
    /// </para>
    /// <para>
    /// Only solid colours are covered. An <see cref="ImageBrush"/> or <see cref="NinePatchImageBrush"/> needs an
    /// asset reference rather than a literal, so those are set as property elements or from code instead.
    /// </para>
    /// </remarks>
    public class BrushTypeConverter : TypeConverter
    {
        private static readonly ColorConverter Colors = new();

        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
            sourceType == typeof(string) || sourceType == typeof(Color) || base.CanConvertFrom(context, sourceType);

        /// <inheritdoc/>
        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            return value switch
            {
                Color color => new SolidColorBrush(color),
                string text => new SolidColorBrush((Color)Colors.ConvertFromString(context, culture ?? CultureInfo.InvariantCulture, text)!),
                _ => base.ConvertFrom(context, culture, value),
            };
        }
    }
}
