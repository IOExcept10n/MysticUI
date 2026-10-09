using Icy.Design.Editor.Panels;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class ToolboxPanelTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="b" Width="40" Height="20"/>
            </StackPanel>
            """;

        [Fact]
        public void TheDefaults_ListBuiltInControls_WithLoadableSnippets()
        {
            using var host = new EditorTestHost(Page);
            var defaults = ToolboxItem.CreateDefaults(host.Configuration);

            Assert.Contains(defaults, x => x.DisplayName == "Button" && x.Snippet == "<Button>Button</Button>");
            Assert.Contains(defaults, x => x.DisplayName == "StackPanel");
            Assert.DoesNotContain(defaults, x => x.DisplayName == "UIElement");
            foreach (ToolboxItem item in defaults)
                new Icy.Markup.MarkupLoader(host.Configuration).Load(item.Snippet);
        }

        [Fact]
        public void Insert_AddsIntoASelectedContainer_AndSelectsTheNewElement()
        {
            using var host = new EditorTestHost(Page);
            var panel = new ToolboxPanel { Session = host.Session };
            host.Session.Select(host.Named<StackPanel>("root"));

            Assert.True(panel.Insert(new ToolboxItem("Slider", "Controls", "<Slider/>")).Succeeded);

            Assert.Contains("<Slider/>", host.Document.Text);
            Assert.IsType<Slider>(host.Session.Selection!.Instance);
        }

        [Fact]
        public void Insert_AddsAfterASelectedLeaf()
        {
            using var host = new EditorTestHost(Page);
            var panel = new ToolboxPanel { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));

            Assert.True(panel.Insert(new ToolboxItem("CheckBox", "Controls", "<CheckBox/>")).Succeeded);

            Assert.True(host.Document.Text.IndexOf("<CheckBox/>", StringComparison.Ordinal) > host.Document.Text.IndexOf("x:Name=\"b\"", StringComparison.Ordinal));
        }

        [Fact]
        public void Insert_WithNothingSelected_SaysWhy()
        {
            using var host = new EditorTestHost(Page);
            var panel = new ToolboxPanel { Session = host.Session };

            var result = panel.Insert(new ToolboxItem("Button", "Controls", "<Button/>"));

            Assert.False(result.Succeeded);
            Assert.Equal("Select where to insert first.", result.Error!.Value.Message);
        }

        [Fact]
        public void ClickingAnItem_Inserts_OrShowsWhyNot()
        {
            using var host = new EditorTestHost(Page);
            var panel = new ToolboxPanel { Session = host.Session };
            panel.Items.Clear();
            panel.Items.Add(new ToolboxItem("Slider", "Controls", "<Slider/>"));
            var scroller = (ScrollViewer)((Grid)panel.Content!).Children[0];
            Button button = ((StackPanel)scroller.Content!).Children.OfType<Button>().Single();

            button.Command!.Execute(null);
            Assert.Equal("Select where to insert first.", panel.StatusText);

            host.Session.Select(host.Named<StackPanel>("root"));
            button.Command!.Execute(null);
            Assert.Contains("<Slider/>", host.Document.Text);
            Assert.Equal(string.Empty, panel.StatusText);
        }

        [Fact]
        public void AGameCanAddItsOwnControls()
        {
            var panel = new ToolboxPanel();
            panel.Items.Add(new ToolboxItem("HealthBar", "Game", "<HealthBar/>"));
            Assert.Contains(panel.Items, x => x.DisplayName == "HealthBar");
        }
    }
}
