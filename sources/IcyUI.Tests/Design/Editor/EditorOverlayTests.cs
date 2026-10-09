using Icy.Design.Editor;
using Icy.Design.Editor.Panels;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorOverlayTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="b" Width="40" Height="20"/>
            </StackPanel>
            """;

        [Fact]
        public void ShowAndHide_AttachAndDetachEverything()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            int before = host.Canvas.Overlays.Count;
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);

            Assert.False(overlay.IsShown);
            Assert.Equal(before, host.Canvas.Overlays.Count);

            overlay.Show();
            Assert.True(overlay.IsShown);
            Assert.NotNull(EditorSession.FindAttached(host.Canvas));
            Assert.Contains(overlay.Docks, x => host.Canvas.Overlays.Contains(x));
            Assert.Contains(overlay.Panels, x => x is OutlinePanel);
            Assert.DoesNotContain(overlay.FrameToolbarForTest!, host.Canvas.Overlays);

            overlay.Hide();
            Assert.Null(EditorSession.FindAttached(host.Canvas));
            Assert.Equal(before, host.Canvas.Overlays.Count);
        }

        [Fact]
        public void TheToggleKey_ShowsAndHides()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);

            host.Input.Events.RaiseGesture(new Icy.Input.Devices.KeyGesture(Icy.Input.Devices.Keys.F4));
            Assert.True(overlay.IsShown);
            host.Input.Events.RaiseGesture(new Icy.Input.Devices.KeyGesture(Icy.Input.Devices.Keys.F4));
            Assert.False(overlay.IsShown);
        }

        [Fact]
        public void ANullToggleKey_BindsNothing()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design, new EditorOverlayOptions { ToggleKey = null });

            host.Input.Events.RaiseGesture(new Icy.Input.Devices.KeyGesture(Icy.Input.Devices.Keys.F4));
            Assert.False(overlay.IsShown);
        }

        [Fact]
        public void HidingKeepsTheUndoHistory()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);
            overlay.Show();
            host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "60");

            overlay.Hide();

            Assert.True(host.Document.Editor.UndoStack.CanUndo);
            Assert.True(host.Document.IsModified);
        }

        [Fact]
        public void ShowingWhileAnotherEditorOwnsTheCanvas_StaysHidden_AndSaysWhy()
        {
            using var host = new EditorTestHost(Page);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);

            overlay.Show();

            Assert.False(overlay.IsShown);
            Assert.Equal("Another editor is attached to this canvas.", overlay.LastShowError);
        }

        [Fact]
        public void AClickOnADock_DoesNotSelectThePageBeneath()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);
            overlay.Show();
            host.Render();
            host.Render();

            // The left dock covers x = 0..DockWidth below the bar; the page's button sits at its top-left.
            var dockPoint = new System.Drawing.Point(5, 60);
            Assert.False(host.Canvas.HitTest(dockPoint) is EditorCaptureLayer);
        }

        [Fact]
        public void AScopedOverlay_PlacesItsDocksInsideTheScope()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            var scope = host.Named<StackPanel>("root");
            scope.Margin = new Icy.UI.Thickness(200, 0, 0, 0);
            scope.Width = 500;
            scope.Height = 400;
            host.Render();
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design, new EditorOverlayOptions { Scope = scope, DockWidth = 100 });

            overlay.Show();
            host.Render();
            host.Render();

            System.Drawing.Rectangle region = overlay.Session!.Region();
            Assert.Equal(200, region.X);
            foreach (Icy.UI.UIElement dock in overlay.Docks)
            {
                Assert.True(region.Contains(dock.ActualBounds), $"{dock.ActualBounds} is outside {region}");
            }
        }

        [Fact]
        public void Hiding_DisconnectsThePanels()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);
            overlay.Show();
            var panels = overlay.Panels.ToList();

            overlay.Hide();

            Assert.All(panels.OfType<PropertiesPanel>(), x => Assert.Null(x.Session));
            Assert.All(panels.OfType<OutlinePanel>(), x => Assert.Null(x.Session));
            Assert.All(panels.OfType<EditorCommandBar>(), x => Assert.Null(x.Session));
        }
    }
}
