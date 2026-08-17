using System.Drawing;
using Icy.Input.Events;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ScrollViewerTests
    {
        private sealed class TestScrollViewer : ScrollViewer
        {
            public void RaiseScroll(ScrollInfo info) => OnScroll(info);
        }

        [Fact]
        public void ArrangeContent_AllowsContentLargerThanViewport()
        {
            var content = new UIElement { Width = 100, Height = 500 };
            var scrollViewer = new ScrollViewer { Width = 100, Height = 100, Content = content };

            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            Assert.Equal(500, content.ActualBounds.Height);
        }

        [Fact]
        public void VerticalOffset_ClampsToScrollableRange()
        {
            var scrollViewer = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                Content = new UIElement { Width = 100, Height = 500 },
            };
            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            scrollViewer.VerticalOffset = 10000;

            Assert.Equal(400, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void VerticalOffset_NegativeClampsToZero()
        {
            var scrollViewer = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                Content = new UIElement { Width = 100, Height = 500 },
            };
            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            scrollViewer.VerticalOffset = -50;

            Assert.Equal(0, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void VerticalOffset_OffsetsContentLayoutPosition()
        {
            var content = new UIElement { Width = 100, Height = 500 };
            var scrollViewer = new ScrollViewer { Width = 100, Height = 100, Content = content };
            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            scrollViewer.VerticalOffset = 50;

            Assert.Equal(-50, content.LayoutOffset.Y);
        }

        [Fact]
        public void Scroll_UpdatesVerticalOffset()
        {
            var scrollViewer = new TestScrollViewer
            {
                Width = 100,
                Height = 100,
                Content = new UIElement { Width = 100, Height = 500 },
            };
            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            // OnScroll applies VerticalOffset -= info.Delta, so a negative delta scrolls the offset forward.
            scrollViewer.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void ContentIsNull_ExtentIsZero()
        {
            var scrollViewer = new ScrollViewer { Width = 100, Height = 100 };
            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            Assert.Equal(0, scrollViewer.ExtentHeight);
            Assert.Equal(0, scrollViewer.ExtentWidth);
        }
    }
}
