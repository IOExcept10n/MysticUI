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
    public readonly record struct GridLength(float Value, GridUnitType UnitType)
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
    }
}
