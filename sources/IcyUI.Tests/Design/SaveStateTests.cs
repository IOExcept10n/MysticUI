using Icy.Design;
using Xunit;

namespace Icy.Tests.Design
{
    public sealed class SaveStateTests : IDisposable
    {
        private const string Markup = """<StackPanel><Button x:Name="b" Width="10"/></StackPanel>""";
        private readonly string root = Directory.CreateTempSubdirectory("icy-save-").FullName;
        private readonly DesignTestHost host = new();

        public void Dispose()
        {
            host.Dispose();
            Directory.Delete(root, recursive: true);
        }

        [Fact]
        public void ADocument_IsModifiedAfterAnEdit_AndCleanAfterSave()
        {
            File.WriteAllText(Path.Combine(root, "page.xml"), Markup);
            host.Session.UseSourceRoot(root);
            (var page, DesignDocument document) = host.Load(Markup, "page.xml");
            int raised = 0;
            document.ModifiedChanged += (_, _) => raised++;

            Assert.False(document.IsModified);
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(page, "b")), "Width", "20");
            Assert.True(document.IsModified);

            document.Save();
            Assert.False(document.IsModified);
            Assert.Equal(2, raised);
            Assert.Contains("Width=\"20\"", File.ReadAllText(Path.Combine(root, "page.xml")));
        }

        [Fact]
        public void UndoingBackToTheSavedText_ClearsModified()
        {
            (var page, DesignDocument document) = host.Load(Markup, "page.xml");
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(page, "b")), "Width", "20");

            document.Editor.UndoStack.Undo();

            Assert.False(document.IsModified);
        }

        [Fact]
        public void ADocumentOutsideTheSourceRoot_CannotSave_AndSaysWhy()
        {
            host.Session.UseSourceRoot(root);
            (_, DesignDocument document) = host.Load(Markup, "missing.xml");

            Assert.False(document.CanSave);
            Assert.Contains("missing.xml", document.SaveBlockedReason);
        }

        [Fact]
        public void ADocumentWithNoSourcePath_CannotSave()
        {
            (_, DesignDocument document) = host.Load(Markup, sourcePath: null);

            Assert.False(document.CanSave);
            Assert.Equal("The page has no source path.", document.SaveBlockedReason);
        }

        [Fact]
        public void SaveAll_SavesModifiedDocuments_AndReportsSkippedOnes()
        {
            File.WriteAllText(Path.Combine(root, "a.xml"), Markup);
            host.Session.UseSourceRoot(root);
            (var a, DesignDocument savable) = host.Load(Markup, "a.xml");
            (var b, DesignDocument unsavable) = host.Load(Markup, "b.xml");
            savable.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(a, "b")), "Width", "30");
            unsavable.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(b, "b")), "Width", "30");

            var skipped = host.Session.SaveAll();

            Assert.False(savable.IsModified);
            Assert.Single(skipped);
            Assert.Same(unsavable, skipped[0].Document);
            GC.KeepAlive(a);
            GC.KeepAlive(b);
        }

        [Fact]
        public void SaveAll_ReportsAnIOErrorInsteadOfThrowing()
        {
            string path = Path.Combine(root, "locked.xml");
            File.WriteAllText(path, Markup);
            host.Session.UseSourceRoot(root);
            (var page, DesignDocument document) = host.Load(Markup, "locked.xml");
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(page, "b")), "Width", "30");

            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var skipped = host.Session.SaveAll();
                Assert.Single(skipped);
            }

            Assert.True(document.IsModified);
        }

        [Fact]
        public void FindSourceRoot_WalksUpToTheMarker()
        {
            string project = Path.Combine(root, "Game");
            string bin = Path.Combine(project, "bin", "Debug");
            Directory.CreateDirectory(bin);
            Directory.CreateDirectory(Path.Combine(project, "Assets", "UI"));
            File.WriteAllText(Path.Combine(project, "Game.csproj"), "<Project/>");

            Assert.Equal(Path.Combine(project, "Assets", "UI"), DesignSession.FindSourceRoot(bin, "Game.csproj", Path.Combine("Assets", "UI")));
            Assert.Null(DesignSession.FindSourceRoot(bin, "Other.csproj", string.Empty));
        }
    }
}
