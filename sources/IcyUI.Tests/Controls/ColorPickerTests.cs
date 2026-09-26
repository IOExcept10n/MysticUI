// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Generic;
using System.Drawing;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ColorPickerTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var picker = new ColorPicker();

            Assert.Equal(Color.FromArgb(255, 0, 0, 0), picker.SelectedColor);
            Assert.Empty(picker.SwatchColors);
        }

        [Fact]
        public void SelectedColor_Set_RaisesColorChanged()
        {
            var picker = new ColorPicker();
            bool raised = false;
            picker.ColorChanged += (_, _) => raised = true;

            picker.SelectedColor = Color.FromArgb(200, 10, 20, 30);

            Assert.True(raised);
            Assert.Equal(Color.FromArgb(200, 10, 20, 30), picker.SelectedColor);
        }

        [Fact]
        public void SelectedColor_Set_SyncsRgbaSlidersAndHexBox()
        {
            var picker = new ColorPicker();

            picker.SelectedColor = Color.FromArgb(255, 10, 20, 30);

            Assert.Equal(10f, GetSlider(picker, "redSlider").Value);
            Assert.Equal(20f, GetSlider(picker, "greenSlider").Value);
            Assert.Equal(30f, GetSlider(picker, "blueSlider").Value);
            Assert.Equal("#FF0A141E", GetHexBox(picker).Text);
        }

        [Fact]
        public void SettingARgbaSlider_UpdatesSelectedColor_WithoutInfiniteRecursion()
        {
            var picker = new ColorPicker();
            int changeCount = 0;
            picker.ColorChanged += (_, _) => changeCount++;

            GetSlider(picker, "redSlider").Value = 128f;

            Assert.Equal(128, picker.SelectedColor.R);
            Assert.Equal(1, changeCount);
        }

        [Fact]
        public void SettingHexBoxText_UpdatesSelectedColor()
        {
            var picker = new ColorPicker();

            GetHexBox(picker).Text = "#FF112233";

            Assert.Equal(Color.FromArgb(255, 0x11, 0x22, 0x33), picker.SelectedColor);
        }

        [Fact]
        public void SettingHexBoxText_SixDigits_PreservesCurrentAlpha()
        {
            var picker = new ColorPicker { SelectedColor = Color.FromArgb(128, 1, 2, 3) };

            GetHexBox(picker).Text = "#0A141E";

            Assert.Equal(128, picker.SelectedColor.A);
            Assert.Equal(Color.FromArgb(128, 0x0A, 0x14, 0x1E), picker.SelectedColor);
        }

        [Fact]
        public void HexBoxText_WhileFocused_IsNotRewrittenByTheResultingSelectedColorChange()
        {
            // Regression: SyncSubWidgets used to unconditionally rewrite hexBox.Text on every SelectedColor
            // change - including a change caused by the user's own in-progress typing in hexBox itself - jumping
            // the caret and fighting the user mid-edit whenever the partial text happened to already parse as a
            // valid color.
            var picker = new ColorPicker();
            var configuration = new Icy.Configuration.IcyConfiguration(
                new Icy.Tests.Input.FakeInputSystem(),
                new Icy.Configuration.AssetConfiguration(Icy.Assets.AssetContext.ApplicationContext),
                new Icy.Tests.Rendering.FakeRenderContext(),
                new Icy.Configuration.ReflectionConfiguration());
            var canvas = new Canvas(configuration);
            TextBox hexBox = GetHexBox(picker);
            canvas.Focus(hexBox);

            hexBox.Text = "#112233";

            Assert.Equal("#112233", hexBox.Text);
            Assert.Equal(Color.FromArgb(255, 0x11, 0x22, 0x33), picker.SelectedColor);

            canvas.Focus(null);

            Assert.Equal("#FF112233", hexBox.Text);
        }

        [Fact]
        public void SelectedColor_Set_SyncsHueSliderAndHsvSquare_ForANonDegenerateColor()
        {
            var picker = new ColorPicker();

            picker.SelectedColor = Color.FromArgb(255, 10, 20, 200);

            // Blue-ish, fully saturated, mid-brightness - none of h/s/v is at a degenerate edge (v != 0, s != 0),
            // so ToHsv's round-trip is exact and every sub-widget should reflect it. Expected values worked out
            // by hand from ToHsv's own formula: r=10/255, g=20/255, b=200/255 -> max=b, min=r,
            // delta=190/255 -> h=60*((r-g)/delta+4)=60*(4-1/19)=236.842..., s=delta/max=190/200=0.95, v=max=200/255.
            Assert.Equal(236.842f, GetSlider(picker, "hueSlider").Value, 2);
            Assert.Equal(236.842f, GetHsvSquare(picker).Hue, 2);
            Assert.Equal(0.95f, GetHsvSquare(picker).Saturation, 2);
            Assert.Equal(200f / 255f, GetHsvSquare(picker).Value, 2);
        }

        [Fact]
        public void DraggingTheSvSquareToTheBottomRow_DoesNotClobberHueOrSaturation()
        {
            // Regression: SyncSubWidgets used to unconditionally overwrite hsvSquare.Saturation/Hue and
            // hueSlider.Value from ToHsv(SelectedColor) - but ToHsv(black) is always (h=0, s=0, v=0) regardless of
            // what the user actually dragged to, since v == 0 makes every saturation/hue map to the same black.
            // A drag along the SV square's bottom row (v=0) would fight itself: UpdateFromPoint sets Saturation
            // from the pointer, SelectedColor becomes black, and the old SyncSubWidgets then stomped Saturation
            // (and the hue slider) back to 0.
            var picker = new ColorPicker();
            var hueSlider = GetSlider(picker, "hueSlider");
            var hsvSquare = GetHsvSquare(picker);

            // Prime a distinctive, fully-saturated blue via the normal hue-slider path first. A fresh ColorPicker's
            // default SelectedColor is black, which construction's own SyncSubWidgets call leaves hsvSquare.Value
            // at 0 - OnHueSliderChanged's FromHsv call stays black regardless of hue until Value/Saturation are
            // non-zero, so both are primed here before the hue slider actually has any visible effect.
            hsvSquare.Saturation = 1f;
            hsvSquare.Value = 1f;
            hueSlider.Value = 240f;
            Assert.Equal(Color.FromArgb(255, 0, 0, 255), picker.SelectedColor);

            // Simulate a drag to the bottom row: each property setter below already fires SaturationValueChanged,
            // triggering the same OnHsvSquareChanged -> SelectedColor -> SyncSubWidgets path a real drag would.
            // Saturation is compared with a tolerance because the intermediate hue=240/saturation=0.7/value=1 color
            // this first setter passes through gets quantized to 8-bit RGB and back - unrelated float noise from
            // that ordinary (non-degenerate) round-trip, not the bug under test.
            hsvSquare.Saturation = 0.7f;
            hsvSquare.Value = 0f;

            Assert.Equal(Color.FromArgb(255, 0, 0, 0), picker.SelectedColor);
            Assert.Equal(240f, hueSlider.Value);
            Assert.Equal(0.7f, hsvSquare.Saturation, 2);
        }

        [Fact]
        public void SwatchColors_TappingASwatch_SetsSelectedColor()
        {
            var picker = new ColorPicker();
            picker.SwatchColors.Add(Color.FromArgb(255, 1, 2, 3));
            picker.SwatchColors.Add(Color.FromArgb(255, 4, 5, 6));
            picker.RefreshSwatches();

            var swatchStrip = (StackPanel)typeof(ColorPicker)
                .GetField("swatchStrip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(picker)!;
            var secondSwatch = swatchStrip.Children[1];
            InvokeOnTap(secondSwatch);

            Assert.Equal(Color.FromArgb(255, 4, 5, 6), picker.SelectedColor);
        }

        private static Slider GetSlider(ColorPicker picker, string fieldName) =>
            (Slider)typeof(ColorPicker).GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(picker)!;

        private static TextBox GetHexBox(ColorPicker picker) =>
            (TextBox)typeof(ColorPicker).GetField("hexBox", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(picker)!;

        private static HsvSquare GetHsvSquare(ColorPicker picker) =>
            (HsvSquare)typeof(ColorPicker).GetField("hsvSquare", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(picker)!;

        private static void InvokeOnTap(UIElement element) =>
            typeof(UIElement).GetMethod("OnTap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(element, null);
    }
}
