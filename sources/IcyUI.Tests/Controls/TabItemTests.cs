// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TabItemTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var item = new TabItem();

            Assert.Null(item.Header);
            Assert.Null(item.Icon);
            Assert.Null(item.Content);
            Assert.True(item.IsEnabled);
        }

        [Fact]
        public void Header_SetToNonNullValue_IsStored()
        {
            var item = new TabItem { Header = "Swatches" };

            Assert.Equal("Swatches", item.Header);
        }

        [Fact]
        public void Icon_Set_IsStored()
        {
            var icon = new Icon { Kind = IconKind.ChevronDown };
            var item = new TabItem { Icon = icon };

            Assert.Same(icon, item.Icon);
        }

        [Fact]
        public void IsEnabled_SetFalse_IsReadable()
        {
            var item = new TabItem { IsEnabled = false };

            Assert.False(item.IsEnabled);
        }
    }
}
