using System.Drawing;
using System.Linq;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ExpanderTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var expander = new Expander();

            Assert.False(expander.IsExpanded);
            Assert.Equal(0f, expander.ExpansionProgress);
            Assert.Null(expander.Header);
            Assert.Null(expander.Content);
        }

        [Fact]
        public void Header_Set_ForwardsToTheHeaderToggleAndWiresParent()
        {
            var expander = new Expander();
            var header = new Border();

            expander.Header = header;

            Assert.Same(header, expander.Header);
            Assert.NotNull(header.Parent);
        }

        [Fact]
        public void Content_Set_WiresParentAndClipsToBounds()
        {
            var expander = new Expander();
            var content = new Border();

            expander.Content = content;

            Assert.Same(expander, content.Parent);
            Assert.True(content.ClipToBounds);
        }

        [Fact]
        public void Content_Replaced_UnparentsTheOldValue()
        {
            var expander = new Expander();
            var oldContent = new Border();
            var newContent = new Border();
            expander.Content = oldContent;

            expander.Content = newContent;

            Assert.Null(oldContent.Parent);
            Assert.Same(expander, newContent.Parent);
        }

        [Fact]
        public void Content_Set_StartsNotVisible_SinceExpansionProgressStartsAtZero()
        {
            var expander = new Expander();
            var content = new Border();

            expander.Content = content;

            Assert.False(content.IsVisible);
        }

        [Fact]
        public void GetVisualChildren_YieldsChromeAndContent()
        {
            var expander = new Expander();
            var content = new Border();
            expander.Content = content;

            var subtree = expander.EnumerateVisualSubtree().ToList();

            Assert.Contains(content, subtree);
            // The header toggle lives inside Chrome, distinct from Content.
            Assert.Contains(subtree, e => e is ExpanderHeader);
        }

        [Fact]
        public void IsExpanded_SetsExpandedControlStateFlag()
        {
            var expander = new Expander { IsExpanded = true };

            Assert.Equal(ControlState.Expanded, expander.ControlState & ControlState.Expanded);

            expander.IsExpanded = false;

            Assert.Equal(ControlState.Normal, expander.ControlState & ControlState.Expanded);
        }

        [Fact]
        public void IsExpandedChanged_FiresOncePerRealChange()
        {
            var expander = new Expander();
            int raisedCount = 0;
            expander.IsExpandedChanged += (_, _) => raisedCount++;

            expander.IsExpanded = true;
            expander.IsExpanded = true; // no-op re-assignment - must not raise again

            Assert.Equal(1, raisedCount);
        }

        [Fact]
        public void ExpanderHeader_ChevronRotation_DefaultsToZeroAndIsSettable()
        {
            var header = new ExpanderHeader();

            Assert.Equal(0f, header.ChevronRotation);

            header.ChevronRotation = 90f;

            Assert.Equal(90f, header.ChevronRotation);
        }

        [Fact]
        public void MeasureContent_ExpansionProgressZero_OnlyCountsTheHeader()
        {
            var expander = new Expander { Header = new Border { Width = 40, Height = 20 } };
            expander.Content = new Border { Width = 100, Height = 100 };

            Size measured = expander.Measure();

            Assert.Equal(20, measured.Height);
        }

        [Fact]
        public void MeasureContent_ScalesWithExpansionProgress()
        {
            var expander = new Expander { Header = new Border { Width = 40, Height = 20 } };
            expander.Content = new Border { Width = 100, Height = 100 };

            expander.ExpansionProgress = 0.5f;
            Size half = expander.Measure();

            Assert.Equal(20 + 50, half.Height); // header + half of content's 100px

            expander.ExpansionProgress = 1f;
            Size full = expander.Measure();

            Assert.Equal(20 + 100, full.Height); // header + full content
        }

        [Fact]
        public void ArrangeContent_ExpansionProgressZero_SkipsContentEntirely()
        {
            var expander = new Expander { Width = 200, Height = 300 };
            var content = new Border { Height = 100 };
            expander.Content = content;

            expander.Arrange(new Rectangle(0, 0, 200, 300));

            Assert.False(content.IsVisible);
            Assert.Equal(0, content.ActualBounds.Width);
            Assert.Equal(0, content.ActualBounds.Height);
        }

        [Fact]
        public void ArrangeContent_PartialExpansion_RevealsAProportionalHeight()
        {
            // Content is an un-sized wrapper (Height=NaN, Stretch) around a 100px-tall child, not an
            // explicitly-sized element itself - an explicit Height would keep the child's own ActualBounds at
            // its full natural size regardless of the smaller Arrange rect (only ClipToBounds would mask the
            // overflow visually), so this shape is what actually exercises the shrink-via-Arrange behavior,
            // matching a realistic Expander.Content (e.g. a Border/StackPanel with no fixed Height of its own).
            var expander = new Expander { Width = 200, Height = 300, ExpansionProgress = 0.5f };
            var content = new Border { Child = new Border { Height = 100 } };
            expander.Content = content;

            expander.Arrange(new Rectangle(0, 0, 200, 300));

            Assert.Equal(50, content.ActualBounds.Height);
        }

        [Fact]
        public void ArrangeContent_FullExpansion_RevealsTheFullContentHeight()
        {
            var expander = new Expander { Width = 200, Height = 300, ExpansionProgress = 1f };
            var content = new Border { Child = new Border { Height = 100 } };
            expander.Content = content;

            expander.Arrange(new Rectangle(0, 0, 200, 300));

            Assert.Equal(100, content.ActualBounds.Height);
        }

        [Fact]
        public void ArrangeContent_ExpansionProgressChangedAfterFirstArrange_ReArrangesContent()
        {
            // Regression, applied proactively (see the SplitPane postmortem in [[project_icyui_tier2_roadmap]]):
            // Arrange(rect) no-ops when the target's own IsArrangeInvalid is already false, regardless of whether
            // rect changed - Content must be force-invalidated before every Arrange call, not just the first one.
            var expander = new Expander { Width = 200, Height = 300, ExpansionProgress = 0.5f };
            var content = new Border { Child = new Border { Height = 100 } };
            expander.Content = content;
            expander.Arrange(new Rectangle(0, 0, 200, 300));
            int firstHeight = content.ActualBounds.Height;

            expander.ExpansionProgress = 1f;
            expander.Arrange(new Rectangle(0, 0, 200, 300));

            Assert.NotEqual(firstHeight, content.ActualBounds.Height);
            Assert.Equal(100, content.ActualBounds.Height);
        }
    }
}
