// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Animations;
using Icy.Data.Markup;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Coasts a distance over a duration with constant deceleration (an <see cref="Easing.EaseOutQuad"/> curve), applying each
    /// frame's step as a relative delta.
    /// </summary>
    /// <remarks>
    /// Relative steps keep offset corrections made by others during the coast (e.g. a virtualized list's height corrections).
    /// The driver stops by itself at the end, or as soon as a non-zero step no longer moves anything (the edge).
    /// </remarks>
    /// <param name="apply">Applies one step; returns whether anything moved.</param>
    internal sealed class InertiaDriver(Func<Vector2, bool> apply) : IFrameTicker
    {
        private Dispatcher? dispatcher;
        private Vector2 distance;
        private TimeSpan duration;
        private TimeSpan elapsed;
        private float lastEased;

        /// <summary>
        /// Gets a value indicating whether a coast is running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Starts a coast, replacing any running one.
        /// </summary>
        /// <param name="distance">The total distance to travel.</param>
        /// <param name="duration">The time the coast takes.</param>
        public void Start(Vector2 distance, TimeSpan duration)
        {
            Stop();
            this.distance = distance;
            this.duration = duration;
            elapsed = TimeSpan.Zero;
            lastEased = 0f;
            IsRunning = true;
            dispatcher = Dispatcher.GetCurrentThreadDispatcher();
            dispatcher.RegisterFrameTicker(this);
        }

        /// <summary>
        /// Stops the running coast, if any.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
                return;
            IsRunning = false;
            dispatcher?.UnregisterFrameTicker(this);
        }

        /// <inheritdoc/>
        public void Tick(TimeSpan delta)
        {
            if (!IsRunning)
                return;

            elapsed += delta;
            float progress = duration <= TimeSpan.Zero ? 1f : MathF.Min(1f, (float)(elapsed / duration));
            float eased = Easing.EaseOutQuad(progress);
            Vector2 step = distance * (eased - lastEased);
            lastEased = eased;

            bool moved = apply(step);
            if (progress >= 1f || (!moved && step != Vector2.Zero))
                Stop();
        }
    }
}
