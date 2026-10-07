// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editor;
using Icy.Input.Devices;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorFrameTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="100,100,0,0">
              <Border x:Name="box" Width="100" Height="40"/>
              <Button x:Name="ok" Width="100" Height="30">OK</Button>
            </StackPanel>
            """;

        [Fact]
        public void Attach_AddsTheTwoLayersOnTop()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);

            Assert.Same(frame.CaptureLayer, host.Canvas.Overlays[^2]);
            Assert.Same(frame.Toolbar, host.Canvas.Overlays[^1]);
            Assert.Contains(frame.CaptureLayer, frame.Session.OwnLayers);
            Assert.Contains(frame.Toolbar, frame.Session.OwnLayers);
        }

        [Fact]
        public void EditMode_CapturesThePointerEverywhere()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Named<Button>("ok"), 5, 5));
            host.Render();

            Assert.Same(frame.CaptureLayer, host.Canvas.HitTest(host.At(host.Named<Button>("ok"), 5, 5)));
            Assert.True(host.Canvas.IsMouseOverGUI);
        }

        [Fact]
        public void InteractMode_LetsTheClickReachThePage()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var ok = host.Named<Button>("ok");
            bool clicked = false;
            ok.Click += (_, _) => clicked = true;

            frame.Session.Mode = EditorMode.Interact;
            host.Input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(host.At(ok, 50, 15), 1));

            Assert.True(clicked);
        }

        [Fact]
        public void TheToolbar_ShowsTheModeTheSelectionAndTheBlockedReason()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);

            frame.Session.Select(host.Named<Border>("box"));
            Assert.Equal("Edit · Border \"box\"", frame.ToolbarLabel);

            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);
            Assert.Equal("The markup has errors; fix them first.", frame.ToolbarStatus);

            frame.Session.Mode = EditorMode.Interact;
            Assert.StartsWith("Interact", frame.ToolbarLabel, StringComparison.Ordinal);
        }

        [Fact]
        public void TheToolbar_SaysSoWhenNothingOnTheCanvasIsTracked()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            var empty = new Canvas(host.Configuration) { IsInputEnabled = true, IsVisible = true };
            empty.Add(new Border { Width = 50, Height = 50 });

            using EditorFrame frame = EditorFrame.Attach(empty, host.Design);

            Assert.Equal("Edit · No tracked pages on this canvas", frame.ToolbarLabel);
        }

        [Fact]
        public void TheFrame_ReturnsOnTopOfALaterPopup()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var popup = new Border { Width = 50, Height = 50 };
            host.Canvas.AddOverlay(popup);

            host.Render();

            Assert.Same(frame.Toolbar, host.Canvas.Overlays[^1]);
            Assert.Same(frame.CaptureLayer, host.Canvas.Overlays[^2]);
            Assert.Same(popup, host.Canvas.Overlays[^3]);
        }

        [Fact]
        public void Dispose_RemovesTheLayersAndTheBindings()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);

            frame.Dispose();

            Assert.DoesNotContain(frame.CaptureLayer, host.Canvas.Overlays);
            Assert.DoesNotContain(frame.Toolbar, host.Canvas.Overlays);
            Assert.True(host.Canvas.IsKeyboardNavigationEnabled);
            Assert.False(host.Input.Events.RaiseGesture(new KeyGesture(Keys.Delete)));
        }

        [Fact]
        public void ToolbarPlacement_MovesTheToolbar()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);

            frame.ToolbarPlacement = EditorToolbarPlacement.BottomRight;

            Assert.Equal(HorizontalAlignment.Right, frame.Toolbar.HorizontalAlignment);
            Assert.Equal(VerticalAlignment.Bottom, frame.Toolbar.VerticalAlignment);
        }
    }
}
