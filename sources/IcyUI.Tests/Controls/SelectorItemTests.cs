// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class SelectorItemTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var item = new SelectorItem();

            Assert.False(item.IsSelected);
            Assert.False(item.IsHighlighted);
        }

        [Fact]
        public void IsSelected_SetsAndClearsTheSelectedControlStateFlag()
        {
            var item = new SelectorItem { IsSelected = true };

            Assert.Equal(ControlState.Selected, item.ControlState & ControlState.Selected);

            item.IsSelected = false;

            Assert.Equal(ControlState.Normal, item.ControlState & ControlState.Selected);
        }

        [Fact]
        public void IsHighlighted_SetsAndClearsTheHighlightedControlStateFlag()
        {
            var item = new SelectorItem { IsHighlighted = true };

            Assert.Equal(ControlState.Highlighted, item.ControlState & ControlState.Highlighted);

            item.IsHighlighted = false;

            Assert.Equal(ControlState.Normal, item.ControlState & ControlState.Highlighted);
        }

        [Fact]
        public void IsSelected_And_IsHighlighted_AreIndependentFlags()
        {
            var item = new SelectorItem { IsSelected = true, IsHighlighted = true };

            Assert.Equal(ControlState.Selected | ControlState.Highlighted, item.ControlState & (ControlState.Selected | ControlState.Highlighted));
        }
    }
}
