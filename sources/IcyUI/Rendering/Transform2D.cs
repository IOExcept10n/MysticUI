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
        /// Applies the transform to the specified rectangle.
        /// </summary>
        /// <param name="source">Source rectangle to apply transform to.</param>
        /// <returns>New rectangle with transformed area.</returns>
        public readonly Rectangle Apply(Rectangle source)
        {
            var position = Apply(new Vector2(source.X, source.Y));
            Vector2 transformScale = new(matrix.M11, matrix.M22);
            Vector2 size = new(source.Width * transformScale.X, source.Height * transformScale.Y);
            return new Rectangle((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
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
