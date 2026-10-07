// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.UI
{
    /// <summary>
    /// The geometry of a directional focus move: picks the rectangle a press in one direction goes to.
    /// </summary>
    /// <remarks>
    /// Pure and screen-space. <see cref="Canvas"/> collects the candidates and their rectangles; this class only scores
    /// them (see the spatial navigation design spec).
    /// </remarks>
    internal static class SpatialNavigation
    {
        /// <summary>Snaps <paramref name="direction"/> to its dominant axis.</summary>
        /// <param name="direction">Any direction, UI space (+Y down).</param>
        /// <returns>
        /// One of the four unit vectors, or <see cref="Vector2.Zero"/> for a zero direction. An exact diagonal snaps
        /// horizontally.
        /// </returns>
        public static Vector2 Snap(Vector2 direction)
        {
            if (direction == Vector2.Zero)
                return Vector2.Zero;

            return MathF.Abs(direction.X) >= MathF.Abs(direction.Y)
                ? new Vector2(MathF.Sign(direction.X), 0)
                : new Vector2(0, MathF.Sign(direction.Y));
        }

        /// <summary>Finds the best candidate for a move from <paramref name="from"/> in <paramref name="direction"/>.</summary>
        /// <param name="from">The focused element's screen rectangle.</param>
        /// <param name="direction">A snapped direction (see <see cref="Snap(Vector2)"/>).</param>
        /// <param name="candidates">The candidates' screen rectangles, in Tab order.</param>
        /// <param name="tolerance">How far, in screen units, a candidate may overlap <paramref name="from"/> and still count as ahead.</param>
        /// <returns>The winning index, or <c>-1</c> when no candidate is ahead.</returns>
        /// <remarks>
        /// <list type="number">
        /// <item><description>A candidate must be ahead: its near edge at or past the far edge of <paramref name="from"/>, minus <paramref name="tolerance"/>.</description></item>
        /// <item><description>Candidates overlapping <paramref name="from"/> on the cross axis (in its beam) beat all others.</description></item>
        /// <item><description>Within a group, the lowest <c>major + 2 * minor</c> wins: the edge gap along the axis plus twice the cross-axis gap.</description></item>
        /// <item><description>Ties go to the closer centre, then to the earlier candidate.</description></item>
        /// </list>
        /// </remarks>
        public static int FindBest(Rectangle from, Vector2 direction, IReadOnlyList<Rectangle> candidates, float tolerance)
        {
            int best = -1;
            bool bestInBeam = false;
            float bestScore = 0;
            float bestCenter = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                Rectangle candidate = candidates[i];
                float major = MajorGap(from, candidate, direction);
                if (major < -tolerance)
                    continue;

                (float minor, bool inBeam) = CrossGap(from, candidate, direction);
                float score = Math.Max(0, major) + (2 * minor);
                float center = Vector2.DistanceSquared(Center(from), Center(candidate));
                if (best < 0 || IsBetter(inBeam, score, center, bestInBeam, bestScore, bestCenter))
                {
                    best = i;
                    bestInBeam = inBeam;
                    bestScore = score;
                    bestCenter = center;
                }
            }

            return best;
        }

        private static bool IsBetter(bool inBeam, float score, float center, bool bestInBeam, float bestScore, float bestCenter)
        {
            if (inBeam != bestInBeam)
                return inBeam;
            if (score != bestScore)
                return score < bestScore;
            return center < bestCenter;
        }

        private static float MajorGap(Rectangle from, Rectangle candidate, Vector2 direction) =>
            direction.X > 0 ? candidate.Left - from.Right
            : direction.X < 0 ? from.Left - candidate.Right
            : direction.Y > 0 ? candidate.Top - from.Bottom
            : from.Top - candidate.Bottom;

        private static (float Gap, bool InBeam) CrossGap(Rectangle from, Rectangle candidate, Vector2 direction)
        {
            (int fromStart, int fromEnd, int start, int end) = direction.X != 0
                ? (from.Top, from.Bottom, candidate.Top, candidate.Bottom)
                : (from.Left, from.Right, candidate.Left, candidate.Right);
            if (start < fromEnd && fromStart < end)
                return (0, true);
            return (start >= fromEnd ? start - fromEnd : fromStart - end, false);
        }

        private static Vector2 Center(Rectangle rectangle) => new(rectangle.X + (rectangle.Width / 2f), rectangle.Y + (rectangle.Height / 2f));
    }
}
