// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Animations
{
    /// <summary>
    /// Provides linear interpolation between two boxed values of the same animatable type, for
    /// <see cref="Animation"/>'s keyframe evaluation.
    /// </summary>
    internal static class ValueInterpolator
    {
        /// <summary>
        /// Attempts to linearly interpolate between two boxed values of a known animatable type.
        /// </summary>
        /// <param name="from">The value at <paramref name="t"/> = 0.</param>
        /// <param name="to">The value at <paramref name="t"/> = 1.</param>
        /// <param name="t">The normalized interpolation factor.</param>
        /// <param name="result">The interpolated value, if interpolation is supported for this type pair.</param>
        /// <returns><see langword="true"/> if <paramref name="from"/>/<paramref name="to"/> are a supported, matching type pair; otherwise <see langword="false"/>.</returns>
        public static bool TryLerp(object? from, object? to, float t, out object? result)
        {
            switch (from, to)
            {
                case (float f1, float f2):
                    result = f1 + ((f2 - f1) * t);
                    return true;
                case (double d1, double d2):
                    result = d1 + ((d2 - d1) * t);
                    return true;
                case (int i1, int i2):
                    result = (int)MathF.Round(i1 + ((i2 - i1) * t));
                    return true;
                case (Vector2 v1, Vector2 v2):
                    result = Vector2.Lerp(v1, v2, t);
                    return true;
                case (Vector3 v1, Vector3 v2):
                    result = Vector3.Lerp(v1, v2, t);
                    return true;
                case (Vector4 v1, Vector4 v2):
                    result = Vector4.Lerp(v1, v2, t);
                    return true;
                case (Color c1, Color c2):
                    result = LerpColor(c1, c2, t);
                    return true;
                default:
                    result = null;
                    return false;
            }
        }

        private static Color LerpColor(Color from, Color to, float t) => Color.FromArgb(
            LerpChannel(from.A, to.A, t),
            LerpChannel(from.R, to.R, t),
            LerpChannel(from.G, to.G, t),
            LerpChannel(from.B, to.B, t));

        private static int LerpChannel(int from, int to, float t) =>
            Math.Clamp((int)MathF.Round(from + ((to - from) * t)), 0, 255);
    }
}
