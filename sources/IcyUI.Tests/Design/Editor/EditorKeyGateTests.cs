using Icy.Input.Devices;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorKeyGateTests
    {
        private const string Page =
            """
            <StackPanel HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="b" Width="40" Height="20" Margin="10"/>
            </StackPanel>
            """;

        [Fact]
        public void ArrowKeys_DontNudge_WhileATextBoxHasFocus()
        {
            using var host = new EditorTestHost(Page);
            var box = new TextBox { Width = 100 };
            host.Canvas.AddOverlay(box);
            host.Render();
            host.Session.Select(host.Named<Button>("b"));

            host.Canvas.Focus(box);
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));

            Assert.DoesNotContain("Margin=\"11", host.Document.Text);

            host.Canvas.Focus(null);
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));

            Assert.Contains("Margin=\"11,10,10,10\"", host.Document.Text);
        }

        [Fact]
        public void ArrowKeys_MoveTheCaret_OfAFocusedTextBox_InEditMode()
        {
            using var host = new EditorTestHost(Page);
            var box = new TextBox { Width = 100, Text = "abc" };
            host.Canvas.AddOverlay(box);
            host.Render();
            host.Session.Select(host.Named<Button>("b"));
            host.Canvas.Focus(box);
            host.Input.Keyboard.RaiseKeyDown(Keys.End);

            // An engine delivers an arrow both as a key gesture and as navigation.
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Left));
            host.Input.Events.Navigation.RaiseFocusChanging(-System.Numerics.Vector2.UnitX);
            host.Input.Keyboard.RaiseKeyDown(Keys.Back);

            Assert.Equal("ac", box.Text);
            Assert.DoesNotContain("Margin=\"9", host.Document.Text);
        }

        [Fact]
        public void ADetachedFocusedTextBox_DoesntBlockTheBindings()
        {
            using var host = new EditorTestHost(Page);
            var box = new TextBox { Width = 100 };
            host.Canvas.AddOverlay(box);
            host.Render();
            host.Session.Select(host.Named<Button>("b"));
            host.Canvas.Focus(box);

            // A panel rebuilding its rows detaches the focused box, but FocusedElement still points at it.
            host.Canvas.RemoveOverlay(box);
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));

            Assert.Contains("Margin=\"11,10,10,10\"", host.Document.Text);
        }

        [Fact]
        public void ArrowKeys_DontNudge_WhileAListHasFocus_UntilThePageIsTapped()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using Icy.Design.Editor.EditorFrame frame = Icy.Design.Editor.EditorFrame.Attach(host.Canvas, host.Design);
            var list = new TreeView { Width = 100, Height = 100 };
            host.Canvas.AddOverlay(list);
            frame.CompanionLayers.Add(list);
            frame.ShowsToolbar = false;
            host.Render();
            Button button = host.Named<Button>("b");
            frame.Session.Select(button);
            host.Canvas.Focus(list);

            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));
            Assert.DoesNotContain("Margin=\"11", host.Document.Text);

            host.Input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(host.At(button, 5, 5), 1));
            Assert.Null(host.Canvas.FocusedElement);
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));
            Assert.Contains("Margin=\"11,10,10,10\"", host.Document.Text);
        }
    }
}
