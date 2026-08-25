// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Drawing;
using Icy.Configuration;
using Icy.Markup;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A two-<see cref="Page"/> document hosted in a <see cref="Frame"/>, with a button on each page driving
    /// <see cref="Frame.Navigation"/> - the M3 slice of the markup system.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Linked into both <c>MonoGame Sample</c> and <c>Stride Sample</c> like the other shared demos, and needs no
    /// per-engine variant for the same reason they don't: navigation is pure core-library state, not rendering.
    /// </para>
    /// <para>
    /// Both pages set <see cref="Page.KeepAlive"/> so <see cref="INavigationService.TryNavigateBack"/> and
    /// <see cref="INavigationService.TryNavigateNext"/> both return to the exact same instance rather than
    /// dropping it from history - see <see cref="Page.KeepAlive"/>'s remarks for why that's the property's default
    /// behavior when unset. Each page is loaded from an inline markup string via <see cref="MarkupLoader.Load{T}"/>
    /// rather than <see cref="INavigationService.Navigate"/>, the same way <see cref="MarkupDemo"/> avoids needing
    /// a real asset file bundled per engine sample project - <see cref="INavigationService.Navigate"/>'s
    /// path-based loading is covered directly by <c>NavigationServiceTests</c> instead.
    /// </para>
    /// </remarks>
    public static class NavigationDemo
    {
        private const string HomeMarkup =
            """
            <Page KeepAlive="True">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Page 1 - Home</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">A Frame hosts whichever page its NavigationService points to.</TextBlock>
                <Button x:Name="next" Padding="12,6" HorizontalAlignment="Left" Background="#FF3C64C8">Go to page 2</Button>
              </StackPanel>
            </Page>
            """;

        private const string DetailsMarkup =
            """
            <Page KeepAlive="True">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Page 2 - Details</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">TryNavigateBack() returns to the same Page 1 instance - KeepAlive keeps it in history.</TextBlock>
                <Button x:Name="back" Padding="12,6" HorizontalAlignment="Left" Background="#FF3C64C8">Back</Button>
              </StackPanel>
            </Page>
            """;

        /// <summary>
        /// Builds the demo's root element: a <see cref="Frame"/> already navigated to the home page.
        /// </summary>
        /// <param name="configuration">The library configuration the loader resolves types and properties through.</param>
        /// <param name="fontFamily">The font family every text element resolves - see <see cref="MarkupDemo.Build"/>'s remarks.</param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">A page's markup is malformed - which would be a bug in this file.</exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            var loader = new MarkupLoader(configuration);
            Page home = loader.Load<Page>(HomeMarkup, $"{nameof(NavigationDemo)}.Home");
            Page details = loader.Load<Page>(DetailsMarkup, $"{nameof(NavigationDemo)}.Details");

            var frame = new Frame
            {
                Padding = new Thickness(14),
                Background = new SolidColorBrush(Color.FromArgb(255, 0x26, 0x26, 0x2C)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0x46, 0x46, 0x52)),
                BorderThickness = new Thickness(1),
            };

            // Wire the buttons before either page is navigated to, while each is still its own tree's root - see
            // UIElementExtensions.FindControl's remarks on why the search starts from the root.
            home.FindRequiredControl<Button>("next").Click += (_, _) => frame.Navigation.NavigateTo(details);
            details.FindRequiredControl<Button>("back").Click += (_, _) => frame.Navigation.TryNavigateBack();

            frame.Navigation.NavigateTo(home);
            return frame;
        }
    }
}
