// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Collections.ObjectModel;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Controls.ItemsControl"/> (Phase 2 of the Tier-2 roadmap):
    /// a <see cref="ScrollViewer"/>-hosted, virtualizing, pooling list bound to a few hundred in-memory strings.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The markup declares the static shape - a fixed-height <see cref="ScrollViewer"/> wrapping an
    /// <see cref="ItemsControl"/> whose <see cref="ItemsControl.ItemTemplate"/> renders each item as a
    /// bordered, bottom-ruled row (so a reused/pooled container is visually indistinguishable from a fresh
    /// one while scrolling - there's nothing per-container to flicker or rebuild) - but markup has no way to
    /// inline a runtime <see cref="ObservableCollection{T}"/>, so <see cref="Build(IcyConfiguration, string)"/>
    /// finds the named <c>ItemsControl</c> after loading (<see cref="UIElementExtensions.FindRequiredControl{T}"/>)
    /// and assigns its <see cref="ItemsControl.ItemsSource"/> from code instead.
    /// </para>
    /// <para>
    /// 300 items in a 400px-tall viewport, at the default ~40px estimated row height, realizes roughly a
    /// dozen containers at a time - most of the list is never simultaneously realized, so this demo actually
    /// exercises virtualization rather than just decorating a fully-realized list.
    /// </para>
    /// </remarks>
    public static class ItemsControlDemo
    {
        /// <summary>
        /// The number of <c>"Item NNN"</c> entries the demo's <see cref="ObservableCollection{T}"/> is seeded with.
        /// </summary>
        public const int ItemCount = 300;

        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="ControlTemplateDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">ItemsControl (Phase 2)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">300 items bound to an ObservableCollection&lt;string&gt;, in a 400px ScrollViewer - only the rows near the viewport are ever realized. Scroll with the mouse wheel or drag the scrollbar; pooled rows should be reused with no flicker/rebuild.</TextBlock>

                <ScrollViewer Width="360" Height="400" HorizontalAlignment="Left">
                  <ItemsControl x:Name="ItemsList">
                    <ItemsControl.ItemTemplate>
                      <DataTemplate>
                        <Border BorderBrush="#FF3A3A44" BorderThickness="0,0,0,1" Padding="10,8">
                          <TextBlock FontSize="14" Foreground="WhiteSmoke" Text="{Binding}"/>
                        </Border>
                      </DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </ScrollViewer>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>, then wires a freshly-populated
        /// <see cref="ObservableCollection{T}"/> of <see cref="ItemCount"/> strings into the loaded
        /// <see cref="ItemsControl"/>'s <see cref="ItemsControl.ItemsSource"/>.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration the loader resolves types, converters, and properties through. Its
        /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> is set to <paramref name="fontFamily"/>
        /// here, which is what gives the document's text a font.
        /// </param>
        /// <param name="fontFamily">
        /// The font family every text element resolves. Import it beforehand (see <c>FontSystem.ImportFont</c>) for
        /// text to actually render.
        /// </param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the markup language.</exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(Markup, nameof(ItemsControlDemo));

            var items = new ObservableCollection<string>();
            for (int i = 0; i < ItemCount; i++)
                items.Add($"Item {i:D3}");

            ItemsControl itemsList = root.FindRequiredControl<ItemsControl>("ItemsList");
            itemsList.ItemsSource = items;

            return root;
        }
    }
}
