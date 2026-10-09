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
        public void Overlay_IsHitTestable()
        {
            // Overlays now participate in HitTest (checked first, before root elements) to support features like
            // Selector's dropdown popup - they're drawn last (on top) and should be hittable to support pointer/touch.
            // Overlays still don't participate in Add's root-element list or focus traversal.
            var canvas = CreateCanvas();
            var overlay = new UIElement { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.AddOverlay(overlay);
            canvas.Render();

            UIElement? hit = canvas.HitTest(new Point(10, 10));
            Assert.Same(overlay, hit);
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

        [Fact]
        public void BringOverlayToFront_ReordersWithoutDetaching()
        {
            var canvas = CreateCanvas();
            var low = new Border();
            var high = new Border();
            var owner = new Border();
            canvas.AddOverlay(low, owner);
            canvas.AddOverlay(high);

            Assert.True(canvas.BringOverlayToFront(low));

            Assert.Equal([high, low], canvas.Overlays);
            Assert.Same(canvas, low.Canvas);
            Assert.Same(owner, canvas.GetOverlayOwner(low));
            Assert.False(canvas.BringOverlayToFront(new Border()));
        }
    }
}
