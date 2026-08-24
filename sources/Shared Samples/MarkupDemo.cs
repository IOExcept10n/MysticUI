// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// Builds the same layout section <see cref="ControlsDemo"/> builds by hand, but declared in markup - the
    /// side-by-side that shows the loader produces an identical tree.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Linked into both <c>MonoGame Sample</c> and <c>Stride Sample</c> like the other shared demos. Markup is
    /// engine-independent, so this file needs no per-engine variant and neither backend needed any change to
    /// support it.
    /// </para>
    /// <para>
    /// Note what the markup does <em>not</em> say: no element names a font. Instead
    /// <see cref="Build(IcyConfiguration, string)"/> sets
    /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> once, and every
    /// <see cref="TextBlock"/> in the document - including the ones the loader creates itself for bare text like
    /// <c>&lt;Button&gt;Click Me&lt;/Button&gt;</c> - resolves through it. That is the whole point of the fallback:
    /// markup can declare text without every element having to repeat a font family it doesn't care about.
    /// </para>
    /// </remarks>
    public static class MarkupDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained.
        /// </summary>
        /// <remarks>
        /// A real project would put this in a <c>.xml</c> file and load it through the asset pipeline with
        /// <see cref="MarkupImporter"/>; nothing about the document changes when it does.
        /// </remarks>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">

                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Layout, declared in markup</TextBlock>

                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,6">StackPanel (horizontal):</TextBlock>
                <StackPanel Orientation="Horizontal">
                  <Border Background="LightCoral" Width="100" Height="60" Margin="0,0,8,0">
                    <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center" VerticalAlignment="Center">Stack 1</TextBlock>
                  </Border>
                  <Border Background="LightGoldenrodYellow" Width="100" Height="60" Margin="0,0,8,0">
                    <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center" VerticalAlignment="Center">Stack 2</TextBlock>
                  </Border>
                  <Border Background="LightSkyBlue" Width="100" Height="60" Margin="0,0,8,0">
                    <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center" VerticalAlignment="Center">Stack 3</TextBlock>
                  </Border>
                </StackPanel>

                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,6">Grid (2x2, via Grid.Row/Grid.Column):</TextBlock>
                <Grid x:Name="cells">
                  <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="*"/>
                  </Grid.ColumnDefinitions>
                  <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="Auto"/>
                  </Grid.RowDefinitions>

                  <Border Background="MediumPurple" Width="100" Height="60" Margin="0,0,8,0">
                    <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center" VerticalAlignment="Center">0,0</TextBlock>
                  </Border>
                  <Border Grid.Column="1" Background="MediumSeaGreen" Width="100" Height="60" Margin="0,0,8,0">
                    <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center" VerticalAlignment="Center">0,1</TextBlock>
                  </Border>
                  <Border Grid.Row="1" Background="IndianRed" Width="100" Height="60" Margin="0,0,8,0">
                    <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center" VerticalAlignment="Center">1,0</TextBlock>
                  </Border>
                  <Border Grid.Row="1" Grid.Column="1" Background="DarkOrange" Width="100" Height="60" Margin="0,0,8,0">
                    <TextBlock FontSize="14" Foreground="Black" HorizontalAlignment="Center" VerticalAlignment="Center">1,1</TextBlock>
                  </Border>
                </Grid>

                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,12,0,6">Bare text becomes a TextBlock - no font named anywhere:</TextBlock>
                <Button x:Name="ok" Padding="12,6" HorizontalAlignment="Left" Background="#FF3C64C8">Click Me</Button>

              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration the loader resolves types, converters, and properties through. Its
        /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> is set to
        /// <paramref name="fontFamily"/> here, which is what gives the document's text a font.
        /// </param>
        /// <param name="fontFamily">
        /// The font family every text element resolves. Import it beforehand (see <c>FontSystem.ImportFont</c>) for
        /// text to actually render.
        /// </param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">The markup is malformed - which would be a bug in this file.</exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            UIElement root = new MarkupLoader(configuration).Load(Markup, nameof(MarkupDemo));

            // x:Name'd elements are reachable by name from anywhere in the tree - the wiring story until bindings
            // and generated fields arrive.
            Button ok = root.FindRequiredControl<Button>("ok");
            int clicks = 0;
            ok.Click += (_, _) =>
            {
                clicks++;
                if (ok.Content is TextBlock label)
                    label.Text = $"Clicked {clicks}x";
            };

            return root;
        }
    }
}
