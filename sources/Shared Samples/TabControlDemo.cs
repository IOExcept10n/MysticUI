// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Collections.Generic;
using System.Drawing;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Controls.TabControl"/> (Phase 8 of the Tier-2 roadmap):
    /// three tabs, one with a disabled tab shown alongside enabled ones for visual comparison.
    /// </summary>
    /// <remarks>
    /// <see cref="Icy.UI.Controls.ItemsControl"/> (which <see cref="Icy.UI.Controls.TabControl"/> derives from) has
    /// no <see cref="Icy.Markup.ContentPropertyAttribute"/>, so nested elements can't be declared directly inside a
    /// <c>&lt;TabControl&gt;</c> tag in markup - same "no markup child-item syntax, wire
    /// <see cref="ItemsControl.ItemsSource"/> from code after loading" convention as
    /// <see cref="ItemsControlDemo"/>/<see cref="WrapGridDemo"/>/<see cref="SelectorDemo"/>.
    /// </remarks>
    public static class TabControlDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="ExpanderDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">TabControl (Phase 8)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Click a tab header to switch content. The third tab is disabled.</TextBlock>
                <TabControl x:Name="Tabs" Width="400"/>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>, then wires three
        /// <see cref="Icy.UI.Controls.TabItem"/>s - the last disabled - into the loaded <c>Tabs</c>
        /// <see cref="Icy.UI.Controls.TabControl"/>'s <see cref="ItemsControl.ItemsSource"/> and selects the first
        /// one (<see cref="SelectingItemsControl.SelectedIndex"/> otherwise defaults to <c>-1</c>, showing no
        /// content at all).
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
            UIElement root = loader.Load(Markup, nameof(TabControlDemo));

            TabControl tabs = root.FindRequiredControl<TabControl>("Tabs");
            tabs.ItemsSource = new List<TabItem>
            {
                new() { Header = "First", Content = new TextBlock { FontSize = 14, Foreground = Color.WhiteSmoke, Text = "First tab's content." } },
                new() { Header = "Second", Content = new TextBlock { FontSize = 14, Foreground = Color.WhiteSmoke, Text = "Second tab's content." } },
                new() { Header = "Disabled", IsEnabled = false, Content = new TextBlock { FontSize = 14, Foreground = Color.WhiteSmoke, Text = "Never shown - this tab can't be selected." } },
            };
            tabs.SelectedIndex = 0;

            return root;
        }
    }
}
