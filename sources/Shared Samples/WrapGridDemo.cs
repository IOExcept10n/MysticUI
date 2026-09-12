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
    /// A single screen demonstrating <see cref="WrapGrid"/> (Tier-2 Phase 6): a <see cref="ScrollViewer"/>-hosted,
    /// virtualizing grid of uniformly-sized tiles bound to a few dozen in-memory items.
    /// </summary>
    /// <remarks>
    /// Same <see cref="ScrollViewer"/>-wrapping/post-load-<see cref="ItemsControl.ItemsSource"/>-assignment
    /// convention as <see cref="ItemsControlDemo"/> - markup has no way to inline a runtime
    /// <see cref="ObservableCollection{T}"/>.
    /// </remarks>
    public static class WrapGridDemo
    {
        /// <summary>
        /// The number of <c>"Tile NN"</c> entries the demo's <see cref="ObservableCollection{T}"/> is seeded with.
        /// </summary>
        public const int ItemCount = 48;

        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="ItemsControlDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">WrapGrid (Phase 6)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">48 tiles bound to an ObservableCollection&lt;string&gt;, auto-flowing into rows in a 300px ScrollViewer. Scroll with the mouse wheel or drag the scrollbar; click a tile to select it.</TextBlock>

                <ScrollViewer Width="360" Height="300" HorizontalAlignment="Left">
                  <WrapGrid x:Name="TileGrid" ItemWidth="72" ItemHeight="72">
                    <WrapGrid.ItemTemplate>
                      <DataTemplate>
                        <Border Margin="2" BorderBrush="#FF3A3A44" BorderThickness="1" Padding="4">
                          <TextBlock FontSize="12" Foreground="WhiteSmoke" Text="{Binding}"/>
                        </Border>
                      </DataTemplate>
                    </WrapGrid.ItemTemplate>
                  </WrapGrid>
                </ScrollViewer>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>, then wires a freshly-populated
        /// <see cref="ObservableCollection{T}"/> of <see cref="ItemCount"/> strings into the loaded
        /// <see cref="WrapGrid"/>'s <see cref="ItemsControl.ItemsSource"/>.
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
            UIElement root = loader.Load(Markup, nameof(WrapGridDemo));

            var items = new ObservableCollection<string>();
            for (int i = 0; i < ItemCount; i++)
                items.Add($"Tile {i:D2}");

            WrapGrid tileGrid = root.FindRequiredControl<WrapGrid>("TileGrid");
            tileGrid.ItemsSource = items;

            return root;
        }
    }
}
