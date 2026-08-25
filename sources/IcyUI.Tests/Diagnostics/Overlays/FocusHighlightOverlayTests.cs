using System.Drawing;
using Icy.Diagnostics.Overlays;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Diagnostics.Overlays
{
    public class FocusHighlightOverlayTests
    {
        [Fact]
        public void Render_ElementWithNoRelevantState_DrawsNothing()
        {
            var element = new UIElement { Width = 40, Height = 20 };
            element.Arrange(new Rectangle(0, 0, 200, 200));
            var context = new FakeRenderContext();
            var overlay = new FocusHighlightOverlay();

            overlay.Render(element, context);

            Assert.Empty(context.DrawCalls);
        }

        [Theory]
        [InlineData(ControlState.Focused)]
        [InlineData(ControlState.Hovered)]
        [InlineData(ControlState.Pressed)]
        public void Render_ElementWithARelevantState_DrawsOneOutline(ControlState state)
        {
            var element = new UIElement { Width = 40, Height = 20, ControlState = state };
            element.Arrange(new Rectangle(0, 0, 200, 200));
            var context = new FakeRenderContext();
            var overlay = new FocusHighlightOverlay();

            overlay.Render(element, context);

            // One DrawRectangle call = 4 strips.
            Assert.Equal(4, context.DrawCalls.Count);
        }

        [Fact]
        public void Render_ElementThatIsBothHoveredAndFocused_DrawsBothOutlines()
        {
            var element = new UIElement { Width = 40, Height = 20, ControlState = ControlState.Hovered | ControlState.Focused };
            element.Arrange(new Rectangle(0, 0, 200, 200));
            var context = new FakeRenderContext();
            var overlay = new FocusHighlightOverlay();

            overlay.Render(element, context);

            Assert.Equal(8, context.DrawCalls.Count);
        }

        [Fact]
        public void Name_IsFocus()
        {
            Assert.Equal("Focus", new FocusHighlightOverlay().Name);
        }
    }
}
