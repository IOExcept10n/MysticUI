using Icy.Design.Editor;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorWorkspaceTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="b" Width="40" Height="20"/>
            </StackPanel>
            """;

        // The game's page stays in the canvas root; the workspace is an overlay standing in for a settings page.
        private static (EditorTestHost Host, EditorWorkspace Workspace) Create()
        {
            var host = new EditorTestHost(Page, attachSession: false);
            var workspace = new EditorWorkspace { Design = host.Design, Width = 800, Height = 600 };
            host.Canvas.AddOverlay(workspace);
            host.Render();
            return (host, workspace);
        }

        [Fact]
        public void OpeningADocument_LoadsAPreview_JoinedToTheSameDocument()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                workspace.Open(host.Document);

                Assert.NotNull(workspace.Preview);
                Assert.Same(host.Document, host.Design.FindDocument(workspace.Preview!, out _));
                Assert.True(workspace.IsEditing);
            }
        }

        [Fact]
        public void AnEditInThePreview_ReachesTheLivePage()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                workspace.Open(host.Document);

                host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "75");

                Assert.Equal(75, host.Named<Button>("b").Width);
                var previewButton = (Button)Icy.Markup.MarkupNameScope.GetScope(workspace.Preview!)!.Find("b")!;
                Assert.Equal(75, previewButton.Width);
            }
        }

        [Fact]
        public void SwitchingDocuments_ReleasesThePreviousPreview()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                var other = new Icy.Markup.MarkupLoader(host.Configuration).Load("<Border x:Name=\"solo\" Width=\"10\" Height=\"10\"/>", "other.xml");
                var otherDocument = host.Design.FindDocument(other, out _)!;

                workspace.Open(host.Document);
                var firstPreview = workspace.Preview!;
                workspace.Open(otherDocument);

                Assert.Null(firstPreview.Canvas);
                Assert.Null(Icy.Markup.MarkupNameScope.GetScope(workspace.Preview!)?.Find("b"));
                Assert.Same(otherDocument, workspace.CurrentDocument);
                GC.KeepAlive(other);
            }
        }

        [Fact]
        public void WhileTheOverlayOwnsTheCanvas_TheWorkspaceWaits_ThenAttaches()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);
                overlay.Show();

                workspace.Open(host.Document);
                Assert.False(workspace.IsEditing);
                Assert.Equal("The editor overlay is active. Close it to edit here.", workspace.StatusText);

                overlay.Hide();
                host.Render();
                Assert.True(workspace.IsEditing);
            }
        }

        [Fact]
        public void ShowingTheOverlayWhileTheWorkspaceEdits_SaysWhy_WithoutThrowing()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                workspace.Open(host.Document);
                using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);

                overlay.Show();

                Assert.False(overlay.IsShown);
                Assert.NotNull(overlay.LastShowError);
                Assert.True(workspace.IsEditing);
            }
        }

        [Fact]
        public void Detaching_ReleasesTheEditor_AndReattachingReopens()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                workspace.Open(host.Document);

                host.Canvas.RemoveOverlay(workspace);
                Assert.False(workspace.IsEditing);
                Assert.Null(EditorSession.FindAttached(host.Canvas));

                host.Canvas.AddOverlay(workspace);
                Assert.True(workspace.IsEditing);
                Assert.Same(host.Document, workspace.CurrentDocument);
            }
        }

        [Fact]
        public void APopupOfAWorkspacePanel_StaysAboveTheEditor()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                workspace.Open(host.Document);
                EditorSession.FindAttached(host.Canvas)!.Select((Icy.UI.UIElement)Icy.Markup.MarkupNameScope.GetScope(workspace.Preview!)!.Find("b")!);
                host.Render();
                ComboBox combo = workspace.EnumerateVisualSubtree().OfType<ComboBox>().First();

                combo.IsOpen = true;
                host.Render();
                host.Render();

                Assert.True(combo.IsOpen);
                Assert.Same(combo, host.Canvas.GetOverlayOwner(host.Canvas.Overlays[^1]));
            }
        }

        [Fact]
        public void OpenFile_TracksANewDocument()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                string path = Path.Combine(Path.GetTempPath(), $"icy-ws-{Guid.NewGuid():N}.xml");
                File.WriteAllText(path, "<Border x:Name=\"fromFile\" Width=\"10\" Height=\"10\"/>");
                try
                {
                    Assert.True(workspace.OpenFile(path));
                    Assert.Equal(path, workspace.CurrentDocument!.SourcePath);
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void AFailedOpen_LetsThePreviousDocumentBeReopened()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                var other = new Icy.Markup.MarkupLoader(host.Configuration).Load("<Border x:Name=\"solo\" Width=\"10\" Height=\"10\"/>", "other.xml");
                var otherDocument = host.Design.FindDocument(other, out _)!;
                otherDocument.ApplyText("<NoSuchElement/>");

                workspace.Open(host.Document);
                workspace.Open(otherDocument);

                Assert.Null(workspace.Preview);
                Assert.Null(workspace.CurrentDocument);
                Assert.NotEmpty(workspace.StatusText);

                workspace.Open(host.Document);
                Assert.NotNull(workspace.Preview);
                Assert.Same(host.Document, workspace.CurrentDocument);
                GC.KeepAlive(other);
            }
        }
    }
}
