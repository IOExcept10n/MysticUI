using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Rendering;
using Icy.Rendering.Brushes;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ScrollViewerTests
    {
        private sealed class TestScrollViewer : ScrollViewer
        {
            public bool RaiseScroll(ScrollInfo info) => OnScroll(info);
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
        public void Draw_ElementInsideScrolledContent_ScissorTracksScrollOffsetAndNestedBounds()
        {
            // Regression covering two compounding bugs Ivan reported (visible only with nested clipped content,
            // e.g. content inside a ScrollViewer that overflows another ScrollViewer's viewport):
            // 1. UIElement.Draw's ClipToBounds cut used to intersect against the raw, un-transformed ActualBounds,
            //    which never reflects a scrolled ancestor's LayoutOffset - the clip window didn't track content
            //    that had actually moved on screen.
            // 2. The Cut() extension used for that intersection treats its second rectangle as an offset relative
            //    to the first, not an absolute rectangle to intersect - correct for its real callers (texture
            //    sub-region slicing) but wrong for composing nested *screen-space* scissor rects, and only
            //    "worked" by coincidence for a single unnested clip starting at X=0.
            var spacer = new UIElement { Width = 100, Height = 80 };
            var innerBox = new Border { Width = 100, Height = 50, Background = new SolidColorBrush(Color.Red) };
            var content = new StackPanel { Orientation = Orientation.Vertical };
            content.Children.Add(spacer);
            content.Children.Add(innerBox);

            var scrollViewer = new ScrollViewer { Width = 100, Height = 60, Content = content };
            scrollViewer.Arrange(new Rectangle(0, 0, 100, 60));
            scrollViewer.VerticalOffset = 50;

            var context = new FakeRenderContext { ViewportSize = new Size(100, 60), Transform = Transform2D.Identity };
            context.Options.Scissor = new Rectangle(0, 0, 100, 60);

            scrollViewer.Draw(context);

            // innerBox sits at content-local Y=[80,130); scrolled up by 50 it visually occupies screen Y=[30,80),
            // further clipped to the ScrollViewer's own [0,60) viewport - expected visible scissor Y=[30,60).
            // It's the last thing drawn (Chrome's own transparent background fill draws first, then spacer, which
            // draws nothing at all - it's a bare UIElement with no background).
            var innerDraw = context.DrawCalls.Last();
            Assert.Equal(new Rectangle(0, 30, 100, 30), innerDraw.ScissorAtDrawTime);
        }

        [Fact]
        public void OnScroll_AtLimit_ReturnsFalseSoItBubbles()
        {
            var scrollViewer = new TestScrollViewer
            {
                Width = 100,
                Height = 100,
                Content = new UIElement { Width = 100, Height = 200 },
            };
            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            // Max VerticalOffset is 200-100=100; this exhausts it in one step.
            bool consumedFirst = scrollViewer.RaiseScroll(new ScrollInfo(-100, Orientation.Vertical));
            Assert.True(consumedFirst);
            Assert.Equal(100, scrollViewer.VerticalOffset);

            // Already at the limit - nothing left to move, so this must report "not consumed" for Canvas to bubble it.
            bool consumedSecond = scrollViewer.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));
            Assert.False(consumedSecond);
            Assert.Equal(100, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void Scroll_NestedScrollViewer_StaysContainedUntilInnerIsExhaustedThenBubbles()
        {
            // Regression: Canvas used to dispatch a wheel/swipe scroll to every ScrollViewer in the hovered
            // element's ancestor chain unconditionally, so scrolling inside a nested ScrollViewer also scrolled
            // every scrollable ancestor around it at the same time.
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };

            var innerScroll = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = new UIElement { Width = 100, Height = 500 },
            };

            var outerContent = new StackPanel { Orientation = Orientation.Vertical };
            outerContent.Children.Add(innerScroll);
            outerContent.Children.Add(new UIElement { Width = 100, Height = 500 });

            var outerScroll = new ScrollViewer
            {
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Content = outerContent,
            };

            canvas.Add(outerScroll);
            canvas.Render();

            // innerScroll is outerContent's first child, so it fully covers outerScroll's own 100x100 viewport -
            // hovering its center hits deep inside innerScroll, matching the real nested-ScrollViewer layout.
            input.Mouse.MouseInfo = new(new Point(50, 50));
            canvas.Render();

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-20, Orientation.Vertical));

            Assert.Equal(20, innerScroll.VerticalOffset);
            Assert.Equal(0, outerScroll.VerticalOffset);

            // Exhaust the inner viewer's scroll range (max is 500-100=400), then scroll once more - now that the
            // inner ScrollViewer has nowhere left to go, the event must bubble to the outer one instead.
            input.Events.Scroll.RaiseScroll(new ScrollInfo(-1000, Orientation.Vertical));
            Assert.Equal(400, innerScroll.VerticalOffset);

            input.Events.Scroll.RaiseScroll(new ScrollInfo(-1000, Orientation.Vertical));

            Assert.Equal(400, innerScroll.VerticalOffset);
            Assert.Equal(500, outerScroll.VerticalOffset);
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
