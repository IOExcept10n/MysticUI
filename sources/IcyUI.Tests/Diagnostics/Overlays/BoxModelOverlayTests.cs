using System.Drawing;
using Icy.Diagnostics.Overlays;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Diagnostics.Overlays
{
    public class BoxModelOverlayTests
    {
        [Fact]
        public void Render_PlainUIElement_DrawsOnlyMarginAndBorderBoxStrips()
        {
            var element = new UIElement { Width = 100, Height = 50 };
            element.Arrange(new Rectangle(0, 0, 200, 200));
            var context = new FakeRenderContext();
            var overlay = new BoxModelOverlay();

            overlay.Render(element, context);

            // ShapesExtensions.DrawRectangle issues 4 strips per box; a plain UIElement resolves margin+border
            // boxes only (see BoxModelResolverTests), so 2 boxes * 4 strips = 8 draw calls.
            Assert.Equal(8, context.DrawCalls.Count);
        }

        [Fact]
        public void Render_BorderBoxTopStrip_MatchesTheElementsOwnLocalBounds()
        {
            var element = new UIElement { Width = 100, Height = 50 };
            element.Arrange(new Rectangle(0, 0, 200, 200));
            var context = new FakeRenderContext();
            var overlay = new BoxModelOverlay();

            overlay.Render(element, context);

            // The border box is drawn second (after the margin box's 4 strips); DrawRectangle's own strip order
            // is top/bottom/left/right, so index 4 is the border box's top strip - starting at local (0,0), full
            // width, 1px thick.
            var borderTopStrip = context.DrawCalls[4];
            Assert.Equal(new Rectangle(0, 0, (int)element.ActualBounds.Width, 1), borderTopStrip.Options.Destination);
        }

        [Fact]
        public void Name_IsBounds()
        {
            Assert.Equal("Bounds", new BoxModelOverlay().Name);
        }
    }
}
