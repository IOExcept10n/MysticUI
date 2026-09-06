using System.Drawing;
using System.Linq;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class SplitPaneTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var pane = new SplitPane();

            Assert.Equal(Orientation.Horizontal, pane.Orientation);
            Assert.Equal(0.5f, pane.SplitterPosition);
            Assert.Equal(0f, pane.MinFirstSize);
            Assert.Equal(0f, pane.MinSecondSize);
            Assert.Equal(6f, pane.DividerSize);
            Assert.Null(pane.First);
            Assert.Null(pane.Second);
        }

        [Fact]
        public void First_Set_WiresParent()
        {
            var pane = new SplitPane();
            var child = new Border();

            pane.First = child;

            Assert.Same(pane, child.Parent);
        }

        [Fact]
        public void Second_Set_WiresParent()
        {
            var pane = new SplitPane();
            var child = new Border();

            pane.Second = child;

            Assert.Same(pane, child.Parent);
        }

        [Fact]
        public void First_Replaced_UnparentsTheOldValue()
        {
            var pane = new SplitPane();
            var oldChild = new Border();
            var newChild = new Border();
            pane.First = oldChild;

            pane.First = newChild;

            Assert.Null(oldChild.Parent);
            Assert.Same(pane, newChild.Parent);
        }

        [Fact]
        public void GetVisualChildren_YieldsChromeThenFirstThenSecond()
        {
            var pane = new SplitPane();
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            var subtree = pane.EnumerateVisualSubtree().ToList();

            int firstIndex = subtree.IndexOf(first);
            int secondIndex = subtree.IndexOf(second);
            Assert.True(firstIndex >= 0 && secondIndex >= 0 && firstIndex < secondIndex);
            // The default divider (Chrome's Child) is present too, distinct from First/Second.
            Assert.True(subtree.Count(e => e is Border) >= 4); // Chrome + divider + inner line + First + Second
        }

        [Fact]
        public void DividerSize_ChangedAfterConstruction_ResizesTheDivider()
        {
            var pane = new SplitPane { DividerSize = 20f };

            var divider = pane.EnumerateVisualSubtree().OfType<Border>().Skip(1).First(); // Chrome, then divider
            Assert.Equal(20f, divider.Width);
        }

        [Fact]
        public void ArrangeContent_Horizontal_SplitsProportionally()
        {
            var pane = new SplitPane { Width = 200, Height = 100, SplitterPosition = 0.5f };
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            pane.Arrange(new Rectangle(0, 0, 200, 100));

            // available = 200, roaming = 200 - DividerSize(6) = 194; firstWidth = 194 * 0.5 = 97.
            // Second starts after First AND the divider band: 97 + DividerSize(6) = 103.
            Assert.Equal(97, first.ActualBounds.Width);
            Assert.Equal(100, first.ActualBounds.Height);
            Assert.Equal(103, second.ActualBounds.X);
            Assert.Equal(200 - 97 - 6, second.ActualBounds.Width);
        }

        [Fact]
        public void ArrangeContent_Vertical_SplitsAlongHeight()
        {
            var pane = new SplitPane { Width = 100, Height = 200, Orientation = Orientation.Vertical, SplitterPosition = 0.25f };
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            pane.Arrange(new Rectangle(0, 0, 100, 200));

            // roaming = 200 - 6 = 194; firstHeight = 194 * 0.25 = 48 (truncated).
            Assert.Equal(48, first.ActualBounds.Height);
            Assert.Equal(100, first.ActualBounds.Width);
            Assert.Equal(48 + 6, second.ActualBounds.Y);
        }

        [Fact]
        public void ArrangeContent_RespectsMinFirstAndMinSecondSize()
        {
            var pane = new SplitPane
            {
                Width = 200,
                Height = 100,
                SplitterPosition = 0.05f, // would put First far below MinFirstSize without clamping
                MinFirstSize = 50,
                MinSecondSize = 50,
            };
            var first = new Border();
            var second = new Border();
            pane.First = first;
            pane.Second = second;

            pane.Arrange(new Rectangle(0, 0, 200, 100));

            Assert.True(first.ActualBounds.Width >= 50, $"First was {first.ActualBounds.Width}, expected >= 50");
            Assert.True(second.ActualBounds.Width >= 50, $"Second was {second.ActualBounds.Width}, expected >= 50");
        }

        [Fact]
        public void ArrangeContent_UndersizedContainer_DegradesWithoutThrowing()
        {
            // MinFirstSize + MinSecondSize + DividerSize (50+50+6=106) exceeds the 80px available - must not throw,
            // and must still produce a stable (if imperfect) split.
            var pane = new SplitPane { Width = 80, Height = 100, MinFirstSize = 50, MinSecondSize = 50 };
            pane.First = new Border();
            pane.Second = new Border();

            var exception = Record.Exception(() => pane.Arrange(new Rectangle(0, 0, 80, 100)));

            Assert.Null(exception);
        }

        [Fact]
        public void MeasureContent_SumsFirstAndSecondPlusDividerSize()
        {
            var pane = new SplitPane { DividerSize = 6 };
            pane.First = new Border { Width = 80, Height = 40 };
            pane.Second = new Border { Width = 60, Height = 30 };

            Size measured = pane.Measure();

            Assert.Equal(80 + 60 + 6, measured.Width);
            Assert.Equal(40, measured.Height);
        }
    }
}
