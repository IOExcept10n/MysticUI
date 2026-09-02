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
    /// <summary>
    /// Covers loading a <see cref="ControlTemplate"/> from markup: content capture, per-control instantiation, and
    /// the loader's own error handling around it - not <c>{TemplateBinding}</c> (see
    /// <c>TemplateBindingExtensionTests</c>) or end-to-end control application (see
    /// <c>ControlTemplateIntegrationTests</c>).
    /// </summary>
    public class ControlTemplateMarkupTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void TargetType_ResolvesFromMarkup()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var template = (ControlTemplate)loader.LoadObject(
                """
                <ControlTemplate TargetType="Button">
                  <Border/>
                </ControlTemplate>
                """);

            Assert.Equal(typeof(Button), template.TargetType);
        }

        [Fact]
        public void LoadContent_CalledTwice_ProducesDistinctTreesPerControl()
        {
            var loader = new MarkupLoader(CreateConfiguration());
            var template = (ControlTemplate)loader.LoadObject(
                """
                <ControlTemplate TargetType="Button">
                  <Border/>
                </ControlTemplate>
                """);
            var first = new Button();
            var second = new Button();

            UIElement firstTree = template.LoadContent(first);
            UIElement secondTree = template.LoadContent(second);

            Assert.NotSame(firstTree, secondTree);
        }

        [Fact]
        public void ZeroRootElements_Throws()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.LoadObject("""<ControlTemplate TargetType="Button"/>"""));
            Assert.Contains("exactly one root element", ex.Description);
        }

        [Fact]
        public void MultipleRootElements_ThrowsSuggestingAPanel()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var ex = Assert.Throws<MarkupException>(() => loader.LoadObject(
                """
                <ControlTemplate TargetType="Button">
                  <Border/>
                  <Border/>
                </ControlTemplate>
                """));
            Assert.Contains("Wrap them in a panel", ex.Description);
        }

        [Fact]
        public void LoadContent_WithNoContentEverSet_Throws()
        {
            var template = new ControlTemplate(typeof(Button));

            Assert.Throws<InvalidOperationException>(() => template.LoadContent(new Button()));
        }

        [Fact]
        public void KeyedInsideResourceDictionary_ResolvesUnderItsExplicitKey()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            object loaded = loader.LoadObject(
                """
                <ResourceDictionary>
                  <ControlTemplate x:Key="Foo" TargetType="Button">
                    <Border/>
                  </ControlTemplate>
                </ResourceDictionary>
                """);

            var dictionary = Assert.IsType<ResourceDictionary>(loaded);
            Assert.True(dictionary.TryGetValue("Foo", out object? value));
            Assert.IsType<ControlTemplate>(value);
        }
    }
}
