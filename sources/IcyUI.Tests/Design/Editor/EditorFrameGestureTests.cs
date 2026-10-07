// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Design.Editor;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorFrameGestureTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="100,100,0,0">
              <Border x:Name="a" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Border x:Name="b" Width="100" Height="20" HorizontalAlignment="Left"/>
              <Button x:Name="ok" Width="100" Height="30">OK</Button>
            </StackPanel>
            """;

        [Fact]
        public void ATap_SelectsTheElement_AndATapOnNothingClearsIt()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var ok = host.Named<Button>("ok");

            host.Input.Events.Touch.RaiseTap(new TouchInfo(host.At(ok, 50, 15), 1));
            Assert.Same(ok, frame.Session.Selection!.Instance);

            host.Input.Events.Touch.RaiseTap(new TouchInfo(new Point(700, 500), 1));
            Assert.Null(frame.Session.Selection);
        }

        [Fact]
        public void AToolbarClick_DoesntChangeTheSelection()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            frame.Session.Select(host.Named<Border>("a"));

            host.Input.Events.Touch.RaiseTap(new TouchInfo(frame.Toolbar.PointToScreen(new Vector2(20, 10)), 1));

            Assert.Same(host.Named<Border>("a"), frame.Session.Selection!.Instance);
        }

        [Fact]
        public void ABodyDrag_MovesTheElement()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var a = host.Named<Border>("a");
            Point from = host.At(a, 50, 10);
            Point to = host.At(host.Named<Button>("ok"), 50, 25);

            host.Input.Events.Gestures.RaiseDragStarted(Drag(from, from));
            host.Input.Events.Gestures.RaiseDragMoved(Drag(from, to));
            Assert.NotNull(frame.BuildScene().Indicator);
            Assert.NotNull(frame.BuildScene().Ghost);
            host.Input.Events.Gestures.RaiseDragCompleted(Drag(from, to));

            Assert.Same(a, host.Named<StackPanel>("root").Children[^1]);
            Assert.Same(a, frame.Session.Selection!.Instance);
        }

        [Fact]
        public void AMiddleButtonDrag_EditsNothing()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var a = host.Named<Border>("a");
            string before = host.Document.Text;
            Point from = host.At(a, 50, 10);
            Point to = host.At(host.Named<Button>("ok"), 50, 25);
            var start = new DragInfo(PointerKind.MouseMiddle, from, from, Vector2.Zero, Vector2.Zero);
            var end = new DragInfo(PointerKind.MouseMiddle, from, to, new Vector2(to.X - from.X, to.Y - from.Y), Vector2.Zero);

            host.Input.Events.Gestures.RaiseDragStarted(start);
            host.Input.Events.Gestures.RaiseDragMoved(end);
            host.Input.Events.Gestures.RaiseDragCompleted(end);

            Assert.Equal(before, host.Document.Text);
        }

        [Fact]
        public void AHandleDrag_ResizesTheElement()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var a = host.Named<Border>("a");
            frame.Session.Select(a);
            Point handle = host.At(a, 100, 10);
            Point to = handle with { X = handle.X + 30 };

            host.Input.Events.Gestures.RaiseDragStarted(Drag(handle, handle));
            host.Input.Events.Gestures.RaiseDragMoved(Drag(handle, to));
            host.Input.Events.Gestures.RaiseDragCompleted(Drag(handle, to));

            Assert.Equal(130f, a.Width);
            Assert.True(host.Document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(100f, a.Width);
        }

        [Fact]
        public void ACancelledDrag_RollsTheResizeBack()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var a = host.Named<Border>("a");
            frame.Session.Select(a);
            Point handle = host.At(a, 100, 10);
            Point to = handle with { X = handle.X + 30 };

            host.Input.Events.Gestures.RaiseDragStarted(Drag(handle, handle));
            host.Input.Events.Gestures.RaiseDragMoved(Drag(handle, to));
            host.Input.Events.Gestures.RaiseDragCanceled(Drag(handle, to));

            Assert.Equal(100f, a.Width);
            Assert.False(host.Document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void TheScene_ShowsTheSelectionAndItsHandles()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            frame.Session.Select(host.Named<Border>("a"));

            AdornerScene scene = frame.BuildScene();

            Assert.Equal(new RectangleF(100, 100, 100, 20), scene.Selection);
            Assert.Equal(8, scene.Handles.Count);
            Assert.False(scene.Dimmed);
        }

        [Fact]
        public void InteractMode_DimsTheSelectionAndHidesTheHandles()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            frame.Session.Select(host.Named<Border>("a"));
            frame.Session.Mode = EditorMode.Interact;

            AdornerScene scene = frame.BuildScene();

            Assert.True(scene.Dimmed);
            Assert.Empty(scene.Handles);
            Assert.Null(scene.Hover);
        }

        [Fact]
        public void TheScene_ShowsTheHoverTarget()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            host.Input.Mouse.MouseInfo = new MouseInfo(host.At(host.Named<Border>("b"), 5, 5));

            Assert.Equal(new RectangleF(100, 120, 100, 20), frame.BuildScene().Hover);
        }

        [Fact]
        public void Rendering_DrawsTheAdorners()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorFrame frame = EditorFrame.Attach(host.Canvas, host.Design);
            var render = (Icy.Tests.Rendering.FakeRenderContext)host.Configuration.RenderContext;
            host.Render();
            Assert.DoesNotContain(render.DrawCalls, x => x.Options.Color == frame.SelectionColor);
            render.DrawCalls.Clear();

            frame.Session.Select(host.Named<Border>("a"));
            host.Render();

            // An outline is four lines; each handle is a white fill and four lines. The page's own draw calls vary from
            // frame to frame, so the adorners are counted by color.
            Assert.Equal(4 + (8 * 4), render.DrawCalls.Count(x => x.Options.Color == frame.SelectionColor));
            Assert.Equal(8, render.DrawCalls.Count(x => x.Options.Color == System.Drawing.Color.White));
        }

        private static DragInfo Drag(Point start, Point position) =>
            new(PointerKind.MouseLeft, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);
    }
}
