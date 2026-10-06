// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Design.Editor;
using Icy.Design.Editor.Placement;
using Icy.Input.Devices;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    /// <summary>
    /// Regression tests for the findings of the 10.3a whole-branch review.
    /// </summary>
    public class EditorReviewFixTests
    {
        private const string Stack =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <TextBlock x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Border x:Name="frame" Width="120" Height="50">
                <TextBlock x:Name="inner" Text="Inside"/>
              </Border>
            </StackPanel>
            """;

        [Fact]
        public void AResize_InAContainerThatMovesWithIt_DoesntRunAway()
        {
            using var host = new EditorTestHost(
                """
                <Grid x:Name="root">
                  <StackPanel x:Name="s" HorizontalAlignment="Right" VerticalAlignment="Top">
                    <TextBlock x:Name="t" Width="100" Height="20" HorizontalAlignment="Left"/>
                  </StackPanel>
                </Grid>
                """);
            var t = host.Named<TextBlock>("t");
            host.Session.Select(t);
            Point start = host.At(t, 100, 10);
            Point held = start with { X = start.X + 10 };

            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            for (int i = 0; i < 3; i++)
            {
                resize.Update(held);
                host.Render();
            }

            Assert.Equal(110f, t.Width);
        }

        [Fact]
        public void UndoDuringAResize_IsRefusedWithoutThrowing()
        {
            using var host = new EditorTestHost(Stack);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            host.Session.Nudge(1, 0);
            Point start = host.At(a, 100, 10);

            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            resize.Update(start with { X = start.X + 10 });

            Assert.False(host.Session.Commands.Undo.CanExecute(null));
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Z, ModifierKeys.Ctrl));

            Assert.True(resize.Complete().Succeeded);
            Assert.True(host.Session.Commands.Undo.CanExecute(null));
        }

        [Fact]
        public void EditsDuringAResize_AreRefused()
        {
            using var host = new EditorTestHost(Stack);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            Point start = host.At(a, 100, 10);

            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            resize.Update(start with { X = start.X + 10 });

            Assert.False(host.Session.Nudge(1, 0).Succeeded);
            Assert.False(host.Session.DeleteSelection().Succeeded);
            Assert.False(host.Session.Commands.Delete.CanExecute(null));
            Assert.False(host.Session.Commands.NudgeLeft.CanExecute(null));
        }

        [Fact]
        public void DisposingDuringAResize_CancelsIt_AndUndoKeepsWorking()
        {
            using var host = new EditorTestHost(Stack);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            Point start = host.At(a, 100, 10);
            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            resize.Update(start with { X = start.X + 10 });

            host.Session.Dispose();

            Assert.Equal(100f, a.Width);
            Assert.True(host.Document.Editor.SetAttribute(host.IdOf(a), "Height", "30").Succeeded);
            Assert.True(host.Document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(20f, a.Height);
        }

        [Fact]
        public void LeavingEditMode_CancelsAResize()
        {
            using var host = new EditorTestHost(Stack);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            Point start = host.At(a, 100, 10);
            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Right, start)!;
            resize.Update(start with { X = start.X + 10 });

            host.Session.Mode = EditorMode.Interact;

            Assert.Equal(100f, a.Width);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void AResizeThatReturnsToItsStart_ChangesNothing()
        {
            using var host = new EditorTestHost(Stack);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);
            string before = host.Document.Text;
            Point start = host.At(a, 0, 10);

            ResizeGesture resize = host.Session.BeginResize(ResizeHandle.Left, start)!;
            resize.Update(start with { X = start.X + 20 });
            host.Render();
            resize.Update(start);
            resize.Complete();

            Assert.Equal(before, host.Document.Text);
        }

        [Fact]
        public void MovingInsideAGrid_KeepsTheMarkupOrder()
        {
            using var host = new EditorTestHost(
                """
                <Grid x:Name="root">
                  <Grid x:Name="g" Width="300" Height="200" HorizontalAlignment="Left" VerticalAlignment="Top">
                    <TextBlock x:Name="a" Width="50" Height="20" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="0,100,0,0"/>
                    <TextBlock x:Name="b" Width="50" Height="20" HorizontalAlignment="Left" VerticalAlignment="Top"/>
                  </Grid>
                </Grid>
                """);
            var a = host.Named<TextBlock>("a");
            host.Session.Select(a);

            MoveGesture move = host.Session.BeginMove(host.At(a, 5, 5))!;
            move.Update(host.At(a, 25, 5));
            Assert.True(move.Complete().Succeeded);

            string text = host.Document.Text;
            Assert.True(text.IndexOf("x:Name=\"a\"", StringComparison.Ordinal) < text.IndexOf("x:Name=\"b\"", StringComparison.Ordinal));
        }

        [Fact]
        public void MovingBetweenGrids_KeepsTheSpan()
        {
            using var host = new EditorTestHost(
                """
                <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
                  <Grid x:Name="g1" Width="300" Height="50">
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width="100"/>
                      <ColumnDefinition Width="100"/>
                      <ColumnDefinition Width="100"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock x:Name="w" Grid.ColumnSpan="2" Height="20"/>
                  </Grid>
                  <Grid x:Name="g2" Width="300" Height="50">
                    <Grid.ColumnDefinitions>
                      <ColumnDefinition Width="100"/>
                      <ColumnDefinition Width="100"/>
                      <ColumnDefinition Width="100"/>
                    </Grid.ColumnDefinitions>
                  </Grid>
                </StackPanel>
                """);
            var w = host.Named<TextBlock>("w");
            host.Session.Select(w);

            MoveGesture move = host.Session.BeginMove(host.At(w, 5, 5))!;
            move.Update(host.At(host.Named<Grid>("g2"), 5, 5));
            Assert.Same(host.Named<Grid>("g2"), move.TargetContainer);
            Assert.True(move.Complete().Succeeded);

            Assert.Contains("<TextBlock x:Name=\"w\" Height=\"20\" Grid.ColumnSpan=\"2\"/>", host.Document.Text, StringComparison.Ordinal);
        }

        [Fact]
        public void TheSelection_FollowsARebuiltAncestor()
        {
            using var host = new EditorTestHost(
                """
                <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
                  <ClrPanel x:Name="frame" Caption="hi">
                    <TextBlock x:Name="inner" Text="Inside"/>
                  </ClrPanel>
                </StackPanel>
                """);
            var inner = host.Named<TextBlock>("inner");
            host.Session.Select(inner);

            // Clearing a plain CLR property rebuilds the panel's whole subtree; the TextBlock keeps its id.
            host.Document.Editor.ClearAttribute(host.IdOf(host.Named<ClrPanel>("frame")), "Caption");

            Assert.NotSame(inner, host.Session.Selection!.Instance);
            Assert.Same(host.Named<TextBlock>("inner"), host.Session.Selection.Instance);
            Assert.NotNull(host.Session.BeginMove(host.At(host.Named<TextBlock>("inner"))));
        }
    }
}
