using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class ResourceDictionaryTests
    {
        [Fact]
        public void TryGetValue_FindsOwnEntry()
        {
            var dictionary = new ResourceDictionary { ["Accent"] = "Blue" };

            Assert.True(dictionary.TryGetValue("Accent", out object? value));
            Assert.Equal("Blue", value);
        }

        [Fact]
        public void TryGetValue_OwnEntryShadowsMergedEntry()
        {
            var merged = new ResourceDictionary { ["Accent"] = "Red" };
            var dictionary = new ResourceDictionary { ["Accent"] = "Blue" };
            dictionary.MergedDictionaries.Add(merged);

            Assert.True(dictionary.TryGetValue("Accent", out object? value));
            Assert.Equal("Blue", value);
        }

        [Fact]
        public void TryGetValue_LaterMergedDictionaryShadowsEarlierOne()
        {
            var earlier = new ResourceDictionary { ["Accent"] = "Red" };
            var later = new ResourceDictionary { ["Accent"] = "Green" };
            var dictionary = new ResourceDictionary();
            dictionary.MergedDictionaries.Add(earlier);
            dictionary.MergedDictionaries.Add(later);

            Assert.True(dictionary.TryGetValue("Accent", out object? value));
            Assert.Equal("Green", value);
        }

        [Fact]
        public void TryGetValue_MissingKey_ReturnsFalse()
        {
            var dictionary = new ResourceDictionary();

            Assert.False(dictionary.TryGetValue("Nope", out _));
        }

        private class TestElement : UIElement
        {
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            protected override void ArrangeContent()
            {
            }
        }

        [Fact]
        public void UIElement_ResourcesIsLazyAndEmptyByDefault()
        {
            var element = new TestElement();

            Assert.Empty(element.Resources);
        }
    }
}
