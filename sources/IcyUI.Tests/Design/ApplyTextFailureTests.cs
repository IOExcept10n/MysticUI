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
    public class ApplyTextFailureTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10"/>
              </StackPanel>
              <StackPanel x:Name="right">
                <TextBlock x:Name="label">Hi</TextBlock>
              </StackPanel>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void MalformedText_KeepsTheLivePagesAndIds_UntilItIsFixed()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            NodeId id = host.IdOf(box);

            document.ApplyText(Page.Replace("Width=\"10\"", "Width=\"20\"", StringComparison.Ordinal)[..^"</StackPanel>".Length]);

            Assert.False(document.IsInSync);
            Assert.Equal(10f, box.Width);
            Assert.Equal("Border", document.GetNode(id)!.Name);

            document.ApplyText(Page.Replace("Width=\"10\"", "Width=\"20\"", StringComparison.Ordinal));

            Assert.True(document.IsInSync);
            Assert.Same(box, DesignTestHost.Named<Border>(root, "box"));
            Assert.Equal(20f, box.Width);
        }

        [Fact]
        public void ALoaderErrorInOneUnit_StillAppliesTheOthers()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);

            ApplyBadWidthAndAnInsertion(document);

            Assert.Equal(10f, DesignTestHost.Named<Border>(root, "box").Width);
            Assert.NotNull(MarkupNameScope.GetScope(root)!.Find("extra"));
            Diagnostic error = Assert.Single(document.LiveErrors);
            Assert.Equal("Width=\"abc\"", document.Text.Substring(error.Span.Start, error.Span.Length));
            Assert.False(document.IsInSync);
        }

        [Fact]
        public void AFailedUnit_HealsOnTheNextValidApply()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            ApplyBadWidthAndAnInsertion(document);

            document.ApplyText(document.Text.Replace("Width=\"abc\"", "Width=\"30\"", StringComparison.Ordinal));

            Assert.True(document.IsInSync);
            Assert.Empty(document.LiveErrors);
            Assert.Equal(30f, DesignTestHost.Named<Border>(root, "box").Width);
        }

        [Fact]
        public void AFailedInsertion_IsInsertedOnceItBuilds()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            string withLate = Page.Replace("<TextBlock x:Name=\"label\">Hi</TextBlock>", "<TextBlock x:Name=\"label\">Hi</TextBlock><Border x:Name=\"late\" Width=\"abc\"/>", StringComparison.Ordinal);

            document.ApplyText(withLate);
            Assert.Null(MarkupNameScope.GetScope(root)!.Find("late"));
            Assert.False(document.IsInSync);

            document.ApplyText(withLate.Replace("Width=\"abc\"", "Width=\"5\"", StringComparison.Ordinal));

            Assert.True(document.IsInSync);
            Assert.Equal(5f, DesignTestHost.Named<Border>(root, "late").Width);
            Assert.Same(DesignTestHost.Named<Border>(root, "late"), DesignTestHost.Named<StackPanel>(root, "right").Children[1]);
        }

        [Fact]
        public void AThrowingSetter_BecomesALiveError()
        {
            using var host = new DesignTestHost();
            const string page = "<StackPanel x:Name=\"root\"><ThrowingBox x:Name=\"t\" Count=\"5\"/></StackPanel>";
            (UIElement root, DesignDocument document) = host.Load(page);

            document.ApplyText(page.Replace("Count=\"5\"", "Count=\"-1\"", StringComparison.Ordinal));

            Assert.Single(document.LiveErrors);
            Assert.Equal(5, DesignTestHost.Named<ThrowingBox>(root, "t").Count);
        }

        [Fact]
        public void VisualEdits_AreRefusedWhileOutOfSync()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            document.ApplyText(Page[..^"</StackPanel>".Length]);

            Assert.False(document.Editor.SetAttribute(box, "Width", "50").Succeeded);
            Assert.False(document.Editor.RemoveElement(box).Succeeded);

            document.ApplyText(Page);
            Assert.True(document.Editor.SetAttribute(box, "Width", "50").Succeeded);
        }

        [Fact]
        public void SyncStateChanged_FiresOnEachTransition()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            int fired = 0;
            document.SyncStateChanged += (_, _) => fired++;

            document.ApplyText(Page[..^"</StackPanel>".Length]);
            document.ApplyText(Page[..^"</StackPanel>".Length] + "  ");
            document.ApplyText(Page);

            Assert.Equal(2, fired);
        }

        [Fact]
        public void UndoThroughAMalformedState()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            document.ApplyText(Page[..^"</StackPanel>".Length]);

            Assert.True(document.Editor.UndoStack.Undo().Succeeded);

            Assert.Equal(Page, document.Text);
            Assert.True(document.IsInSync);
        }

        [Fact]
        public void NamesFollowMovesOutOfRemovedContainers()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");

            document.ApplyText(
                """
                <StackPanel x:Name="root">
                  <Border x:Name="box" Width="10"/>
                  <StackPanel x:Name="right">
                    <TextBlock x:Name="label">Hi</TextBlock>
                  </StackPanel>
                </StackPanel>
                """.ReplaceLineEndings("\n"));

            Assert.Same(box, MarkupNameScope.GetScope(root)!.Find("box"));
            Assert.Same(box, ((StackPanel)root).Children[0]);
            Assert.Null(MarkupNameScope.GetScope(root)!.Find("left"));
        }

        [Fact]
        public void ARootProblem_ClearsWhenTheRootMatchesAgain()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            document.ApplyText("<Border/>");

            document.ApplyText(Page);

            Assert.True(document.IsInSync);
            Assert.Empty(document.LiveErrors);
        }

        private static void ApplyBadWidthAndAnInsertion(DesignDocument document) =>
            document.ApplyText(Page
                .Replace("Width=\"10\"", "Width=\"abc\"", StringComparison.Ordinal)
                .Replace("<TextBlock x:Name=\"label\">Hi</TextBlock>", "<TextBlock x:Name=\"label\">Hi</TextBlock><Border x:Name=\"extra\"/>", StringComparison.Ordinal));
    }
}
