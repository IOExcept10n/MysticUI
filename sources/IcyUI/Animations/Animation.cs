// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup;

namespace Icy.Animations
{
    /// <summary>
    /// Represents a single running (or stopped) instance of a <see cref="Animations.Timeline"/> applied to a target
    /// object's property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Drives its target property through the <see cref="PropertyValuePrecedence.Animation"/> precedence tier
    /// (see <see cref="IPropertyReference.SetTierValue(object, PropertyValuePrecedence, object?)"/>) rather than
    /// assigning the property directly, so a local assignment always still wins, and clearing this contribution
    /// (<see cref="Stop"/>) cleanly reverts to whatever the style/visual-state/local value was.
    /// </para>
    /// <para>
    /// Ticked once per frame by <see cref="Dispatcher.UpdateAnimations(TimeSpan)"/> (called from
    /// <see cref="UI.Canvas.Render"/>) once <see cref="Start"/> registers it - construct via
    /// <see cref="AnimationExtensions.Animate(UI.UIElement, Timeline)"/> for the common case of animating a
    /// <see cref="UI.UIElement"/> property.
    /// </para>
    /// </remarks>
    public class Animation
    {
        private readonly IPropertyReference property;
        private TimeSpan elapsed;
        private int completedPasses;
        private bool reversed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Animation"/> class for <paramref name="timeline"/> applied
        /// to <paramref name="target"/>.
        /// </summary>
        /// <param name="target">
        /// The object to animate. Must have a property named <see cref="Animations.Timeline.TargetProperty"/>
        /// registered with <see cref="PropertyRegistry"/> (e.g. via <see cref="Data.Markup.Attributes.RegisterReferenceAttribute"/>).
        /// </param>
        /// <param name="timeline">The animation to play.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="target"/>'s type has no property named <see cref="Animations.Timeline.TargetProperty"/> registered.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// The targeted property is marked with <see cref="Data.Markup.Attributes.NonAnimatableAttribute"/>.
        /// </exception>
        public Animation(object target, Timeline timeline)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(timeline);

            IPropertyStore store = PropertyRegistry.For(target).GetPropertyStore(target.GetType());
            if (!store.TryGetProperty(timeline.TargetProperty, out IPropertyReference? reference))
            {
                ThrowHelper.ThrowArgumentException(
                    nameof(timeline),
                    $"'{target.GetType()}' has no property named '{timeline.TargetProperty}' registered with {nameof(PropertyRegistry)}.");
            }

            if (reference.Metadata is UIPropertyMetadata { IsAnimationProhibited: true })
            {
                ThrowHelper.ThrowInvalidOperationException(
                    $"Property '{timeline.TargetProperty}' on '{target.GetType()}' is marked non-animatable and cannot be driven by an {nameof(Animation)}.");
            }

            Target = target;
            Timeline = timeline;
            property = reference;
        }

        /// <summary>
        /// Occurs when this animation finishes playing every configured repeat on its own. Never raised by <see cref="Stop"/>.
        /// </summary>
        public event EventHandler? Completed;

        /// <summary>
        /// Gets a value indicating whether this animation is currently ticking (running and not <see cref="Pause"/>d).
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this animation is running but temporarily suspended via <see cref="Pause"/>.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>
        /// Gets the object this animation targets.
        /// </summary>
        public object Target { get; }

        /// <summary>
        /// Gets the <see cref="Animations.Timeline"/> this animation plays.
        /// </summary>
        public Timeline Timeline { get; }

        /// <summary>
        /// Starts (or restarts, if not currently running) this animation from the beginning.
        /// </summary>
        /// <remarks>A no-op if the animation is already running - call <see cref="Stop"/> first to restart mid-flight.</remarks>
        public void Start()
        {
            if (IsRunning)
                return;

            elapsed = TimeSpan.Zero;
            completedPasses = 0;
            reversed = false;
            IsRunning = true;
            IsPaused = false;
            Dispatcher.GetCurrentThreadDispatcher().RegisterAnimation(this);
            Apply(Timeline.Easing(0f));
        }

