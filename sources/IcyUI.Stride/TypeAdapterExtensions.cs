// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
// System types
using SColor = System.Drawing.Color;
using SMatrix3x2 = System.Numerics.Matrix3x2;
using SMatrix4x4 = System.Numerics.Matrix4x4;
using SPoint = System.Drawing.Point;
using SRectangle = System.Drawing.Rectangle;
using SVector2 = System.Numerics.Vector2;
using SVector3 = System.Numerics.Vector3;
using SVector4 = System.Numerics.Vector4;

// Target engine types
using TColor = Stride.Core.Mathematics.Color;
using TMatrix = Stride.Core.Mathematics.Matrix;
using TPoint = Stride.Core.Mathematics.Point;
using TRectangle = Stride.Core.Mathematics.Rectangle;
using TVector2 = Stride.Core.Mathematics.Vector2;
using TVector3 = Stride.Core.Mathematics.Vector3;
using TVector4 = Stride.Core.Mathematics.Vector4;

// Do not use regions and place arguments on the same line
#pragma warning disable SA1124, SA1117

namespace Icy.Stride
{
    /// <summary>
    /// Provides extension methods for conversion between most common structures of the target engine and system library ones.
    /// </summary>
    /// <remarks>
    /// Mirrors <c>Icy.MonoGame.TypeAdapterExtensions</c> - same method names/shapes, targeting Stride's
    /// <see cref="Stride.Core.Mathematics"/> types instead of MonoGame's.
    /// </remarks>
    public static class TypeAdapterExtensions
    {
        #region Vector2 and Point conversion

        /// <summary>
        /// Converts a specified instance of <see cref="TVector2"/> to an instance of <see cref="SVector2"/>.
        /// </summary>
        /// <param name="value">The <see cref="TVector2"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SVector2"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static SVector2 AsSystemVector(this TVector2 value) => new(value.X, value.Y);

        /// <summary>
        /// Converts a specified instance of <see cref="SVector2"/> to an instance of <see cref="TVector2"/>.
        /// </summary>
        /// <param name="value">The <see cref="SVector2"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TVector2"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static TVector2 AsEngineVector(this SVector2 value) => new(value.X, value.Y);

        /// <summary>
        /// Converts a specified instance of <see cref="TVector2"/> to an instance of <see cref="SPoint"/>.
        /// </summary>
        /// <param name="value">The <see cref="TVector2"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SPoint"/> with field values mapped from <paramref name="value"/>,
        /// with the X and Y values cast to integers.
        /// </returns>
        public static SPoint AsSystemPoint(this TVector2 value) => new((int)value.X, (int)value.Y);

        /// <summary>
        /// Converts a specified instance of <see cref="SPoint"/> to an instance of <see cref="TVector2"/>.
        /// </summary>
        /// <param name="value">The <see cref="SPoint"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TVector2"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static TVector2 AsEngineVector(this SPoint value) => new(value.X, value.Y);

        /// <summary>
        /// Converts a specified instance of <see cref="TPoint"/> to an instance of <see cref="SPoint"/>.
        /// </summary>
        /// <param name="value">The <see cref="TPoint"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SPoint"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static SPoint AsSystemPoint(this TPoint value) => new(value.X, value.Y);

        /// <summary>
        /// Converts a specified instance of <see cref="SPoint"/> to an instance of <see cref="TPoint"/>.
        /// </summary>
        /// <param name="value">The <see cref="SPoint"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TPoint"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static TPoint AsEnginePoint(this SPoint value) => new(value.X, value.Y);

        #endregion Vector2 and Point conversion

        #region Vector3 conversion

        /// <summary>
        /// Converts a specified instance of <see cref="TVector3"/> to an instance of <see cref="SVector3"/>.
        /// </summary>
        /// <param name="value">The <see cref="TVector3"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SVector3"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static SVector3 AsSystemVector(this TVector3 value) => new(value.X, value.Y, value.Z);

        /// <summary>
        /// Converts a specified instance of <see cref="SVector3"/> to an instance of <see cref="TVector3"/>.
        /// </summary>
        /// <param name="value">The <see cref="SVector3"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TVector3"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static TVector3 AsEngineVector(this SVector3 value) => new(value.X, value.Y, value.Z);

        #endregion Vector3 conversion

        #region Vector4 conversion

