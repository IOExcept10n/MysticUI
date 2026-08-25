using System.Drawing;
using Icy.Diagnostics;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Diagnostics
{
    public class BoxModelResolverTests
    {
        [Fact]
        public void Resolve_PlainUIElement_HasNoPaddingOrContentBox()
        {
            var element = new UIElement { Width = 100, Height = 50, Margin = new Thickness(4) };
            element.Arrange(new Rectangle(0, 0, 200, 200));

            BoxModel box = BoxModelResolver.Resolve(element);

            Assert.Equal(new Rectangle(Point.Empty, element.ActualBounds.Size), box.BorderBox);
            Assert.Null(box.PaddingBox);
            Assert.Null(box.ContentBox);
        }

        [Fact]
        public void Resolve_AnyElement_MarginBoxIsBorderBoxExpandedByMargin()
        {
            var element = new UIElement { Width = 100, Height = 50, Margin = new Thickness(3, 4, 5, 6) };
            element.Arrange(new Rectangle(0, 0, 200, 200));

            BoxModel box = BoxModelResolver.Resolve(element);

            Assert.Equal(box.BorderBox + element.Margin, box.MarginBox);
        }

        [Fact]
        public void Resolve_Border_HasAPaddingBoxInsetByBorderThickness()
        {
            var border = new Border { Width = 100, Height = 60, BorderThickness = new Thickness(5) };
            border.Arrange(new Rectangle(0, 0, 200, 200));

            BoxModel box = BoxModelResolver.Resolve(border);

            Assert.NotNull(box.PaddingBox);
            Assert.Equal(box.BorderBox - border.BorderThickness, box.PaddingBox);
        }

        [Fact]
        public void Resolve_Border_HasAContentBoxSinceItImplementsIContainerLayout()
        {
            var border = new Border { Width = 100, Height = 60, BorderThickness = new Thickness(5) };
            border.Arrange(new Rectangle(0, 0, 200, 200));

            BoxModel box = BoxModelResolver.Resolve(border);

            Assert.NotNull(box.ContentBox);
        }

        [Fact]
        public void Resolve_Panel_HasAContentBoxButNoPaddingBox()
        {
            var panel = new StackPanel { Width = 100, Height = 60, Padding = new Thickness(4) };
            panel.Arrange(new Rectangle(0, 0, 200, 200));

            BoxModel box = BoxModelResolver.Resolve(panel);

            Assert.Null(box.PaddingBox);
            Assert.NotNull(box.ContentBox);
        }

        [Fact]
        public void Resolve_Control_HasDistinctPaddingAndContentBoxes()
        {
            // Control.BorderThickness insets the padding box (Chrome's own border), but Control.ContentBounds
            // insets by Padding - a different thickness here on purpose, so the two boxes are genuinely distinct.
            var control = new ContentControl { Width = 100, Height = 60, BorderThickness = new Thickness(3), Padding = new Thickness(8) };
            control.Arrange(new Rectangle(0, 0, 200, 200));

            BoxModel box = BoxModelResolver.Resolve(control);

            Assert.NotNull(box.PaddingBox);
            Assert.NotNull(box.ContentBox);
            Assert.NotEqual(box.PaddingBox, box.ContentBox);
        }

        [Fact]
        public void Resolve_ElementWithZeroBorderThickness_HasNoPaddingBox()
        {
            var border = new Border { Width = 100, Height = 60, BorderThickness = Thickness.Zero };
            border.Arrange(new Rectangle(0, 0, 200, 200));

            BoxModel box = BoxModelResolver.Resolve(border);

            Assert.Null(box.PaddingBox);
        }
    }
}
