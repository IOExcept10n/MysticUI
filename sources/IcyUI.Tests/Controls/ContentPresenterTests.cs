// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Linq;
using Icy.Rendering;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    /// <summary>
    /// Covers <see cref="ContentPresenter"/> - a pure passthrough single-child host, mirroring
    /// <see cref="Icy.Tests.UI.BorderTests"/>'s child-hosting coverage minus everything specific to
    /// <see cref="Border"/>'s own background/border decoration, which <see cref="ContentPresenter"/> has none of.
    /// </summary>
    public class ContentPresenterTests
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
        public void Measure_WithContent_IncludesContentInDesiredSize()
        {
            var content = new ContentElement { ContentSize = new Size(100, 50) };
            var presenter = new ContentPresenter { Content = content };

            var size = presenter.Measure();

            Assert.Equal(100, size.Width);
            Assert.Equal(50, size.Height);
        }

        [Fact]
        public void Measure_WithNoContent_ReturnsEmpty()
        {
            var presenter = new ContentPresenter();

            var size = presenter.Measure();

            Assert.Equal(Size.Empty, size);
        }

        [Fact]
        public void Measure_WithContentMargin_IncludesMarginInDesiredSize()
        {
            // Regression guard mirroring BorderTests' equivalent test: UIElement.Measure()/DesiredSize excludes a
            // child's own Margin, so ContentPresenter must add it back in itself.
            var content = new ContentElement { ContentSize = new Size(100, 50), Margin = new Thickness(1, 2, 3, 4) };
            var presenter = new ContentPresenter { Content = content };

            var size = presenter.Measure();

            Assert.Equal(100 + 1 + 3, size.Width);
            Assert.Equal(50 + 2 + 4, size.Height);
        }

        [Fact]
        public void ContentBounds_InsetsActualBoundsByPadding()
        {
            var presenter = new ContentPresenter { Width = 200, Height = 200, Padding = new Thickness(5, 10, 15, 20) };
            presenter.Arrange();

            Assert.Equal(presenter.ActualBounds.X + 5, presenter.ContentBounds.X);
            Assert.Equal(presenter.ActualBounds.Y + 10, presenter.ContentBounds.Y);
            Assert.Equal(presenter.ActualBounds.Width - 20, presenter.ContentBounds.Width);
            Assert.Equal(presenter.ActualBounds.Height - 30, presenter.ContentBounds.Height);
        }

        [Fact]
        public void Content_SetAndCleared_PropagatesParentAndCanvas()
        {
            var presenter = new ContentPresenter();
            var content = new ContentElement();

            presenter.Content = content;
            Assert.Same(presenter, content.Parent);

            presenter.Content = null;
            Assert.Null(content.Parent);
        }

        [Fact]
        public void Content_Reassigned_DetachesPreviousContent()
        {
            var presenter = new ContentPresenter();
            var first = new ContentElement();
            var second = new ContentElement();

            presenter.Content = first;
            presenter.Content = second;

            Assert.Null(first.Parent);
            Assert.Same(presenter, second.Parent);
        }

        [Fact]
        public void Arrange_WithContent_ArrangesContentWithoutThrowing()
        {
            var content = new ContentElement { ContentSize = new Size(100, 50) };
            var presenter = new ContentPresenter { Content = content };

            presenter.Arrange(new Rectangle(0, 0, 100, 50));

            Assert.False(content.ActualBounds.IsEmpty);
        }

        [Fact]
        public void Arrange_ContentWithMarginStretchedIntoAutoSizedPresenter_ContentIsNotShrunkBelowDesiredSize()
        {
            var content = new ContentElement { ContentSize = new Size(100, 50), Margin = new Thickness(0, 0, 0, 6) };
            var presenter = new ContentPresenter { Content = content };

            // Arrange into a container sized to exactly what the presenter itself measured - mirrors
            // BorderTests' equivalent regression test.
            Size measured = presenter.Measure();
            presenter.Arrange(new Rectangle(Point.Empty, measured));

            Assert.True(
                content.ActualBounds.Height >= content.DesiredSize.Height,
                $"Content was arranged at height {content.ActualBounds.Height}, below its own DesiredSize.Height {content.DesiredSize.Height}.");
        }

        [Fact]
        public void GetVisualChildren_WithContent_YieldsIt()
        {
            var content = new ContentElement();
            var presenter = new ContentPresenter { Content = content };

            Assert.Same(content, presenter.EnumerateVisualSubtree().Skip(1).Single());
        }

        [Fact]
        public void Draw_WithContent_DrawsIt()
        {
            var presenter = new ContentPresenter
            {
                Width = 50,
                Height = 50,
                Content = new Border { Width = 50, Height = 50 },
            };
            presenter.Arrange(new Rectangle(0, 0, 50, 50));

            var context = new FakeRenderContext { Transform = Transform2D.Identity };
            presenter.Draw(context);

            // A ContentPresenter draws nothing of its own - the only draw call comes from Content (the Border's
            // background fill).
            Assert.Single(context.DrawCalls);
        }
    }
}
