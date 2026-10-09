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
    }
}
