// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Drawing;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Controls.ColorPicker"/> and
    /// <see cref="Icy.UI.Controls.ColorPickerButton"/> (Phase 8 of the Tier-2 roadmap).
    /// </summary>
    /// <remarks>
    /// <see cref="ColorPicker.SwatchColors"/>/<see cref="ColorPickerButton.SwatchColors"/> have no markup
    /// collection-syntax support yet, so this demo builds its markup skeleton, then wires the swatch palette into
    /// both controls from code - same "no markup child-item syntax, wire from code after loading" convention as
    /// <see cref="ItemsControlDemo"/>/<see cref="WrapGridDemo"/>/<see cref="SelectorDemo"/>.
    /// </remarks>
    public static class ColorPickerDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">ColorPicker (Phase 8)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Full picker on the left; a popup-trigger button on the right.</TextBlock>
                <StackPanel Orientation="Horizontal">
                  <ColorPicker x:Name="MainPicker" Width="360" Height="260" Margin="0,0,20,0"/>
                  <ColorPickerButton x:Name="PopupButton" Width="48" Height="32"/>
                </StackPanel>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>, then wiring the same swatch palette into
        /// the loaded <c>MainPicker</c> and <c>PopupButton</c> in code (no markup collection-syntax support for
        /// <see cref="ColorPicker.SwatchColors"/> yet).
        /// </summary>
        /// <param name="configuration">The library configuration the loader resolves types, converters, and properties through.</param>
        /// <param name="fontFamily">The font family every text element resolves.</param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the markup language.</exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(Markup, nameof(ColorPickerDemo));

            Color[] palette =
            [
                Color.Red, Color.Orange, Color.Yellow, Color.Lime, Color.Cyan,
                Color.Blue, Color.Magenta, Color.White, Color.Black,
            ];

            ColorPicker mainPicker = root.FindRequiredControl<ColorPicker>("MainPicker");
            foreach (Color color in palette)
                mainPicker.SwatchColors.Add(color);
            mainPicker.RefreshSwatches();

            ColorPickerButton popupButton = root.FindRequiredControl<ColorPickerButton>("PopupButton");
            foreach (Color color in palette)
                popupButton.SwatchColors.Add(color);
            popupButton.RefreshSwatches();

            return root;
        }
    }
}
