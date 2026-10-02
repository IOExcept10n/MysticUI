// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering
{
    /// <summary>
    /// Provides pixel snapping for axis-aligned quads, shared by every engine's <see cref="IRenderContext.Draw"/> implementation.
    /// </summary>
    /// <remarks>
    /// Snapping rounds the quad's <em>edges</em> (left/top and right/bottom) to whole physical pixels, rather than its position
    /// and size separately. Two quads that share an edge in logical units therefore share it in physical pixels at any
    /// scale (e.g. 125 % or 150 %), so no seams or 1-pixel gaps appear, and linearly sampled textures aren't blurred by
    /// sub-pixel offsets.
    /// </remarks>
    public static class PixelSnapping
    {
        private const float RotationEpsilon = 0.0001f;

        /// <summary>
        /// Tries to snap a destination rectangle, transformed by <paramref name="transform"/>, to whole physical pixels.
        /// </summary>
        /// <param name="transform">The render context's current transform.</param>
        /// <param name="destination">The destination rectangle before <paramref name="transform"/> is applied.</param>
        /// <param name="rotation">The per-draw rotation in radians, added to the transform's own rotation.</param>
        /// <param name="origin">The per-draw origin; snapping only applies when it is <see cref="Vector2.Zero"/>.</param>
        /// <param name="position">The snapped top-left corner in physical pixels.</param>
        /// <param name="size">The snapped size in physical pixels.</param>
        /// <returns>
        /// <see langword="true"/> if the quad is axis-aligned with a positive scale and was snapped; <see langword="false"/>
        /// (and default outputs) when it is rotated, mirrored or has a non-zero origin, in which case the caller draws it unsnapped.
        /// </returns>
        public static bool TrySnap(in Transform2D transform, Rectangle destination, float rotation, Vector2 origin, out Vector2 position, out Vector2 size)
        {
            position = default;
            size = default;
            Vector2 scale = transform.Scale;
            if (MathF.Abs(rotation + transform.Rotation) > RotationEpsilon || origin != Vector2.Zero || scale.X <= 0 || scale.Y <= 0)
                return false;

            Vector2 topLeft = Round(transform.Apply(new Vector2(destination.Left, destination.Top)));
            Vector2 bottomRight = Round(transform.Apply(new Vector2(destination.Right, destination.Bottom)));
            position = topLeft;
            size = bottomRight - topLeft;
            return true;
        }

        private static Vector2 Round(Vector2 value) => new(
            MathF.Round(value.X, MidpointRounding.AwayFromZero),
            MathF.Round(value.Y, MidpointRounding.AwayFromZero));
    }
}