        /// <summary>
        /// Converts a specified instance of <see cref="TVector4"/> to an instance of <see cref="SVector4"/>.
        /// </summary>
        /// <param name="value">The <see cref="TVector4"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SVector4"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static SVector4 AsSystemVector(this TVector4 value) => new(value.X, value.Y, value.Z, value.W);

        /// <summary>
        /// Converts a specified instance of <see cref="SVector4"/> to an instance of <see cref="TVector4"/>.
        /// </summary>
        /// <param name="value">The <see cref="SVector4"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TVector4"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static TVector4 AsEngineVector(this SVector4 value) => new(value.X, value.Y, value.Z, value.W);

        #endregion Vector4 conversion

        #region Matrix conversion

        /// <summary>
        /// Converts a specified instance of <see cref="TMatrix"/> to an instance of <see cref="SMatrix3x2"/>.
        /// </summary>
        /// <param name="value">The <see cref="TMatrix"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SMatrix3x2"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static SMatrix3x2 AsSystemMatrix3x2(this TMatrix value)
            => new(value.M11, value.M12,
                   value.M21, value.M22,
                   value.M31, value.M32);

        /// <summary>
        /// Converts a specified instance of <see cref="TMatrix"/> to an instance of <see cref="SMatrix4x4"/>.
        /// </summary>
        /// <param name="value">The <see cref="TMatrix"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SMatrix4x4"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static SMatrix4x4 AsSystemMatrix4x4(this TMatrix value)
            => new(value.M11, value.M12, value.M13, value.M14,
                   value.M21, value.M22, value.M23, value.M24,
                   value.M31, value.M32, value.M33, value.M34,
                   value.M41, value.M42, value.M43, value.M44);

        /// <summary>
        /// Converts a specified instance of <see cref="SMatrix3x2"/> to an instance of <see cref="TMatrix"/>.
        /// </summary>
        /// <param name="value">The <see cref="SMatrix3x2"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TMatrix"/> with field values mapped from <paramref name="value"/>.
        /// The last row is set to represent a homogeneous coordinate system.
        /// </returns>
        public static TMatrix AsEngineMatrix(this SMatrix3x2 value)
            => new(value.M11, value.M12, 0, 0,
                   value.M21, value.M22, 0, 0,
                   value.M31, value.M32, 1, 0,
                   0, 0, 0, 1);

        /// <summary>
        /// Converts a specified instance of <see cref="SMatrix4x4"/> to an instance of <see cref="TMatrix"/>.
        /// </summary>
        /// <param name="value">The <see cref="SMatrix4x4"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TMatrix"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static TMatrix AsEngineMatrix(this SMatrix4x4 value)
            => new(value.M11, value.M12, value.M13, value.M14,
                   value.M21, value.M22, value.M23, value.M24,
                   value.M31, value.M32, value.M33, value.M34,
                   value.M41, value.M42, value.M43, value.M44);

        #endregion Matrix conversion

        #region Rectangle conversion

        /// <summary>
        /// Converts a specified instance of <see cref="TRectangle"/> to an instance of <see cref="SRectangle"/>.
        /// </summary>
        /// <param name="value">The <see cref="TRectangle"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SRectangle"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static SRectangle AsSystemRectangle(this TRectangle value) => new(value.X, value.Y, value.Width, value.Height);

        /// <summary>
        /// Converts a specified instance of <see cref="SRectangle"/> to an instance of <see cref="TRectangle"/>.
        /// </summary>
        /// <param name="value">The <see cref="SRectangle"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TRectangle"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static TRectangle AsEngineRectangle(this SRectangle value) => new(value.X, value.Y, value.Width, value.Height);

        #endregion Rectangle conversion

        #region Color conversion

        /// <summary>
        /// Converts a specified instance of <see cref="TColor"/> to an instance of <see cref="SColor"/>.
        /// </summary>
        /// <param name="value">The <see cref="TColor"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="SColor"/> created from the ARGB values of <paramref name="value"/>.
        /// </returns>
        public static SColor AsSystemColor(this TColor value) => SColor.FromArgb(value.A, value.R, value.G, value.B);

        /// <summary>
        /// Converts a specified instance of <see cref="SColor"/> to an instance of <see cref="TColor"/>.
        /// </summary>
        /// <param name="value">The <see cref="SColor"/> instance to convert.</param>
        /// <returns>
        /// An instance of <see cref="TColor"/> with field values mapped from <paramref name="value"/>.
        /// </returns>
        public static TColor AsEngineColor(this SColor value) => new(value.R, value.G, value.B, value.A);

        #endregion Color conversion
    }
}
