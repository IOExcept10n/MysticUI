// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace AquaUI.Data
{
    /// <summary>
    /// Provides extensions for the <see cref="Point"/> struct.
    /// </summary>
    public static class PointExtensions
    {
        /// <summary>
        /// Gets the corresponding <see cref="Vector2"/> value for the specified point.
        /// </summary>
        /// <param name="point">The point to get values from.</param>
        /// <returns>An instance of <see cref="Vector2"/> with the same values.</returns>
        public static Vector2 ToVector2(this Point point) => new(point.X, point.Y);

        /// <summary>
        /// Gets the <see cref="Point"/> with rounded <see cref="Vector2"/> coordinates.
        /// </summary>
        /// <param name="vector">The vector to get values from.</param>
        /// <returns>An instance of <see cref="Point"/> with rounded coordinates.</returns>
        public static Point ToPoint(this Vector2 vector) => new((int)vector.X, (int)vector.Y);
    }
}