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
    /// A single screen demonstrating <see cref="Dropdown"/>/<see cref="ComboBox"/> (Tier-2 Phase 5): both bound to
    /// the same small in-memory list, exercising open/close, keyboard navigation, <see cref="ComboBox"/>
    /// filtering, and <see cref="Dropdown"/> typeahead.
    /// </summary>
    public static class SelectorDemo
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
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Dropdown / ComboBox (Phase 5)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Click either control to open its popup. Arrow keys/gamepad navigate; Enter selects; Escape closes. Type a letter in the Dropdown for typeahead, or type in the ComboBox to filter.</TextBlock>

                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,4">Dropdown:</TextBlock>
                <Dropdown x:Name="FruitDropdown" Width="240" Margin="0,0,0,16"/>

                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,4">ComboBox:</TextBlock>
                <ComboBox x:Name="FruitComboBox" Width="240"/>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>, then wires the same
        /// <see cref="ObservableCollection{T}"/> of fruit names into both the <see cref="Dropdown"/> and the
        /// <see cref="ComboBox"/>'s <see cref="ItemsControl.ItemsSource"/>.
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
            UIElement root = loader.Load(Markup, nameof(SelectorDemo));

            var fruits = new ObservableCollection<string> { "Apple", "Apricot", "Banana", "Cherry", "Grape", "Mango", "Orange", "Peach", "Pear", "Plum" };

            Dropdown dropdown = root.FindRequiredControl<Dropdown>("FruitDropdown");
            dropdown.ItemsSource = fruits;

            ComboBox comboBox = root.FindRequiredControl<ComboBox>("FruitComboBox");
            comboBox.ItemsSource = fruits;

            return root;
        }
    }
}
