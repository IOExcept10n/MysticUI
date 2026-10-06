// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Design;
using Icy.Design.Editor;
using Icy.Design.Editor.Placement;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorGestureTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <TextBlock x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
              <TextBlock x:Name="b" Width="100" Height="20" HorizontalAlignment="Left"/>
              <TextBlock x:Name="c" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Grid x:Name="grid" Width="200" Height="100">
                <Grid.ColumnDefinitions>
                  <ColumnDefinition Width="100"/>
                  <ColumnDefinition Width="100"/>
                </Grid.ColumnDefinitions>
                <TextBlock x:Name="cell" Grid.Column="1"/>
              </Grid>
            </StackPanel>
            """;

        [Fact]
        public void AMove_ToAnotherGap_IsOneUndoStepWithAMinimalChange()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            string before = host.Document.Text;

            MoveGesture move = host.Session.BeginMove(host.At(a, 5, 5))!;
            move.Update(host.At(host.Named<TextBlock>("c"), 5, 15));
            Assert.Equal(2, move.Target!.Index);
            Assert.True(move.Complete().Succeeded);

            var root = host.Named<StackPanel>("root");
            Assert.Same(a, root.Children[2]);
            Assert.True(host.Document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(before, host.Document.Text);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void AMove_IntoAGridCell_WritesTheCell()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<TextBlock>("a");
            var grid = host.Named<Grid>("grid");
            host.Session.Select(a);

            MoveGesture move = host.Session.BeginMove(host.At(a, 1, 1))!;
            move.Update(host.At(grid, 150, 10));
            Assert.Same(grid, move.TargetContainer);
            move.Complete();

            Assert.Contains("<TextBlock x:Name=\"a\" Width=\"100\" Height=\"20\" HorizontalAlignment=\"Left\" Grid.Column=\"1\"/>", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void MovingOutOfAGrid_DropsTheGridAttributes()
        {
            using var host = new EditorTestHost(Page);
            var cell = host.Named<TextBlock>("cell");
            host.Session.Select(cell);

            MoveGesture move = host.Session.BeginMove(host.At(cell, 1, 1))!;
            move.Update(host.At(host.Named<TextBlock>("a"), 1, 1));
            move.Complete();

            Assert.Contains("<TextBlock x:Name=\"cell\"/>", host.Document.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Grid.Column=\"", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void AMove_NeverDropsAnElementIntoItself()
        {
            using var host = new EditorTestHost(Page);
            var grid = host.Named<Grid>("grid");
            host.Session.Select(grid);

            MoveGesture move = host.Session.BeginMove(host.At(grid, 5, 5))!;
            move.Update(host.At(host.Named<TextBlock>("cell"), 5, 5));

            Assert.NotSame(grid, move.TargetContainer);
            Assert.NotSame(host.Named<TextBlock>("cell"), move.TargetContainer);
        }

        [Fact]
        public void ACancelledMove_ChangesNothing()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            string before = host.Document.Text;

            MoveGesture move = host.Session.BeginMove(host.At(a, 1, 1))!;
            move.Update(host.At(host.Named<TextBlock>("c"), 1, 15));
            move.Cancel();

            Assert.Equal(before, host.Document.Text);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void AResize_FollowsThePointerLive_AndIsOneUndoStep()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            Point start = host.At(a, 100, 10);

            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            resize.Update(start with { X = start.X + 10 });
            Assert.Equal(110f, a.Width);
            resize.Update(start with { X = start.X + 25 });
            Assert.Equal(125f, a.Width);
            Assert.True(resize.Complete().Succeeded);

            Assert.True(host.Document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(100f, a.Width);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void ACancelledResize_PutsTheSizeBack()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            string before = host.Document.Text;
            Point start = host.At(a, 100, 10);

            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            resize.Update(start with { X = start.X + 30 });
            resize.Cancel();

            Assert.Equal(100f, a.Width);
            Assert.Equal(before, host.Document.Text);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void Nudge_WritesTheMargin()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<TextBlock>("a"));

            Assert.True(host.Session.Nudge(3, 0).Succeeded);
            Assert.True(host.Session.Nudge(2, 1).Succeeded);

            Assert.Contains("Margin=\"5,1,0,0\"", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void DeleteSelection_RemovesTheElement_ButNeverTheRoot()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<TextBlock>("b"));
            Assert.True(host.Session.DeleteSelection().Succeeded);
            Assert.DoesNotContain("x:Name=\"b\"", host.Document.Text, StringComparison.Ordinal);
            Assert.Null(host.Session.Selection);

            host.Session.Select(host.Root);
            Assert.False(host.Session.DeleteSelection().Succeeded);
        }

        [Fact]
        public void TheRoot_CanBeResizedButNotMoved()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Root);

            Assert.Null(host.Session.BeginMove(host.At(host.Root)));
            Assert.NotNull(host.Session.BeginResize(ResizeHandle.Right, host.At(host.Root)));
        }

        [Fact]
        public void Gestures_AreRefusedWhileBlocked()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<TextBlock>("a"));
            host.Document.ApplyText(host.Document.Text[..^"</StackPanel>".Length]);

            Assert.Null(host.Session.BeginMove(host.At(host.Named<TextBlock>("a"))));
            Assert.Null(host.Session.BeginResize(ResizeHandle.Right, host.At(host.Named<TextBlock>("a"))));
            Assert.False(host.Session.Nudge(1, 0).Succeeded);
        }

        [Fact]
        public void AFailingEditInsideAGesture_RollsTheWholeGestureBack()
        {
            using var host = new EditorTestHost(Page);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            string before = host.Document.Text;
            var failing = new FailingPlacement();
            host.Session.Placement.Register<Grid>(failing);

            MoveGesture move = host.Session.BeginMove(host.At(a, 1, 1))!;
            move.Update(host.At(host.Named<Grid>("grid"), 150, 10));
            EditResult result = move.Complete();

            Assert.False(result.Succeeded);
            Assert.Equal(before, host.Document.Text);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void LastEdited_IsTheDocumentOfTheLastGesture()
        {
            using var host = new EditorTestHost(Page);
            host.Session.Select(host.Named<TextBlock>("a"));
            host.Session.Nudge(1, 0);
            host.Session.Clear();

            Assert.Same(host.Document, host.Session.LastEdited);
        }

        [Theory]
        [InlineData(ResizeHandle.Right, 20, 0)]
        [InlineData(ResizeHandle.Left, -20, 0)]
        [InlineData(ResizeHandle.Bottom, 0, 10)]
        [InlineData(ResizeHandle.Top, 0, -10)]
        public void ASizelessStackChild_GrowsWithTheDrag_AndTheStackGrowsWithIt(ResizeHandle handle, int dx, int dy)
        {
            using var host = new EditorTestHost(SizelessStack);
            host.Render();
            var one = host.Named<UIElement>("one");
            var stack = host.Named<StackPanel>("stack");
            RectangleF before = one.ActualBounds;
            RectangleF stackBefore = stack.ActualBounds;
            host.Session.Select(one);
            Point start = host.At(one, 2, 2);

            ResizeGesture resize = host.Session.BeginResize(handle, start)!;
            resize.Update(new Point(start.X + dx, start.Y + dy));
            Assert.True(resize.Complete().Succeeded);
            host.Render();

            Assert.Equal(before.Width + Math.Abs(dx), one.ActualBounds.Width);
            Assert.Equal(before.Height + Math.Abs(dy), one.ActualBounds.Height);
            Assert.Equal(stackBefore.Width + Math.Abs(dx), stack.ActualBounds.Width);
            Assert.Equal(stackBefore.Height + Math.Abs(dy), stack.ActualBounds.Height);
            Assert.Contains("<Button x:Name=\"one\" Padding=\"12,6\" Margin=\"0,0,8,0\"", host.Document.Text, StringComparison.Ordinal);
        }

        private const string SizelessStack =
            """
            <StackPanel HorizontalAlignment="Left" VerticalAlignment="Top">
              <StackPanel x:Name="stack" Orientation="Horizontal">
                <Button x:Name="one" Padding="12,6" Margin="0,0,8,0">One</Button>
                <Button Padding="12,6">Two</Button>
              </StackPanel>
            </StackPanel>
            """;

        /// <summary>
        /// A grid placement whose drop writes an attribute that isn't valid, so the move succeeds and the edit fails.
        /// </summary>
        private sealed class FailingPlacement : MarginPlacement
        {
            public override PlacementTarget? GetDropTarget(PlacementContext context, System.Numerics.Vector2 point) =>
                new(context.ContentCount, [new AttributeEdit("not a name", "1")], RectangleF.Empty, IndicatorIsLine: false);
        }
    }
}
