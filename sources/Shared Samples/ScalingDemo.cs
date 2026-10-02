// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using Icy.Configuration;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// Interactive demo of UI scaling: switch <see cref="UIScaleMode"/>/<see cref="ReferenceFit"/>, drag the user scale,
    /// and watch the live readout. Contains a <see cref="ComboBox"/> and a <see cref="ColorPickerButton"/> so popup
    /// placement can be checked under scaling.
    /// </summary>
    public static class ScalingDemo
    {
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

            var modeBox = new ComboBox { ItemsSource = Enum.GetValues<UIScaleMode>(), SelectedItem = scaling.Mode, Width = 260 };
            modeBox.SelectionChanged += (_, _) =>
            {
                if (modeBox.SelectedItem is UIScaleMode mode)
                    scaling.Mode = mode;
            };

            var fitBox = new ComboBox { ItemsSource = Enum.GetValues<ReferenceFit>(), SelectedItem = scaling.ReferenceFit, Width = 260 };
            fitBox.SelectionChanged += (_, _) =>
            {
                if (fitBox.SelectedItem is ReferenceFit fit)
                    scaling.ReferenceFit = fit;
            };

            var userScale = new Slider { Minimum = 0.5f, Maximum = 2f, Value = scaling.UserScale, Width = 260 };
            userScale.ValueChanged += (_, _) => scaling.UserScale = userScale.Value;

            root.Children.Add(new TextBlock { Text = "UI scaling" });
            root.Children.Add(readout);
            root.Children.Add(new TextBlock { Text = "Scale mode" });
            root.Children.Add(modeBox);
            root.Children.Add(new TextBlock { Text = "Reference fit (1920x1080)" });
            root.Children.Add(fitBox);
            root.Children.Add(new TextBlock { Text = "User scale (0.5 - 2.0)" });
            root.Children.Add(userScale);
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
    }
}
