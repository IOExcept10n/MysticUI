using System.Drawing;
using Icy.Rendering.Brushes;
using Icy.Tests.Rendering;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ImageTests
    {
        [Fact]
        public void MeasureContent_ReturnsSourceSize()
        {
            var context = new FakeRenderContext();
            var texture = context.CreateTexture<byte>(20, 10, new byte[20 * 10]);
            var image = new Image { Source = new ImageBrush(texture) };

            Size measured = image.Measure();

            Assert.Equal(new Size(20, 10), measured);
        }

        [Fact]
        public void MeasureContent_ReturnsEmpty_WhenNoSource()
        {
            var image = new Image();

            Size measured = image.Measure();

            Assert.Equal(Size.Empty, measured);
        }

        [Fact]
        public void Draw_DrawsSourceBrush()
        {
            var context = new FakeRenderContext();
            var texture = context.CreateTexture<byte>(20, 10, new byte[20 * 10]);
            var image = new Image { Source = new ImageBrush(texture) };

            image.Arrange(new Rectangle(0, 0, 20, 10));
            image.Draw(context);

            Assert.Single(context.DrawCalls);
            Assert.Same(texture, context.DrawCalls[0].Texture);
        }

        [Fact]
        public void Draw_DrawsNothing_WhenNoSource()
        {
            var context = new FakeRenderContext();
            var image = new Image();

            image.Arrange(new Rectangle(0, 0, 20, 10));
            image.Draw(context);

            Assert.Empty(context.DrawCalls);
        }
    }
}
