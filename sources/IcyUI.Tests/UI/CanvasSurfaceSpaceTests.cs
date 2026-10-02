using System.Drawing;
using System.Numerics;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasSurfaceSpaceTests
    {
        private static UIElement TopLeftBox(int size) => new()
        {
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        [Fact]
        public void HitTest_RootElement_UsesPhysicalPointsAtScale2()
        {
            var (canvas, _, _) = CanvasScalingTests.Create(displayScale: 2f);
            UIElement box = TopLeftBox(100);
            canvas.Add(box);
            canvas.Render();

            Assert.Same(box, canvas.HitTest(new Point(150, 150)));
            Assert.Null(canvas.HitTest(new Point(250, 250)));
        }

        [Fact]
        public void HitTest_Overlay_UsesSurfaceSpaceAtScale2()
        {
            var (canvas, _, _) = CanvasScalingTests.Create(displayScale: 2f);
            UIElement overlay = TopLeftBox(100);
            canvas.AddOverlay(overlay);
            canvas.Render();

            Assert.Same(overlay, canvas.HitTest(new Point(150, 150)));
            Assert.Null(canvas.HitTest(new Point(250, 250)));
        }

        [Fact]
        public void Overlay_IsDrawnWithSurfaceTransform()
        {
            var (canvas, context, _) = CanvasScalingTests.Create(displayScale: 2f);
            canvas.AddOverlay(new Icy.UI.Border { Width = 10, Height = 10, Background = new Icy.Rendering.Brushes.SolidColorBrush(Color.Red), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top });

            canvas.Render();

            var overlayDraw = context.DrawCalls.Last();
            Assert.Equal(new Vector2(2f, 2f), overlayDraw.TransformAtDrawTime.Scale);
        }

        [Fact]
        public void PointToLocal_PointToScreen_RoundTrip_WithDpiAndCanvasTransform()
        {
            var (canvas, _, _) = CanvasScalingTests.Create(displayScale: 2f);
            canvas.Offset = new Vector2(30, 20);
            canvas.Scale = new Vector2(1.5f, 1.5f);
            UIElement box = TopLeftBox(200);
            canvas.Add(box);
            canvas.Render();

            var physical = new Point(301, 207);
            Vector2 local = box.PointToLocal(physical);
            Point back = box.PointToScreen(local);

            Assert.InRange(back.X, physical.X - 1, physical.X + 1);
            Assert.InRange(back.Y, physical.Y - 1, physical.Y + 1);
            Assert.Same(box, canvas.HitTest(physical));
        }

        [Fact]
        public void PointToSurface_IsPhysicalDividedByScale()
        {
            var (canvas, _, _) = CanvasScalingTests.Create(displayScale: 2f);
            canvas.Offset = new Vector2(10, 10);
            UIElement box = TopLeftBox(100);
            canvas.Add(box);
            canvas.Render();

            Point screen = box.PointToScreen(new Vector2(40, 40));
            Point surface = box.PointToSurface(new Vector2(40, 40));

            Assert.Equal(new Point(50, 50), surface);
            Assert.Equal(new Point(100, 100), screen);
        }

        [Fact]
        public void Overlay_IsReArrangedInsideNewSurface_WhenScaleChangesWhileOpen()
        {
            var (canvas, _, configuration) = CanvasScalingTests.Create(displayScale: 1f);
            var overlay = new UIElement { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            canvas.AddOverlay(overlay);
            canvas.Render();
            Assert.Equal(new Size(800, 600), overlay.ActualBounds.Size);

            configuration.Scaling.UserScale = 2f;
            canvas.Render();

            Assert.Equal(new Size(400, 300), overlay.ActualBounds.Size);
        }
    }
}
