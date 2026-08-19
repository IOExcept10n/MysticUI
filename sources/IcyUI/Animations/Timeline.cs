// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;

namespace Icy.Animations
{
    /// <summary>
    /// Describes how a single property should be animated: which property, over how long, through which
    /// <see cref="AnimationKeyframe"/> values, with which <see cref="EasingFunction"/>, and how many times to repeat.
    /// </summary>
    /// <remarks>
    /// A <see cref="Timeline"/> is a reusable description, not a running animation - construct an
    /// <see cref="Animation"/> from it (or call <see cref="AnimationExtensions.Animate(UI.UIElement, Timeline)"/>)
    /// to actually play it against a target object.
    /// </remarks>
    public class Timeline
    {
        /// <summary>
        /// A <see cref="RepeatCount"/> value indicating the timeline should repeat indefinitely until explicitly stopped.
        /// </summary>
        public const int Forever = -1;

        private readonly List<AnimationKeyframe> keyframes = [];
        private int repeatCount = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="Timeline"/> class.
        /// </summary>
        /// <param name="targetProperty">
        /// The name of the property to animate, as registered with <see cref="Data.Markup.PropertyRegistry"/>
        /// (see <see cref="Data.Markup.Attributes.RegisterReferenceAttribute"/>).
        /// </param>
        /// <param name="duration">How long one playback pass takes.</param>
        public Timeline(string targetProperty, TimeSpan duration)
        {
            Guard.IsNotNullOrWhiteSpace(targetProperty);
            if (duration <= TimeSpan.Zero)
                ThrowHelper.ThrowArgumentOutOfRangeException(nameof(duration), "Timeline duration must be positive.");
            TargetProperty = targetProperty;
            Duration = duration;
        }

        /// <summary>
        /// Gets the name of the property this timeline animates.
        /// </summary>
        public string TargetProperty { get; }

        /// <summary>
        /// Gets how long one playback pass takes.
        /// </summary>
        public TimeSpan Duration { get; }

        /// <summary>
        /// Gets the keyframes this timeline passes through, sorted ascending by <see cref="AnimationKeyframe.Offset"/>.
        /// </summary>
        /// <remarks>
        /// Between two keyframes whose values are both a type <see cref="Animation"/> knows how to interpolate
        /// (currently <see cref="float"/>, <see cref="double"/>, <see cref="int"/>,
        /// <see cref="System.Numerics.Vector2"/>/<see cref="System.Numerics.Vector3"/>/<see cref="System.Numerics.Vector4"/>,
        /// and <see cref="System.Drawing.Color"/>), the effective value is linearly interpolated between them. For
        /// any other type (e.g. an <see langword="enum"/> or <see langword="string"/>), the earlier keyframe's
        /// value holds until playback reaches the later one, which it then snaps to.
        /// </remarks>
        public IReadOnlyList<AnimationKeyframe> Keyframes => keyframes;

        /// <summary>
        /// Gets the easing function applied to the normalized playback position before evaluating <see cref="Keyframes"/>.
        /// </summary>
        public EasingFunction Easing { get; init; } = Animations.Easing.Linear;

        /// <summary>
        /// Gets how many times this timeline plays before stopping on its own, or <see cref="Forever"/> to repeat
        /// indefinitely until explicitly stopped.
        /// </summary>
        /// <remarks>
        /// With <see cref="AutoReverse"/> set, each repeat is one forward-then-backward round trip.
        /// </remarks>
        public int RepeatCount
        {
            get => repeatCount;
            init
            {
                if (value != Forever && value < 1)
                    ThrowHelper.ThrowArgumentOutOfRangeException(nameof(value), "RepeatCount must be Timeline.Forever or at least 1.");
                repeatCount = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether each repeat plays forward and then backward, instead of always forward.
        /// </summary>
        public bool AutoReverse { get; init; }

        /// <summary>
        /// Adds a keyframe to this timeline.
        /// </summary>
        /// <param name="offset">The normalized position within the timeline, from 0 (start) to 1 (end).</param>
        /// <param name="value">The value at this position.</param>
        /// <returns>This instance, for chaining.</returns>
        public Timeline AddKeyframe(float offset, object? value)
        {
            keyframes.Add(new AnimationKeyframe(offset, value));
            keyframes.Sort(static (a, b) => a.Offset.CompareTo(b.Offset));
            return this;
        }

        /// <summary>
        /// Creates a timeline that animates directly from one value to another - the common case, covering the vast
        /// majority of animations (a fade, a slide, a color transition).
        /// </summary>
        /// <param name="targetProperty">The name of the property to animate.</param>
        /// <param name="duration">How long one playback pass takes.</param>
        /// <param name="from">The value at the start of playback.</param>
        /// <param name="to">The value at the end of playback.</param>
        /// <param name="easing">The easing function to use, or <see langword="null"/> for <see cref="Animations.Easing.Linear"/>.</param>
        /// <param name="repeatCount">How many times to repeat - see <see cref="RepeatCount"/>.</param>
        /// <param name="autoReverse">Whether each repeat plays forward and then backward - see <see cref="AutoReverse"/>.</param>
        /// <returns>The new timeline, with <paramref name="from"/> and <paramref name="to"/> already added as its keyframes.</returns>
        public static Timeline FromTo(
            string targetProperty,
            TimeSpan duration,
            object? from,
            object? to,
            EasingFunction? easing = null,
            int repeatCount = 1,
            bool autoReverse = false)
        {
            var timeline = new Timeline(targetProperty, duration)
            {
                Easing = easing ?? Animations.Easing.Linear,
                RepeatCount = repeatCount,
                AutoReverse = autoReverse,
            };
            timeline.AddKeyframe(0f, from);
            timeline.AddKeyframe(1f, to);
            return timeline;
        }
    }
}
