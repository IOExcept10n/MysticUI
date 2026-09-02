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

        [Fact]
        public void Padding_ForwardsToInternalChrome()
        {
            var control = new Control { Padding = new Thickness(2, 3, 4, 5) };

            Assert.Equal(new Thickness(2, 3, 4, 5), control.Padding);
        }

        [Fact]
        public void Padding_IsNotDoubleCountedInMeasure()
        {
            // Regression: Control.Padding forwards to Chrome.Padding (like Background/BorderBrush/BorderThickness
            // already do) so Chrome's own Measure() adds it exactly once - Control's own base UIElement.Padding
            // field must stay untouched (always zero) or the generic UIElement.Measure() padding step would add
            // it again on top, doubling it.
            var control = new Control { Padding = new Thickness(5) };

            Size measured = control.Measure();

            Assert.Equal(new Size(10, 10), measured);
        }

        [Fact]
        public void ContentBounds_InsetsByBothBorderThicknessAndPadding()
        {
            // Regression: Control.ContentBounds used to subtract only Padding, ignoring BorderThickness entirely -
            // the actual content-available area needs both.
            var control = new Control { BorderThickness = new Thickness(2), Padding = new Thickness(3) };
            control.Arrange(new Rectangle(0, 0, 100, 100));

            Assert.Equal(new Rectangle(5, 5, 90, 90), control.ContentBounds);
        }

        [Fact]
        public void Draw_ChromeBackgroundCoversFullBoundsIncludingPadding()
        {
            // Regression: Control.ArrangeContent used to arrange Chrome into ContentBounds (already inset by
            // Padding), shrinking Background/BorderBrush - drawn across Chrome's own full local bounds - down to
            // roughly the size of the content. Background must cover the whole control (including the padding
            // band), matching the standard box model; only the actual content is inset by Padding.
            var control = new Control
            {
                Background = new SolidColorBrush(Color.Blue),
                Padding = new Thickness(10),
            };

            control.Arrange(new Rectangle(0, 0, 50, 50));

            var chromeProperty = typeof(Control).GetProperty("Chrome", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var chrome = (Border)chromeProperty.GetValue(control)!;

            Assert.Equal(control.ActualBounds, chrome.ActualBounds);
        }
    }
}
