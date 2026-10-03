// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Estimates a pointer's velocity by a least-squares fit over its most recent samples.
    /// </summary>
    internal sealed class VelocityTracker
    {
        private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(100);
        private readonly List<(double Time, Vector2 Position)> samples = [];

        /// <summary>
        /// Records a position, discarding samples older than the 100 ms window.
        /// </summary>
        /// <param name="time">The recognizer clock.</param>
        /// <param name="position">The pointer position, in physical pixels.</param>
        public void Add(TimeSpan time, Point position)
        {
            double now = time.TotalSeconds;
            samples.Add((now, new Vector2(position.X, position.Y)));
            samples.RemoveAll(sample => now - sample.Time > Window.TotalSeconds);
        }

        /// <summary>
        /// Gets the velocity in physical pixels per second, or <see cref="Vector2.Zero"/> when the window holds fewer than
        /// two samples or no time span.
        /// </summary>
        /// <returns>The estimated velocity.</returns>
        public Vector2 GetVelocity()
        {
            if (samples.Count < 2)
                return Vector2.Zero;

            double meanTime = 0, meanX = 0, meanY = 0;
            foreach (var (time, position) in samples)
            {
                meanTime += time;
                meanX += position.X;
                meanY += position.Y;
            }

            meanTime /= samples.Count;
            meanX /= samples.Count;
            meanY /= samples.Count;

            double denominator = 0, numeratorX = 0, numeratorY = 0;
            foreach (var (time, position) in samples)
            {
                double dt = time - meanTime;
                denominator += dt * dt;
                numeratorX += dt * (position.X - meanX);
                numeratorY += dt * (position.Y - meanY);
            }

            return denominator < 1e-12 ? Vector2.Zero : new Vector2((float)(numeratorX / denominator), (float)(numeratorY / denominator));
        }
    }
}
