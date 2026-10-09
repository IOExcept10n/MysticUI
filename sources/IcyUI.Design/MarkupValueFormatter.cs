// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Icy.Data;
using Icy.Design.Editor.Placement;
using Icy.Rendering.Brushes;
using Icy.UI;

namespace Icy.Design
{
    /// <summary>
    /// Writes property values as markup attribute text that loads back to the same value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each value gets a candidate text in the form people write by hand: invariant-culture numbers, enum names,
    /// <c>#AARRGGBB</c> colors, the shortest <see cref="Thickness"/> form, and comma-separated vector and size components.
    /// The candidate is then converted back with the configuration's <see cref="ITypeConverter"/>, and it's only accepted
    /// when that reproduces an equal value. A value with no verified form is refused, so an editor can show it read-only
    /// instead of writing markup that wouldn't load.
    /// </para>
    /// </remarks>
    public sealed class MarkupValueFormatter
    {
        private readonly ITypeConverter converter;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupValueFormatter"/> class.
        /// </summary>
        /// <param name="converter">The converter markup loads values with, usually <c>configuration.Types.TypeConverter</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="converter"/> is <see langword="null"/>.</exception>
        public MarkupValueFormatter(ITypeConverter converter)
        {
            ArgumentNullException.ThrowIfNull(converter);
            this.converter = converter;
        }

        /// <summary>
        /// Formats <paramref name="value"/> as markup text for a property of type <paramref name="type"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="type">The property's type.</param>
        /// <param name="text">The markup text, when this method returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when a text was found that loads back to an equal value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        public bool TryFormat(object? value, Type type, [NotNullWhen(true)] out string? text)
        {
            ArgumentNullException.ThrowIfNull(type);
            text = null;

            if (type == typeof(string))
            {
                text = (string?)value ?? string.Empty;
                return true;
            }

            if (value == null || Candidate(value) is not { } candidate)
                return false;

            if (!RoundTrips(candidate, value, type))
                return false;

            text = candidate;
            return true;
        }

        private static string? Candidate(object value) => value switch
        {
            bool b => b ? "True" : "False",
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            Enum e => e.ToString().Replace(" ", string.Empty, StringComparison.Ordinal),
            Thickness t => MarkupValues.Format(t),
            Color c => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}",
            SolidColorBrush brush => Candidate(brush.Color),
            Vector2 v => Join(v.X, v.Y),
            Vector3 v => Join(v.X, v.Y, v.Z),
            Vector4 v => Join(v.X, v.Y, v.Z, v.W),
            Point p => $"{p.X.ToString(CultureInfo.InvariantCulture)},{p.Y.ToString(CultureInfo.InvariantCulture)}",
            Size s => $"{s.Width.ToString(CultureInfo.InvariantCulture)},{s.Height.ToString(CultureInfo.InvariantCulture)}",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => null,
        };

        private static string Join(params float[] values) =>
            string.Join(",", values.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));

        private bool RoundTrips(string candidate, object value, Type type)
        {
            try
            {
                object? back = converter.Convert(candidate, type);
                return back is SolidColorBrush loaded && value is SolidColorBrush original
                    ? loaded.Color == original.Color
                    : Equals(back, value);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or ArgumentException or OverflowException or NotSupportedException)
            {
                return false;
            }
        }
    }
}