        /// <summary>
        /// Suspends ticking this animation in place, without losing its current position. Resume with <see cref="Resume"/>.
        /// </summary>
        public void Pause() => IsPaused = true;

        /// <summary>
        /// Resumes ticking a <see cref="Pause"/>d animation from where it left off. A no-op if not currently running.
        /// </summary>
        public void Resume() => IsPaused = false;

        /// <summary>
        /// Stops this animation immediately (whether running or already completed) and clears its contribution to
        /// the target property's <see cref="PropertyValuePrecedence.Animation"/> tier, reverting to whatever the
        /// next-highest precedence tier (or the local/default value) currently provides.
        /// </summary>
        public void Stop()
        {
            if (IsRunning)
            {
                IsRunning = false;
                IsPaused = false;
                Dispatcher.GetCurrentThreadDispatcher().UnregisterAnimation(this);
            }

            property.ClearTierValue(Target, PropertyValuePrecedence.Animation);
        }

        /// <summary>
        /// Advances this animation by <paramref name="delta"/> and re-applies its value at the new position.
        /// </summary>
        /// <param name="delta">The amount of time to advance by.</param>
        /// <remarks>
        /// Called once per frame, for every animation currently registered via <see cref="Start"/>, by
        /// <see cref="Dispatcher.UpdateAnimations(TimeSpan)"/> - exposed as <see langword="internal"/> rather than
        /// <see langword="private"/> so tests can drive an animation with fake time steps without a real
        /// <see cref="UI.Canvas"/>.
        /// </remarks>
        internal void Update(TimeSpan delta)
        {
            if (!IsRunning || IsPaused)
                return;

            elapsed += delta;
            bool infinite = Timeline.RepeatCount == Timeline.Forever;
            int totalPasses = infinite ? int.MaxValue : Timeline.RepeatCount * (Timeline.AutoReverse ? 2 : 1);

            while (elapsed >= Timeline.Duration)
            {
                completedPasses++;
                if (!infinite && completedPasses >= totalPasses)
                {
                    Apply(Timeline.Easing(reversed ? 0f : 1f));
                    IsRunning = false;
                    Dispatcher.GetCurrentThreadDispatcher().UnregisterAnimation(this);
                    Completed?.Invoke(this, EventArgs.Empty);
                    return;
                }

                elapsed -= Timeline.Duration;
                if (Timeline.AutoReverse)
                    reversed = !reversed;
            }

            float normalized = (float)(elapsed / Timeline.Duration);
            Apply(Timeline.Easing(reversed ? 1f - normalized : normalized));
        }

        private void Apply(float easedTime)
        {
            property.SetTierValue(Target, PropertyValuePrecedence.Animation, Evaluate(easedTime));
        }

        private object? Evaluate(float easedTime)
        {
            IReadOnlyList<AnimationKeyframe> keyframes = Timeline.Keyframes;
            if (keyframes.Count == 0)
                return null;
            if (keyframes.Count == 1)
                return keyframes[0].Value;

            AnimationKeyframe left = keyframes[0];
            AnimationKeyframe right = keyframes[^1];
            for (int i = 0; i < keyframes.Count - 1; i++)
            {
                if (easedTime >= keyframes[i].Offset && easedTime <= keyframes[i + 1].Offset)
                {
                    left = keyframes[i];
                    right = keyframes[i + 1];
                    break;
                }
            }

            float span = right.Offset - left.Offset;
            float localT = span > 0f ? (easedTime - left.Offset) / span : 1f;

            if (ValueInterpolator.TryLerp(left.Value, right.Value, localT, out object? interpolated))
                return interpolated;

            // Neither value is a type ValueInterpolator knows how to lerp (e.g. an enum or string) - hold the
            // earlier keyframe's value until playback reaches the later one, then snap.
            return localT >= 1f ? right.Value : left.Value;
        }
    }
}
