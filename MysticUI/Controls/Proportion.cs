using CommunityToolkit.Diagnostics;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a base class for grid proportion definitions such as <seealso cref="RowDefinition"/> and <seealso cref="ColumnDefinition"/>
    /// </summary>
    public class ProportionDefinition
    {
        /// <summary>
        /// Occurs when a proportion value changes.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Raises the <see cref="Changed"/> event.
        /// </summary>
        protected void OnProportionChanged()
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Sets the width or height of the proportion depending on its type.
        /// </summary>
        /// <param name="length">New length to set.</param>
        protected internal virtual void SetValue(GridLength length) => throw new NotSupportedException();

        /// <summary>
        /// Gets the amount of the proportion.
        /// </summary>
        protected internal virtual float GetValueAmount() => throw new NotSupportedException();
    }

    /// <summary>
    /// Represents a definition of the row <see cref="Grid"/> proportion.
    /// </summary>
    public sealed class RowDefinition : ProportionDefinition
    {
        private GridLength height;

        /// <summary>
        /// Default auto proportion.
        /// </summary>
        public static RowDefinition Default => new();

        /// <summary>
        /// Height of the proportion.
        /// </summary>
        public GridLength Height
        {
            get => height;
            set
            {
                height = value;
                OnProportionChanged();
            }
        }

        /// <summary>
        /// Maximal proportion height.
        /// </summary>
        public int MaxHeight { get; set; }

        /// <summary>
        /// Minimal proportion height.
        /// </summary>
        public int MinHeight { get; set; }

        /// <inheritdoc/>
        protected internal override void SetValue(GridLength length)
        {
            Height = length;
        }

        /// <inheritdoc/>
        protected internal override float GetValueAmount()
        {
            return Height.Value;
        }
    }

    /// <summary>
    /// Represents a definition of the column <see cref="Grid"/> proportion.
    /// </summary>
    public sealed class ColumnDefinition : ProportionDefinition
    {
        private GridLength width;

        /// <summary>
        /// Default auto proportion.
        /// </summary>
        public static ColumnDefinition Default => new();

        /// <summary>
        /// Width of the proportion.
        /// </summary>
        public GridLength Width
        {
            get => width;
            set
            {
                width = value;
                OnProportionChanged();
            }
        }

        /// <summary>
        /// Maximal width of the proportion.
        /// </summary>
        public int MaxWidth { get; set; }

        /// <summary>
        /// Minimal width of the proportion.
        /// </summary>
        public int MinWidth { get; set; }

        /// <inheritdoc/>
        protected internal override void SetValue(GridLength length)
        {
            Width = length;
        }

        /// <inheritdoc/>
        protected internal override float GetValueAmount()
        {
            return Width.Value;
        }
    }

    /// <summary>
    /// Represents the length that explicitly support Star unit types.
    /// </summary>
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct GridLength : IEquatable<GridLength>, IEqualityComparer<GridLength>
    {
        /// <summary>
        /// Provides a <see cref="GridLength"/> with <see cref="GridUnitType"/> set to <see cref="GridUnitType.Auto"/>.
        /// </summary>
        public static GridLength Auto => new(0, GridUnitType.Auto);

        /// <summary>
        /// Provides a <see cref="GridLength"/> with <see cref="GridUnitType"/> set to <see cref="GridUnitType.Star"/> and value set to <see langword="1"/>.
        /// </summary>
        public static GridLength OneStar => new(1, GridUnitType.Star);

        /// <summary>
        /// Gets the associated <see cref="Controls.GridUnitType"/> for the <see cref="GridLength"/>.
        /// </summary>
        public readonly GridUnitType GridUnitType { get; }

        /// <summary>
        /// Gets a value that indicates whether the <see cref="GridLength"/> holds a value that is expressed in pixels.
        /// </summary>
        public readonly bool IsAbsolute => GridUnitType == GridUnitType.Pixel;

        /// <summary>
        /// Gets a value that indicates whether the <see cref="GridLength"/> holds a value whose size is determined by
        /// the size properties of the content object.
        /// </summary>
        public readonly bool IsAuto => GridUnitType == GridUnitType.Auto;

        /// <summary>
        /// Gets a value that indicates whether the <see cref="GridLength"/> holds a value that is expressed as
        /// a weighted proportion of available space.
        /// </summary>
        public readonly bool IsStar => GridUnitType == GridUnitType.Star;

        /// <summary>
        /// Gets a value of the <see cref="GridLength"/>
        /// </summary>
        public readonly float Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GridLength"/> using specified absolute value in pixels.
        /// </summary>
        /// <param name="value">Value to apply.</param>
        public GridLength(float value)
        {
            GridUnitType = GridUnitType.Pixel;
            Value = value;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GridLength"/> using specified value and value unit type.
        /// </summary>
        /// <param name="value">Value to set.</param>
        /// <param name="unitType">Type of grid unit to set.</param>
        public GridLength(float value, GridUnitType unitType)
        {
            GridUnitType = unitType;
            Value = value;
        }

        /// <summary>
        /// Reads a string value to convert it into a <see cref="GridLength"/>
        /// </summary>
        /// <param name="s">A string that contains a formatted <see cref="GridLength"/> value.</param>
        /// <returns>A value of a <see cref="GridLength"/> that is equivalent to a string.</returns>
        public static GridLength Parse(string s)
        {
            Guard.IsNotNull(s);
            s = s.Replace(" ", "");
            if (s.Equals("auto", StringComparison.InvariantCultureIgnoreCase))
            {
                return Auto;
            }
            else if (s == "*")
            {
                return OneStar;
            }
            else if (s.EndsWith("*"))
            {
                return new GridLength(int.Parse(s[..^1]), GridUnitType.Star);
            }
            else if (s.EndsWith("px"))
            {
                return new GridLength(int.Parse(s[..^2]));
            }
            else
            {
                return new GridLength(int.Parse(s));
            }
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is GridLength length &&
                   GridUnitType == length.GridUnitType &&
                   Value == length.Value;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(GridUnitType, Value);
        }

        /// <inheritdoc/>
        public bool Equals(GridLength other)
        {
            return GridUnitType == other.GridUnitType &&
                   Value == other.Value;
        }

        /// <inheritdoc/>
        public bool Equals(GridLength x, GridLength y)
        {
            return x.Equals(y);
        }

        /// <inheritdoc/>
        public int GetHashCode([DisallowNull] GridLength obj)
        {
            return HashCode.Combine(obj.GridUnitType, obj.Value);
        }

        /// <summary>
        /// Determines whether two <see cref="GridLength"/> values are equal.
        /// </summary>
        /// <param name="left">First <see cref="GridLength"/>.</param>
        /// <param name="right">Second <see cref="GridLength"/>.</param>
        /// <returns><see langword="true"/> if the <paramref name="left"/> is equal to the <paramref name="right"/>, <see langword="false"/> otherwise.</returns>
        public static bool operator ==(GridLength left, GridLength right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two <see cref="GridLength"/> values are not equal.
        /// </summary>
        /// <param name="left">First <see cref="GridLength"/>.</param>
        /// <param name="right">Second <see cref="GridLength"/>.</param>
        /// <returns><see langword="true"/> if the <paramref name="left"/> is not equal to the <paramref name="right"/>, <see langword="false"/> otherwise.</returns>
        public static bool operator !=(GridLength left, GridLength right)
        {
            return !(left == right);
        }

        private string GetDebuggerDisplay()
        {
            return IsAuto ? "Auto" : $"{Value}{(IsStar ? "*" : "px")}";
        }
    }

    /// <summary>
    /// Describes the kind of value that a <see cref="GridLength"/> object is holding.
    /// </summary>
    public enum GridUnitType
    {
        /// <summary>
        /// The size is determined by the size properties of the content object.
        /// </summary>
        Auto,

        /// <summary>
        /// The value is expressed as a pixel.
        /// </summary>
        Pixel,

        /// <summary>
        /// The value is expressed as a weighted proportion of available space.
        /// </summary>
        Star
    }
}