using Icy.Configuration;
using Icy.Data.Markup;
using Icy.Data.Markup.Attributes;
using Xunit;

namespace Icy.Tests.Data.Markup
{
    /// <summary>
    /// Covers <see cref="PropertyRegistry"/> as a per-configuration service: scoping, capture at construction,
    /// and the isolation two configurations get from independent registries.
    /// </summary>
    public class PropertyRegistryIsolationTests
    {
        private class Widget : DependencyObject
        {
            private int number;

            [RegisterReference]
            public int Number
            {
                get => number;
                set => SetProperty(ref number, value);
            }
        }

        [Fact]
        public void Current_DefaultsToDefaultRegistry()
        {
            Assert.Same(PropertyRegistry.Default, PropertyRegistry.Current);
        }

        [Fact]
        public void UseScope_RedirectsCurrentAndRestoresOnDispose()
        {
            var scoped = new PropertyRegistry();

            using (PropertyRegistry.UseScope(scoped))
            {
                Assert.Same(scoped, PropertyRegistry.Current);
            }

            Assert.Same(PropertyRegistry.Default, PropertyRegistry.Current);
        }

        [Fact]
        public void UseScope_NestsAndUnwindsInOrder()
        {
            var outer = new PropertyRegistry();
            var inner = new PropertyRegistry();

            using (PropertyRegistry.UseScope(outer))
            {
                using (PropertyRegistry.UseScope(inner))
                {
                    Assert.Same(inner, PropertyRegistry.Current);
                }

                Assert.Same(outer, PropertyRegistry.Current);
            }

            Assert.Same(PropertyRegistry.Default, PropertyRegistry.Current);
        }

        [Fact]
        public void GetPropertyStore_ReturnsDistinctStoresPerRegistry()
        {
            var first = new PropertyRegistry();
            var second = new PropertyRegistry();

            IPropertyStore firstStore = first.GetPropertyStore(typeof(Widget));
            IPropertyStore secondStore = second.GetPropertyStore(typeof(Widget));

            Assert.NotSame(firstStore, secondStore);
            Assert.Equal(typeof(Widget), firstStore.TargetType);
            Assert.Equal(typeof(Widget), secondStore.TargetType);
            Assert.NotSame(firstStore.GetProperty(nameof(Widget.Number)), secondStore.GetProperty(nameof(Widget.Number)));
        }

        [Fact]
        public void DependencyObject_CapturesScopedRegistryAtConstruction()
        {
            var scoped = new PropertyRegistry();

            Widget scopedWidget;
            using (PropertyRegistry.UseScope(scoped))
            {
                scopedWidget = new Widget();
            }

            var defaultWidget = new Widget();

            // The capture outlives the scope - an object never migrates between registries.
            Assert.Same(scoped, scopedWidget.PropertyRegistry);
            Assert.Same(PropertyRegistry.Default, defaultWidget.PropertyRegistry);
            Assert.NotSame(scopedWidget.GetPropertyStore(), defaultWidget.GetPropertyStore());
        }

        [Fact]
        public void For_ResolvesTheRegistryAnObjectWasBuiltAgainst()
        {
            var scoped = new PropertyRegistry();

            Widget widget;
            using (PropertyRegistry.UseScope(scoped))
            {
                widget = new Widget();
            }

            Assert.Same(scoped, PropertyRegistry.For(widget));

            // A plain (non-DependencyObject) target has nothing to carry a registry, so it falls back to Current.
            Assert.Same(PropertyRegistry.Default, PropertyRegistry.For(new object()));
        }

        [Fact]
        public void TwoConfigurations_WithIndependentRegistries_DoNotSharePrecedenceBookkeeping()
        {
            var firstRegistry = new PropertyRegistry();
            var secondRegistry = new PropertyRegistry();
            var firstConfiguration = new ReflectionConfiguration { PropertyRegistry = firstRegistry };
            var secondConfiguration = new ReflectionConfiguration { PropertyRegistry = secondRegistry };

            Widget firstWidget;
            using (PropertyRegistry.UseScope(firstRegistry))
            {
                firstWidget = new Widget();
            }

            Widget secondWidget;
            using (PropertyRegistry.UseScope(secondRegistry))
            {
                secondWidget = new Widget();
            }

            IPropertyReference firstProperty = firstConfiguration.PropertyRegistry.GetPropertyStore(typeof(Widget)).GetProperty(nameof(Widget.Number));
            IPropertyReference secondProperty = secondConfiguration.PropertyRegistry.GetPropertyStore(typeof(Widget)).GetProperty(nameof(Widget.Number));

            firstProperty.SetTierValue(firstWidget, PropertyValuePrecedence.Style, 7);
            secondProperty.SetTierValue(secondWidget, PropertyValuePrecedence.Style, 9);

            Assert.Equal(7, firstWidget.Number);
            Assert.Equal(9, secondWidget.Number);

            // Each reference keeps its own per-target tier table, so clearing through one leaves the other intact.
            firstProperty.ClearTierValue(firstWidget, PropertyValuePrecedence.Style);

            Assert.Equal(0, firstWidget.Number);
            Assert.Equal(9, secondWidget.Number);
        }
    }
}
