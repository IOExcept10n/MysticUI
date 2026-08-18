// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Rendering;
using System.Drawing;
using System.Numerics;
using Xunit;

namespace Icy.Tests.Rendering
{
    public class Transform2DTests
    {
        private const float Epsilon = 1e-5f;

        public static TheoryData<Vector2, float, Vector2, Vector2> GenerateForMatrix()
        {
            return new()
            {
                { new(0, 0), 0f, new(1, 1), Vector2.Zero }, // Identity
                { new(10, 5), 0f, new(1, 1), Vector2.Zero }, // Translation only
                { new(0, 0), MathF.PI / 2, new(1, 1), Vector2.Zero }, // Rotation 90 degrees
                { new(0, 0), 0f, new(2, 2), Vector2.Zero }, // Scaling only
                { new(10, 5), MathF.PI / 4, new(2, 2), Vector2.Zero } // Translation, rotation, and scaling
            };
        }

        public static TheoryData<Transform2D, Vector2, Vector2> GenerateToTransform()
        {
            return new()
            {
                { Transform2D.Identity, new(1, 1), new(1, 1) }, // Identity
                { Transform2D.Create(new(10, 5), 0f, new(0, 0), new(1, 1)), new(2, 2), new(12, 7) }, // Translation only
                { Transform2D.Create(new(0, 0), MathF.PI / 2, new(0, 0), new(1, 1)), new(1, 0), new(0, 1) }, // Rotation 90 degrees
                { Transform2D.Create(new(0, 0), 0f, new(0, 0), new(2, 2)), new(1, 1), new(2, 2) }, // Scaling only
                // Combined: BuildMatrix composes Translate(position) * Rotate(rotation, origin) * Scale(scale) -
                // translation is applied before rotation/scale, so it's affected by both, not just the rotated delta.
                { Transform2D.Create(new(10, 5), MathF.PI / 4, new(10, 5), new(2, 2)), new(1, 1), new(20, 10 + 2 * MathF.Sqrt(2)) }
            };
        }

        public static TheoryData<Transform2D, Transform2D, Vector2> GenerateToAdd()
        {
            return new()
            {
                {
                    Transform2D.Create(new(1, 1), 0f, new(0, 0), new(1, 1)),
                    Transform2D.Create(new(2, 2), 0f, new(0, 0), new(1, 1)),
                    new(1, 1)
                },
                {
                    Transform2D.Create(new(0, 0), MathF.PI / 4, new(0, 0), new(1, 1)),
                    Transform2D.Create(new(0, 0), MathF.PI / 4, new(0, 0), new(1, 1)),
                    new(1, 0)
                },
                {
                    Transform2D.Create(new(1, 1), 0f, new(0, 0), new(2, 2)),
                    Transform2D.Create(new(1, 1), 0f, new(0, 0), new(2, 2)),
                    new(1, 1)
                },
                {
                    Transform2D.Create(new(1, 1), 0f, new(0, 0), new(1, 1)),
                    Transform2D.Create(new(1, 1), 0f, new(0, 0), new(1, 1)),
                    new(1, 1)
                },
                {
                    Transform2D.Create(new(0, 0), MathF.PI / 2, new(0, 0), new(2, 2)),
                    Transform2D.Create(new(0, 0), MathF.PI / 2, new(0, 0), new(2, 2)),
                    new(1, 1)
                }
            };
        }

        [Theory]
        [MemberData(nameof(GenerateForMatrix))]
        public void TestMatrixDecomposition(Vector2 position, float rotation, Vector2 scale, Vector2 origin)
        {
            // Arrange
            var transform = Transform2D.Create(position, rotation, origin, scale);

            // Act
            transform.Matrix.Decompose(out var newPosition, out var newRotation, out var newScale);

            // Assert
            Assert.True(Vector2Equals(position, newPosition));
            Assert.True(MathF.Abs(rotation - newRotation) < Epsilon);
            Assert.True(Vector2Equals(scale, newScale));
        }


        [Theory]
        [MemberData(nameof(GenerateToTransform))]
        public void TestTransformationWorks(Transform2D transform, Vector2 source, Vector2 expected)
        {
            // Act
            var actual = transform.Apply(source);

            // Assert
            Assert.True(Vector2Equals(actual, expected));
        }

        [Fact]
        public void Apply_Rectangle_PureTranslation_PreservesSize()
        {
            var transform = Transform2D.Create(new Vector2(10, 5), 0f, Vector2.Zero, Vector2.One);

            var result = transform.Apply(new Rectangle(0, 0, 80, 40));

            Assert.Equal(new Rectangle(10, 5, 80, 40), result);
        }

        [Fact]
        public void Apply_Rectangle_Rotated90Degrees_ReturnsTrueBoundingBoxNotShrunkSliver()
        {
            // Regression: this used to scale width/height by (matrix.M11, matrix.M22) directly, which mixes
            // rotation and scale for any rotated matrix. A 90-degree rotation multiplies both dimensions by
            // cos(90deg)=0, collapsing the computed "size" to (0,0) entirely (an even more extreme case than the
            // 45-degree sliver a rotated UI element got clipped down to). The true bounding box of an 80x40 rect
            // rotated 90 degrees around its own center is a 40x80 box (dimensions swapped), not a zero-size one.
            var transform = Transform2D.Create(Vector2.Zero, MathF.PI / 2, new Vector2(40, 20), Vector2.One);

            var result = transform.Apply(new Rectangle(0, 0, 80, 40));

            Assert.Equal(40, result.Width);
            Assert.Equal(80, result.Height);
        }

        [Theory]
        [MemberData(nameof(GenerateToAdd))]
        public void TestAddition(Transform2D left, Transform2D right, Vector2 source)
        {
            // Arrange
            var combined = left;
            combined.AddTransform(right);

            // Act
            var test1 = combined.Apply(source);
            var test2 = right.Apply(left.Apply(source));

            // Assert
            Assert.True(Vector2Equals(test1, test2));
        }

        private static bool Vector2Equals(Vector2 a, Vector2 b)
        {
            return MathF.Abs(a.X - b.X) < Epsilon && MathF.Abs(a.Y - b.Y) < Epsilon;
        }
    }
}
