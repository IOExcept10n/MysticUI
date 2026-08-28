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
    public class StaticResourceExtensionTests
    {
        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            configuration.Types.Markup.RegisterShortName<Style>();
            return configuration;
        }

        [Fact]
        public void ResolvesFromTheDeclaringElementsOwnResources()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            // <Border.Resources> is processed before <Border.Style> - both are property elements handled in
            // document order within the same ApplyChildren call, so this works even though Style is an ATTRIBUTE
            // -eligible property: written as a property element here specifically so Resources is populated first
            // (an attribute-form Style="..." would be resolved during ApplyAttributes, which runs before ANY
            // property elements - including this element's own <Border.Resources> - so it could never see it).
            var border = (Border)loader.Load(
                """
                <Border>
                  <Border.Resources>
                    <Style x:Key="Accent" TargetType="Border" Width="42"/>
                  </Border.Resources>
                  <Border.Style>{StaticResource Accent}</Border.Style>
                </Border>
                """);

            Assert.NotNull(border.Style);
            Assert.Equal(42f, border.Style!.Setters["Width"]);
        }

        [Fact]
        public void ResolvesFromAnAncestorsResourcesWhenNotFoundLocally()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var root = (StackPanel)loader.Load(
                """
                <StackPanel>
                  <StackPanel.Resources>
                    <Style x:Key="Accent" TargetType="Border" Width="42"/>
                  </StackPanel.Resources>
                  <Border>
                    <Border.Style>{StaticResource Accent}</Border.Style>
                  </Border>
                </StackPanel>
                """);

            var child = (Border)root.Children[0];
            Assert.NotNull(child.Style);
            Assert.Equal(42f, child.Style!.Setters["Width"]);
        }

        [Fact]
        public void UnresolvableKey_ThrowsMarkupExceptionAtLoad()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.Load("""<Border Background="{StaticResource Nope}"/>"""));
            Assert.Contains("Nope", ex.Description);
        }
    }
}
