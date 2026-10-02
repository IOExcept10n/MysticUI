// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Linq;
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
        public void SelectedColor_Set_RaisesPropertyChanged()
        {
            // Regression: SelectedColor's setter was a pure forwarder (get => picker.SelectedColor;
            // set => picker.SelectedColor = value;) with no OnPropertyChanged(nameof(SelectedColor)) call - the
            // same defect class Control.cs's precedence fix (6c4cf38) addressed for Background/BorderBrush/
            // BorderThickness/Padding, left unfixed on this sibling control. A direct assignment never raised
            // change notification despite [RegisterReference] advertising this property as bindable/stylable.
            var button = new ColorPickerButton();
            string? raisedPropertyName = null;
            button.PropertyChanged += (_, e) => raisedPropertyName = e.PropertyName;

            button.SelectedColor = Color.FromArgb(255, 10, 20, 30);

            Assert.Equal(nameof(ColorPickerButton.SelectedColor), raisedPropertyName);
        }

        [Fact]
        public void OpeningNearViewportBottom_PlacesThePopupAboveInstead()
        {
            // Regression: PositionPopup only ever anchored below the trigger button, unlike Selector's own
            // PositionPopup - a 360x260-ish ColorPicker popup opened from a button anywhere in the lower half of
            // the window used to render mostly/fully off-screen. Mirrors
            // SelectorTests.OpeningNearViewportBottom_PlacesThePopupAboveInstead's own setup.
            var configuration = new IcyConfiguration(
                new FakeInputSystem(),
                new AssetConfiguration(AssetContext.ApplicationContext),
                new FakeRenderContext { ViewportSize = new Size(800, 200) },
                new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var button = new ColorPickerButton
            {
                Width = 40,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,

                // The button's own bottom edge (190 + 24 = 214) already sits below the 200px-tall viewport -
                // spaceBelow is negative before the popup's own height even enters into it, so this forces the
                // flip unconditionally, regardless of exactly how tall the popup's own natural size measures out
                // to (as long as it's non-negative, which it always is).
                Margin = new Thickness(0, 190, 0, 0),
            };

            // A default ColorPicker's "Swatches" tab (selected by default) with no swatches added, and this test
            // environment's headers rendering without a real font system, both measure to a natural height of 0 -
            // giving the popup a real (non-zero) height here so the "placed above" assertion below is a
            // meaningful strict inequality rather than an accidental Y-coordinate coincidence.
            button.SwatchColors.Add(Color.FromArgb(255, 1, 2, 3));
            button.RefreshSwatches();

            canvas.Add(button);
            canvas.Render();

            button.IsOpen = true;
            canvas.Render();

            UIElement popup = canvas.Overlays.Single();
            Assert.True(popup.ActualBounds.Y < button.ActualBounds.Y);
        }

        [Fact]
        public void OpenPopup_FollowsItsAnchor_WhenTheUIScaleChanges()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var button = new ColorPickerButton { Width = 100, Height = 30, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            canvas.Add(button);
            canvas.Render();
            button.IsOpen = true;
            canvas.Render();
            UIElement popup = canvas.Overlays.Single();
            Assert.Equal(700, popup.Margin.Left);

            // 800 px viewport at 2x = a 400-unit surface, so the right-aligned anchor moves to x = 300.
            configuration.Scaling.UserScale = 2f;
            canvas.Render();

            Assert.Equal(300, popup.Margin.Left);
        }

        [Fact]
        public void OpeningWithRoomBelow_PlacesThePopupBelow()
        {
            var configuration = new IcyConfiguration(
                new FakeInputSystem(),
                new AssetConfiguration(AssetContext.ApplicationContext),
                new FakeRenderContext { ViewportSize = new Size(800, 600) },
                new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var button = new ColorPickerButton
            {
                Width = 40,
                Height = 24,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(button);
            canvas.Render();

            button.IsOpen = true;
            canvas.Render();

            UIElement popup = canvas.Overlays.Single();
            Assert.True(popup.ActualBounds.Y > button.ActualBounds.Y);
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
