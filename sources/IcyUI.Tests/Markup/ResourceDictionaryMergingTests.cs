// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Markup
{
    /// <summary>
    /// Covers loading a <see cref="ResourceDictionary"/> as a markup document's root, and composing
    /// <see cref="ResourceDictionary.MergedDictionaries"/> from an external file via <c>Source=</c> - the M4 slice
    /// that makes a resource dictionary file-shareable/themeable instead of only inline.
    /// </summary>
    public class ResourceDictionaryMergingTests
    {
        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

            // Style now lives in the Icy.UI.Styles built-in namespace, so it no longer needs registering here -
            // doing so would collide with the built-in lookup.
            configuration.Assets.AssetResolver.RegisterImporter(new ResourceDictionaryImporter(configuration));
            return configuration;
        }

        [Fact]
        public void LoadingADocumentWhoseRootIsAResourceDictionary_ReturnsIt()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            object loaded = loader.LoadObject(
                """
                <ResourceDictionary>
                  <Style x:Key="Accent" TargetType="Border" Width="42"/>
                </ResourceDictionary>
                """);

            var dictionary = Assert.IsType<ResourceDictionary>(loaded);
            Assert.True(dictionary.TryGetValue("Accent", out object? value));
            var style = Assert.IsType<Style>(value);
            Assert.Equal(42f, style.Setters["Width"]);
        }

        [Fact]
        public void MergedDictionarySource_LoadsThroughTheAssetPipeline()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            object loaded = loader.LoadObject(
                """
                <ResourceDictionary>
                  <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source="Resources/theme.xml"/>
                  </ResourceDictionary.MergedDictionaries>
                </ResourceDictionary>
                """);

            var dictionary = Assert.IsType<ResourceDictionary>(loaded);
            Assert.True(dictionary.TryGetValue("ThemedAccent", out object? value));
            var style = Assert.IsType<Style>(value);
            Assert.Equal(99f, style.Setters["Width"]);
        }

        [Fact]
        public void MergedDictionarySource_APathThatDoesNotExist_ThrowsMarkupException()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            MarkupException ex = Assert.Throws<MarkupException>(() => loader.LoadObject(
                """
                <ResourceDictionary>
                  <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source="Resources/DoesNotExist.xml"/>
                  </ResourceDictionary.MergedDictionaries>
                </ResourceDictionary>
                """));

            Assert.Contains("No resource dictionary found at", ex.Message);
        }
    }
}
