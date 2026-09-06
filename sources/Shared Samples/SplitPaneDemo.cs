// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Controls.SplitPane"/> (Phase 3 of the Tier-2 roadmap):
    /// two nested <see cref="Icy.UI.Controls.SplitPane"/>s forming a 3-pane IDE-like layout, matching the design
    /// spec's own nesting mockup (<c>docs/superpowers/specs/2026-09-06-splitpane-design.md</c>).
    /// </summary>
    public static class SplitPaneDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="ItemsControlDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">SplitPane (Phase 3)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Drag either divider to resize. The outer split is horizontal (tree vs. editor+output); the inner split is vertical (editor over output).</TextBlock>

                <SplitPane Orientation="Horizontal" Width="600" Height="360" MinFirstSize="80" MinSecondSize="160">
                  <SplitPane.First>
                    <Border Background="#FF2E2E38" BorderBrush="#FF3A3A44" BorderThickness="0,0,1,0" Padding="10">
                      <TextBlock FontSize="14" Foreground="WhiteSmoke">Tree</TextBlock>
                    </Border>
                  </SplitPane.First>
                  <SplitPane.Second>
                    <SplitPane Orientation="Vertical" MinFirstSize="60" MinSecondSize="60">
                      <SplitPane.First>
                        <Border Background="#FF2E2E38" BorderBrush="#FF3A3A44" BorderThickness="0,0,0,1" Padding="10">
                          <TextBlock FontSize="14" Foreground="WhiteSmoke">Editor</TextBlock>
                        </Border>
                      </SplitPane.First>
                      <SplitPane.Second>
                        <Border Background="#FF2E2E38" Padding="10">
                          <TextBlock FontSize="14" Foreground="WhiteSmoke">Output</TextBlock>
                        </Border>
                      </SplitPane.Second>
                    </SplitPane>
                  </SplitPane.Second>
                </SplitPane>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>.
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
            return loader.Load(Markup, nameof(SplitPaneDemo));
        }
    }
}
