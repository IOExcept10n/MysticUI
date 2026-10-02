using System.Drawing;
using System.Numerics;
using Icy.Rendering;
using Xunit;

namespace Icy.Tests.Rendering
{
    public class PixelSnappingTests
    {
        private static Transform2D ScaleBy(float s) => Transform2D.Create(Matrix3x2.CreateScale(s));

        [Fact]
        public void TrySnap_RoundsEdgesToWholePixels()
        {
            Assert.True(PixelSnapping.TrySnap(ScaleBy(1.5f), new Rectangle(1, 1, 3, 3), 0f, Vector2.Zero, out Vector2 position, out Vector2 size));

            Assert.Equal(new Vector2(2f, 2f), position); // 1.5 → 2
            Assert.Equal(new Vector2(4f, 4f), size);     // right edge 6 → 6
        }

        [Fact]
        public void TrySnap_SharedEdgesStayShared_At125Percent()
        {
            var transform = ScaleBy(1.25f);
            for (int x = 0; x < 20; x++)
            {
                PixelSnapping.TrySnap(transform, new Rectangle(x, 0, 1, 1), 0f, Vector2.Zero, out Vector2 leftPos, out Vector2 leftSize);
                PixelSnapping.TrySnap(transform, new Rectangle(x + 1, 0, 1, 1), 0f, Vector2.Zero, out Vector2 rightPos, out _);

                Assert.Equal(leftPos.X + leftSize.X, rightPos.X);
            }
        }

        [Fact]
        public void TrySnap_Rotated_DoesNotSnap()
        {
            Assert.False(PixelSnapping.TrySnap(ScaleBy(1f), new Rectangle(0, 0, 5, 5), 0.5f, Vector2.Zero, out _, out _));
            Assert.False(PixelSnapping.TrySnap(Transform2D.Create(Matrix3x2.CreateRotation(0.5f)), new Rectangle(0, 0, 5, 5), 0f, Vector2.Zero, out _, out _));
        }

        [Fact]
        public void TrySnap_NonZeroOrigin_DoesNotSnap() =>
            Assert.False(PixelSnapping.TrySnap(ScaleBy(1f), new Rectangle(0, 0, 5, 5), 0f, new Vector2(2, 2), out _, out _));

        [Fact]
        public void TrySnap_IdentityIntegerRect_IsUnchanged()
        {
            Assert.True(PixelSnapping.TrySnap(ScaleBy(1f), new Rectangle(3, 4, 10, 20), 0f, Vector2.Zero, out Vector2 position, out Vector2 size));

            Assert.Equal(new Vector2(3, 4), position);
            Assert.Equal(new Vector2(10, 20), size);
        }
    }
}
