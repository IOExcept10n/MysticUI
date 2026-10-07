using System.Drawing;
using Icy.Rendering.Brushes;
using Icy.Tests.Rendering;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class IconTests
    {
        [Fact]
        public void MeasureContent_ReturnsFixedDefaultSize()
        {
            var icon = new Icon();

            Size measured = icon.Measure();

            Assert.Equal(new Size(16, 16), measured);
        }

        [Fact]
        public void OnRender_DrawsWithoutThrowing()
        {
            var icon = new Icon { Kind = IconKind.Checkmark, Stroke = new SolidColorBrush(Color.White) };
            icon.Arrange(new Rectangle(0, 0, 16, 16));
            var context = new FakeRenderContext();

            icon.Draw(context);

            Assert.NotEmpty(context.DrawCalls);
        }

        [Theory]
        [InlineData(IconKind.Checkmark)]
        [InlineData(IconKind.ChevronUp)]
        [InlineData(IconKind.ChevronDown)]
        [InlineData(IconKind.ChevronLeft)]
        [InlineData(IconKind.ChevronRight)]
        public void OnRender_EveryKind_DrawsWithoutThrowing(IconKind kind)
        {
            var icon = new Icon { Kind = kind };
            icon.Arrange(new Rectangle(0, 0, 16, 16));
            var context = new FakeRenderContext();

            icon.Draw(context);

            Assert.NotEmpty(context.DrawCalls);
        }

        [Fact]
        public void OnRender_AwayFromTheOrigin_DrawsInsideItsOwnBounds()
        {
            // Draw() already moves the context to the icon's position, so the glyph must be drawn in local space.
            var icon = new Icon { Kind = IconKind.ChevronRight };
            icon.Arrange(new Rectangle(100, 50, 16, 16));
            var context = new FakeRenderContext { Transform = Icy.Rendering.Transform2D.Identity };

            icon.Draw(context);

            Assert.NotEmpty(context.DrawCalls);
            foreach (var call in context.DrawCalls)
            {
                Rectangle screen = call.TransformAtDrawTime.Apply(call.Options.Destination);
                Assert.InRange(screen.X, 100, 116);
                Assert.InRange(screen.Y, 50, 66);
            }
        }
    
        [Theory]
        [InlineData(IconKind.Checkmark)]
        [InlineData(IconKind.ChevronRight)]
        public void OnRender_DrawsAnOpenStroke_NotAClosedTriangle(IconKind kind)
        {
            var icon = new Icon { Kind = kind };
            icon.Arrange(new Rectangle(0, 0, 16, 16));
            var context = new FakeRenderContext { Transform = Icy.Rendering.Transform2D.Identity };

            icon.Draw(context);

            // Three points, two segments: closing the shape would add a third.
            Assert.Equal(2, context.DrawCalls.Count);
        }

        [Fact]
        public void DrawPolyline_CentersEachSegmentAndCapsBothEnds()
        {
            var context = new FakeRenderContext { Transform = Icy.Rendering.Transform2D.Identity };

            Icy.Rendering.ShapesExtensions.DrawPolyline(context, new System.Numerics.Vector2(5, 0), [new(0, 10), new(10, 10)], Color.White, 2);

            var call = Assert.Single(context.DrawCalls);
            Assert.Equal(new Rectangle(4, 9, 12, 2), call.Options.Destination);
            Assert.Equal(0f, call.Options.Rotation);
        }
    }
}
