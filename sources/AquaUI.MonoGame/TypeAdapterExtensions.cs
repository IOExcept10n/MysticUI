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
using TColor = Microsoft.Xna.Framework.Color;
using TMatrix = Microsoft.Xna.Framework.Matrix;
using TPoint = Microsoft.Xna.Framework.Point;
using TRectangle = Microsoft.Xna.Framework.Rectangle;
using TVector2 = Microsoft.Xna.Framework.Vector2;
using TVector3 = Microsoft.Xna.Framework.Vector3;
using TVector4 = Microsoft.Xna.Framework.Vector4;

namespace AquaUI.MonoGame
{
    public static class TypeAdapterExtensions
    {
        #region Vector2 and Point conversion

        public static SVector2 AsSystemVector(this TVector2 value) => new(value.X, value.Y);

        public static TVector2 AsEngineVector(this SVector2 value) => new(value.X, value.Y);

        public static SPoint AsSystemPoint(this TVector2 value) => new((int)value.X, (int)value.Y);

        public static TVector2 AsEngineVector(this SPoint value) => new(value.X, value.Y);

        public static SPoint AsSystemPoint(this TPoint value) => new(value.X, value.Y);

        public static TPoint AsEnginePoint(this SPoint value) => new(value.X, value.Y);

        #endregion Vector2 and Point conversion

        #region Vector3 conversion

        public static SVector3 AsSystemVector(this TVector3 value) => new(value.X, value.Y, value.Z);

        public static TVector3 AsEngineVector(this SVector3 value) => new(value.X, value.Y, value.Z);

        #endregion Vector3 conversion

        #region Vector4 conversion

        public static SVector4 AsSystemVector(this TVector4 value) => new(value.X, value.Y, value.Z, value.W);

        public static TVector4 AsEngineVector(this SVector4 value) => new(value.X, value.Y, value.Z, value.W);

        #endregion Vector4 conversion

        #region Matrix conversion

        public static SMatrix3x2 AsSystemMatrix3x2(this TMatrix value)
            => new(value.M11, value.M12,
                   value.M21, value.M22,
                   value.M31, value.M32);

        public static SMatrix4x4 AsSystemMatrix4x4(this TMatrix value)
            => new(value.M11, value.M12, value.M13, value.M14,
                   value.M21, value.M22, value.M23, value.M24,
                   value.M31, value.M32, value.M33, value.M34,
                   value.M41, value.M42, value.M43, value.M44);

        public static TMatrix AsEngineMatrix(this SMatrix3x2 value)
            => new(value.M11, value.M12, 0, 0,
                   value.M21, value.M22, 0, 0,
                   value.M31, value.M32, 1, 0,
                   0, 0, 0, 1);

        public static TMatrix AsEngineMatrix(this SMatrix4x4 value)
            => new(value.M11, value.M12, value.M13, value.M14,
                   value.M21, value.M22, value.M23, value.M24,
                   value.M31, value.M32, value.M33, value.M34,
                   value.M41, value.M42, value.M43, value.M44);

        #endregion Matrix conversion

        #region Rectangle conversion

        public static SRectangle AsSystemRectangle(this TRectangle value) => new(value.X, value.Y, value.Width, value.Height);

        public static TRectangle AsEngineRectangle(this SRectangle value) => new(value.X, value.Y, value.Width, value.Height);

        #endregion Rectangle conversion

        #region Color conversion

        public static SColor AsSystemColor(this TColor value) => SColor.FromArgb(value.A, value.R, value.G, value.B);

        public static TColor AsEngineColor(this SColor value) => new(value.R, value.G, value.B, value.A);

        #endregion Color conversion
    }
}