using System.Drawing;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class BorderTests
    {
        private class ContentElement : UIElement
        {
            private Size contentSize;

            public Size ContentSize
            {
                get => contentSize;
                set
                {
                    if (SetProperty(ref contentSize, value))
                        InvalidateMeasure();
                }
            }

            protected override Size MeasureContent() => ContentSize;

            protected override void ArrangeContent()
            {
                // Test element doesn't need to arrange content
            }
        }

        [Fact]
        public void Measure_WithChildAndBorderThickness_IncludesBothInDesiredSize()
        {
            var child = new ContentElement { ContentSize = new Size(100, 50) };
            var border = new Border { Child = child, BorderThickness = new Thickness(5, 10, 15, 20) };

            var size = border.Measure();

            Assert.Equal(100 + 5 + 15, size.Width);
            Assert.Equal(50 + 10 + 20, size.Height);
        }

        [Fact]
        public void Measure_WithNoChild_ReturnsJustBorderThickness()
        {
            var border = new Border { BorderThickness = new Thickness(5, 10, 15, 20) };

            var size = border.Measure();

            Assert.Equal(5 + 15, size.Width);
            Assert.Equal(10 + 20, size.Height);
        }

        [Fact]
        public void ContentBounds_InsetsActualBoundsByBorderThickness()
        {
            // Explicit size, comfortably larger than the border thickness on every side, so ActualBounds
            // doesn't get clamped to zero by the empty (default) container this standalone element arranges into.
            var border = new Border { Width = 200, Height = 200, BorderThickness = new Thickness(5, 10, 15, 20) };
            border.Arrange();

            Assert.Equal(border.ActualBounds.X + 5, border.ContentBounds.X);
            Assert.Equal(border.ActualBounds.Y + 10, border.ContentBounds.Y);
            Assert.Equal(border.ActualBounds.Width - 20, border.ContentBounds.Width);
            Assert.Equal(border.ActualBounds.Height - 30, border.ContentBounds.Height);
        }

        [Fact]
        public void Child_SetAndCleared_PropagatesParentAndCanvas()
        {
            var border = new Border();
            var child = new ContentElement();

            border.Child = child;
            Assert.Same(border, child.Parent);

            border.Child = null;
            Assert.Null(child.Parent);
        }

        [Fact]
        public void Child_Reassigned_DetachesPreviousChild()
        {
            var border = new Border();
            var firstChild = new ContentElement();
            var secondChild = new ContentElement();

            border.Child = firstChild;
            border.Child = secondChild;

            Assert.Null(firstChild.Parent);
            Assert.Same(border, secondChild.Parent);
        }

        [Fact]
        public void Arrange_WithChild_ArrangesChildWithoutThrowing()
        {
            var child = new ContentElement { ContentSize = new Size(100, 50) };
            var border = new Border { Child = child, BorderThickness = new Thickness(5, 10, 15, 20) };

            border.Arrange();

            Assert.False(child.ActualBounds.IsEmpty);
        }
    }
}
