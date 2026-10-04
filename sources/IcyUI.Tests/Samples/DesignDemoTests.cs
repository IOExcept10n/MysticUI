// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Markup;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    public class DesignDemoTests
    {
        [Fact]
        public void Build_TracksThePageAndShowsItsMarkup()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            var root = (StackPanel)DesignDemo.Build(configuration, "Airfool");

            Assert.Equal(4, root.Children.Count);
            Assert.Equal(DesignDemo.Markup, Assert.IsType<TextBlock>(root.Children[3]).Text);
            Assert.NotNull(configuration.Types.Markup.LoadObserver);
        }

        [Fact]
        public void Build_TwiceOnOneConfiguration_ReusesItsSession()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            DesignDemo.Build(configuration, "Airfool");
            DesignDemo.Build(configuration, "Airfool");
        }
    }
}
