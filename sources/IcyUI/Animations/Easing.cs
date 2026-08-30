// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;

namespace Icy.Animations
{
    /// <summary>
    /// Represents a function that reshapes a <see cref="Timeline"/>'s normalized playback position (0 at the
    /// start, 1 at the end) into the position actually used to evaluate its keyframes.
    /// </summary>
    /// <param name="normalizedTime">The linear playback position, from 0 (start) to 1 (end).</param>
    /// <returns>The reshaped position. Well-behaved easing functions return 0 for an input of 0 and 1 for an input of 1.</returns>
    [TypeConverter(typeof(EasingFunctionTypeConverter))]
    public delegate float EasingFunction(float normalizedTime);

    /// <summary>
    /// Provides a small set of standard <see cref="EasingFunction"/>s for use with <see cref="Timeline.Easing"/>.
    /// </summary>
    public static class Easing
    {
        /// <summary>
        /// Gets an easing function with no reshaping - playback position and progress advance at the same rate.
        /// </summary>
        public static EasingFunction Linear { get; } = static t => t;

        /// <summary>
        /// Gets an easing function that starts slow and accelerates towards the end.
        /// </summary>
        public static EasingFunction EaseInQuad { get; } = static t => t * t;

        /// <summary>
        /// Gets an easing function that starts fast and decelerates towards the end.
        /// </summary>
        public static EasingFunction EaseOutQuad { get; } = static t => 1f - ((1f - t) * (1f - t));

        /// <summary>
        /// Gets an easing function that accelerates through the first half and decelerates through the second.
        /// </summary>
        public static EasingFunction EaseInOutQuad { get; } = static t =>
            t < 0.5f ? 2f * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 2) / 2f);

        /// <summary>
        /// Gets a stronger-accelerating variant of <see cref="EaseInQuad"/>.
        /// </summary>
        public static EasingFunction EaseInCubic { get; } = static t => t * t * t;

        /// <summary>
        /// Gets a stronger-decelerating variant of <see cref="EaseOutQuad"/>.
        /// </summary>
        public static EasingFunction EaseOutCubic { get; } = static t => 1f - MathF.Pow(1f - t, 3);

        /// <summary>
        /// Gets a stronger variant of <see cref="EaseInOutQuad"/>.
        /// </summary>
        public static EasingFunction EaseInOutCubic { get; } = static t =>
            t < 0.5f ? 4f * t * t * t : 1f - (MathF.Pow((-2f * t) + 2f, 3) / 2f);
    }
}
