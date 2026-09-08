// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Controls.Expander"/> (Phase 4 of the Tier-2 roadmap):
    /// a couple of independent <see cref="Icy.UI.Controls.Expander"/>s with varied header/content, showing off
    /// the expand/collapse animation and an arbitrary (non-string) <see cref="Icy.UI.Controls.Expander.Header"/>.
    /// </summary>
    public static class ExpanderDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="SplitPaneDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Expander (Phase 4)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Click either header to expand/collapse its content.</TextBlock>

                <Expander Width="400" Margin="0,0,0,12">
                  <Expander.Header>
                    <TextBlock FontSize="14" Foreground="WhiteSmoke">Plain-text header</TextBlock>
                  </Expander.Header>
                  <Border Background="#FF2E2E38" Padding="10">
                    <TextBlock FontSize="14" Foreground="WhiteSmoke">Simple collapsible content, revealed below the header.</TextBlock>
                  </Border>
                </Expander>

                <Expander Width="400">
                  <Expander.Header>
                    <StackPanel Orientation="Horizontal">
                      <Border Width="16" Height="16" Background="#FF3C78D8" Margin="0,0,8,0"/>
                      <TextBlock FontSize="14" Foreground="WhiteSmoke">Header with an icon swatch</TextBlock>
                    </StackPanel>
                  </Expander.Header>
                  <Border Background="#FF2E2E38" Padding="10">
                    <StackPanel Orientation="Vertical">
                      <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,6">A taller content block,</TextBlock>
                      <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,6">confirming Header can be any UIElement,</TextBlock>
                      <TextBlock FontSize="14" Foreground="WhiteSmoke">not just plain text.</TextBlock>
                    </StackPanel>
                  </Border>
                </Expander>
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
            return loader.Load(Markup, nameof(ExpanderDemo));
        }
    }
}
