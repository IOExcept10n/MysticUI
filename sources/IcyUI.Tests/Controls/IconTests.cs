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
    }
}
