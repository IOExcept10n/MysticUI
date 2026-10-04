// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Icy.Design;
using Icy.Design.Editing;
using Icy.Design.Text;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Icy.Tests.Design
{
    public class DesignLifetimeTests(ITestOutputHelper output)
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <Border x:Name="a" Width="10"/>
              <Border x:Name="b" Width="20"/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void Save_WritesTheTextToTheResolvedFile()
        {
            using var host = new DesignTestHost();
            string path = Path.Combine(Path.GetTempPath(), $"icy-design-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, Page);
            try
            {
                (UIElement root, DesignDocument document) = host.Load(File.ReadAllText(path), path);
                document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "a")), "Width", "99");

                document.Save();

                Assert.Equal(document.Text, File.ReadAllText(path));
                Assert.Contains("Width=\"99\"", File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Save_WithAPathThatIsNoFile_Throws()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page, "Pages/NotAFile.xml");

            Assert.Throws<InvalidOperationException>(document.Save);
        }

        [Fact]
        public void SaveAs_WritesUtf8WithoutABom()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            string path = Path.Combine(Path.GetTempPath(), $"icy-design-{Guid.NewGuid():N}.xml");
            try
            {
                document.SaveAs(path);

                byte[] bytes = File.ReadAllBytes(path);
                Assert.Equal(Encoding.UTF8.GetBytes(Page), bytes);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Editor_AfterSessionDispose_Throws()
        {
            var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId a = host.IdOf(DesignTestHost.Named<Border>(root, "a"));

            host.Dispose();

            Assert.Throws<ObjectDisposedException>(() => document.Editor.SetAttribute(a, "Width", "1"));
            Assert.Throws<ObjectDisposedException>(() => document.Editor.InsertElement(a, 0, "<Border/>"));
            Assert.Throws<ObjectDisposedException>(() => document.Editor.UndoStack.Undo());
        }

        [Fact]
        public void AnElementRemovedThroughTheEditor_IsCollectable()
        {
            using var host = new DesignTestHost();
            WeakReference removed = RemoveThroughEditor(host);

            CollectEverything();

            Assert.False(removed.IsAlive);
        }

        [Fact]
        public void AnElementTheGameRemoved_IsCollectable()
        {
            using var host = new DesignTestHost();
            WeakReference removed = RemoveThroughGame(host);

            CollectEverything();

            Assert.False(removed.IsAlive);
        }

        [Fact]
        public void ACollectedPage_IsDroppedFromTheDocuments()
        {
            using var host = new DesignTestHost();
            LoadAndForget(host);

            CollectEverything();

            Assert.Empty(host.Session.Documents);
        }

        [Fact]
        public void PooledTemplateItems_AddNothingToTheMap()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(
                """
                <StackPanel x:Name="root">
                  <ItemsControl x:Name="list">
                    <ItemsControl.ItemTemplate>
                      <DataTemplate><Border Height="20"/></DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </StackPanel>
                """.ReplaceLineEndings("\n"));
            var list = DesignTestHost.Named<ItemsControl>(root, "list");
            int tracked = document.TrackedObjectCount;
            int documents = host.Session.Documents.Count;

            for (int i = 0; i < 50; i++)
                list.ItemTemplate!.Build(new object());

            Assert.Equal(tracked, document.TrackedObjectCount);
            Assert.Equal(documents, host.Session.Documents.Count);
        }

        [Fact]
        public void ParseAndMatch_OfA100KbDocument_IsMeasured()
        {
            using var host = new DesignTestHost();
            var builder = new StringBuilder("<StackPanel x:Name=\"root\">\n");
            for (int i = 0; builder.Length < 100_000; i++)
                builder.Append($"  <Border x:Name=\"b{i}\" Width=\"10\" Height=\"20\" Margin=\"1,2,3,4\" HorizontalAlignment=\"Left\"/>\n");
            builder.Append("</StackPanel>");
            (UIElement root, DesignDocument document) = host.Load(builder.ToString());
            Assert.Empty(document.Syntax.Diagnostics);

            var timings = new List<double>();
            for (int i = 0; i < 7; i++)
            {
                var width = document.Syntax.Root!.Elements.First().FindAttribute("Width")!;
                var step = new EditStep(new TextChangeSet([new TextChange(width.ValueSpan, (11 + i).ToString(System.Globalization.CultureInfo.InvariantCulture))]), [], "measure");
                var watch = Stopwatch.StartNew();
                document.Apply(step, out _);
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }

            timings.Sort();
            var fast = Stopwatch.StartNew();
            NodeId first = host.IdOf(((StackPanel)root).Children[0]);
            for (int i = 0; i < 60; i++)
                document.Editor.SetAttribute(first, "Width", (30 + i).ToString(System.Globalization.CultureInfo.InvariantCulture));
            double perFrame = fast.Elapsed.TotalMilliseconds / 60;

            output.WriteLine($"{document.Text.Length} chars: parse + match median {timings[timings.Count / 2]:0.00} ms (budget 5 ms); fast-path set {perFrame:0.000} ms per frame.");
        }

        private static void CollectEverything()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RemoveThroughEditor(DesignTestHost host)
        {
            (UIElement root, DesignDocument document) = host.Load(Page);
            var b = DesignTestHost.Named<Border>(root, "b");
            document.Editor.RemoveElement(host.IdOf(b));
            return new WeakReference(b);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RemoveThroughGame(DesignTestHost host)
        {
            (UIElement root, _) = host.Load(Page);
            var b = DesignTestHost.Named<Border>(root, "b");
            ((StackPanel)root).Children.Remove(b);
            Icy.Markup.MarkupNameScope.GetScope(root)!.Unregister("b");
            return new WeakReference(b);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LoadAndForget(DesignTestHost host) => host.Load(Page);
    }
}
