using Newtonsoft.Json;
using Stride.Core.Mathematics;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Xml.Serialization;

namespace MysticUI
{
    /// <summary>
    /// Represents the same base struct for both paddings and margins.
    /// </summary>
    [DataContract, Serializable]
    public struct Thickness : IEquatable<Thickness>, IFormattable, IEqualityComparer<Thickness>
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
        /// The <see langword="left"/> side of the thickness.
        /// </summary>
        public int Left;

        /// <summary>
        /// The <see langword="top"/> side of the thickness.
        /// </summary>
        public int Top;

        /// <summary>
        /// The <see langword="right"/> side of the thickness.
        /// </summary>
        public int Right;

        /// <summary>
        /// The <see langword="bottom"/> side of the thickness.
        /// </summary>
        public int Bottom;

        /// <summary>
        /// Total width of the thickness.
        /// </summary>
        [Browsable(false), XmlIgnore, JsonIgnore]
        public readonly int Width => Right + Left;

        /// <summary>
        /// Total height of the thickness.
        /// </summary>
        [Browsable(false), XmlIgnore, JsonIgnore]
        public readonly int Height => Top + Bottom;

        /// <summary>
        /// Indicates if the thickness is uniform (all sides are equal).
        /// </summary>
        [Browsable(false), XmlIgnore, JsonIgnore]
        public readonly bool IsUniform => Top == Bottom && Bottom == Right && Right == Left;

        /// <summary>
        /// Creates new thickness with all components.
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
        /// Creates new thickness from the horizontal and vertical components.
        /// </summary>
        /// <param name="horizontal">Uniform horizontal value.</param>
        /// <param name="vertical">Uniform vertical value.</param>
        public Thickness(int horizontal, int vertical) : this(horizontal, vertical, horizontal, vertical)
        {
        }

        /// <summary>
        /// Creates new thickness with uniform sides.
        /// </summary>
        /// <param name="uniform">Value of one side.</param>
        public Thickness(int uniform) : this(uniform, uniform, uniform, uniform)
        {
        }

        /// <summary>
        /// Parses the thickness from the string.
        /// </summary>
        /// <param name="s">String with saved thickness.</param>
        /// <returns>A <see cref="Thickness"/> with sides from the string.</returns>
        public static Thickness Parse(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return new();
            var parts = Array.ConvertAll(s.Split(), int.Parse);
            if (parts.Length == 1) return new(parts[0]);
            if (parts.Length == 2) return new(parts[0], parts[1]);
            if (parts.Length == 4) return new(parts[0], parts[1], parts[2], parts[3]);
            return new();
        }

        /// <inheritdoc/>
        public override readonly bool Equals(object? obj)
        {
            return obj is Thickness thickness && Equals(thickness);
        }

        /// <inheritdoc/>
        public override readonly int GetHashCode()
        {
            return HashCode.Combine(Left, Top, Right, Bottom);
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
        public readonly bool Equals(Thickness x, Thickness y)
        {
            return x.Equals(y);
        }

        /// <inheritdoc/>
        public readonly int GetHashCode([DisallowNull] Thickness obj)
        {
            return HashCode.Combine(obj.Left, obj.Top, obj.Right, obj.Bottom);
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
                .Replace("bottom", Bottom.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                // After that replace short-qualified identifiers
                .Replace("l", Left.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("t", Top.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("r", Right.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase)
                .Replace("b", Bottom.ToString(formatProvider), StringComparison.CurrentCultureIgnoreCase);
            return result;
        }

        /// <summary>
        /// Determines whether <paramref name="left"/> is equals to <paramref name="right"/>
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns><see langword="true"/> if first <see cref="Thickness"/> is equal to other, <see langword="false"/> otherwise.</returns>
        public static bool operator ==(Thickness left, Thickness right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether <paramref name="left"/> is <see langword="not"/> equals to <paramref name="right"/>
        /// </summary>
        /// <param name="left">Left operand.</param>
        /// <param name="right">Right operand.</param>
        /// <returns><see langword="false"/> if first <see cref="Thickness"/> is equal to other, <see langword="true"/> otherwise.</returns>
        public static bool operator !=(Thickness left, Thickness right)
        {
            return !(left == right);
        }

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

        /// <summary>
        /// Increases the <see cref="Thickness"/> with other <see cref="Thickness"/>.
        /// </summary>
        /// <param name="left">Left argument.</param>
        /// <param name="right">Right argument.</param>
        /// <returns></returns>
        public static Thickness operator +(Thickness left, Thickness right) =>
            new(left.Left + right.Left, left.Top + right.Top, left.Right + right.Right, left.Bottom + right.Bottom);

        /// <summary>
        /// Subtracts the <see cref="Thickness"/> with other <see cref="Thickness"/>.
        /// </summary>
        /// <param name="left">Left argument.</param>
        /// <param name="right">Right argument.</param>
        /// <returns></returns>
        public static Thickness operator -(Thickness left, Thickness right) =>
            new(left.Left - right.Left, left.Top - right.Top, left.Right - right.Right, left.Bottom - right.Bottom);

        /// <summary>
        /// Inverts the <see cref="Thickness"/>.
        /// </summary>
        /// <param name="value">Value to invert.</param>
        /// <returns>New <see cref="Thickness"/> with negative sides.</returns>
        public static Thickness operator -(Thickness value) =>
            new(-value.Left, -value.Top, -value.Right, -value.Bottom);
    }
}