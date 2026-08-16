using System.Diagnostics;
using System.Drawing;
using System.Numerics;
using Icy.Data;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class UIElementTests
    {
        private class TestElement : UIElement
        {
            private Size contentSize;

            public Size ContentSize
            {
                get => contentSize;
                set
                {
                    if (SetProperty(ref contentSize, value))
                    {
                        InvalidateMeasure();
                    }
                }
            }

            protected override Size MeasureContent()
            {
                return ContentSize;
            }

            protected override void ArrangeContent()
            {
                // Test element doesn't need to arrange content
            }
        }

        [Fact]
        public void Measure_WithContent_ReturnsCorrectSize()
        {
            // Arrange
            var element = new TestElement { ContentSize = new Size(100, 50) };

            // Act
            var size = element.Measure();

            // Assert
            Assert.Equal(100, size.Width);
            Assert.Equal(50, size.Height);
        }

        [Fact]
        public void Measure_WithNaNWidth_ReturnsContentWidth()
        {
            // Arrange
            var element = new TestElement 
            { 
                ContentSize = new Size(100, 50),
                Width = float.NaN
            };

            // Act
            var size = element.Measure();

            // Assert
            Assert.Equal(100, size.Width);
        }

        [Fact]
        public void Measure_WithExplicitWidth_ReturnsExplicitWidth()
        {
            // Arrange
            var element = new TestElement 
            { 
                ContentSize = new Size(100, 50),
                Width = 200
            };

            // Act
            var size = element.Measure();

            // Assert
            Assert.Equal(200, size.Width);
        }

        [Fact]
        public void Measure_WithPadding_IncludesPaddingInSize()
        {
            // Arrange
            var element = new TestElement 
            { 
                ContentSize = new Size(100, 50),
                Padding = new Thickness(10, 20, 30, 40)
            };

            // Act
            var size = element.Measure();

            // Assert
            Assert.Equal(140, size.Width);  // 100 + 10 + 30
            Assert.Equal(110, size.Height); // 50 + 20 + 40
        }

        [Fact]
        public void Arrange_WithStretchAlignment_ReturnsZeroBoundsOnEmptySpace()
        {
            // Arrange
            var element = new TestElement 
            { 
                ContentSize = new Size(100, 50),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Act
            element.Arrange();

            // Assert
            Assert.Equal(Rectangle.Empty, element.ActualBounds); // If the space is empty, zero bounds should be returned
        }

        [Fact]
        public void Arrange_WithCenterAlignment_PositionsInCenter()
        {
            // Arrange
            var element = new TestElement 
            { 
                ContentSize = new Size(100, 50),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Act
            element.Arrange();

            // Assert
            Assert.Equal(0, element.ActualBounds.Center().X); // Center of 0-width container
            Assert.Equal(0, element.ActualBounds.Center().Y); // Center of 0-height container
        }

        [Fact]
        public void Arrange_WithMargins_RespectsMargins()
        {
            // Arrange
            var element = new TestElement 
            { 
                ContentSize = new Size(100, 50),
                Margin = new Thickness(10, 20, 30, 40)
            };

            // Act
            element.Arrange();

            // Assert
            Assert.Equal(10, element.ActualBounds.X);
            Assert.Equal(20, element.ActualBounds.Y);
        }

    }
} 