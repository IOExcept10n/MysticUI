// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Drawing;
using System.Globalization;
using Icy.Animations;
using Icy.Configuration;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// Interactive demo of UI scaling: pick a <see cref="UIScaleMode"/>/<see cref="ReferenceFit"/> and a user scale,
    /// apply them, and watch the live readout. Contains a <see cref="ComboBox"/> and a <see cref="ColorPickerButton"/>
    /// so popup placement can be checked under scaling.
    /// </summary>
    /// <remarks>
    /// Display settings never apply live: the controls only edit a draft. <b>Apply</b> commits it and asks for
    /// confirmation, and an unconfirmed change reverts on its own once the countdown runs out - the same safety net as
    /// the Windows display settings, so a bad scale can never leave the UI unusable.
    /// </remarks>
    public static class ScalingDemo
    {
        private const float CountdownBarWidth = 380;
        private static readonly TimeSpan RevertDelay = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Builds the demo's root element.
        /// </summary>
        /// <param name="configuration">The library configuration whose <see cref="IcyConfiguration.Scaling"/> the demo edits.</param>
        /// <param name="fontFamily">The font family every text element resolves.</param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;
            ScalingConfiguration scaling = configuration.Scaling;

            var root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20),
                Width = 420,
            };

            var readout = new TextBlock { Text = "(not attached)" };

            // Touch smoke-test aid: shows what the core gesture recognizer saw last.
            var gestureReadout = new TextBlock { Name = "GestureReadout", Text = "Last gesture: (none)" };
            Icy.Input.Gestures.IGestureEvents gestures = configuration.Input.Events.Gestures;
            gestures.Tapped += (_, e) => gestureReadout.Text = $"Last gesture: {e.Data.Kind} tap x{e.Data.Count} at {e.Data.Position.X},{e.Data.Position.Y}";
            gestures.Held += (_, e) => gestureReadout.Text = $"Last gesture: {e.Data.Kind} hold at {e.Data.Position.X},{e.Data.Position.Y}";
            gestures.DragCompleted += (_, e) => gestureReadout.Text = $"Last gesture: {e.Data.Kind} drag, {e.Data.Velocity.Length():0} px/s";
            gestures.PinchChanged += (_, e) => gestureReadout.Text = string.Create(CultureInfo.InvariantCulture, $"Last gesture: pinch x{e.Data.Scale:0.00} at {e.Data.Center.X:0},{e.Data.Center.Y:0}");

            // The draft editors. They never touch ScalingConfiguration directly - only Apply does.
            var modeBox = new ComboBox { Name = "ScaleModeBox", ItemsSource = Enum.GetValues<UIScaleMode>(), Width = 260 };
            var fitBox = new ComboBox { Name = "ReferenceFitBox", ItemsSource = Enum.GetValues<ReferenceFit>(), Width = 260 };
            var userScaleLabel = new TextBlock();
            var userScale = new Slider { Name = "UserScaleSlider", Minimum = 0.5f, Maximum = 2f, Width = 260 };
            void UpdateUserScaleLabel() => userScaleLabel.Text = $"User scale: {userScale.Value:0.00}x (0.5 - 2.0)";
            userScale.ValueChanged += (_, _) => UpdateUserScaleLabel();

            var applyButton = CreateButton("ApplyButton", "Apply");
            var discardButton = CreateButton("DiscardButton", "Discard");
            var editButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 8) };
            editButtons.Children.Add(applyButton);
            editButtons.Children.Add(discardButton);

            var countdownBar = new Border
            {
                Name = "RevertCountdownBar",
                Height = 4,
                Width = CountdownBarWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
                Background = new SolidColorBrush(Color.FromArgb(255, 230, 160, 40)),
            };
            var keepButton = CreateButton("KeepButton", "Keep");
            var revertButton = CreateButton("RevertButton", "Revert");
            var confirmButtons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            confirmButtons.Children.Add(keepButton);
            confirmButtons.Children.Add(revertButton);
            var confirmPanel = new StackPanel { Name = "ConfirmPanel", Orientation = Orientation.Vertical, IsVisible = false };
            confirmPanel.Children.Add(new TextBlock { Text = $"Keep these display settings? Reverting in {RevertDelay.TotalSeconds:0} s." });
            confirmPanel.Children.Add(countdownBar);
            confirmPanel.Children.Add(confirmButtons);

            void LoadDraft(UIScaleMode mode, ReferenceFit fit, float user)
            {
                modeBox.SelectedItem = mode;
                fitBox.SelectedItem = fit;
                userScale.Value = user;
            }

            // The settings that were in effect before the pending Apply, restored by Revert or the countdown.
            UIScaleMode previousMode = scaling.Mode;
            ReferenceFit previousFit = scaling.ReferenceFit;
            float previousUserScale = scaling.UserScale;
            Animation? countdown = null;

            void EndConfirmation()
            {
                // Clear the field first: Stop() doesn't raise Completed today, but the guard in Completed must not
                // depend on that.
                Animation? running = countdown;
                countdown = null;
                running?.Stop();
                confirmPanel.IsVisible = false;
                applyButton.IsEnabled = true;
                discardButton.IsEnabled = true;
            }

            void Revert()
            {
                scaling.Mode = previousMode;
                scaling.ReferenceFit = previousFit;
                scaling.UserScale = previousUserScale;
                LoadDraft(previousMode, previousFit, previousUserScale);
                EndConfirmation();
            }

            applyButton.Click += (_, _) =>
            {
                previousMode = scaling.Mode;
                previousFit = scaling.ReferenceFit;
                previousUserScale = scaling.UserScale;
                if (modeBox.SelectedItem is UIScaleMode mode)
                    scaling.Mode = mode;
                if (fitBox.SelectedItem is ReferenceFit fit)
                    scaling.ReferenceFit = fit;
                scaling.UserScale = userScale.Value;

                confirmPanel.IsVisible = true;
                applyButton.IsEnabled = false;
                discardButton.IsEnabled = false;
                countdownBar.Width = CountdownBarWidth;
                Animation started = countdownBar.Animate(Timeline.FromTo(nameof(Border.Width), RevertDelay, CountdownBarWidth, 0f));
                countdown = started;
                started.Completed += (_, _) =>
                {
                    if (ReferenceEquals(countdown, started))
                        Revert();
                };
            };
            discardButton.Click += (_, _) => LoadDraft(scaling.Mode, scaling.ReferenceFit, scaling.UserScale);
            keepButton.Click += (_, _) => EndConfirmation();
            revertButton.Click += (_, _) => Revert();

            LoadDraft(scaling.Mode, scaling.ReferenceFit, scaling.UserScale);
            UpdateUserScaleLabel();

            root.Children.Add(new TextBlock { Text = "UI scaling" });
            root.Children.Add(readout);
            root.Children.Add(gestureReadout);
            root.Children.Add(new TextBlock { Text = "Scale mode" });
            root.Children.Add(modeBox);
            root.Children.Add(new TextBlock { Text = "Reference fit (1920x1080)" });
            root.Children.Add(fitBox);
            root.Children.Add(userScaleLabel);
            root.Children.Add(userScale);
            root.Children.Add(editButtons);
            root.Children.Add(confirmPanel);
            root.Children.Add(new TextBlock { Text = "Popup placement check" });
            root.Children.Add(new ColorPickerButton());

            void Refresh()
            {
                Canvas? canvas = root.Canvas;
                readout.Text = canvas == null
                    ? "(not attached)"
                    : $"Display {configuration.RenderContext.DisplayScale:0.00}x | Effective {canvas.EffectiveScale:0.00}x | Surface {canvas.SurfaceSize.Width}x{canvas.SurfaceSize.Height}";
            }

            Canvas? subscribed = null;
            root.Attached += (_, _) =>
            {
                if (root.Canvas != null && !ReferenceEquals(root.Canvas, subscribed))
                {
                    subscribed = root.Canvas;
                    subscribed.PropertyChanged += (_, _) => Refresh();
                }

                Refresh();
            };

            return root;
        }

        private static Button CreateButton(string name, string text) => new()
        {
            Name = name,
            Content = new TextBlock { Text = text },
            Padding = new Thickness(12, 6),
            Margin = new Thickness(0, 0, 8, 0),
        };
    }
}
