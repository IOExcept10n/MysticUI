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
    }
}
