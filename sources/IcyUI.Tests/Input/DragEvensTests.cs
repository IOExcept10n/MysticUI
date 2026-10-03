using System.Drawing;
using System.Numerics;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Xunit;

namespace Icy.Tests.Input
{
    public class DragEvensTests
    {
        private static (DragEvens Drag, FakeGestureEvents Gestures) Create()
        {
            var input = new FakeInputSystem();
            var drag = new DragEvens(input);
            drag.Initialize();
            return (drag, input.Events.Gestures);
        }

        private static DragInfo Info(PointerKind kind, int x, int y) => new(kind, new Point(0, 0), new Point(x, y), Vector2.Zero, Vector2.Zero);

        [Fact]
        public void TouchAndLeftMouseDrags_AreForwardedWithAbsolutePositions()
        {
            var (drag, gestures) = Create();
            var log = new List<string>();
            drag.DragStarted += (_, e) => log.Add($"start {e.Data.X},{e.Data.Y}");
            drag.DragPerforming += (_, e) => log.Add($"move {e.Data.X},{e.Data.Y}");
            drag.DragEnded += (_, e) => log.Add($"end {e.Data.X},{e.Data.Y}");

            gestures.RaiseDragStarted(new DragInfo(PointerKind.Touch, new Point(10, 10), new Point(30, 10), Vector2.Zero, Vector2.Zero));
            gestures.RaiseDragMoved(Info(PointerKind.Touch, 50, 80));
            gestures.RaiseDragMoved(Info(PointerKind.Touch, 200, 0));
            gestures.RaiseDragCompleted(Info(PointerKind.Touch, 210, 0));

            Assert.Equal(new[] { "start 10,10", "move 50,80", "move 200,0", "end 210,0" }, log);
        }

        [Fact]
        public void MiddleMouseDrags_AreNotForwarded()
        {
            var (drag, gestures) = Create();
            bool raised = false;
            drag.DragStarted += (_, _) => raised = true;

            gestures.RaiseDragStarted(Info(PointerKind.MouseMiddle, 50, 0));

            Assert.False(raised);
        }

        [Fact]
        public void CanceledGestureDrag_RaisesDragCanceled_NotDragEnded()
        {
            var (drag, gestures) = Create();
            bool ended = false;
            Point? canceled = null;
            drag.DragEnded += (_, _) => ended = true;
            drag.DragCanceled += (_, e) => canceled = e.Data;

            gestures.RaiseDragStarted(Info(PointerKind.Touch, 30, 0));
            gestures.RaiseDragCanceled(Info(PointerKind.Touch, 40, 0));

            Assert.False(ended);
            Assert.Equal(new Point(40, 0), canceled);
        }

        [Fact]
        public void CancelingDragStarted_PropagatesToTheGesture()
        {
            var (drag, gestures) = Create();
            drag.DragStarted += (_, e) => e.Cancel = true;

            var args = gestures.RaiseDragStarted(Info(PointerKind.Touch, 30, 0));

            Assert.True(args.Cancel);
        }
    }
}
