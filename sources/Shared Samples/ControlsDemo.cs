// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Configuration;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// Builds a single scrollable page exercising every v1 Tier-1 control (<see cref="Button"/>,
    /// <see cref="ToggleButton"/>/<see cref="CheckBox"/>, <see cref="Slider"/>, <see cref="TextBox"/>,
    /// <see cref="ScrollViewer"/>, <see cref="Window"/>, <see cref="StackPanel"/>/<see cref="Grid"/>,
    /// <see cref="ProgressBar"/>) with live, wired-up behavior rather than static visuals.
    /// </summary>
    /// <remarks>
    /// Linked into both <c>MonoGame Sample</c> and <c>Stride Sample</c> (see each project's <c>.csproj</c>) rather
    /// than compiled into either one directly, so the same demo runs identically on both engine backends. Only
    /// depends on the engine-independent <c>Icy.*</c> namespaces - no MonoGame/Stride types.
    /// </remarks>
    public static class ControlsDemo
    {
        /// <summary>
        /// Builds the demo page's root element.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration to use. Only its <see cref="IcyConfiguration.Fonts"/> service is consulted
        /// here (to check <paramref name="fontFamily"/> is worth using), but callers should have imported
        /// <paramref name="fontFamily"/> beforehand (see <c>FontSystem.ImportFont</c>) for any text to render.
        /// </param>
        /// <param name="fontFamily">The font family every text-displaying control in the demo is set to use.</param>
        /// <returns>
        /// The root element to add to a <see cref="Canvas"/> (e.g. <c>canvas.Add(ControlsDemo.Build(...))</c>).
        /// Sized to fill its container - give the canvas (or whatever it's added to) real screen dimensions.
        /// </returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            _ = configuration;

            var content = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Padding = new Thickness(24),
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            content.Children.Add(CreateLabel("IcyUI Tier-1 Controls Demo", fontFamily, 28));
            content.Children.Add(CreateLabel("Every control below is live - click, drag, type, and scroll to verify behavior.", fontFamily, 14));
            content.Children.Add(BuildButtonsSection(fontFamily));
            content.Children.Add(BuildSliderSection(fontFamily));
            content.Children.Add(BuildTextBoxSection(fontFamily));
            content.Children.Add(BuildLayoutSection(fontFamily));
            content.Children.Add(BuildScrollSection(fontFamily));

            var scrollViewer = new ScrollViewer
            {
                Content = content,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            UIElement windowSection = BuildWindowSection(fontFamily, out Window window);
            content.Children.Add(windowSection);

            // The Window itself must be a sibling of the ScrollViewer (both children of the same root Panel), not
            // nested inside the scrolling content - otherwise its position would shift as the page scrolls, and a
            // higher ZIndex wouldn't lift it above the ScrollViewer's own clipped content.
            var root = new Panel();
            root.Children.Add(scrollViewer);
            root.Children.Add(window);
            return root;
        }

        private static UIElement BuildButtonsSection(string fontFamily)
        {
            var status = CreateLabel("Clicks: 0 | Toggle: Off | Checked: No", fontFamily);
            int clicks = 0;

            var button = new Button
            {
                Content = CreateLabel("Click Me", fontFamily, margin: Thickness.Zero),
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 100, 200)),
            };

            var toggle = new ToggleButton
            {
                Content = CreateLabel("Toggle", fontFamily, margin: Thickness.Zero),
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromArgb(255, 80, 80, 90)),
            };

            var checkBox = new CheckBox
            {
                Content = CreateLabel("Check Me", fontFamily, margin: Thickness.Zero),
                Padding = new Thickness(12, 6),
                Background = new SolidColorBrush(Color.FromArgb(255, 80, 80, 90)),
            };

            void UpdateStatus() =>
                status.Text = $"Clicks: {clicks} | Toggle: {(toggle.IsChecked ? "On" : "Off")} | Checked: {(checkBox.IsChecked ? "Yes" : "No")}";

            button.Click += (_, _) =>
            {
                clicks++;
                UpdateStatus();
            };
            toggle.IsCheckedChanged += (_, _) => UpdateStatus();
            checkBox.IsCheckedChanged += (_, _) => UpdateStatus();

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(button);
            row.Children.Add(toggle);
            row.Children.Add(checkBox);

            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(row);
            column.Children.Add(status);

            return CreateSection("Buttons (Button / ToggleButton / CheckBox)", fontFamily, column);
        }

        private static UIElement BuildLayoutSection(string fontFamily)
        {
            var stackRow = new StackPanel { Orientation = Orientation.Horizontal };
            stackRow.Children.Add(CreateSwatch(Color.LightCoral, "Stack 1", fontFamily));
            stackRow.Children.Add(CreateSwatch(Color.LightGoldenrodYellow, "Stack 2", fontFamily));
            stackRow.Children.Add(CreateSwatch(Color.LightSkyBlue, "Stack 3", fontFamily));

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var cell00 = CreateSwatch(Color.MediumPurple, "0,0", fontFamily);
            var cell01 = CreateSwatch(Color.MediumSeaGreen, "0,1", fontFamily);
            var cell10 = CreateSwatch(Color.IndianRed, "1,0", fontFamily);
            var cell11 = CreateSwatch(Color.DarkOrange, "1,1", fontFamily);
            Grid.SetColumn(cell01, 1);
            Grid.SetRow(cell10, 1);
            Grid.SetRow(cell11, 1);
            Grid.SetColumn(cell11, 1);
            grid.Children.Add(cell00);
            grid.Children.Add(cell01);
            grid.Children.Add(cell10);
            grid.Children.Add(cell11);

            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(CreateLabel("StackPanel (horizontal):", fontFamily, 14));
            column.Children.Add(stackRow);
            column.Children.Add(CreateLabel("Grid (2x2, via Grid.Row/Grid.Column):", fontFamily, 14));
            column.Children.Add(grid);

            return CreateSection("Layout (StackPanel / Grid)", fontFamily, column);
        }

        private static UIElement BuildScrollSection(string fontFamily)
        {
            var itemsList = new StackPanel { Orientation = Orientation.Vertical };
            for (int i = 1; i <= 20; i++)
            {
                itemsList.Children.Add(CreateLabel($"Item {i}", fontFamily, 14));
            }

            var nestedScroll = new ScrollViewer
            {
                Content = itemsList,
                Width = 300,
                Height = 150,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 34)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 100)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6),
            };

            return CreateSection("ScrollViewer (mouse wheel/touch-swipe to scroll - 20 items in a 150px viewport)", fontFamily, nestedScroll);
        }

        private static UIElement BuildSliderSection(string fontFamily)
        {
            var valueLabel = CreateLabel("Value: 50", fontFamily);

            var progressBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                Width = 300,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 70)),
                FillBrush = new SolidColorBrush(Color.FromArgb(255, 90, 180, 90)),
            };

            var slider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                Width = 300,
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 60, 70)),
            };

            slider.ValueChanged += (_, _) =>
            {
                valueLabel.Text = $"Value: {slider.Value:0}";
                progressBar.Value = slider.Value;
            };

            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(valueLabel);
            column.Children.Add(slider);
            column.Children.Add(progressBar);

            return CreateSection("Slider (drag the thumb) + ProgressBar (mirrors it)", fontFamily, column);
        }

        private static UIElement BuildTextBoxSection(string fontFamily)
        {
            var echo = CreateLabel("You typed: (nothing yet)", fontFamily);

            var textBox = new TextBox
            {
                FontFamily = fontFamily,
                FontSize = 16,
                Width = 300,
                Height = 28,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(255, 30, 30, 34)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 90, 90, 100)),
                BorderThickness = new Thickness(1),
                Foreground = Color.WhiteSmoke,
            };

            textBox.TextChanged += (_, _) => echo.Text = $"You typed: {textBox.Text}";

            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(textBox);
            column.Children.Add(echo);

            return CreateSection("TextBox (click to focus, then type - Backspace/Delete/Left/Right/Home/End all work)", fontFamily, column);
        }

        private static UIElement BuildWindowSection(string fontFamily, out Window window)
        {
            var closeButton = new Button
            {
                Content = CreateLabel("Close", fontFamily, margin: Thickness.Zero),
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 12, 0, 0),
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 100, 200)),
            };

            var okButton = new Button
            {
                Content = CreateLabel("OK", fontFamily, margin: Thickness.Zero),
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 12, 10, 0),
                Background = new SolidColorBrush(Color.FromArgb(255, 80, 80, 90)),
            };

            var buttonRow = new StackPanel { Orientation = Orientation.Horizontal };
            buttonRow.Children.Add(okButton);
            buttonRow.Children.Add(closeButton);

            var windowBody = new StackPanel { Orientation = Orientation.Vertical };
            windowBody.Children.Add(CreateLabel("This is a modal Window.", fontFamily, 18));
            windowBody.Children.Add(CreateLabel("It's a focus scope: Tab/gamepad focus traversal is trapped", fontFamily, 14));
            windowBody.Children.Add(CreateLabel("inside while open, and restores to whatever opened it on close.", fontFamily, 14));
            windowBody.Children.Add(CreateLabel("(No pointer-modality backdrop in v1 - clicking outside won't close it.)", fontFamily, 12));
            windowBody.Children.Add(buttonRow);

            var dialog = new Window
            {
                Content = windowBody,
                Width = 420,
                Height = 220,
                Padding = new Thickness(18),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new SolidColorBrush(Color.FromArgb(255, 45, 45, 54)),
                BorderBrush = new SolidColorBrush(Color.White),
                BorderThickness = new Thickness(2),
                ZIndex = 100,
            };
            window = dialog;

            var openButton = new Button
            {
                Content = CreateLabel("Open Window", fontFamily, margin: Thickness.Zero),
                Padding = new Thickness(12, 6),
                Background = new SolidColorBrush(Color.FromArgb(255, 60, 100, 200)),
            };
            openButton.Click += (_, _) => dialog.Show();
            closeButton.Click += (_, _) => dialog.Close();
            okButton.Click += (_, _) => dialog.Close();

            return CreateSection("Window (modal dialog, focus-trapping)", fontFamily, openButton);
        }

        private static Border CreateSection(string title, string fontFamily, UIElement body)
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };
            stack.Children.Add(CreateLabel(title, fontFamily, 18));
            stack.Children.Add(body);

            return new Border
            {
                Child = stack,
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 16),
                Background = new SolidColorBrush(Color.FromArgb(255, 38, 38, 44)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 70, 70, 82)),
                BorderThickness = new Thickness(1),
            };
        }

        // Margin defaults to a bottom gap sized for stacking multiple labels as vertical siblings (e.g.
        // content.Children.Add(CreateLabel(...))); a label passed as a Button/ToggleButton/CheckBox's own Content
        // (a single, non-stacked child inside that control's own Padding) should pass Thickness.Zero instead - the
        // control's Padding already provides symmetric breathing room, and a non-zero Margin there just shows up
        // as extra, asymmetric empty space under the label once Arrange positions a Stretch-aligned child correctly
        // (see UIElementTests.Arrange_StretchWithAutoSizeAndMargin_PositionsRightAfterMargin_NotCentered).
        private static TextBlock CreateLabel(string text, string fontFamily, float fontSize = 16, Thickness? margin = null) => new()
        {
            Text = text,
            FontFamily = fontFamily,
            FontSize = fontSize,
            Foreground = Color.WhiteSmoke,
            Margin = margin ?? new Thickness(0, 0, 0, 6),
        };

        private static Border CreateSwatch(Color color, string label, string fontFamily) => new()
        {
            Background = new SolidColorBrush(color),
            Width = 100,
            Height = 60,
            Margin = new Thickness(0, 0, 8, 0),
            Child = new TextBlock
            {
                Text = label,
                FontFamily = fontFamily,
                FontSize = 14,
                Foreground = Color.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
    }
}
