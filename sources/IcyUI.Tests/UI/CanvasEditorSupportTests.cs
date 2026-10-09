// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasEditorSupportTests
    {
        [Fact]
        public void IsMouseOverGUI_FollowsTheHitTest()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            canvas.Add(Box(0, 0, 100, 100));

            input.Mouse.MouseInfo = new MouseInfo(new Point(50, 50));
            canvas.Render();
            Assert.True(canvas.IsMouseOverGUI);

            input.Mouse.MouseInfo = new MouseInfo(new Point(500, 500));
            canvas.Render();
            Assert.False(canvas.IsMouseOverGUI);
        }

        [Fact]
        public void IsMouseOverGUI_IsTrueOverAnOverlay()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            canvas.AddOverlay(Box(400, 400, 50, 50));

            input.Mouse.MouseInfo = new MouseInfo(new Point(420, 420));
            canvas.Render();

            Assert.True(canvas.IsMouseOverGUI);
        }

        [Fact]
        public void HitTest_WithAnOverlayFilter_SkipsTheFilteredOverlays()
        {
            (Canvas canvas, _) = CreateCanvas();
            UIElement page = Box(0, 0, 100, 100);
            canvas.Add(page);
            UIElement cover = Box(0, 0, 800, 600);
            canvas.AddOverlay(cover);
            canvas.Render();

            Assert.Same(cover, canvas.HitTest(new Point(10, 10)));
            Assert.Same(page, canvas.HitTest(new Point(10, 10), overlay => overlay != cover));
        }

        [Fact]
        public void KeyboardNavigationDisabled_IgnoresFocusMovesAndCloseModal()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            var first = new UIElement { IsFocusable = true, Width = 10, Height = 10 };
            canvas.Add(first);
            canvas.Render();

            canvas.IsKeyboardNavigationEnabled = false;
            input.Events.Navigation.RaiseFocusNext();
            Assert.Null(canvas.FocusedElement);

            canvas.IsKeyboardNavigationEnabled = true;
            input.Events.Navigation.RaiseFocusNext();
            Assert.Same(first, canvas.FocusedElement);
        }

        [Fact]
        public void PointToSurface_ForOverlayContent_IgnoresTheCanvasTransform()
        {
            (Canvas canvas, _) = CreateCanvas();
            canvas.Offset = new Vector2(50, 0);
            UIElement popup = Box(10, 20, 30, 30);
            canvas.AddOverlay(popup);
            canvas.Render();

            Assert.Equal(new Point(10, 20), popup.PointToSurface(Vector2.Zero));
        }

        [Fact]
        public void AnOverlayOwner_IsRecorded_AndForgottenOnRemoval()
        {
            (Canvas canvas, _) = CreateCanvas();
            UIElement owner = Box(0, 0, 10, 10);
            UIElement popup = Box(0, 0, 10, 10);
            UIElement plain = Box(0, 0, 10, 10);
            canvas.Add(owner);

            canvas.AddOverlay(popup, owner);
            canvas.AddOverlay(plain);

            Assert.Same(owner, canvas.GetOverlayOwner(popup));
            Assert.Null(canvas.GetOverlayOwner(plain));
            canvas.RemoveOverlay(popup);
            Assert.Null(canvas.GetOverlayOwner(popup));
        }

        [Fact]
        public void AComboBoxPopup_IsOwnedByItsComboBox()
        {
            (Canvas canvas, _) = CreateCanvas();
            var comboBox = new Icy.UI.Controls.ComboBox { ItemsSource = new[] { "a", "b" } };
            canvas.Add(comboBox);
            canvas.Render();

            comboBox.IsOpen = true;

            Assert.Same(comboBox, canvas.GetOverlayOwner(canvas.Overlays[^1]));
        }

        private static UIElement Box(int x, int y, int width, int height) => new()
        {
            Width = width,
            Height = height,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(x, y, 0, 0),
        };

        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var canvas = new Canvas(new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration()))
            {
                IsInputEnabled = true,
                IsVisible = true,
            };
            return (canvas, input);
        }
    }
}
