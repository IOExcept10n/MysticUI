// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Generic;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Markup
{
    /// <summary>
    /// Tests that the markup loader can populate collections using duck-typed <c>Add</c> method detection,
    /// not just explicit <see cref="System.Collections.IList"/> implementations.
    /// </summary>
    public class MarkupLoaderCollectionPopulationTests
    {
        /// <summary>
        /// A custom collection that implements <see cref="IReadOnlyList{T}"/> with an <c>Add</c> method,
        /// but does not implement <see cref="System.Collections.IList"/>.
        /// This tests duck-typed collection population.
        /// </summary>
        private class CustomCollection : IReadOnlyList<IntBox>
        {
            private readonly List<IntBox> items = [];

            /// <summary>
            /// Gets the number of items in the collection.
            /// </summary>
            public int Count => items.Count;

            /// <summary>
            /// Gets the item at the specified index.
            /// </summary>
            /// <param name="index">The index of the item to get.</param>
            /// <returns>The item at the specified index.</returns>
            public IntBox this[int index] => items[index];

            /// <summary>
            /// Returns an enumerator for the items in the collection.
            /// </summary>
            /// <returns>An enumerator.</returns>
            public IEnumerator<IntBox> GetEnumerator() => items.GetEnumerator();

            /// <summary>
            /// Returns an enumerator for the items in the collection.
            /// </summary>
            /// <returns>An enumerator.</returns>
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => items.GetEnumerator();

            /// <summary>
            /// Adds an item to the collection.
            /// </summary>
            /// <param name="item">The item to add.</param>
            public void Add(IntBox item) => items.Add(item);
        }

        /// <summary>
        /// Mirrors <c>Timeline.Keyframes</c>' exact shape: content-property is <see cref="IReadOnlyList{T}"/>,
        /// backed by a custom collection that doesn't implement <see cref="System.Collections.IList"/>.
        /// </summary>
        [ContentProperty(nameof(ReadOnlyListHost.Items))]
        private class ReadOnlyListHost : UIElement
        {
            private readonly CustomCollection items = new();

            /// <summary>
            /// Gets the content property, which is backed by a custom collection without IList implementation.
            /// </summary>
            public IReadOnlyList<IntBox> Items => items;

            /// <summary>
            /// Measures the host's content.
            /// </summary>
            protected override System.Drawing.Size MeasureContent() => System.Drawing.Size.Empty;

            /// <summary>
            /// Arranges the host's content.
            /// </summary>
            protected override void ArrangeContent()
            {
            }
        }

        /// <summary>
        /// A simple test class with a settable value property for markup instantiation.
        /// </summary>
        private class IntBox
        {
            /// <summary>
            /// Gets or sets the boxed value.
            /// </summary>
            public int Value { get; set; }
        }

        /// <summary>
        /// Creates the configuration for these tests.
        /// </summary>
        private static IcyConfiguration CreateConfiguration()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            configuration.Types.Markup.RegisterShortName<ReadOnlyListHost>();
            configuration.Types.Markup.RegisterShortName<IntBox>();
            return configuration;
        }

        /// <summary>
        /// Tests that content children can be added to a collection property that is declared as an
        /// <see cref="IReadOnlyList{T}"/> but backed by a real list at runtime.
        /// </summary>
        [Fact]
        public void ContentChildren_PopulateAnIReadOnlyListBackedByARealListAtRuntime()
        {
            var loader = new MarkupLoader(CreateConfiguration());

            var host = (ReadOnlyListHost)loader.Load(
                """
                <ReadOnlyListHost>
                  <IntBox Value="1"/>
                  <IntBox Value="2"/>
                </ReadOnlyListHost>
                """);

            // Duck-typed Add detection allows the IReadOnlyList<IntBox> to be populated even though
            // it doesn't implement IList, because the backing list has a public Add method.
            Assert.Equal(2, host.Items.Count);
            Assert.Equal(1, host.Items[0].Value);
            Assert.Equal(2, host.Items[1].Value);
        }
    }
}
