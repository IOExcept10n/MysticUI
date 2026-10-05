// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Icy.Design;
using Icy.Design.Text;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Icy.Tests.Design
{
    public class ApplyTextTests(ITestOutputHelper output)
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10" Margin="1"/>
              </StackPanel>
              <StackPanel x:Name="right">
                <TextBlock x:Name="label">Hi</TextBlock>
              </StackPanel>
              <Button>Unnamed</Button>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void AttributeChanges_AreMirroredOnTheSameInstance()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");

            EditResult result = document.ApplyText(Page.Replace("Width=\"10\" Margin=\"1\"", "Width=\"20\" Height=\"5\"", StringComparison.Ordinal));

            Assert.True(result.Succeeded, result.ToString());
            Assert.Same(box, DesignTestHost.Named<Border>(root, "box"));
            Assert.Equal(20f, box.Width);
            Assert.Equal(5f, box.Height);
            Assert.NotEqual(new Thickness(1), box.Margin);
            Assert.True(document.IsInSync);
        }

        [Fact]
        public void InsertsRemovesAndMoves_AreMirroredKeepingNamedInstances()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            var left = DesignTestHost.Named<StackPanel>(root, "left");
            var right = DesignTestHost.Named<StackPanel>(root, "right");

            document.ApplyText(
                """
                <StackPanel x:Name="root">
                  <StackPanel x:Name="left">
                    <Border x:Name="added"/>
                  </StackPanel>
                  <StackPanel x:Name="right">
                    <TextBlock x:Name="label">Hi</TextBlock>
                    <Border x:Name="box" Width="10" Margin="1"/>
                  </StackPanel>
                </StackPanel>
                """.ReplaceLineEndings("\n"));

            Assert.Same(box, right.Children[1]);
            Assert.Same(DesignTestHost.Named<Border>(root, "added"), Assert.Single(left.Children));
            Assert.Equal(2, ((StackPanel)root).Children.Count);
            Assert.DoesNotContain(((StackPanel)root).Children, x => x is Button);
        }

        [Fact]
        public void ChangedElementText_ReplacesTheElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var label = DesignTestHost.Named<TextBlock>(root, "label");
            int replaced = 0;
            document.SubtreeReplaced += (_, _) => replaced++;

            document.ApplyText(Page.Replace(">Hi<", ">Bye<", StringComparison.Ordinal));

            var rebuilt = DesignTestHost.Named<TextBlock>(root, "label");
            Assert.NotSame(label, rebuilt);
            Assert.Equal("Bye", rebuilt.Text);
            Assert.Equal(1, replaced);
        }

        [Fact]
        public void IdenticalText_IsANoOp()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);

            document.ApplyText(Page);

            Assert.Equal(0, document.Version);
            Assert.False(document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void Changed_CarriesTheMinimalChange()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            TextChangeSet? changes = null;
            document.Changed += (_, e) => changes = e.Changes;

            document.ApplyText(Page.Replace("Width=\"10\"", "Width=\"20\"", StringComparison.Ordinal));

            TextChange change = Assert.Single(changes!);
            Assert.Equal("2", change.NewText);
            Assert.Equal(1, change.Span.Length);
            Assert.Equal(document.Text, changes!.Apply(Page));
        }

        [Fact]
        public void ApplyText_IsOneUndoStep()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            string edited = Page.Replace("Width=\"10\"", "Width=\"20\"", StringComparison.Ordinal);
            document.ApplyText(edited);

            Assert.True(document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, box.Width);

            Assert.True(document.Editor.UndoStack.Redo().Succeeded);
            Assert.Equal(edited, document.Text);
            Assert.Equal(20f, box.Width);
        }

        [Fact]
        public void AFormattingOnlySave_KeepsEveryInstance()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            UIElement[] before = [.. new[] { "root", "left", "box", "right", "label" }.Select(x => DesignTestHost.Named<UIElement>(root, x))];
            int replaced = 0;
            document.SubtreeReplaced += (_, _) => replaced++;

            document.ApplyText(Page.Replace("  ", "\t", StringComparison.Ordinal).Replace("\"10\"", "'10'", StringComparison.Ordinal));

            Assert.Equal(before, new[] { "root", "left", "box", "right", "label" }.Select(x => DesignTestHost.Named<UIElement>(root, x)));
            Assert.Equal(0, replaced);
            Assert.True(document.IsInSync);
        }

        [Fact]
        public void IdsSurviveForMatchedAndMovedElements()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));

            document.ApplyText(Page
                .Replace("    <Border x:Name=\"box\" Width=\"10\" Margin=\"1\"/>\n", string.Empty, StringComparison.Ordinal)
                .Replace("<TextBlock x:Name=\"label\">Hi</TextBlock>", "<TextBlock x:Name=\"label\">Hi</TextBlock><Border x:Name=\"box\" Width=\"10\" Margin=\"1\"/>", StringComparison.Ordinal));

            Assert.Equal(box, host.IdOf(DesignTestHost.Named<Border>(root, "box")));
            Assert.Equal("Border", document.GetNode(box)!.Name);
        }

        [Fact]
        public void TheSameFileLoadedTwice_IsUpdatedInBothCopies()
        {
            using var host = new DesignTestHost();
            (UIElement first, DesignDocument document) = host.Load(Page, "same.xml");
            (UIElement second, _) = host.Load(Page, "same.xml");

            document.ApplyText(Page.Replace("Width=\"10\"", "Width=\"30\"", StringComparison.Ordinal));

            Assert.Equal(30f, DesignTestHost.Named<Border>(first, "box").Width);
            Assert.Equal(30f, DesignTestHost.Named<Border>(second, "box").Width);
        }

        [Fact]
        public void ReloadFromSource_AppliesTheResolvedFile()
        {
            using var host = new DesignTestHost();
            string path = Path.Combine(Path.GetTempPath(), $"icy-reload-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, Page);
            try
            {
                (UIElement root, DesignDocument document) = host.Load(Page, path);
                File.WriteAllText(path, Page.Replace("Width=\"10\"", "Width=\"40\"", StringComparison.Ordinal));

                Assert.True(document.ReloadFromSource());
                Assert.Equal(40f, DesignTestHost.Named<Border>(root, "box").Width);
            }
            finally
            {
                File.Delete(path);
            }

            (_, DesignDocument unresolved) = host.Load(Page, "Pages/NotAFile.xml");
            Assert.False(unresolved.ReloadFromSource());
        }

        [Fact]
        public void UndoAcrossVisualAndTextEdits_RestoresTheExactText()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            document.Editor.SetAttribute(box, "Height", "5");
            document.ApplyText(document.Text.Replace("Width=\"10\"", "Width=\"30\"", StringComparison.Ordinal));
            document.Editor.SetAttribute(box, "Width", "40");
            document.Editor.InsertElement(host.IdOf(DesignTestHost.Named<StackPanel>(root, "left")), 0, "<TextBlock/>");
            string final = document.Text;

            while (document.Editor.UndoStack.CanUndo)
                Assert.True(document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, DesignTestHost.Named<Border>(root, "box").Width);

            while (document.Editor.UndoStack.CanRedo)
                Assert.True(document.Editor.UndoStack.Redo().Succeeded);
            Assert.Equal(final, document.Text);
        }

        [Fact]
        public void ARootThatChangedType_StoresTheTextAndNeedsReload()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);

            document.ApplyText("<Border/>");

            Assert.Equal("<Border/>", document.Text);
            Assert.True(document.NeedsReload);
            Assert.False(document.IsInSync);
            Assert.Single(document.LiveErrors);
            Assert.IsType<StackPanel>(root);
        }

        [Fact]
        public void ParseAndDiff_OfA100KbDocument_IsMeasured()
        {
            using var host = new DesignTestHost();
            var builder = new StringBuilder("<StackPanel x:Name=\"root\">\n");
            for (int i = 0; builder.Length < 100_000; i++)
                builder.Append(CultureInfo.InvariantCulture, $"  <Border x:Name=\"b{i}\" Width=\"10\" Height=\"20\" Margin=\"1,2,3,4\" HorizontalAlignment=\"Left\"/>\n");
            builder.Append("</StackPanel>");
            string page = builder.ToString();
            (_, DesignDocument document) = host.Load(page);

            var timings = new List<double>();
            for (int i = 0; i < 7; i++)
            {
                string next = page.Replace("x:Name=\"b0\" Width=\"10\"", $"x:Name=\"b0\" Width=\"{11 + i}\"", StringComparison.Ordinal);
                var watch = Stopwatch.StartNew();
                document.ApplyText(next);
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }

            timings.Sort();
            Assert.True(document.IsInSync);
            output.WriteLine($"{page.Length} chars: ApplyText (parse + diff + one attribute) median {timings[timings.Count / 2]:0.00} ms (budget 5 ms).");
        }
    }
}
