// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class MarkupEditorStructureTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root">
              <Border x:Name="a" Width="10"/>
              <Border x:Name="b" Width="20"/>
            </StackPanel>
            """;

        private const string MovePage =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10"/>
              </StackPanel>
              <StackPanel x:Name="right">
                <TextBlock Text="A"/>
              </StackPanel>
            </StackPanel>
            """;

        [Fact]
        public void Insert_AppendsALineStyledChildAndBuildsItLive()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            EditResult result = document.Editor.InsertElement(host.IdOf(root), 2, "<TextBlock Text=\"Hi\"/>");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="a" Width="10"/>
                  <Border x:Name="b" Width="20"/>
                  <TextBlock Text="Hi"/>
                </StackPanel>
                """),
                document.Text);
            var block = Assert.IsType<TextBlock>(((StackPanel)root).Children[2]);
            Assert.Equal("Hi", block.Text);
            Assert.Equal(result.Node, host.IdOf(block));
        }

        [Fact]
        public void Insert_AtAnIndex_GoesBeforeThatChild()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            document.Editor.InsertElement(host.IdOf(root), 1, "<TextBlock/>");

            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="a" Width="10"/>
                  <TextBlock/>
                  <Border x:Name="b" Width="20"/>
                </StackPanel>
                """),
                document.Text);
            Assert.IsType<TextBlock>(((StackPanel)root).Children[1]);
        }

        [Fact]
        public void Insert_IntoASelfClosingParent_ExpandsIt()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <StackPanel x:Name="root">
                  <StackPanel x:Name="inner"/>
                </StackPanel>
                """));
            var inner = DesignTestHost.Named<StackPanel>(root, "inner");

            document.Editor.InsertElement(host.IdOf(inner), 0, "<Border/>");

            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <StackPanel x:Name="inner">
                    <Border/>
                  </StackPanel>
                </StackPanel>
                """),
                document.Text);
            Assert.IsType<Border>(Assert.Single(inner.Children));
        }

        [Fact]
        public void Insert_KeepsCrlfLineEndings()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page.ReplaceLineEndings("\r\n"));

            document.Editor.InsertElement(host.IdOf(root), 2, "<TextBlock/>");

            Assert.Contains("\r\n  <TextBlock/>\r\n", document.Text);
            Assert.DoesNotContain("\n", document.Text.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        }

        [Fact]
        public void Insert_ResolvesAPrefixDeclaredOnAnAncestor()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                $"""
                <ui:StackPanel xmlns:ui="{MarkupNamespaces.Default}">
                  <ui:Border/>
                </ui:StackPanel>
                """));

            EditResult result = document.Editor.InsertElement(host.IdOf(root), 1, "<ui:TextBlock Text=\"p\"/>");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal("p", Assert.IsType<TextBlock>(((StackPanel)root).Children[1]).Text);
        }

        [Fact]
        public void Insert_IntoASingleChildParentThatHasOne_Fails()
        {
            using var host = new DesignTestHost();
            string page = N(
                """
                <StackPanel x:Name="root">
                  <Border x:Name="frame">
                    <TextBlock/>
                  </Border>
                </StackPanel>
                """);
            (UIElement root, DesignDocument document) = host.Load(page);

            EditResult result = document.Editor.InsertElement(host.IdOf(DesignTestHost.Named<Border>(root, "frame")), 1, "<TextBlock/>");

            Assert.False(result.Succeeded);
            Assert.Equal(page, document.Text);
        }

        [Fact]
        public void Insert_IntoAnElementWithText_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <StackPanel x:Name="root">
                  <TextBlock x:Name="t">Hello</TextBlock>
                </StackPanel>
                """));

            EditResult result = document.Editor.InsertElement(host.IdOf(DesignTestHost.Named<TextBlock>(root, "t")), 0, "<Border/>");

            Assert.False(result.Succeeded);
        }

        [Fact]
        public void Insert_UnknownType_FailsAndChangesNothing()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            EditResult result = document.Editor.InsertElement(host.IdOf(root), 0, "<Nope/>");

            Assert.False(result.Succeeded);
            Assert.Equal(N(Page), document.Text);
            Assert.Equal(0, document.Version);
            Assert.Equal(2, ((StackPanel)root).Children.Count);
        }

        [Fact]
        public void Insert_MalformedMarkup_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            Assert.False(document.Editor.InsertElement(host.IdOf(root), 0, "<Border>").Succeeded);
            Assert.False(document.Editor.InsertElement(host.IdOf(root), 0, "<Border/><Border/>").Succeeded);
            Assert.Equal(N(Page), document.Text);
        }

        [Fact]
        public void Insert_AnchorsAfterTheNearestMappedSibling()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var panel = (StackPanel)root;
            var runtime = new Border();
            panel.Children.Insert(1, runtime);

            document.Editor.InsertElement(host.IdOf(root), 1, "<TextBlock/>");

            Assert.IsType<TextBlock>(panel.Children[1]);
            Assert.Same(runtime, panel.Children[2]);
        }

        [Fact]
        public void Insert_WithOnlyLeadingRuntimeContent_GoesBeforeTheFirstMappedChild()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var panel = (StackPanel)root;
            var runtime = new Border();
            panel.Children.Insert(0, runtime);

            document.Editor.InsertElement(host.IdOf(root), 0, "<TextBlock/>");

            Assert.Same(runtime, panel.Children[0]);
            Assert.IsType<TextBlock>(panel.Children[1]);
            Assert.Same(DesignTestHost.Named<Border>(root, "a"), panel.Children[2]);
        }

        [Fact]
        public void Insert_IntoAPropertyElement_ChangesTheTextAndNeedsReload()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <Grid x:Name="root">
                  <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                  </Grid.RowDefinitions>
                </Grid>
                """));
            ElementSyntax rows = document.Syntax.Elements.Single(x => x.Name == "Grid.RowDefinitions");

            EditResult result = document.Editor.InsertElement(document.GetNodeId(rows)!.Value, 1, "<RowDefinition Height=\"10\"/>");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Contains("<RowDefinition Height=\"10\"/>", document.Text);
            Assert.True(document.NeedsReload);
            Assert.Single(((Grid)root).RowDefinitions);
        }

        [Fact]
        public void Remove_DeletesTheWholeLineAndDetachesTheElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var b = DesignTestHost.Named<Border>(root, "b");

            EditResult result = document.Editor.RemoveElement(host.IdOf(b));

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="a" Width="10"/>
                </StackPanel>
                """),
                document.Text);
            Assert.Single(((StackPanel)root).Children);
            Assert.Null(b.Parent);
        }

        [Fact]
        public void Remove_TheRoot_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            Assert.False(document.Editor.RemoveElement(host.IdOf(root)).Succeeded);
        }

        [Fact]
        public void Remove_AnElementTheGameAlreadyDetached_StillEditsTheText()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var b = DesignTestHost.Named<Border>(root, "b");
            ((StackPanel)root).Children.Remove(b);

            EditResult result = document.Editor.RemoveElement(host.IdOf(b));

            Assert.True(result.Succeeded, result.ToString());
            Assert.DoesNotContain("x:Name=\"b\"", document.Text);
        }

        [Fact]
        public void Remove_UnregistersNamesInTheSubtree()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            document.Editor.RemoveElement(host.IdOf(DesignTestHost.Named<Border>(root, "b")));

            Assert.Null(MarkupNameScope.GetScope(root)!.Find("b"));
        }

        [Fact]
        public void Move_ReparentsTheSameInstance()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(MovePage));
            var box = DesignTestHost.Named<Border>(root, "box");
            var left = DesignTestHost.Named<StackPanel>(root, "left");
            var right = DesignTestHost.Named<StackPanel>(root, "right");

            EditResult result = document.Editor.MoveElement(host.IdOf(box), host.IdOf(right), 1);

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <StackPanel x:Name="left">
                  </StackPanel>
                  <StackPanel x:Name="right">
                    <TextBlock Text="A"/>
                    <Border x:Name="box" Width="10"/>
                  </StackPanel>
                </StackPanel>
                """),
                document.Text);
            Assert.Empty(left.Children);
            Assert.Same(box, right.Children[1]);
            Assert.Equal("Border", document.GetNode(host.IdOf(box))!.Name);
        }

        [Fact]
        public void Move_WithinTheSameParent_Reorders()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var a = DesignTestHost.Named<Border>(root, "a");
            var b = DesignTestHost.Named<Border>(root, "b");

            document.Editor.MoveElement(host.IdOf(b), host.IdOf(root), 0);

            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="b" Width="20"/>
                  <Border x:Name="a" Width="10"/>
                </StackPanel>
                """),
                document.Text);
            Assert.Equal([b, a], ((StackPanel)root).Children);
        }

        [Fact]
        public void Move_IntoItsOwnDescendant_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(MovePage));

            EditResult result = document.Editor.MoveElement(
                host.IdOf(DesignTestHost.Named<StackPanel>(root, "left")),
                host.IdOf(DesignTestHost.Named<Border>(root, "box")),
                0);

            Assert.False(result.Succeeded);
            Assert.Equal(N(MovePage), document.Text);
        }

        private static string N(string text) => text.ReplaceLineEndings("\n");
    }
}
