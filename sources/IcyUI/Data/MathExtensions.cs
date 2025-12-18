// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
// Math algorithms are taken from the Stride.Core.Mathematics (https://github.com/stride3d/stride/tree/master/sources/core/Stride.Core.Mathematics).
// The Stride code is licensed under MIT license. See https://github.com/stride3d/stride/ LICENSE.md file.
using System.Drawing;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Icy.Data
{
    /// <summary>
    /// Provides utility extensions for the mathematical objects such as vectors, quaternions etc.
    /// </summary>
    public static class MathExtensions
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Size Max(this Size left, Size right) =>
            new(
                Math.Max(left.Width, right.Width),
                Math.Max(left.Height, right.Height));

        /// <summary>
        /// Gets the location of the middle point of the specified rectangle.
        /// </summary>
        /// <remarks>
        /// If the middle point is not placed on an integer coordinate, it will be rounded with default rounding rules.
        /// </remarks>
        /// <param name="rect">An instance of the <see cref="Rectangle"/> to get the middle point for.</param>
        /// <returns>An instance of the <see cref="Point"/> with the middle location for the specified rectangle.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point Center(this Rectangle rect) => new((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);

        /// <summary>
        /// Gets the vector representation of the specified <see cref="Size"/> instance.
        /// </summary>
        /// <param name="size">An instance of the <see cref="Size"/> to convert.</param>
        /// <returns>Vector representation of the specified size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 AsVector(this Size size) => new(size.Width, size.Height);

        /// <summary>
        /// Gets the vector representation of the specified <see cref="SizeF"/> instance.
        /// </summary>
        /// <param name="size">An instance of the <see cref="SizeF"/> to convert.</param>
        /// <returns>Vector representation of the specified size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Vector2 AsVector(this SizeF size) => new(size.Width, size.Height);

        /// <summary>
        /// Gets the point representation of the specified <see cref="Size"/> instance.
        /// </summary>
        /// <param name="size">An instance of the <see cref="Size"/> to convert.</param>
        /// <returns>Point representation of the specified size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Point AsPoint(this Size size) => new(size.Width, size.Height);

        /// <summary>
        /// Gets the point representation of the specified <see cref="SizeF"/> instance.
        /// </summary>
        /// <param name="size">An instance of the <see cref="SizeF"/> to convert.</param>
        /// <returns>Point representation of the specified size.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PointF AsPoint(this SizeF size) => new(size.Width, size.Height);

        /// <summary>
        /// Gets the <b>yaw</b>-<b>pitch</b>-<b>roll</b> <see cref="Vector3"/> from the given <see cref="Quaternion"/>.
        /// </summary>
        /// <param name="q">A <see cref="Quaternion"/> to get Euler angles for.</param>
        /// <returns>One of the possible rotation Euler angles in radians for the specified quaternion.</returns>
        public static Vector3 ToYawPitchRoll(this Quaternion q)
        {
            float yaw, pitch, roll;
            float xx = q.X * q.X;
            float yy = q.Y * q.Y;
            float zz = q.Z * q.Z;
            float xy = q.X * q.Y;
            float zw = q.Z * q.W;
            float zx = q.Z * q.X;
            float yw = q.Y * q.W;
            float yz = q.Y * q.Z;
            float xw = q.X * q.W;

            float m11 = 1.0f - (2.0f * (yy + zz));
            float m12 = 2.0f * (xy + zw);

            float m21 = 2.0f * (xy - zw);
            float m22 = 1.0f - (2.0f * (zz + xx));

            float m31 = 2.0f * (zx + yw);
            float m32 = 2.0f * (yz - xw);
            float m33 = 1.0f - (2.0f * (yy + xx));

            /*** Refer to Matrix.Decompose(out float yaw, out float pitch, out float roll) for code and license ***/
            if (MathF.Abs(MathF.Abs(m32) - 1) < 0.001f)
            {
                if (m32 >= 0)
                {
                    // Edge case where M32 == +1
                    pitch = -MathF.PI / 2;
                    yaw = MathF.Atan2(-m21, m11);
                    roll = 0;
                }
                else
                {
                    // Edge case where M32 == -1
                    pitch = MathF.PI / 2;
                    yaw = -MathF.Atan2(-m21, m11);
                    roll = 0;
                }
            }
            else
            {
                // Common case
                pitch = MathF.Asin(-m32);
                yaw = MathF.Atan2(m31, m33);
                roll = MathF.Atan2(m12, m22);
            }

            return new(yaw, pitch, roll);
        }
    }
}
