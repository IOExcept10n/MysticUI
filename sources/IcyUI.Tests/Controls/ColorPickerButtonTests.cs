// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ColorPickerButtonTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var button = new ColorPickerButton();

            Assert.False(button.IsOpen);
            Assert.Equal(Color.FromArgb(255, 0, 0, 0), button.SelectedColor);
        }

        [Fact]
        public void Toggling_OpensAndClosesThePopup_OnTheOwningCanvasOverlays()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration);
            var button = new ColorPickerButton();
            canvas.Add(button);

            button.IsOpen = true;
            Assert.Single(canvas.Overlays);

            button.IsOpen = false;
            Assert.Empty(canvas.Overlays);
        }

        [Fact]
        public void SettingIsOpen_BeforeAttaching_OpensThePopupOnceAttachedToACanvas()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration);
            var button = new ColorPickerButton();

            button.IsOpen = true;
            canvas.Add(button);

            Assert.Single(canvas.Overlays);
        }

        [Fact]
        public void SelectedColor_ChangesArea_TracksThroughToThePreviewSwatch()
        {
            var button = new ColorPickerButton();

            button.SelectedColor = Color.FromArgb(255, 10, 20, 30);

            var toggle = (ToggleButton)typeof(ColorPickerButton).GetField("toggle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(button)!;
            var brush = (Icy.Rendering.Brushes.SolidColorBrush)toggle.Background;
            Assert.Equal(Color.FromArgb(255, 10, 20, 30), brush.Color);
        }

        [Fact]
        public void RefreshSwatches_ForwardsToTheInternalPicker_RebuildingItsSwatchStrip()
        {
            var button = new ColorPickerButton();
            button.SwatchColors.Add(Color.FromArgb(255, 1, 2, 3));
            button.SwatchColors.Add(Color.FromArgb(255, 4, 5, 6));

            button.RefreshSwatches();

            var picker = (ColorPicker)typeof(ColorPickerButton).GetField("picker", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(button)!;
            var swatchStrip = (StackPanel)typeof(ColorPicker).GetField("swatchStrip", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(picker)!;
            Assert.Equal(button.SwatchColors.Count, swatchStrip.Children.Count);
        }
    }
}
