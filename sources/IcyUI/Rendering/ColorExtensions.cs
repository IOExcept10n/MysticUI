// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering
{
    /// <summary>
    /// Provides extension methods for the colors.
    /// </summary>
    public static class ColorExtensions
    {
        private const float Multiplier = 1 / 255f;

        /// <summary>
        /// Converts four-dimensional normalized vector to its color representation.
        /// </summary>
        /// <param name="vector">Normalized vector to convert.</param>
        /// <returns>
        /// An instance of the <see cref="Color"/> struct
        /// with mapping from vector XYZW to color ARGB (alpha, red, green, blue)
        /// and multiplying by 255 each component.
        /// </returns>
        public static Color AsColor(this Vector4 vector)
        {
            vector *= 255;
            return Color.FromArgb(
                alpha: (int)vector.X,
                red: (int)vector.Y,
                green: (int)vector.Z,
                blue: (int)vector.W);
        }

        /// <summary>
        /// Converts color to the vector representation converting integer color components to normalized float values.
        /// </summary>
        /// <param name="c">Color to convert.</param>
        /// <returns>
        /// An instance of the <see cref="Vector4"/> struct
        /// with mapping from color ARGB (alpha, red, green, blue)
        /// to vector XYZW normalized by dividing by 255.
        /// </returns>
        public static Vector4 AsVector(this Color c) => new Vector4(x: c.A, y: c.R, z: c.G, w: c.B) * Multiplier;
    }
}
