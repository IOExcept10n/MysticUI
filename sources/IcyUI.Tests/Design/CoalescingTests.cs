// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Syntax;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class CoalescingTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <Border x:Name="a" Width="10" Height="10"/>
              <Border x:Name="b" Width="10"/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void RepeatedSets_WithinTheWindow_AreOneUndoStep()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, FakeClock clock) = Load(host);
            var a = DesignTestHost.Named<Border>(root, "a");
            NodeId id = host.IdOf(a);

            foreach (string width in new[] { "11", "12", "13" })
            {
                Assert.True(document.Editor.SetAttribute(id, "Width", width).Succeeded);
                clock.Advance(TimeSpan.FromMilliseconds(16));
            }

            Assert.Contains("x:Name=\"a\" Width=\"13\"", document.Text);
            Assert.Equal(13f, a.Width);

            document.Editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, a.Width);
            Assert.False(document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void Sets_FurtherApartThanTheWindow_AreSeparateSteps()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, FakeClock clock) = Load(host);
            NodeId id = host.IdOf(DesignTestHost.Named<Border>(root, "a"));

            document.Editor.SetAttribute(id, "Width", "11");
            clock.Advance(TimeSpan.FromSeconds(1));
            document.Editor.SetAttribute(id, "Width", "12");

            document.Editor.UndoStack.Undo();
            Assert.Contains("x:Name=\"a\" Width=\"11\"", document.Text);
        }

        [Fact]
        public void FastSets_DeferTheReparseUntilTheTreeIsNeeded()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            NodeId id = host.IdOf(DesignTestHost.Named<Border>(root, "a"));
            int parses = document.ParseCount;

            for (int i = 0; i < 20; i++)
                document.Editor.SetAttribute(id, "Width", (20 + i).ToString(System.Globalization.CultureInfo.InvariantCulture));

            Assert.Equal(parses, document.ParseCount);
            AttributeSyntax width = document.Syntax.Elements.Single(x => x.FindAttribute("x:Name")?.Value == "a").FindAttribute("Width")!;
            Assert.Equal(parses + 1, document.ParseCount);
            Assert.Equal("39", document.Text.Substring(width.ValueSpan.Start, width.ValueSpan.Length));
            Assert.Equal(id, document.GetNodeId(width.Parent!));
        }

        [Fact]
        public void FastSet_InvalidValue_FailsAndKeepsTheLiveValue()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            var a = DesignTestHost.Named<Border>(root, "a");

            EditResult result = document.Editor.SetAttribute(host.IdOf(a), "Width", "abc");

            Assert.False(result.Succeeded);
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, a.Width);
        }

        [Fact]
        public void FastSets_OnAnotherElement_FlushTheFirstOnesPendingValues()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);

            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "a")), "Width", "123");
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "b")), "Width", "456");

            Assert.Contains("x:Name=\"a\" Width=\"123\"", document.Text);
            Assert.Contains("x:Name=\"b\" Width=\"456\"", document.Text);
            Assert.Empty(document.Syntax.Diagnostics);
        }

        [Fact]
        public void AStructuralEdit_AfterFastSets_SeesTheirText()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "a")), "Width", "123");

            document.Editor.InsertElement(host.IdOf(root), 2, "<TextBlock/>");

            Assert.Contains("x:Name=\"a\" Width=\"123\"", document.Text);
            Assert.Contains("<TextBlock/>", document.Text);
            document.Editor.UndoStack.Undo();
            document.Editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
        }

        [Fact]
        public void ATransactionDraggingTwoAttributes_IsOneUndoStepRestoringBoth()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            var a = DesignTestHost.Named<Border>(root, "a");
            NodeId id = host.IdOf(a);

            using (document.Editor.BeginTransaction("Resize"))
            {
                for (int i = 1; i <= 10; i++)
                {
                    document.Editor.SetAttribute(id, "Width", (10 + i).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    document.Editor.SetAttribute(id, "Height", (10 + (2 * i)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            Assert.Equal((20f, 30f), (a.Width, a.Height));
            document.Editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
            Assert.Equal((10f, 10f), (a.Width, a.Height));
            Assert.False(document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void ChangedEvents_ReplayToTheSameText()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            string mirror = document.Text;
            document.Changed += (_, e) => mirror = e.Changes.Apply(mirror);
            NodeId a = host.IdOf(DesignTestHost.Named<Border>(root, "a"));

            document.Editor.SetAttribute(a, "Width", "1");
            document.Editor.SetAttribute(a, "Height", "22222");
            document.Editor.SetAttribute(a, "Width", "333");
            document.Editor.InsertElement(host.IdOf(root), 0, "<TextBlock/>");
            document.Editor.UndoStack.Undo();

            Assert.Equal(document.Text, mirror);
        }

        [Fact]
        public void BindingValues_TakeTheNormalPath()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<TextBlock Text=\"x\"/>");
            int parses = document.ParseCount;

            document.Editor.SetAttribute(host.IdOf(root), "Text", "{Binding Path=A}");

            Assert.Equal(parses + 1, document.ParseCount);
        }

        private static (UIElement Root, DesignDocument Document, FakeClock Clock) Load(DesignTestHost host)
        {
            (UIElement root, DesignDocument document) = host.Load(Page);
            var clock = new FakeClock();
            document.Editor.UndoStack.Clock = clock;
            return (root, document, clock);
        }

        private sealed class FakeClock : TimeProvider
        {
            private DateTimeOffset now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => now;

            public void Advance(TimeSpan by) => now += by;
        }
    }
}
