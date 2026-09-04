// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ItemContainerTests
    {
        [Fact]
        public void Content_HostsAnArbitraryElement()
        {
            var container = new ItemContainer();
            var content = new UIElement();

            container.Content = content;

            Assert.Same(content, container.Content);
            Assert.Same(container, content.Parent);
        }
    }
}
