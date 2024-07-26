// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using AquaUI.Rendering;
using System.Numerics;

namespace AquaUI.Tests.Rendering
{
    public class Transform2DTests
    {
        private const float Epsilon = 1e-5f;

        public static TheoryData<Vector2, float, Vector2> GenerateForMatrix()
        {
            return new()
            {
                { new(0, 0), 0f, new(1, 1) }, // Identity
                { new(10, 5), 0f, new(1, 1) }, // Translation only
                { new(0, 0), MathF.PI / 2, new(1, 1) }, // Rotation 90 degrees
                { new(0, 0), 0f, new(2, 2) }, // Scaling only
                { new(10, 5), MathF.PI / 4, new(2, 2) } // Translation, rotation, and scaling
            };
        }

        public static TheoryData<Transform2D, Vector2, Vector2> GenerateToTransform()
        {
            return new()
            {
                { Transform2D.Identity, new(1, 1), new(1, 1) }, // Identity
                { Transform2D.Create(new(10, 5), 0f, new(1, 1)), new(2, 2), new(12, 7) }, // Translation only
                { Transform2D.Create(new(0, 0), MathF.PI / 2, new(1, 1)), new(1, 0), new(0, 1) }, // Rotation 90 degrees
                { Transform2D.Create(new(0, 0), 0f, new(2, 2)), new(1, 1), new(2, 2) }, // Scaling only
                { Transform2D.Create(new(10, 5), MathF.PI / 4, new(2, 2)), new(1, 1), new(10, 5 + 2 * MathF.Sqrt(2)) } // Combined
            };
        }

        public static TheoryData<Transform2D, Transform2D, Vector2> GenerateToAdd()
        {
            return new()
        {
            {
                Transform2D.Create(new(1, 1), 0f, new(1, 1)),
                Transform2D.Create(new(2, 2), 0f, new(1, 1)),
                new(1, 1)
            },
            {
                Transform2D.Create(new(0, 0), MathF.PI / 4, new(1, 1)),
                Transform2D.Create(new(0, 0), MathF.PI / 4, new(1, 1)),
                new(1, 0)
            },
            {
                Transform2D.Create(new(1, 1), 0f, new(2, 2)),
                Transform2D.Create(new(1, 1), 0f, new(2, 2)),
                new(1, 1)
            },
            {
                Transform2D.Create(new(1, 1), 0f, new(1, 1)),
                Transform2D.Create(new(1, 1), 0f, new(1, 1)),
                new(1, 1)
            },
            {
                Transform2D.Create(new(0, 0), MathF.PI / 2, new(2, 2)),
                Transform2D.Create(new(0, 0), MathF.PI / 2, new(2, 2)),
                new(1, 1)
            }
        };
        }

        [Theory]
        [MemberData(nameof(GenerateForMatrix))]
        public void TestMatrixDecomposition(Vector2 position, float rotation, Vector2 scale)
        {
            // Arrange
            var transform = Transform2D.Create(position, rotation, scale);

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