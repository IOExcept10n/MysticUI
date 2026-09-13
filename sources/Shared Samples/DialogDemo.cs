// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Threading.Tasks;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="Icy.UI.Controls.Dialog"/>/<see cref="Icy.UI.Controls.Dialog{TResult}"/>
    /// and <see cref="Icy.UI.Controls.MessageBox"/> (Phase 7 of the Tier-2 roadmap): one button per
    /// <see cref="Icy.UI.Controls.DialogButtons"/> variant plus <see cref="Icy.UI.Controls.MessageBox.ShowInputAsync"/>,
    /// with the returned result/typed value displayed below so the async round-trip is visually confirmable.
    /// </summary>
    public static class DialogDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="ListBoxDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Dialog (Phase 7)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Each button opens a modal MessageBox. The result of the last one shows below.</TextBlock>

                <StackPanel Orientation="Horizontal" Margin="0,0,0,12">
                  <Button x:Name="OkButton" Margin="0,0,8,0">OK</Button>
                  <Button x:Name="OkCancelButton" Margin="0,0,8,0">OK/Cancel</Button>
                  <Button x:Name="YesNoButton" Margin="0,0,8,0">Yes/No</Button>
                  <Button x:Name="YesNoCancelButton" Margin="0,0,8,0">Yes/No/Cancel</Button>
                  <Button x:Name="InputButton">Input</Button>
                </StackPanel>

                <TextBlock x:Name="ResultText" FontSize="14" Foreground="WhiteSmoke">No dialog shown yet.</TextBlock>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/> and wiring each button's
        /// <see cref="Button.Click"/> to a <see cref="MessageBox"/> call, displaying the awaited result in the
        /// loaded <c>ResultText</c> <see cref="TextBlock"/>.
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
            UIElement root = loader.Load(Markup, nameof(DialogDemo));

            var resultText = root.FindRequiredControl<TextBlock>("ResultText");

            async void ShowResult(Task<DialogResult> task) => resultText.Text = $"Result: {await task}";

            root.FindRequiredControl<Button>("OkButton").Click += (sender, _) =>
                ShowResult(MessageBox.ShowAsync(((Button)sender!).Canvas!, "This is an OK-only message.", DialogButtons.OK));
            root.FindRequiredControl<Button>("OkCancelButton").Click += (sender, _) =>
                ShowResult(MessageBox.ShowAsync(((Button)sender!).Canvas!, "Proceed with the operation?", DialogButtons.OKCancel));
            root.FindRequiredControl<Button>("YesNoButton").Click += (sender, _) =>
                ShowResult(MessageBox.ShowAsync(((Button)sender!).Canvas!, "Do you want to continue?", DialogButtons.YesNo));
            root.FindRequiredControl<Button>("YesNoCancelButton").Click += (sender, _) =>
                ShowResult(MessageBox.ShowAsync(((Button)sender!).Canvas!, "Save changes before closing?", DialogButtons.YesNoCancel));

            root.FindRequiredControl<Button>("InputButton").Click += async (sender, _) =>
            {
                string? input = await MessageBox.ShowInputAsync(((Button)sender!).Canvas!, "Enter a value:", "default");
                resultText.Text = input == null ? "Input cancelled." : $"Input: {input}";
            };

            return root;
        }
    }
}
