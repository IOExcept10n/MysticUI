// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Icy.Rendering
{
    /// <summary>
    /// Represents generalized interface for platform texture type adapters.
    /// </summary>
    public interface ITexture
    {
        /// <summary>
        /// Gets the size of a texture.
        /// </summary>
        Size Size { get; }

        /// <summary>
        /// Gets the colors stored in texture, reading data in format represented by <typeparamref name="TColor"/> struct.
        /// </summary>
        /// <typeparam name="TColor">Format of colors stored in the texture.</typeparam>
        /// <param name="region">The section of the texture where the data will be copied from. <see langword="null"/> indicates the data will be copied over the entire texture.</param>
        /// <param name="buffer">The array to receive texture data. If <paramref name="region"/> is null, the number of elements in the array must be equal to the <see cref="Size"/>; otherwise, the number of elements in the array should be equal to the size of the rectangle.</param>
        /// <param name="startIndex">The index of the element in the array at which to start copying.</param>
        /// <param name="elementCount">The number of elements to copy.</param>
        void GetTextureData<TColor>(Rectangle? region, TColor[] buffer, int startIndex, int elementCount)
            where TColor : struct;

        /// <summary>
        /// Sets the colors data to the specified bounds in texture.
        /// </summary>
        /// <typeparam name="TColor">Format of colors stored in the texture.</typeparam>
        /// <param name="region">The section of the texture where the data will be placed. <see langword="null"/> indicates the data will be copied over the entire texture.</param>
        /// <param name="buffer">Buffer to read data from.</param>
        /// <param name="startIndex">The index of the element in the array at which to start copying.</param>
        /// <param name="elementCount">The number of elements to copy.</param>
        void SetTextureData<TColor>(Rectangle? region, TColor[] buffer, int startIndex, int elementCount)
            where TColor : struct;
    }

    /// <summary>
    /// Represents a color of the texture that contains <c>Red</c>, <c>Green</c>, <c>Blue</c> and <c>Alpha</c> channels.
    /// </summary>
    /// <remarks>
    /// This type of colors is the most commonly used when handling textures over the game engines.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 4)]
    public struct Rgba32
    {
        /// <summary>
        /// Gets or sets the value of the Red color channel.
        /// </summary>
        public byte R;

        /// <summary>
        /// Gets or sets the value of the Green color channel.
        /// </summary>
        public byte G;

        /// <summary>
        /// Gets or sets the value of the Blue color channel.
        /// </summary>
        public byte B;

        /// <summary>
        /// Gets or sets the value of the Alpha color channel.
        /// </summary>
        public byte A;

        /// <summary>
        /// Initializes a new instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="r">Value of the red color channel.</param>
        /// <param name="g">Value of the blue color channel.</param>
        /// <param name="b">Value of the green color channel.</param>
        /// <param name="a">Value of the alpha color channel.</param>
        public Rgba32(byte r, byte g, byte b, byte a)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        /// <summary>
        /// Converts an instance of the <see cref="Color"/> to an according instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="c">An instance of the <see cref="Color"/> struct to convert.</param>
        /// <returns>An instance of the <see cref="Rgba32"/> struct with the same channel values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator Rgba32(Color c) => FromColor(c);

        /// <summary>
        /// Converts an instance of the <see cref="Rgba32"/> to the according instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <param name="p">An instance of the <see cref="Rgba32"/> to convert.</param>
        /// <returns>An instance of the <see cref="Color"/> struct with the same channel values.</returns>
        public static explicit operator Color(Rgba32 p) => p.ToColor();

        /// <summary>
        /// Converts an instance of the <see cref="Color"/> to an according instance of the <see cref="Rgba32"/> struct.
        /// </summary>
        /// <param name="c">An instance of the <see cref="Color"/> struct to convert.</param>
        /// <returns>An instance of the <see cref="Rgba32"/> struct with the same channel values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Rgba32 FromColor(Color c) => new(c.R, c.G, c.B, c.A);

        /// <summary>
        /// Converts current instance of the <see cref="Rgba32"/> to the according instance of the <see cref="Color"/> struct.
        /// </summary>
        /// <returns>An instance of the <see cref="Color"/> struct with the same channel values.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Color ToColor() => Color.FromArgb(A, R, G, B);

        /// <summary>
        /// Gets the packed <see langword="int"/> value of the current <see cref="Rgba32"/> instance.
        /// </summary>
        /// <returns>An <see langword="int"/> with the color channel values from the current <see cref="Rgba32"/> instance.</returns>
        public readonly int GetPackedValue() => (R << 24) | (G << 16) | (B << 8) | A;

        /// <summary>
        /// Gets the brightness of the current pixel, needed for some calculations.
        /// </summary>
        /// <remarks>
        /// This metric is calculated using the following formula: (<see cref="R"/> + <see cref="G"/> + <see cref="B"/>) / <c>3</c>.
        /// Common usages of it can be alpha pre-multiplication.
        /// </remarks>
        /// <returns>An average intensity of the current <see cref="Rgba32"/> instance.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly byte GetIntensity() => (byte)((R + G + B) / 3);
    }
}