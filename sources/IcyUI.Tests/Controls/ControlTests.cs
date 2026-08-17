using System.Drawing;
using Icy.Rendering.Brushes;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ControlTests
    {
        [Fact]
        public void Background_ForwardsToInternalChrome()
        {
            var control = new Control();
            var brush = new SolidColorBrush(Color.Red);

            control.Background = brush;

            Assert.Same(brush, control.Background);
        }

        [Fact]
        public void BorderThickness_ForwardsToInternalChrome()
        {
            var control = new Control { BorderThickness = new Thickness(2, 3, 4, 5) };

            Assert.Equal(new Thickness(2, 3, 4, 5), control.BorderThickness);
        }

        [Fact]
        public void MeasureContent_WithNoContent_EqualsBorderThickness()
        {
            var control = new Control { BorderThickness = new Thickness(5) };

            Size measured = control.Measure();

            Assert.Equal(new Size(10, 10), measured);
        }

        [Fact]
        public void Draw_DrawsInternalChromeBackground()
        {
            var control = new Control { Background = new SolidColorBrush(Color.Blue) };
            var context = new FakeRenderContext();

            control.Arrange(new Rectangle(0, 0, 50, 50));
            control.Draw(context);

            Assert.Single(context.DrawCalls);
        }
    }
}
