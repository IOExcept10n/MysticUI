// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Styles.ControlTemplate"/> (Phase 9 M5's first slice): a
    /// <c>Button</c> restyled with an entirely different visual tree via markup, shown side-by-side with a plain
    /// untemplated <c>Button</c> for comparison, plus a templated <c>CheckBox</c> whose checked/unchecked
    /// appearance still comes from an ordinary <c>Style</c>/<c>VisualState</c>.
    /// </summary>
    /// <remarks>
    /// The point of the last piece: <c>Style</c>/<c>VisualState</c> need no template-awareness of their own -
    /// they still only ever set the control's own <c>Background</c>, exactly as on an untemplated control. Only
    /// the template's content (via <c>{TemplateBinding Background}</c>) knows it's being templated at all.
    /// </remarks>
    public static class ControlTemplateDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="MarkupDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <StackPanel.Resources>
                  <ControlTemplate x:Key="RoundedButtonSkin" TargetType="Button">
                    <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
                      <ContentPresenter Content="{TemplateBinding Content}"/>
                    </Border>
                  </ControlTemplate>

                  <ControlTemplate x:Key="CheckSkin" TargetType="ToggleButton">
                    <Border Background="{TemplateBinding Background}" BorderBrush="White" BorderThickness="2" Padding="{TemplateBinding Padding}">
                      <ContentPresenter Content="{TemplateBinding Content}"/>
                    </Border>
                  </ControlTemplate>

                  <Style x:Key="CheckedStyle" TargetType="CheckBox" Background="#FF505058">
                    <Style.StateGroups>
                      <VisualStateGroup Name="CheckStates">
                        <VisualState Name="Checked" State="Checked" Background="#FF3CA050"/>
                      </VisualStateGroup>
                    </Style.StateGroups>
                  </Style>
                </StackPanel.Resources>

                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">ControlTemplate (Phase 9 M5, step 1)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Left: a plain Button, default Chrome. Right: the identical Button, wrapped in a completely different visual tree via a ControlTemplate - Content/Background/BorderBrush/BorderThickness all still reach it, only through TemplateBinding now. Below: a templated CheckBox whose checked color still comes from an ordinary Style.</TextBlock>

                <StackPanel Orientation="Horizontal" Margin="0,0,0,10">
                  <Button Padding="12,6" Background="#FF3C64C8" Margin="0,0,10,0">Plain Button</Button>
                  <Button Template="{StaticResource RoundedButtonSkin}" Padding="12,6" Background="#FFB85C1E" BorderBrush="White" BorderThickness="2">Templated Button</Button>
                </StackPanel>

                <CheckBox Template="{StaticResource CheckSkin}" Style="{StaticResource CheckedStyle}" Padding="12,6">Templated CheckBox</CheckBox>
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
            return loader.Load(Markup, nameof(ControlTemplateDemo));
        }
    }
}
