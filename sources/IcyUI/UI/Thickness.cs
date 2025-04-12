using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Icy.UI
{
    /// <summary>
    /// Represents the same base struct for both paddings and margins.
    /// </summary>
    [DataContract]
    [Serializable]
    public struct Thickness :
        IEquatable<Thickness>,
        IFormattable,
        IParsable<Thickness>,
        IEqualityOperators<Thickness, Thickness, bool>,
        IAdditionOperators<Thickness, Thickness, Thickness>,
        ISubtractionOperators<Thickness, Thickness, Thickness>
    {
        /// <summary>
        /// Default format to convert the thickness.
        /// </summary>
        public const string DefaultFormat = "left top right bottom";

        /// <summary>
        /// The zero (default) thickness.
        /// </summary>
        public static readonly Thickness Zero = default;

        /// <summary>
        /// The <see langword="bottom"/> side of the thickness.
        /// </summary>
        public int Bottom;

        /// <summary>
        /// The <see langword="left"/> side of the thickness.
        /// </summary>
        public int Left;

        /// <summary>
        /// The <see langword="right"/> side of the thickness.
        /// </summary>
        public int Right;

        /// <summary>
        /// The <see langword="top"/> side of the thickness.
        /// </summary>
        public int Top;

        /// <summary>
        /// Initializes a new instance of the <see cref="Thickness"/> struct with all components.
        /// </summary>
        /// <param name="left"><see cref="Left"/> side.</param>
        /// <param name="top"><see cref="Top"/> side.</param>
        /// <param name="right"><see cref="Right"/> side.</param>
        /// <param name="bottom"><see cref="Bottom"/> side.</param>
        public Thickness(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Thickness"/> struct from the horizontal and vertical components.
        /// </summary>
        /// <param name="horizontal">Uniform horizontal value.</param>
        /// <param name="vertical">Uniform vertical value.</param>
        public Thickness(int horizontal, int vertical)
            : this(horizontal, vertical, horizontal, vertical)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Thickness"/> struct with uniform sides.
        /// </summary>
        /// <param name="uniform">Value of one side.</param>
        public Thickness(int uniform)
            : this(uniform, uniform, uniform, uniform)
        {
        }

        /// <summary>
        /// Gets total height of the thickness.
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public readonly int Height => Top + Bottom;

        /// <summary>
        /// Gets a value indicating whether the thickness is uniform (all sides are equal).
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public readonly bool IsUniform => Top == Bottom && Bottom == Right && Right == Left;

        /// <summary>
        /// Gets total width of the thickness.
        /// </summary>
        [XmlIgnore]
        [JsonIgnore]
        public readonly int Width => Right + Left;

        /// <summary>
        /// Subtracts the <see cref="Thickness"/> from the <see cref="Rectangle"/>.
        /// </summary>
        /// <param name="area">A <seealso cref="Rectangle"/> to subtract padding.</param>
        /// <param name="padding">A <seealso cref="Thickness"/> to remove.</param>
        /// <returns>New rectangle whose size is subtracted by <paramref name="padding"/>'s side values.</returns>
        public static Rectangle operator -(Rectangle area, Thickness padding)
        {
            var result = area;
            result.X += padding.Left;
            result.Y += padding.Top;

            result.Width -= padding.Width;
            if (result.Width < 0)
            {
                result.Width = 0;
            }

            result.Height -= padding.Height;
            if (result.Height < 0)
            {
                result.Height = 0;
            }

            return result;
        }

        /// <inheritdoc cref="ISubtractionOperators{TSelf, TOther, TResult}.operator-"/>
        public static Thickness operator -(Thickness left, Thickness right) =>
            new(left.Left - right.Left, left.Top - right.Top, left.Right - right.Right, left.Bottom - right.Bottom);

        /// <inheritdoc cref="IUnaryNegationOperators{TSelf, TResult}.op_UnaryNegation"/>
        public static Thickness operator -(Thickness value) =>
            new(-value.Left, -value.Top, -value.Right, -value.Bottom);

        /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.operator!="/>
        public static bool operator !=(Thickness left, Thickness right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Adds the <see cref="Thickness"/> to the <see cref="Rectangle"/>.
        /// </summary>
        /// <param name="area">A <seealso cref="Rectangle"/> to add margin.</param>
        /// <param name="margin">A <seealso cref="Thickness"/> to apply.</param>
        /// <returns>New rectangle whose size is extended by <paramref name="margin"/>'s side values.</returns>
        public static Rectangle operator +(Rectangle area, Thickness margin)
        {
            var result = area;
            result.X -= margin.Left;
            result.Y -= margin.Top;

            result.Width += margin.Width;
            if (result.Width < 0)
            {
                result.Width = 0;
            }

            result.Height += margin.Height;
            if (result.Height < 0)
            {
                result.Height = 0;
            }

            return result;
        }

        /// <inheritdoc cref="IAdditionOperators{TSelf, TOther, TResult}.operator+"/>
        public static Thickness operator +(Thickness left, Thickness right) =>
            new(left.Left + right.Left, left.Top + right.Top, left.Right + right.Right, left.Bottom + right.Bottom);

        /// <inheritdoc cref="IEqualityOperators{TSelf, TOther, TResult}.operator=="/>
        public static bool operator ==(Thickness left, Thickness right)
        {
            return left.Equals(right);
        }

        /// <inheritdoc cref="IParsable{TSelf}.Parse(string, IFormatProvider?)"/>
        public static Thickness Parse(string s, IFormatProvider? provider)
        {
            if (string.IsNullOrWhiteSpace(s)) return default;
            var parts = s.Split(' ', ',', ';');
            if (parts.Length == 1) return new(int.Parse(parts[0], provider));
            if (parts.Length == 2) return new(int.Parse(parts[0], provider), int.Parse(parts[1], provider));
            if (parts.Length == 4)
            {
                return new(
                    int.Parse(parts[0], provider),
                    int.Parse(parts[1], provider),
                    int.Parse(parts[2], provider),
                    int.Parse(parts[3], provider));
            }

            return default;
        }

        /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)"/>
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Thickness result)
        {
            result = default;
            if (string.IsNullOrEmpty(s))
                return false;
            var parts = s.Split(' ', ',', ';');
            if (parts.Length == 1)
            {
                if (int.TryParse(s, out int uniform))
                {
                    result = new(uniform);
                    return true;
                }
            }
            else if (parts.Length == 2)
            {
                if (int.TryParse(parts[0], out int horizontal) && int.TryParse(parts[1], out int vertical))
                {
                    result = new(horizontal, vertical);
                    return true;
                }
            }
            else if (parts.Length == 4)
            {
                if (int.TryParse(parts[0], out int left) &&
                    int.TryParse(parts[1], out int top) &&
                    int.TryParse(parts[2], out int right) &&
                    int.TryParse(parts[3], out int bottom))
                {
                    result = new(left, top, right, bottom);
                    return true;
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj)
        {
            return obj is Thickness thickness && Equals(thickness);
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals(Thickness other)
        {
            return Left == other.Left &&
                   Top == other.Top &&
                   Right == other.Right &&
                   Bottom == other.Bottom;
        }

        /// <inheritdoc/>
        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Right, Bottom);
        }

        /// <inheritdoc/>
        public override readonly string ToString()
        {
            if (IsUniform)
                return Left.ToString();
            if (Left == Right && Top == Bottom)
                return $"{Left} {Top}";
            return $"{Left} {Top} {Right} {Bottom}";
        }

        /// <inheritdoc/>
        public readonly string ToString(string? format, IFormatProvider? formatProvider)
        {
            if (string.IsNullOrWhiteSpace(format))
                format = DefaultFormat;
            formatProvider ??= CultureInfo.CurrentCulture;

            // Replace fully-qualified identifiers
            var result = format.Replace("left", Left.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("top", Top.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("right", Right.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("bottom", Bottom.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase);

            // After that replace short-qualified identifiers
            return result
                .Replace("l", Left.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("t", Top.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("r", Right.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("b", Bottom.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase);
        }
    }
}
