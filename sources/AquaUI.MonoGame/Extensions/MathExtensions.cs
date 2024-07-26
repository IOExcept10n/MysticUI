// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.MonoGame.Extensions
{
    public static class MathExtensions
    {
        public static System.Numerics.Vector2 ToSystemVector(this Microsoft.Xna.Framework.Vector2 value) => new(value.X, value.Y);

        public static Microsoft.Xna.Framework.Vector2 ToMonoGameVector(this System.Numerics.Vector2 value) => new(value.X, value.Y);

        public static System.Drawing.Point ToSystemPoint(this Microsoft.Xna.Framework.Vector2 value) => new((int)value.X, (int)value.Y);

        public static Microsoft.Xna.Framework.Vector2 ToMonoGameVector(this System.Drawing.Point value) => new(value.X, value.Y);
    }
}