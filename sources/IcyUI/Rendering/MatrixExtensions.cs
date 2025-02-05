// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering
{
    /// <summary>
    /// Provides some extensions for the matrices and vectors.
    /// </summary>
    public static class MatrixExtensions
    {
        /// <summary>
        /// Decomposes Matrix 3x2 and gets its parts such as <paramref name="translation"/>, <paramref name="rotation"/> and <paramref name="scale"/>.
        /// </summary>
        /// <param name="matrix">Matrix instance to decompose.</param>
        /// <param name="translation">Translation matrix component.</param>
        /// <param name="rotation">Rotation matrix component.</param>
        /// <param name="scale">Scale matrix component.</param>
        public static void Decompose(this in Matrix3x2 matrix, out Vector2 translation, out float rotation, out Vector2 scale)
        {
            // Extract translation
            translation = new Vector2(matrix.M31, matrix.M32);

            // Extract scale
            scale = new Vector2(
                MathF.Sqrt((matrix.M11 * matrix.M11) + (matrix.M12 * matrix.M12)),
                MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22)));

            // Extract rotation
            rotation = MathF.Atan2(-matrix.M21 / scale.Y, matrix.M11 / scale.X);
        }

        /// <summary>
        /// Gets one of the possible angle components from the specified quaternion.
        /// </summary>
        /// <remarks>
        /// Note that there are two possible angle combinations so only one of them will be returned.
        /// </remarks>
        /// <param name="r"><see cref="Quaternion"/> to get rotation from.</param>
        /// <param name="yaw">Yaw rotation component (around Y axis).</param>
        /// <param name="pitch">Pitch rotation component (around X axis).</param>
        /// <param name="roll">Roll rotation component (around Z axis).</param>
        public static void DecomposeAngles(this Quaternion r, out float yaw, out float pitch, out float roll)
        {
            yaw = MathF.Atan2(2.0f * ((r.Y * r.W) + (r.X * r.Z)), 1.0f - (2.0f * ((r.X * r.X) + (r.Y * r.Y))));
            pitch = MathF.Asin(2.0f * ((r.X * r.W) - (r.Y * r.Z)));
            roll = MathF.Atan2(2.0f * ((r.X * r.Y) + (r.Z * r.W)), 1.0f - (2.0f * ((r.X * r.X) + (r.Z * r.Z))));
        }

        /// <summary>
        /// Gets one of the possible angle components from the specified quaternion.
        /// </summary>
        /// <remarks>
        /// Note that there are two possible angle combinations so only one of them will be returned.
        /// Resulting vector components does not represent axes to rotate around.
        /// </remarks>
        /// <param name="rotation"><see cref="Quaternion"/> to get rotation from.</param>
        /// <returns>Vector with yaw-pitch-roll components mapped to XYZ alphabetically.</returns>
        public static Vector3 GetYawPitchRoll(this Quaternion rotation)
        {
            rotation.DecomposeAngles(out float yaw, out float pitch, out float roll);
            return new Vector3(yaw, pitch, roll);
        }

        /// <summary>
        /// Gets the <see cref="Point"/> with the same coordinates as the specified <see cref="Vector2"/>.
        /// </summary>
        /// <param name="vector">The vector to get coordinates from.</param>
        /// <returns>An instance of the <see cref="Point"/> with the same coordinates as the <paramref name="vector"/> have.</returns>
        public static Point ToPoint(this Vector2 vector) => new((int)vector.X, (int)vector.Y);

        /// <summary>
        /// Gets the <see cref="PointF"/> with the same coordinates as the specified <see cref="Vector2"/>.
        /// </summary>
        /// <param name="vector">The vector to get coordinates from.</param>
        /// <returns>An instance of the <see cref="PointF"/> with the same coordinates as the <paramref name="vector"/> have.</returns>
        public static PointF ToPointF(this Vector2 vector) => new(vector.X, vector.Y);

        /// <summary>
        /// Get the vector representation of the specified <see cref="Point"/>.
        /// </summary>
        /// <param name="point">The point to get vector for.</param>
        /// <returns>An instance of the <see cref="Vector2"/> representing the same coordinates as the <paramref name="point"/> have.</returns>
        public static Vector2 ToVector(this Point point) => new(point.X, point.Y);

        /// <summary>
        /// Get the vector representation of the specified <see cref="PointF"/>.
        /// </summary>
        /// <param name="point">The point to get vector for.</param>
        /// <returns>An instance of the <see cref="Vector2"/> representing the same coordinates as the <paramref name="point"/> have.</returns>
        public static Vector2 ToVector(this PointF point) => new(point.X, point.Y);

        /// <summary>
        /// Gets two-dimensional <see cref="Vector3"/> components represented as <see cref="Vector2"/>.
        /// </summary>
        /// <param name="vector">The vector to get X and Y components from.</param>
        /// <returns>Instance of the <see cref="Vector2"/> struct with X set to <paramref name="vector"/>.X and Y set to <paramref name="vector"/>.Y.</returns>
        public static Vector2 XY(this Vector3 vector) => new(vector.X, vector.Y);

        /// <summary>
        /// Gets two-dimensional <see cref="Vector4"/> components represented as <see cref="Vector2"/>.
        /// </summary>
        /// <param name="vector">The vector to get X and Y components from.</param>
        /// <returns>Instance of the <see cref="Vector2"/> struct with X set to <paramref name="vector"/>.X and Y set to <paramref name="vector"/>.Y.</returns>
        public static Vector2 XY(this Vector4 vector) => new(vector.X, vector.Y);

        /// <summary>
        /// Gets trhee-dimensional <see cref="Vector4"/> components represented as <see cref="Vector3"/>.
        /// </summary>
        /// <param name="vector">The vector to get X, Y and Z components from.</param>
        /// <returns>Instance of the <see cref="Vector3"/> struct with X set to <paramref name="vector"/>.X, Y set to <paramref name="vector"/>.Y and Z set to <paramref name="vector"/>.Z.</returns>
        public static Vector3 XYZ(this Vector4 vector) => new(vector.X, vector.Y, vector.Z);

        /// <summary>
        /// Cuts the part of speecfied rectangle.
        /// </summary>
        /// <param name="origin">A rectangle to cut part from.</param>
        /// <param name="part">A sector that is to be cut from this one.</param>
        /// <returns>Maximal possible part of the cut rectangle.</returns>
        public static Rectangle Cut(this Rectangle origin, Rectangle part)
        {
            int width = part.Width;
            if (width + part.X > origin.Width)
            {
                width = origin.Width - part.X;
            }

            int height = part.Height;
            if (height + part.Y > origin.Height)
            {
                height = origin.Height - part.Y;
            }

            return new(part.X + origin.X, part.Y + origin.Y, width, height);
        }
    }
}
