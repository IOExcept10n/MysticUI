using System.Drawing;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ContentControlTests
    {
        [Fact]
        public void Content_ForwardsToInternalChromeChild()
        {
            var contentControl = new ContentControl();
            var content = new UIElement();

            contentControl.Content = content;

            Assert.Same(content, contentControl.Content);

            // Content becomes the child of the internal chrome Border, not contentControl directly - see
            // Control.Chrome/ContentControl.Content forwarding to Chrome.Child.
            Assert.IsType<Border>(content.Parent);
        }

        [Fact]
        public void MeasureContent_IncludesContentSizeAndBorderThickness()
        {
            var contentControl = new ContentControl
            {
                BorderThickness = new Thickness(2),
                Content = new UIElement { Width = 40, Height = 20 },
            };

            Size measured = contentControl.Measure();

            Assert.Equal(new Size(44, 24), measured);
        }

        [Fact]
        public void ArrangeContent_PositionsContentInsetByBorderThickness()
        {
            var content = new UIElement { Width = 10, Height = 10 };
            var contentControl = new ContentControl
            {
                BorderThickness = new Thickness(5),
                Content = content,
            };

            contentControl.Arrange(new Rectangle(0, 0, 100, 100));

            // Content is Stretch-aligned by default with a fixed Width/Height, so it centers within the space left
            // after the 5px border inset on every side (90x90, from (5,5) to (95,95)) - see
            // UIElement.CalculateLocation's Stretch-without-NaN-size behavior, which falls back to centering.
            Assert.Equal(45, content.ActualBounds.X);
            Assert.Equal(45, content.ActualBounds.Y);
        }
    }
}
