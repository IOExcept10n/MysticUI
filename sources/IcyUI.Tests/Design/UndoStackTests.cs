// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class UndoStackTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10"/>
              </StackPanel>
              <StackPanel x:Name="right"/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void UndoAndRedo_OfASetAttribute()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            MarkupEditor editor = document.Editor;
            editor.SetAttribute(host.IdOf(box), "Width", "{}20");
            string edited = document.Text;

            Assert.True(editor.UndoStack.Undo().Succeeded);
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, box.Width);

            Assert.True(editor.UndoStack.Redo().Succeeded);
            Assert.Equal(edited, document.Text);
            Assert.Equal(20f, box.Width);
        }

        [Fact]
        public void UndoAndRedo_OfAnInsert()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var right = DesignTestHost.Named<StackPanel>(root, "right");
            document.Editor.InsertElement(host.IdOf(right), 0, "<TextBlock Text=\"new\"/>");

            document.Editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
            Assert.Empty(right.Children);

            document.Editor.UndoStack.Redo();
            Assert.Equal("new", Assert.IsType<TextBlock>(Assert.Single(right.Children)).Text);
        }

        [Fact]
        public void Undo_OfARemove_RestoresTheTextExactlyAndAFreshElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var left = DesignTestHost.Named<StackPanel>(root, "left");
            document.Editor.RemoveElement(host.IdOf(DesignTestHost.Named<Border>(root, "box")));

            document.Editor.UndoStack.Undo();

            Assert.Equal(Page, document.Text);
            var restored = Assert.IsType<Border>(Assert.Single(left.Children));
            Assert.Equal(10f, restored.Width);
            Assert.Same(restored, MarkupNameScope.GetScope(root)!.Find("box"));
        }

        [Fact]
        public void Undo_OfAMove_PutsTheSameInstanceBack()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            var left = DesignTestHost.Named<StackPanel>(root, "left");
            document.Editor.MoveElement(host.IdOf(box), host.IdOf(DesignTestHost.Named<StackPanel>(root, "right")), 0);

            document.Editor.UndoStack.Undo();

            Assert.Equal(Page, document.Text);
            Assert.Same(box, Assert.Single(left.Children));
        }

        [Fact]
        public void UndoingEverything_RestoresTheTextByteForByte_AndRedoingEverythingRestoresTheResult()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            MarkupEditor editor = document.Editor;
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            NodeId right = host.IdOf(DesignTestHost.Named<StackPanel>(root, "right"));
            editor.SetAttribute(box, "Height", "5");
            editor.InsertElement(right, 0, "<TextBlock/>");
            editor.MoveElement(box, right, 1);
            editor.ClearAttribute(box, "Width");
            string final = document.Text;

            while (editor.UndoStack.CanUndo)
                Assert.True(editor.UndoStack.Undo().Succeeded);
            Assert.Equal(Page, document.Text);

            while (editor.UndoStack.CanRedo)
                Assert.True(editor.UndoStack.Redo().Succeeded);
            Assert.Equal(final, document.Text);
        }

        [Fact]
        public void Transaction_IsOneUndoStep()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            MarkupEditor editor = document.Editor;
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));

            using (editor.BeginTransaction("Resize"))
            {
                editor.SetAttribute(box, "Height", "5");
                editor.SetAttribute(box, "Margin", "2");
            }

            Assert.Equal("Resize", editor.UndoStack.UndoDescription);
            editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
            Assert.False(editor.UndoStack.CanUndo);
        }

        [Fact]
        public void NewEdit_ClearsRedo()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            document.Editor.SetAttribute(box, "Height", "5");
            document.Editor.UndoStack.Undo();

            document.Editor.SetAttribute(box, "Margin", "2");

            Assert.False(document.Editor.UndoStack.CanRedo);
        }

        [Fact]
        public void Undo_OfABindingChange_RestoresTheOriginalBinding()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<TextBlock Text=\"{Binding Path=A}\"/>");
            var model = new MarkupEditorAttributeTests.Model { A = "a", B = "b" };
            root.DataContext = model;
            document.Editor.SetAttribute(host.IdOf(root), "Text", "{Binding Path=B}");

            document.Editor.UndoStack.Undo();

            var block = (TextBlock)root;
            Assert.Equal("a", block.Text);
            Assert.Single(block.Bindings);
            model.A = "again";
            Assert.Equal("again", block.Text);
        }

        [Fact]
        public void Undo_InsideATransaction_Throws()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);

            using (document.Editor.BeginTransaction("open"))
                Assert.Throws<InvalidOperationException>(() => document.Editor.UndoStack.Undo());
        }

        [Fact]
        public void FailedEdit_RecordsNothing()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);

            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "box")), "Width", "abc");

            Assert.False(document.Editor.UndoStack.CanUndo);
        }
    }
}
