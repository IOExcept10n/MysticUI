using System.Drawing;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Xunit;

namespace Icy.Tests.Input
{
    public class TouchEventsTests
    {
        private static (TouchEvents Touch, FakeGestureEvents Gestures) Create()
        {
            var input = new FakeInputSystem();
            var touch = new TouchEvents(input);
            touch.Initialize();
            return (touch, input.Events.Gestures);
        }

        [Fact]
        public void TouchPressAndRelease_RaiseTouchDownAndUp()
        {
            var (touch, gestures) = Create();
            var log = new List<string>();
            touch.TouchDown += (_, e) => log.Add($"down {e.Data.X},{e.Data.Y}");
            touch.TouchUp += (_, e) => log.Add($"up {e.Data.X},{e.Data.Y}");

            gestures.RaisePointerPressed(new PointerInfo(PointerKind.Touch, new Point(3, 4)));
            gestures.RaisePointerReleased(new PointerInfo(PointerKind.Touch, new Point(3, 4)));

            Assert.Equal(new[] { "down 3,4", "up 3,4" }, log);
        }

        [Fact]
        public void MiddleMouse_RaisesNoTouchDown()
        {
            var (touch, gestures) = Create();
            bool raised = false;
            touch.TouchDown += (_, _) => raised = true;

            gestures.RaisePointerPressed(new PointerInfo(PointerKind.MouseMiddle, new Point(3, 4)));

            Assert.False(raised);
        }

        [Fact]
        public void Tapped_IsForwardedWithItsCount()
        {
            var (touch, gestures) = Create();
            TouchInfo? tap = null;
            touch.Tap += (_, e) => tap = e.Data;

            gestures.RaiseTapped(new TapInfo(PointerKind.MouseLeft, new Point(9, 9), 2));

            Assert.Equal(new Point(9, 9), tap!.Value.LastTouch);
            Assert.Equal(2, tap.Value.TouchCount);
        }

        [Fact]
        public void Held_IsForwarded()
        {
            var (touch, gestures) = Create();
            Point? held = null;
            touch.Hold += (_, e) => held = e.Data;

            gestures.RaiseHeld(new PointerInfo(PointerKind.MouseRight, new Point(1, 2)));

            Assert.Equal(new Point(1, 2), held);
        }
    }
}
