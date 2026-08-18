// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Rendering
{
    /// <summary>
    /// Represents a wrapper for the 2D transform matrix for the UI elements.
    /// </summary>
    public struct Transform2D
    {
        private Matrix3x2 matrix;
        private Vector2 position;
        private float rotation;
        private Vector2 scale;
        private Vector2 origin;

        /// <summary>
        /// Gets the identity transform that does nothing.
        /// </summary>
        public static Transform2D Identity { get; } = Create(Matrix3x2.Identity);

        /// <summary>
        /// Gets the matrix used to form this transform.
        /// </summary>
        public readonly Matrix3x2 Matrix => matrix;

        /// <summary>
        /// Gets or sets the position to translate points to.
        /// </summary>
        public Vector2 Position
        {
            readonly get => position;
            set
            {
                position = value;
                BuildMatrix();
            }
        }

        /// <summary>
        /// Gets or sets the rotation in radians to apply for the points.
        /// </summary>
        public float Rotation
        {
            readonly get => rotation;
            set
            {
                rotation = value;
                BuildMatrix();
            }
        }

        /// <summary>
        /// Gets or sets the scale factor to apply.
        /// </summary>
        public Vector2 Scale
        {
            readonly get => scale;
            set
            {
                scale = value;
                BuildMatrix();
            }
        }

        /// <summary>
        /// Gets or sets the origin of the rotation.
        /// </summary>
        public Vector2 Origin
        {
            readonly get => origin;
            set
            {
                origin = value;
                BuildMatrix();
            }
        }

        /// <summary>
        /// Creates an instance of the <see cref="Transform2D"/> struct
        /// with specified <paramref name="position"/>, <paramref name="rotation"/> around <paramref name="origin"/>, and <paramref name="scale"/>.
        /// </summary>
        /// <param name="position">Position to translate.</param>
        /// <param name="rotation">Rotation in radians to apply.</param>
        /// <param name="origin">Origin of the rotation.</param>
        /// <param name="scale">Scale to apply.</param>
        /// <returns>New instance of the <see cref="Transform2D"/> struct with specified parameters.</returns>
        public static Transform2D Create(Vector2 position, float rotation, Vector2 origin, Vector2 scale)
        {
            var transform = new Transform2D
            {
                position = position,
                rotation = rotation,
                scale = scale,
                origin = origin,
            };
            transform.BuildMatrix();
            return transform;
        }

        /// <summary>
        /// Creates a transform based on specified 2D transform matrix.
        /// </summary>
        /// <param name="matrix">Matrix to get values from.</param>
        /// <returns>New instance of the <see cref="Transform2D"/> struct with <see cref="Matrix"/> set to param values.</returns>
        public static Transform2D Create(Matrix3x2 matrix)
        {
            var transform = new Transform2D
            {
                matrix = matrix,
            };
            transform.DecomposeMatrix();
            return transform;
        }

        /// <summary>
        /// Adds another <see cref="Transform2D"/> to the current instance.
        /// </summary>
        /// <param name="other">Other transform to add.</param>
        public void AddTransform(in Transform2D other)
        {
            matrix = Matrix3x2.Multiply(other.Matrix, matrix);
            DecomposeMatrix();
        }

        /// <summary>
        /// Applies the transform to the specified <see cref="Vector2"/>.
        /// </summary>
        /// <param name="source">Source vector to apply transform to.</param>
        /// <returns>New vector with transformed coordinates.</returns>
        public readonly Vector2 Apply(Vector2 source)
        {
            return Vector2.Transform(source, matrix);
        }

        /// <summary>
        /// Applies the transform to the specified <see cref="Point"/>.
        /// </summary>
        /// <param name="source">Source point to apply transform to.</param>
        /// <returns>New point with transformed coordinates.</returns>
        public readonly Point Apply(Point source)
        {
            var transformed = Apply(new Vector2(source.X, source.Y));
            return new Point((int)transformed.X, (int)transformed.Y);
        }

        /// <summary>
        /// Applies the transform to the specified rectangle, returning the axis-aligned bounding box of its
        /// transformed area.
        /// </summary>
        /// <param name="source">Source rectangle to apply transform to.</param>
        /// <returns>
        /// The axis-aligned bounding box containing all four of <paramref name="source"/>'s corners after being
        /// transformed. For a rotation-free, uniform-position-only transform this is exactly the same rectangle,
        /// just translated; for a rotated and/or scaled transform, this is the true bounding box of the resulting
        /// (generally non-axis-aligned) shape - not that shape itself, which a <see cref="Rectangle"/> can't
        /// represent.
        /// </returns>
        /// <remarks>
        /// This used to transform only <paramref name="source"/>'s top-left corner and scale the width/height by
        /// <c>(matrix.M11, matrix.M22)</c> directly - correct only when the matrix has no rotation, since those
        /// components mix rotation and scale together for any rotated matrix (e.g. <c>M11 = scale.X * cos(θ)</c>).
        /// A 45° rotation multiplied both dimensions by <c>cos(45°) ≈ 0.707</c> instead of computing the actual
        /// (larger) rotated bounding box, collapsing the result down to a small sliver overlapping just the
        /// transformed top-left corner - visible as a rotated element getting clipped down to almost nothing by
        /// <see cref="UI.UIElement.ClipToBounds"/> scissoring, or a rotated/scaled glyph's measured bounds being
        /// wrong.
        /// </remarks>
        public readonly Rectangle Apply(Rectangle source)
        {
            Vector2 topLeft = Apply(new Vector2(source.Left, source.Top));
            Vector2 topRight = Apply(new Vector2(source.Right, source.Top));
            Vector2 bottomLeft = Apply(new Vector2(source.Left, source.Bottom));
            Vector2 bottomRight = Apply(new Vector2(source.Right, source.Bottom));

            float minX = MathF.Min(MathF.Min(topLeft.X, topRight.X), MathF.Min(bottomLeft.X, bottomRight.X));
            float maxX = MathF.Max(MathF.Max(topLeft.X, topRight.X), MathF.Max(bottomLeft.X, bottomRight.X));
            float minY = MathF.Min(MathF.Min(topLeft.Y, topRight.Y), MathF.Min(bottomLeft.Y, bottomRight.Y));
            float maxY = MathF.Max(MathF.Max(topLeft.Y, topRight.Y), MathF.Max(bottomLeft.Y, bottomRight.Y));

            return Rectangle.FromLTRB((int)MathF.Floor(minX), (int)MathF.Floor(minY), (int)MathF.Ceiling(maxX), (int)MathF.Ceiling(maxY));
        }

        /// <inheritdoc/>
        public override readonly string ToString()
        {
            return $"2D Transform (Scale: {Scale}, Rotate: {float.RadiansToDegrees(Rotation)} around {Origin}, Translate: {Position})";
        }

        private void BuildMatrix()
        {
            matrix = Matrix3x2.CreateTranslation(position) *
                     Matrix3x2.CreateRotation(rotation, origin) *
                     Matrix3x2.CreateScale(scale);
        }

        private void DecomposeMatrix()
        {
            matrix.Decompose(out position, out rotation, out scale);
        }
    }
}
