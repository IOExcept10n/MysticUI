// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Editing;
using Icy.Design.Text;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    /// <summary>
    /// Regressions found by the Phase 10.1 whole-branch review.
    /// </summary>
    public class ReviewRegressionTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10"/>
              </StackPanel>
              <StackPanel x:Name="right">
                <TextBlock Text="A"/>
              </StackPanel>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        // C1: a failed edit left the last-parsed text stale, so the next fast set recorded a garbage undo value.
        [Fact]
        public void AFastSetAfterAFailedEdit_UndoesToTheRealOldValue()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            Assert.False(document.Editor.InsertElement(host.IdOf(root), 0, "<NoSuchType/>").Succeeded);

            Assert.True(document.Editor.SetAttribute(host.IdOf(box), "Width", "20").Succeeded);
            EditResult undo = document.Editor.UndoStack.Undo();

            Assert.True(undo.Succeeded, undo.ToString());
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, box.Width);
        }

        // C2: undoing a remove gave the element a new id, so older history entries for it broke.
        [Fact]
        public void SetRemoveUndoUndo_OnTheFastPath_RestoresEverything()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            document.Editor.SetAttribute(box, "Width", "20");
            document.Editor.RemoveElement(box);

            Assert.True(document.Editor.UndoStack.Undo().Succeeded);
            EditResult second = document.Editor.UndoStack.Undo();

            Assert.True(second.Succeeded, second.ToString());
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, DesignTestHost.Named<Border>(root, "box").Width);
        }

        [Fact]
        public void SetRemoveUndoUndo_OnTheNormalPath_KeepsThePageInSync()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            document.Editor.SetAttribute(box, "Height", "5");
            document.Editor.RemoveElement(box);

            document.Editor.UndoStack.Undo();
            document.Editor.UndoStack.Undo();

            Assert.Equal(Page, document.Text);
            Assert.True(float.IsNaN(DesignTestHost.Named<Border>(root, "box").Height));
            Assert.Equal(box, host.IdOf(DesignTestHost.Named<Border>(root, "box")));
        }

        // I1: a failed Set that would add an attribute rebuilt the element in its revert.
        [Fact]
        public void AFailedSetOfANewAttribute_KeepsTheSameInstance()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            int replaced = 0;
            document.SubtreeReplaced += (_, _) => replaced++;

            Assert.False(document.Editor.SetAttribute(host.IdOf(box), "Height", "abc").Succeeded);
            Assert.False(document.Editor.SetAttribute(host.IdOf(root), "Height", "abc").Succeeded);

            Assert.Same(box, DesignTestHost.Named<StackPanel>(root, "left").Children[0]);
            Assert.Equal(0, replaced);
            Assert.False(document.NeedsReload);
        }

        // I2: inside a transaction, a later fast set of the same attribute was dropped after a structural edit.
        [Fact]
        public void ATransactionMixingFastSetsAndAnInsert_UndoesCleanly()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));

            using (document.Editor.BeginTransaction("Mixed"))
            {
                document.Editor.SetAttribute(box, "Width", "20");
                document.Editor.InsertElement(host.IdOf(DesignTestHost.Named<StackPanel>(root, "right")), 0, "<Border/>");
                document.Editor.SetAttribute(box, "Width", "300000000000");
            }

            EditResult undo = document.Editor.UndoStack.Undo();

            Assert.True(undo.Succeeded, undo.ToString());
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, DesignTestHost.Named<Border>(root, "box").Width);
        }

        // I3: moving an only child to where it already is threw from the public API.
        [Fact]
        public void MovingAnOnlyChildToWhereItIs_SucceedsWithoutChanges()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");

            EditResult result = document.Editor.MoveElement(host.IdOf(box), host.IdOf(DesignTestHost.Named<StackPanel>(root, "left")), 0);

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(Page, document.Text);
            Assert.Same(box, DesignTestHost.Named<StackPanel>(root, "left").Children[0]);
        }

        // I4: an undo that failed partway left the entry half-undone and wiped the history.
        [Fact]
        public void AnUndoThatFailsPartway_LeavesTheEntryFullyApplied()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            using (document.Editor.BeginTransaction("Broken"))
            {
                document.Editor.UndoStack.Record(new EditStep(TextChangeSet.Empty, [new FailingAction()], "fails"), "fails");
                document.Editor.SetAttribute(box, "Height", "5");
            }

            string edited = document.Text;

            Assert.False(document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(edited, document.Text);
            Assert.Equal(5f, DesignTestHost.Named<Border>(root, "box").Height);
            Assert.True(document.Editor.UndoStack.CanUndo);
        }

        private sealed class FailingAction : MirrorAction
        {
            public override void Execute(MirrorContext context) => throw new DesignEditException("boom");

            public override void Revert(MirrorContext context)
            {
            }

            public override MirrorAction CreateInverse(MirrorContext context) => this;
        }
    }
}
