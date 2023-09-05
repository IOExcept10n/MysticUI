// Code here is based on Myra's code: https://github.com/rds1983/Myra
using Stride.Core.Mathematics;

namespace MysticUI
{
    /// <summary>
    /// Provides some tools to transform objects.
    /// </summary>
    public struct Transform
    {
        private Vector3 position;
        private Vector3 scale;
        private Quaternion rotation;
        private Matrix matrix;

        /// <summary>
        /// Position of the transform object.
        /// </summary>
        public Vector3 Position
        {
            readonly get => position;
            set
            {
                position = value;
                BuildMatrix();
            }
        }

        /// <summary>
        /// Rotation of the transform object.
        /// </summary>
        public Quaternion Rotation
        {
            readonly get => rotation;
            set
            {
                rotation = value;
                BuildMatrix();
            }
        }

        /// <summary>
        /// Scale of the transform object.
        /// </summary>
        public Vector3 Scale
        {
            readonly get => scale;
            set
            {
                scale = value;
                BuildMatrix();
            }
        }

        /// <summary>
        /// 2D position of the transform object.
        /// </summary>
        public Vector2 Position2D
        {
            readonly get => Position.XY();
            set
            {
                scale = new Vector3(value, 0);
                BuildMatrix();
            }
        }

        /// <summary>
        /// 2D scale of the transform object.
        /// </summary>
        public Vector2 Scale2D
        {
            readonly get => Scale.XY();
            set
            {
                scale = new Vector3(value, 0);
                BuildMatrix();
            }
        }

        /// <summary>
        /// 2D rotation around the Z-axis in radians.
        /// </summary>
        public float Rotation2D
        {
            readonly get => Rotation.YawPitchRoll.Z;
            set
            {
                rotation = Quaternion.RotationZ(value);
                BuildMatrix();
            }
        }

        /// <summary>
        /// Matrix of the transform object.
        /// </summary>
        public readonly Matrix Matrix => matrix;

        /// <summary>
        /// Creates new transform from the given components.
        /// </summary>
        /// <param name="position">Position.</param>
        /// <param name="rotation">Rotation.</param>
        /// <param name="scale">Scale.</param>
        public Transform(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            matrix = default;//This line is needed to not break struct logic.
            BuildMatrix();
        }

        /// <summary>
        /// Creates new transform from the transformation matrix.
        /// </summary>
        /// <param name="matrix">Prepared transformation matrix.</param>
        public Transform(Matrix matrix) : this()
        {
            this.matrix = matrix;
            DecomposeMatrix();
        }

        /// <summary>
        /// Creates 2D transform for the objects like UI elements.
        /// </summary>
        /// <param name="position">Position of the element.</param>
        /// <param name="origin">Origin position.</param>
        /// <param name="scale">Element scale.</param>
        /// <param name="rotation">Rotation of the element.</param>
        /// <returns></returns>
        public static Transform Create2DTransform(Vector2 position, Vector2 origin, Vector2 scale, float rotation)
        {
            // This is the code from Myra
            Matrix result = Matrix.Identity;
            float offsetX, offsetY;
            if (rotation == 0)
            {
                result.M11 = scale.X;
                result.M22 = scale.Y;
                offsetX = position.X - (origin.X * result.M11);
                offsetY = position.Y - (origin.Y * result.M22);
            }
            else
            {
                var cos = (float)Math.Cos(rotation);
                var sin = (float)Math.Sin(rotation);
                result.M11 = scale.X * cos;
                result.M12 = scale.X * sin;
                result.M21 = scale.Y * -sin;
                result.M22 = scale.Y * cos;
                offsetX = position.X - (origin.X * result.M11) - (origin.Y * result.M21);
                offsetY = position.Y - (origin.X * result.M12) - (origin.Y * result.M22);
            }

            offsetX += origin.X;
            offsetY += origin.Y;
            result.M41 = offsetX;
            result.M42 = offsetY;
            return new Transform(result)
            {
                position = new Vector3(position, 0),
                rotation = Quaternion.RotationZ(rotation),
                scale = new Vector3(scale, 0)
            };
        }

        /// <summary>
        /// Adds transform for the current transform.
        /// </summary>
        /// <param name="addition">Transform to add.</param>
        public void AddTransform(ref Transform addition)
        {
            matrix = addition.Matrix * Matrix;

            DecomposeMatrix();
            //scale *= addition.Scale;
            //rotation += addition.Rotation;
            //position += addition.position;
        }

        /// <summary>
        /// Applies transform to the vector.
        /// </summary>
        /// <param name="source">Vector to apply transform to.</param>
        /// <returns>New vector with applied transform.</returns>
        public Vector2 Apply(Vector2 source)
        {
            Vector2.Transform(ref source, ref matrix, out var result);
            return result.XY();
        }

        /// <summary>
        /// Applies transform to the point.
        /// </summary>
        /// <param name="source">Point to apply transform to.</param>
        /// <returns>New transformed point.</returns>
        public Point Apply(Point source) => (Point)Apply(new Vector2(source.X, source.Y));

        /// <summary>
        /// Applies transform to the rectangle.
        /// </summary>
        /// <remarks>
        /// Note that rotation and scale in 3D-space may not be transformed correctly for the 2D-rectangle.
        /// </remarks>
        /// <param name="source">Rectangle to apply transform to.</param>
        /// <returns></returns>
        public Rectangle Apply(Rectangle source)
        {
            Vector2 position = Apply(source.Location);
            Vector2 transformScale = new(matrix.M11, matrix.M22);
            Vector2 scale = new(transformScale.X * source.Width, transformScale.Y * source.Height);
            return new Rectangle((int)position.X, (int)position.Y, (int)scale.X, (int)scale.Y);
        }

        private void BuildMatrix()
        {
            Matrix.Transformation(ref scale, ref rotation, ref position, out matrix);
        }

        private void DecomposeMatrix()
        {
            position = matrix.TranslationVector;
            scale = matrix.ScaleVector;
            // I really don't know will it work or not but I think nobody will get the rotation after building from matrix
            rotation = Quaternion.RotationMatrix(matrix);
        }
    }
}