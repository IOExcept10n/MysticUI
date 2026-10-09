// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;

namespace Icy.Data
{
    /// <summary>
    /// The values a numeric property accepts, as declared by
    /// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/>.
    /// </summary>
    /// <param name="Min">The lower bound; <see cref="double.NegativeInfinity"/> for none.</param>
    /// <param name="Max">The upper bound; <see cref="double.PositiveInfinity"/> for none.</param>
    /// <param name="MinIsExclusive">Whether <paramref name="Min"/> itself is outside the range.</param>
    /// <param name="MaxIsExclusive">Whether <paramref name="Max"/> itself is outside the range.</param>
    /// <remarks>
    /// An infinite bound means the range is open on that side. Infinite and NaN values are never inside a range: "open"
    /// means no limit on finite values.
    /// </remarks>
    public readonly record struct ValueRange(double Min, double Max, bool MinIsExclusive, bool MaxIsExclusive)
    {
        /// <summary>
        /// Gets a value indicating whether both bounds are finite, so the range can drive a slider.
        /// </summary>
        public bool IsBounded => double.IsFinite(Min) && double.IsFinite(Max);

        /// <summary>
        /// Determines whether <paramref name="value"/> is inside the range.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns><see langword="true"/> when the value is finite and satisfies both bounds.</returns>
        public bool Contains(double value)
        {
            if (!double.IsFinite(value))
                return false;

            bool aboveMin = MinIsExclusive ? value > Min : value >= Min;
            bool belowMax = MaxIsExclusive ? value < Max : value <= Max;
            return aboveMin && belowMax;
        }

        /// <summary>
        /// Describes the range as a user-facing rule, such as "Must be greater than 0.".
        /// </summary>
        /// <returns>An English sentence with numbers in the invariant culture.</returns>
        public string Describe()
        {
            bool hasMin = double.IsFinite(Min);
            bool hasMax = double.IsFinite(Max);
            string min = Min.ToString(CultureInfo.InvariantCulture);
            string max = Max.ToString(CultureInfo.InvariantCulture);
            string lower = MinIsExclusive ? $"greater than {min}" : $"at least {min}";
            string upper = MaxIsExclusive ? $"less than {max}" : $"at most {max}";

            return (hasMin, hasMax) switch
            {
                (true, true) when !MinIsExclusive && !MaxIsExclusive => $"Must be between {min} and {max}.",
                (true, true) => $"Must be {lower} and {upper}.",
                (true, false) => $"Must be {lower}.",
                (false, true) => $"Must be {upper}.",
                _ => "Must be a finite number.",
            };
        }
    }
}
