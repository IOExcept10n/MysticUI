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
    }
}
