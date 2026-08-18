using System.Drawing;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class StackPanelTests
    {
        [Fact]
        public void Vertical_StacksChildrenTopToBottom()
        {
            var a = new UIElement { Width = 40, Height = 10 };
            var b = new UIElement { Width = 40, Height = 20 };
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(a);
            stack.Children.Add(b);

            stack.Arrange(new Rectangle(0, 0, 100, 200));

            Assert.Equal(0, a.ActualBounds.Y);
            Assert.Equal(10, b.ActualBounds.Y);
        }

        [Fact]
        public void Horizontal_StacksChildrenLeftToRight()
        {
            var a = new UIElement { Width = 10, Height = 30 };
            var b = new UIElement { Width = 20, Height = 30 };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(a);
            stack.Children.Add(b);

            stack.Arrange(new Rectangle(0, 0, 200, 100));

            Assert.Equal(0, a.ActualBounds.X);
            Assert.Equal(10, b.ActualBounds.X);
        }

        [Fact]
        public void MeasureContent_Vertical_SumsHeightsAndTakesMaxWidth()
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(new UIElement { Width = 40, Height = 10 });
            stack.Children.Add(new UIElement { Width = 60, Height = 20 });

            Size measured = stack.Measure();

            Assert.Equal(new Size(60, 30), measured);
        }

        [Fact]
        public void MeasureContent_Horizontal_SumsWidthsAndTakesMaxHeight()
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new UIElement { Width = 10, Height = 40 });
            stack.Children.Add(new UIElement { Width = 20, Height = 60 });

            Size measured = stack.Measure();

            Assert.Equal(new Size(30, 60), measured);
        }

        [Fact]
        public void MeasureContent_Vertical_IncludesChildMargins()
        {
            // Regression: MeasureContent used to ignore Margin entirely while ArrangeContent already accounted
            // for it when positioning children - under-reporting the panel's true height by the sum of every
            // child's margin. Most consequential for ScrollViewer.ExtentHeight (reads this measured size as the
            // content's real extent), which under-clamped VerticalOffset's scrollable range, leaving the last few
            // items of a long list permanently unreachable by scrolling.
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(new UIElement { Width = 40, Height = 10, Margin = new Thickness(0, 0, 0, 6) });
            stack.Children.Add(new UIElement { Width = 60, Height = 20, Margin = new Thickness(0, 0, 0, 6) });

            Size measured = stack.Measure();

            Assert.Equal(new Size(60, 42), measured);
        }

        [Fact]
        public void MeasureContent_Horizontal_IncludesChildMargins()
        {
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            stack.Children.Add(new UIElement { Width = 10, Height = 40, Margin = new Thickness(0, 0, 4, 0) });
            stack.Children.Add(new UIElement { Width = 20, Height = 60, Margin = new Thickness(0, 0, 4, 0) });

            Size measured = stack.Measure();

            Assert.Equal(new Size(38, 60), measured);
        }

        [Fact]
        public void ArrangeContent_SkipsInvisibleChildren()
        {
            var a = new UIElement { Width = 40, Height = 10, IsVisible = false };
            var b = new UIElement { Width = 40, Height = 20 };
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(a);
            stack.Children.Add(b);

            stack.Arrange(new Rectangle(0, 0, 100, 200));

            Assert.Equal(0, b.ActualBounds.Y);
        }
    }
}
