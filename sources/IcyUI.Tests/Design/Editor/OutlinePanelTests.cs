using Icy.Design;
using Icy.Design.Editor.Panels;
using Icy.Input.Devices;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class OutlinePanelTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="first" Width="40" Height="20"/>
              <StackPanel x:Name="inner">
                <TextBlock x:Name="leaf" Text="x"/>
              </StackPanel>
            </StackPanel>
            """;

        private static (EditorTestHost Host, OutlinePanel Panel) Create()
        {
            var host = new EditorTestHost(Page);
            var panel = new OutlinePanel { Session = host.Session };
            host.Canvas.AddOverlay(panel);
            host.Render();
            return (host, panel);
        }

        [Fact]
        public void TheOutline_MirrorsTheElementTree_WithTypeAndNameLabels()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;

                OutlineItem root = Assert.Single(panel.Roots);
                Assert.Equal("StackPanel \"root\"", root.Label);
                Assert.Equal(["Button \"first\"", "StackPanel \"inner\""], root.Children.Select(x => x.Label));
                Assert.Equal("TextBlock \"leaf\"", root.Children[1].Children[0].Label);
            }
        }

        [Fact]
        public void SelectingInTheFrame_SelectsTheRow_AndSelectingARow_SelectsTheElement()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<TextBlock>("leaf"));
                Assert.Same(host.Document, panel.Document);
                Assert.Equal("TextBlock \"leaf\"", ((OutlineItem)panel.Tree.SelectedItem!).Label);

                panel.Tree.SelectedItem = panel.Roots[0].Children[0];
                Assert.Same(host.Named<Button>("first"), host.Session.Selection!.Instance);
            }
        }

        [Fact]
        public void AnAttributeEdit_KeepsTheSameItems()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                OutlineItem before = panel.Roots[0].Children[0];

                host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("first")), "Width", "60");

                Assert.Same(before, panel.Roots[0].Children[0]);
            }
        }

        [Fact]
        public void SeveralEditsInOneFrame_RebuildTheOutlineOnce()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                host.Render();
                int before = panel.RebuildCount;

                for (int i = 0; i < 5; i++)
                    host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("first")), "Width", (50 + i).ToString(System.Globalization.CultureInfo.InvariantCulture));
                host.Render();

                Assert.Equal(before + 1, panel.RebuildCount);
            }
        }

        [Fact]
        public void AStructuralEdit_UpdatesTheTree_AndKeepsExpansion()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                OutlineItem inner = panel.Roots[0].Children[1];
                panel.Tree.Expand(inner);

                host.Document.Editor.RemoveElement(host.IdOf(host.Named<Button>("first")));

                Assert.Same(inner, panel.Roots[0].Children[0]);
                Assert.True(panel.Tree.IsExpanded(inner));
            }
        }

        [Fact]
        public void RetargetingToAnotherDocument_ShowsOnlyItsElements()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                var other = new Icy.Markup.MarkupLoader(host.Configuration).Load("<Border x:Name=\"solo\"/>", "other.xml");
                DesignDocument otherDocument = host.Design.FindDocument(other, out _)!;

                panel.Document = otherDocument;

                OutlineItem root = Assert.Single(panel.Roots);
                Assert.Equal("Border \"solo\"", root.Label);
                Assert.Empty(root.Children);

                panel.Document = host.Document;
                Assert.Equal("StackPanel \"root\"", Assert.Single(panel.Roots).Label);
                GC.KeepAlive(other);
            }
        }

        [Fact]
        public void SwitchingBack_RestoresTheDocumentsExpansion_OnFreshRows()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                OutlineItem innerBefore = panel.Roots[0].Children[1];
                panel.Tree.Expand(panel.Roots[0]);
                panel.Tree.Expand(innerBefore);
                var other = new Icy.Markup.MarkupLoader(host.Configuration).Load("<Border x:Name=\"solo\"/>", "other.xml");

                panel.Document = host.Design.FindDocument(other, out _)!;
                panel.Document = host.Document;

                OutlineItem innerAfter = panel.Roots[0].Children[1];
                Assert.NotSame(innerBefore, innerAfter);
                Assert.True(panel.Tree.IsExpanded(innerAfter));
                GC.KeepAlive(other);
            }
        }

        [Fact]
        public void AnExplicitDocument_StaysWhileTheSelectionIsElsewhere()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<TextBlock>("leaf"));
                var other = new Icy.Markup.MarkupLoader(host.Configuration).Load("<Border x:Name=\"solo\"/>", "other.xml");
                DesignDocument otherDocument = host.Design.FindDocument(other, out _)!;

                panel.Document = otherDocument;

                Assert.Same(otherDocument, panel.Document);
                Assert.Equal("Border \"solo\"", Assert.Single(panel.Roots).Label);
                GC.KeepAlive(other);
            }
        }

        [Fact]
        public void Duplicate_InsertsACopyAfterTheSelection()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<TextBlock>("leaf"));

                Assert.True(panel.Duplicate().Succeeded);

                Assert.Equal(2, panel.Roots[0].Children[1].Children.Count);
                Assert.Single(System.Text.RegularExpressions.Regex.Matches(host.Document.Text, "x:Name=\"leaf\""));
                Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(host.Document.Text, "<TextBlock Text=\"x\"/>|<TextBlock x:Name=\"leaf\" Text=\"x\"/>").Count);
            }
        }

        [Fact]
        public void CtrlD_DuplicatesTheSelection_OnlyWhileTheTreeHasFocus()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<TextBlock>("leaf"));
                host.Input.Keyboard.ModifierKeys = ModifierKeys.Ctrl;

                host.Input.Keyboard.RaiseKeyDown(Keys.D);
                Assert.Single(panel.Roots[0].Children[1].Children);

                host.Canvas.Focus(panel.Tree);
                host.Input.Keyboard.RaiseKeyDown(Keys.D);
                Assert.Equal(2, panel.Roots[0].Children[1].Children.Count);
            }
        }

        [Fact]
        public void Move_PutsAnElementInsideAnotherContainer()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                OutlineItem first = panel.Roots[0].Children[0];
                OutlineItem inner = panel.Roots[0].Children[1];

                Assert.True(panel.Move(first, inner, OutlineDropPosition.Inside).Succeeded);

                Assert.Equal(["StackPanel \"inner\""], panel.Roots[0].Children.Select(x => x.Label));
                Assert.Equal(2, panel.Roots[0].Children[0].Children.Count);
            }
        }
    }
}
