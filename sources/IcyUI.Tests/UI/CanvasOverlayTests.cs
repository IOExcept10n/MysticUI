using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasOverlayTests
    {
        private static Canvas CreateCanvas() =>
            new(new IcyConfiguration(new Tests.Input.FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration()));

        [Fact]
        public void AddOverlay_AddsToOverlaysAndWiresCanvas()
        {
            var canvas = CreateCanvas();
            var overlay = new UIElement();

            canvas.AddOverlay(overlay);

            Assert.Contains(overlay, canvas.Overlays);
            Assert.Same(canvas, overlay.Canvas);
        }

        [Fact]
        public void RemoveOverlay_RemovesFromOverlaysAndClearsCanvas()
        {
            var canvas = CreateCanvas();
            var overlay = new UIElement();
            canvas.AddOverlay(overlay);

            bool removed = canvas.RemoveOverlay(overlay);

            Assert.True(removed);
            Assert.DoesNotContain(overlay, canvas.Overlays);
            Assert.Null(overlay.Canvas);
        }

        [Fact]
        public void RemoveOverlay_NotPresent_ReturnsFalse()
        {
            var canvas = CreateCanvas();

            Assert.False(canvas.RemoveOverlay(new UIElement()));
        }

        [Fact]
        public void Overlay_IsNotHitTestable()
        {
            // Overlays deliberately don't participate in HitTest/Add's root-element list - a drag-and-drop ghost
            // sitting under the cursor must never itself become the hit-tested element.
            var canvas = CreateCanvas();
            var overlay = new UIElement { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.AddOverlay(overlay);
            canvas.Render();

            Assert.Null(canvas.HitTest(new Point(10, 10)));
        }

        [Fact]
        public void Render_ArrangesAndDrawsOverlayElements()
        {
            var canvas = CreateCanvas();
            canvas.IsVisible = true;
            var overlay = new UIElement { Width = 50, Height = 50, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.AddOverlay(overlay);

            canvas.Render();

            Assert.False(overlay.ActualBounds.IsEmpty);
        }
    }
}
