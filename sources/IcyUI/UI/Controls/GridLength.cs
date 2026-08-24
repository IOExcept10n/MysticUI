// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// Specifies how a <see cref="GridLength"/>'s <see cref="GridLength.Value"/> should be interpreted.
    /// </summary>
    public enum GridUnitType
    {
        /// <summary>
        /// The track sizes to the largest desired size among the children placed in it. <see cref="GridLength.Value"/> is ignored.
        /// </summary>
        Auto,

        /// <summary>
        /// The track has a fixed size, in pixels, given by <see cref="GridLength.Value"/>.
        /// </summary>
        Pixel,

        /// <summary>
        /// The track takes a proportional share of the space remaining after <see cref="Auto"/>/<see cref="Pixel"/>
        /// tracks are sized, weighted by <see cref="GridLength.Value"/> relative to other <see cref="Star"/> tracks.
        /// </summary>
        Star,
    }

    /// <summary>
    /// Specifies the size of a <see cref="Grid"/> row or column.
    /// </summary>
    /// <param name="Value">
    /// The size, interpreted per <paramref name="UnitType"/> - pixels for <see cref="GridUnitType.Pixel"/>, a
    /// proportional weight for <see cref="GridUnitType.Star"/>, or ignored for <see cref="GridUnitType.Auto"/>.
    /// </param>
    /// <param name="UnitType">How <paramref name="Value"/> should be interpreted.</param>
    public readonly record struct GridLength(float Value, GridUnitType UnitType) : IParsable<GridLength>
    {
        /// <summary>
        /// Gets a <see cref="GridLength"/> that sizes to the largest desired size among the track's children.
        /// </summary>
        public static GridLength Auto => new(0, GridUnitType.Auto);

        /// <summary>
        /// Gets a <see cref="GridLength"/> that takes an equal (weight <c>1</c>) proportional share of remaining space.
        /// </summary>
        public static GridLength Star => new(1, GridUnitType.Star);

        /// <summary>
        /// Implicitly converts a pixel size to a fixed <see cref="GridLength"/>.
        /// </summary>
        /// <param name="pixels">The fixed size, in pixels.</param>
        public static implicit operator GridLength(float pixels) => new(pixels, GridUnitType.Pixel);

        /// <summary>
        /// Creates a <see cref="GridLength"/> that takes a proportional share of remaining space, weighted relative
        /// to other <see cref="GridUnitType.Star"/> tracks.
        /// </summary>
        /// <param name="weight">The proportional weight.</param>
        /// <returns>A <see cref="GridUnitType.Star"/> <see cref="GridLength"/> with the given weight.</returns>
        public static GridLength StarWeighted(float weight) => new(weight, GridUnitType.Star);

        /// <summary>
        /// Parses a track size written the way markup writes it.
        /// </summary>
        /// <param name="s">
        /// <c>Auto</c>, <c>*</c>, a star weight such as <c>2*</c>, or a pixel count such as <c>24</c>.
        /// Case-insensitive for <c>Auto</c>.
        /// </param>
        /// <param name="provider">The format provider used for the numeric part.</param>
        /// <returns>The parsed track size.</returns>
        /// <exception cref="FormatException"><paramref name="s"/> is none of the accepted forms.</exception>
        /// <remarks>
        /// Implementing <see cref="IParsable{TSelf}"/> is what lets
        /// <see cref="Data.TypeConversionManager"/> - and therefore markup - turn
        /// <c>Width="*"</c> into a value with no bespoke converter registration.
        /// </remarks>
        public static GridLength Parse(string s, IFormatProvider? provider)
        {
            return TryParse(s, provider, out GridLength result)
                ? result
                : throw new FormatException($"'{s}' is not a valid {nameof(GridLength)}. Expected 'Auto', '*', a star weight like '2*', or a pixel count like '24'.");
        }

        /// <summary>
        /// Attempts to parse a track size written the way markup writes it.
        /// </summary>
        /// <param name="s">
        /// <c>Auto</c>, <c>*</c>, a star weight such as <c>2*</c>, or a pixel count such as <c>24</c>.
        /// </param>
        /// <param name="provider">The format provider used for the numeric part.</param>
        /// <param name="result">The parsed track size, or the default value when parsing failed.</param>
        /// <returns><see langword="true"/> when <paramref name="s"/> was parsed.</returns>
        public static bool TryParse(string? s, IFormatProvider? provider, out GridLength result)
        {
            result = default;
            if (string.IsNullOrWhiteSpace(s))
                return false;

            ReadOnlySpan<char> text = s.AsSpan().Trim();
            if (text.Equals(nameof(Auto), StringComparison.OrdinalIgnoreCase))
            {
                result = Auto;
                return true;
            }

            if (text[^1] == '*')
            {
                ReadOnlySpan<char> weight = text[..^1];
                if (weight.IsEmpty)
                {
                    result = Star;
                    return true;
                }

                if (!float.TryParse(weight, System.Globalization.NumberStyles.Float, provider, out float parsedWeight))
                    return false;

                result = StarWeighted(parsedWeight);
                return true;
            }

            if (!float.TryParse(text, System.Globalization.NumberStyles.Float, provider, out float pixels))
                return false;

            result = new GridLength(pixels, GridUnitType.Pixel);
            return true;
        }
    }
}
