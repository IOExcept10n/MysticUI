// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupSetterCollectionTests
    {
        private class TestElement : UIElement
        {
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            configuration.Types.Markup.RegisterShortName<TestElement>();
            configuration.Types.Markup.RegisterShortName<Style>();
            return configuration;
        }

        [Fact]
        public void UnresolvedAttributeOnStyle_BecomesASetterConvertedAgainstTargetType()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var style = (Style)loader.Load("""<Style TargetType="TestElement" Width="42"/>""");

            Assert.Equal(42f, style.Setters["Width"]);
        }

        [Fact]
        public void UnresolvableSetterName_ThrowsMarkupException()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.Load("""<Style TargetType="TestElement" NotARealProperty="1"/>"""));
            Assert.Contains("NotARealProperty", ex.Description);
        }
    }
}
