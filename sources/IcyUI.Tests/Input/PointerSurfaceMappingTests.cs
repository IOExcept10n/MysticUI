using System.Drawing;
using System.Numerics;
using Icy.Input.Devices;
using Xunit;

namespace Icy.Tests.Input
{
    public class PointerSurfaceMappingTests
    {
        [Fact]
        public void SameAspect_IsAPureScale() =>
            Assert.Equal(new Point(640, 360), PointerSurfaceMapping.ToViewport(new Vector2(0.5f, 0.5f), new SizeF(1920, 1080), new Size(1280, 720)));

        [Theory]
        [InlineData(0.5f, 0.078125f, 640, 0)]     // top edge of the picture, just below the top black bar
        [InlineData(0.5f, 0.921875f, 640, 720)]   // bottom edge of the picture
        [InlineData(0f, 0.5f, 0, 360)]
        public void TallerSurface_Letterboxes(float x, float y, int expectedX, int expectedY) =>
            // Regression: Stride fullscreen presented a 16:9 back buffer centered on a 3:2 touch screen; touches arrived
            // shifted down by the bar height because the touch surface is the whole screen.
            Assert.Equal(new Point(expectedX, expectedY), PointerSurfaceMapping.ToViewport(new Vector2(x, y), new SizeF(2880, 1920), new Size(1280, 720)));

        [Fact]
        public void WiderSurface_Pillarboxes() =>
            Assert.Equal(new Point(0, 300), PointerSurfaceMapping.ToViewport(new Vector2(0.125f, 0.5f), new SizeF(1920, 1080), new Size(800, 600)));

        [Fact]
        public void DegenerateSurface_FallsBackToPlainScaling() =>
            Assert.Equal(new Point(400, 300), PointerSurfaceMapping.ToViewport(new Vector2(0.5f, 0.5f), SizeF.Empty, new Size(800, 600)));
    }
}
