// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;

namespace Icy.Animations
{
    /// <summary>
    /// Represents a single value a <see cref="Timeline"/> passes through at a normalized point in its playback.
    /// </summary>
    public readonly struct AnimationKeyframe
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AnimationKeyframe"/> struct.
        /// </summary>
        /// <param name="offset">The normalized position within the timeline, from 0 (start) to 1 (end).</param>
        /// <param name="value">The value at this position.</param>
        public AnimationKeyframe(float offset, object? value)
        {
            Guard.IsBetweenOrEqualTo(offset, 0f, 1f);
            Offset = offset;
            Value = value;
        }

        /// <summary>
        /// Gets the normalized position within the timeline this keyframe applies at, from 0 (start) to 1 (end).
        /// </summary>
        public float Offset { get; }

        /// <summary>
        /// Gets the value at <see cref="Offset"/>.
        /// </summary>
        public object? Value { get; }
    }
}
