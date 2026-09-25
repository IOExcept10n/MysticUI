// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Rendering.Brushes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A full color picker - swatches, an HSV square + hue strip, and RGBA sliders + a hex field, switchable via
    /// an internal <see cref="TabControl"/>.
    /// </summary>
    /// <remarks>
    /// All sub-widgets stay synchronized to the single <see cref="SelectedColor"/> behind a re-entrancy guard -
    /// each sub-widget's own change handler checks the guard before writing back to <see cref="SelectedColor"/>,
    /// so <see cref="SelectedColor"/>'s own setter pushing the new value out to every sub-widget doesn't bounce
    /// back through them and loop.
    /// </remarks>
    public class ColorPicker : Control
    {
        private readonly TabControl tabControl = new();
        private readonly StackPanel swatchStrip = new() { Orientation = Orientation.Horizontal };
        private readonly HsvSquare hsvSquare = new();
        private readonly Slider hueSlider = new() { Minimum = 0, Maximum = 360 };
        private readonly Slider redSlider = new() { Minimum = 0, Maximum = 255 };
        private readonly Slider greenSlider = new() { Minimum = 0, Maximum = 255 };
        private readonly Slider blueSlider = new() { Minimum = 0, Maximum = 255 };
        private readonly Slider alphaSlider = new() { Minimum = 0, Maximum = 255, Value = 255 };
        private readonly TextBox hexBox = new();
        private readonly ObservableCollection<Color> swatchColors = [];

        private Color selectedColor = Color.FromArgb(255, 0, 0, 0);
        private bool isSyncing;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorPicker"/> class.
        /// </summary>
        public ColorPicker()
        {
            hueSlider.Background = BuildHueGradient();
            hsvSquare.SaturationValueChanged += (_, _) => OnHsvSquareChanged();
            hueSlider.ValueChanged += (_, _) => OnHueSliderChanged();
            redSlider.ValueChanged += (_, _) => OnRgbaSliderChanged();
            greenSlider.ValueChanged += (_, _) => OnRgbaSliderChanged();
            blueSlider.ValueChanged += (_, _) => OnRgbaSliderChanged();
            alphaSlider.ValueChanged += (_, _) => OnRgbaSliderChanged();
            hexBox.TextChanged += (_, _) => OnHexBoxChanged();

            var pickerTab = new StackPanel { Orientation = Orientation.Vertical };
            pickerTab.Children.Add(hsvSquare);
            pickerTab.Children.Add(hueSlider);

            var slidersTab = new StackPanel { Orientation = Orientation.Vertical };
            slidersTab.Children.Add(redSlider);
            slidersTab.Children.Add(greenSlider);
            slidersTab.Children.Add(blueSlider);
            slidersTab.Children.Add(alphaSlider);
            slidersTab.Children.Add(hexBox);

            tabControl.ItemsSource = new List<object>
            {
                new TabItem { Header = "Swatches", Content = swatchStrip },
                new TabItem { Header = "Picker", Content = pickerTab },
                new TabItem { Header = "Sliders", Content = slidersTab },
            };
            tabControl.SelectedIndex = 0;

            ((Border)Chrome).Child = tabControl;

            SyncSubWidgets();
        }

        /// <summary>
        /// Occurs when <see cref="SelectedColor"/> changes.
        /// </summary>
        public event EventHandler? ColorChanged;

        /// <summary>
        /// Gets or sets the currently picked color.
        /// </summary>
        [Category("Appearance")]
        [RegisterReference]
        public Color SelectedColor
        {
            get => selectedColor;
            set
            {
                if (!SetProperty(ref selectedColor, value))
                    return;
                isSyncing = true;
                try
                {
                    SyncSubWidgets();
                }
                finally
                {
                    isSyncing = false;
                }

                ColorChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets the caller-supplied palette shown in the "Swatches" tab. Call <see cref="RefreshSwatches"/> after
        /// mutating this collection - it isn't itself change-notifying.
        /// </summary>
        [Category("Content")]
        [RegisterReference]
        public IList<Color> SwatchColors => swatchColors;

        /// <summary>
        /// Rebuilds the "Swatches" tab's buttons from the current <see cref="SwatchColors"/>.
        /// </summary>
        public void RefreshSwatches()
        {
            swatchStrip.Children.Clear();
            foreach (Color color in swatchColors)
            {
                var swatch = new SwatchButton { Width = 24, Height = 24, Margin = new Thickness(0, 0, 4, 0), Background = new SolidColorBrush(color) };
                swatch.Tapped += (_, _) => SelectedColor = color;
                swatchStrip.Children.Add(swatch);
            }
        }

        private static GradientBrush BuildHueGradient()
        {
            var brush = new GradientBrush { Kind = GradientKind.Linear, Angle = 0f };
            brush.GradientStops.Add(new GradientStop(0f, Color.Red));
            brush.GradientStops.Add(new GradientStop(1f / 6f, Color.Yellow));
            brush.GradientStops.Add(new GradientStop(2f / 6f, Color.Lime));
            brush.GradientStops.Add(new GradientStop(3f / 6f, Color.Cyan));
            brush.GradientStops.Add(new GradientStop(4f / 6f, Color.Blue));
            brush.GradientStops.Add(new GradientStop(5f / 6f, Color.Magenta));
            brush.GradientStops.Add(new GradientStop(1f, Color.Red));
            return brush;
        }

        private void SyncSubWidgets()
        {
            redSlider.Value = SelectedColor.R;
            greenSlider.Value = SelectedColor.G;
            blueSlider.Value = SelectedColor.B;
            alphaSlider.Value = SelectedColor.A;
            hexBox.Text = $"#{SelectedColor.A:X2}{SelectedColor.R:X2}{SelectedColor.G:X2}{SelectedColor.B:X2}";

            (float h, float s, float v) = ToHsv(SelectedColor);
            hueSlider.Value = h;
            hsvSquare.Hue = h;
            hsvSquare.Saturation = s;
            hsvSquare.Value = v;
        }

        private void OnRgbaSliderChanged()
        {
            if (isSyncing)
                return;
            SelectedColor = Color.FromArgb((int)alphaSlider.Value, (int)redSlider.Value, (int)greenSlider.Value, (int)blueSlider.Value);
        }

        private void OnHexBoxChanged()
        {
            if (isSyncing)
                return;
            if (TryParseHex(hexBox.Text, out Color parsed))
                SelectedColor = parsed;
        }

        private void OnHueSliderChanged()
        {
            if (isSyncing)
                return;
            hsvSquare.Hue = hueSlider.Value;
            SelectedColor = FromHsv(hueSlider.Value, hsvSquare.Saturation, hsvSquare.Value, SelectedColor.A);
        }

        private void OnHsvSquareChanged()
        {
            if (isSyncing)
                return;
            SelectedColor = FromHsv(hsvSquare.Hue, hsvSquare.Saturation, hsvSquare.Value, SelectedColor.A);
        }

        private static bool TryParseHex(string text, out Color color)
        {
            color = default;
            string hex = text.TrimStart('#');
            if (hex.Length == 6 && byte.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out byte r6)
                && byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte g6)
                && byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte b6))
            {
                color = Color.FromArgb(255, r6, g6, b6);
                return true;
            }

            if (hex.Length == 8 && byte.TryParse(hex[0..2], System.Globalization.NumberStyles.HexNumber, null, out byte a8)
                && byte.TryParse(hex[2..4], System.Globalization.NumberStyles.HexNumber, null, out byte r8)
                && byte.TryParse(hex[4..6], System.Globalization.NumberStyles.HexNumber, null, out byte g8)
                && byte.TryParse(hex[6..8], System.Globalization.NumberStyles.HexNumber, null, out byte b8))
            {
                color = Color.FromArgb(a8, r8, g8, b8);
                return true;
            }

            return false;
        }

        private static (float H, float S, float V) ToHsv(Color color)
        {
            float r = color.R / 255f;
            float g = color.G / 255f;
            float b = color.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            float delta = max - min;

            float h = 0f;
            if (delta > 0f)
            {
                if (max == r)
                    h = 60f * (((g - b) / delta) % 6f);
                else if (max == g)
                    h = 60f * (((b - r) / delta) + 2f);
                else
                    h = 60f * (((r - g) / delta) + 4f);
            }

            if (h < 0f)
                h += 360f;

            float s = max <= 0f ? 0f : delta / max;
            return (h, s, max);
        }

        private static Color FromHsv(float h, float s, float v, byte alpha)
        {
            float c = v * s;
            float x = c * (1 - Math.Abs((h / 60f % 2f) - 1));
            float m = v - c;
            (float r, float g, float b) = (h / 60f) switch
            {
                float n when n < 1 => (c, x, 0f),
                float n when n < 2 => (x, c, 0f),
                float n when n < 3 => (0f, c, x),
                float n when n < 4 => (0f, x, c),
                float n when n < 5 => (x, 0f, c),
                _ => (c, 0f, x),
            };
            return Color.FromArgb(alpha, (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
        }

        /// <summary>
        /// A plain clickable swatch button - mirrors <see cref="SelectorItem.Tapped"/>'s exact shape, without any
        /// selection state (a swatch tap just sets <see cref="SelectedColor"/>, it isn't itself a selected item).
        /// </summary>
        private sealed class SwatchButton : Border
        {
            public event EventHandler? Tapped;

            protected internal override void OnTap()
            {
                base.OnTap();
                Tapped?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
