// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Mvvm.ComponentModel;
using Icy.Design;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class MarkupEditorAttributeTests
    {
        [Fact]
        public void Set_ReplacesAnExistingValueInPlace()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>"));
            var b = DesignTestHost.Named<Border>(root, "b");

            EditResult result = document.Editor.SetAttribute(host.IdOf(b), "Width", "20");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"20\"/>\n</StackPanel>"), document.Text);
            Assert.Equal(20f, b.Width);
        }

        [Fact]
        public void Set_AppendsANewAttributeOnTheSameLine()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>"));
            var b = DesignTestHost.Named<Border>(root, "b");

            document.Editor.SetAttribute(host.IdOf(b), "Height", "5");

            Assert.Contains("<Border x:Name=\"b\" Width=\"10\" Height=\"5\"/>", document.Text);
            Assert.Equal(5f, b.Height);
        }

        [Fact]
        public void Set_AppendsOnItsOwnLineWhenAttributesAreOnePerLine()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <StackPanel x:Name="root">
                  <Border x:Name="b"
                          Width="10"
                          Height="5"/>
                </StackPanel>
                """));
            var b = DesignTestHost.Named<Border>(root, "b");

            document.Editor.SetAttribute(host.IdOf(b), "Margin", "1");

            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="b"
                          Width="10"
                          Height="5"
                          Margin="1"/>
                </StackPanel>
                """),
                document.Text);
            Assert.Equal(new Thickness(1), b.Margin);
        }

        [Fact]
        public void Set_EscapesForTheExistingQuoteChar()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <TextBlock x:Name=\"t\" Text='a'/>\n</StackPanel>"));
            var t = DesignTestHost.Named<TextBlock>(root, "t");
            const string value = "it's \"q\" <b> & c";

            document.Editor.SetAttribute(host.IdOf(t), "Text", value);

            Assert.Contains("Text='it&apos;s \"q\" &lt;b> &amp; c'", document.Text);
            Assert.Equal(value, t.Text);
        }

        [Fact]
        public void Set_LineBreakSurvivesAsACharacterReference()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <TextBlock x:Name=\"t\" Text=\"a\"/>\n</StackPanel>"));
            var t = DesignTestHost.Named<TextBlock>(root, "t");

            document.Editor.SetAttribute(host.IdOf(t), "Text", "a\nb");

            Assert.Contains("Text=\"a&#10;b\"", document.Text);
            Assert.Equal("a\nb", t.Text);
        }

        [Fact]
        public void Set_InvalidValue_FailsAndLeavesTheTextAndLiveValue()
        {
            using var host = new DesignTestHost();
            string page = N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>");
            (UIElement root, DesignDocument document) = host.Load(page);
            var b = DesignTestHost.Named<Border>(root, "b");

            EditResult result = document.Editor.SetAttribute(host.IdOf(b), "Width", "abc");

            Assert.False(result.Succeeded);
            Assert.Equal(page, document.Text);
            Assert.Equal(10f, b.Width);
        }

        [Fact]
        public void Set_ReplacesABinding()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, Model model) = LoadBound(host);
            var t = DesignTestHost.Named<TextBlock>(root, "t");

            document.Editor.SetAttribute(host.IdOf(t), "Text", "{Binding Path=B}");

            Assert.Equal("b", t.Text);
            Assert.Single(t.Bindings);
            model.A = "changed";
            Assert.Equal("b", t.Text);
        }

        [Fact]
        public void Set_ABindingToALiteral_DisposesTheBinding()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, Model model) = LoadBound(host);
            var t = DesignTestHost.Named<TextBlock>(root, "t");

            document.Editor.SetAttribute(host.IdOf(t), "Text", "x");

            Assert.Empty(t.Bindings);
            model.A = "changed";
            Assert.Equal("x", t.Text);
        }

        [Fact]
        public void Set_AnAttachedProperty()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<Grid x:Name=\"root\">\n  <Border x:Name=\"cell\"/>\n</Grid>"));
            var cell = DesignTestHost.Named<Border>(root, "cell");

            document.Editor.SetAttribute(host.IdOf(cell), "Grid.Row", "1");

            Assert.Contains("<Border x:Name=\"cell\" Grid.Row=\"1\"/>", document.Text);
            Assert.Equal(1, Grid.GetRow(cell));
        }

        [Fact]
        public void Clear_ARegisteredProperty_FallsBackToTheDefault()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>"));
            var b = DesignTestHost.Named<Border>(root, "b");

            document.Editor.ClearAttribute(host.IdOf(b), "Width");

            Assert.Contains("<Border x:Name=\"b\"/>", document.Text);
            Assert.True(float.IsNaN(b.Width));
        }

        [Fact]
        public void Clear_APropertyAStyleAlsoSets_FallsBackToTheStyleValue()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <StackPanel x:Name="root">
                  <StackPanel.Resources>
                    <Style x:Key="s" TargetType="Border" Width="42"/>
                  </StackPanel.Resources>
                  <Border x:Name="b" Style="{StaticResource s}" Width="10"/>
                </StackPanel>
                """));
            var b = DesignTestHost.Named<Border>(root, "b");
            Assert.Equal(10f, b.Width);

            document.Editor.ClearAttribute(host.IdOf(b), "Width");

            Assert.Equal(42f, b.Width);
        }

        [Fact]
        public void Clear_APlainClrProperty_RebuildsTheElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <ClrBox x:Name=\"box\" Caption=\"hi\"/>\n</StackPanel>"));
            var box = DesignTestHost.Named<ClrBox>(root, "box");
            SubtreeReplacedEventArgs? replaced = null;
            document.SubtreeReplaced += (_, e) => replaced = e;

            document.Editor.ClearAttribute(host.IdOf(box), "Caption");

            var rebuilt = Assert.IsType<ClrBox>(Assert.Single(((StackPanel)root).Children));
            Assert.NotSame(box, rebuilt);
            Assert.Null(rebuilt.Caption);
            Assert.Same(box, replaced!.OldElement);
            Assert.Same(rebuilt, replaced.NewElement);
            Assert.Same(rebuilt, DesignTestHost.Named<ClrBox>(root, "box"));
        }

        [Fact]
        public void Set_AConstructorAttribute_RebuildsTheElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Labeled x:Name=\"tag\" label=\"a\"/>\n</StackPanel>"));

            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Labeled>(root, "tag")), "label", "b");

            Assert.Equal("b", Assert.IsType<Labeled>(Assert.Single(((StackPanel)root).Children)).Label);
        }

        [Fact]
        public void Set_AConstructorAttributeOnTheRoot_ChangesTheTextAndNeedsReload()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<Labeled label=\"a\"/>");

            EditResult result = document.Editor.SetAttribute(host.IdOf(root), "label", "b");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal("<Labeled label=\"b\"/>", document.Text);
            Assert.True(document.NeedsReload);
            Assert.Equal("a", ((Labeled)root).Label);
        }

        [Fact]
        public void SetXName_RenamesInPlace()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"a\"/>\n  <Border x:Name=\"b\"/>\n</StackPanel>"));
            var a = DesignTestHost.Named<Border>(root, "a");

            document.Editor.SetAttribute(host.IdOf(a), "x:Name", "c");

            MarkupNameScope names = MarkupNameScope.GetScope(root)!;
            Assert.Same(a, names.Find("c"));
            Assert.Null(names.Find("a"));
            Assert.Equal("c", a.Name);
        }

        [Fact]
        public void SetXName_ToATakenName_Fails()
        {
            using var host = new DesignTestHost();
            string page = N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"a\"/>\n  <Border x:Name=\"b\"/>\n</StackPanel>");
            (UIElement root, DesignDocument document) = host.Load(page);
            var a = DesignTestHost.Named<Border>(root, "a");

            EditResult result = document.Editor.SetAttribute(host.IdOf(a), "x:Name", "b");

            Assert.False(result.Succeeded);
            Assert.Equal(page, document.Text);
            Assert.Same(a, MarkupNameScope.GetScope(root)!.Find("a"));
        }

        [Fact]
        public void Set_InsideAStyle_ChangesTheTextAndNeedsReload()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(N(
                """
                <Border x:Name="root">
                  <Border.Resources>
                    <Style x:Key="s" TargetType="Border" Width="1"/>
                  </Border.Resources>
                </Border>
                """));
            ElementSyntax style = document.Syntax.Elements.Single(x => x.Name == "Style");

            EditResult result = document.Editor.SetAttribute(document.GetNodeId(style)!.Value, "Width", "2");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Contains("Width=\"2\"", document.Text);
            Assert.True(document.NeedsReload);
        }

        [Fact]
        public void Set_MirrorsOntoEveryLoadOfTheSameFile()
        {
            using var host = new DesignTestHost();
            string page = N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>");
            (UIElement first, DesignDocument document) = host.Load(page, "same.xml");
            (UIElement second, _) = host.Load(page, "same.xml");

            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(first, "b")), "Width", "30");

            Assert.Equal(30f, DesignTestHost.Named<Border>(first, "b").Width);
            Assert.Equal(30f, DesignTestHost.Named<Border>(second, "b").Width);
        }

        [Fact]
        public void Set_ANamespaceDeclaration_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<StackPanel/>");

            Assert.False(document.Editor.SetAttribute(host.IdOf(root), "xmlns:z", "z").Succeeded);
        }

        private static (UIElement Root, DesignDocument Document, Model Model) LoadBound(DesignTestHost host)
        {
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <TextBlock x:Name=\"t\" Text=\"{Binding Path=A}\"/>\n</StackPanel>"));
            var model = new Model { A = "a", B = "b" };
            root.DataContext = model;
            return (root, document, model);
        }

        private static string N(string text) => text.ReplaceLineEndings("\n");

        internal sealed class Model : ObservableObject
        {
            private string a = string.Empty;
            private string b = string.Empty;

            public string A
            {
                get => a;
                set => SetProperty(ref a, value);
            }

            public string B
            {
                get => b;
                set => SetProperty(ref b, value);
            }
        }
    }
}
