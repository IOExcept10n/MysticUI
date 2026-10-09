using Icy.Design.Editor.Panels;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public sealed class EditorCommandBarTests : IDisposable
    {
        private const string Page = """<StackPanel HorizontalAlignment="Left" VerticalAlignment="Top"><Button x:Name="b" Width="40" Height="20"/></StackPanel>""";
        private readonly string root = Directory.CreateTempSubdirectory("icy-bar-").FullName;

        public void Dispose() => Directory.Delete(root, recursive: true);

        [Fact]
        public void WithNoSourceRoot_SaveIsDisabled_AndTheStatusSaysWhy()
        {
            using var host = new EditorTestHost(Page);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));

            Assert.False(bar.CanSave);
            Assert.Contains("page.xml", bar.StatusText);
        }

        [Fact]
        public void Save_WritesTheSelectedDocument_AndClearsTheBadge()
        {
            using var host = new EditorTestHost(Page);
            File.WriteAllText(Path.Combine(root, "page.xml"), Page);
            host.Design.UseSourceRoot(root);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));
            host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "60");
            Assert.True(bar.HasUnsavedChanges);

            bar.Save();

            Assert.False(bar.HasUnsavedChanges);
            Assert.Contains("Width=\"60\"", File.ReadAllText(Path.Combine(root, "page.xml")));
        }

        [Fact]
        public void AnIOError_IsShownInTheStatus_NotThrown()
        {
            using var host = new EditorTestHost(Page);
            string path = Path.Combine(root, "page.xml");
            File.WriteAllText(path, Page);
            host.Design.UseSourceRoot(root);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));
            host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "60");

            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                bar.Save();

            Assert.NotEmpty(bar.StatusText);
            Assert.True(bar.HasUnsavedChanges);
        }

        [Fact]
        public void ABlockedSelection_ShowsWhyInTheStatus()
        {
            using var host = new EditorTestHost(Page);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));

            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);
            Assert.Equal(host.Session.BlockedReason, bar.StatusText);

            host.Document.ApplyText(host.Document.Text + "</StackPanel>");
            Assert.DoesNotContain("errors", bar.StatusText);
        }

        [Fact]
        public void SaveAll_ReportsHowManyWereSkipped()
        {
            using var host = new EditorTestHost(Page);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "60");

            bar.SaveAll();

            Assert.Contains("1 page", bar.StatusText);
        }
    }
}
