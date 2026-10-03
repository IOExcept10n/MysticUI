using System.Drawing;
using System.Numerics;
using Icy.Input.Devices;
using Icy.Input.Gestures;
using Xunit;

namespace Icy.Tests.Input
{
    public class GestureRecognizerTests
    {
        private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(10);

        private sealed class Harness
        {
            public Harness()
            {
                Input = new FakeInputSystem { Touch = new FakeTouchInput() };
                Recognizer = new GestureRecognizer(Input);
                Recognizer.Initialize();
                Recognizer.PointerPressed += (_, e) => Log.Add($"down {e.Data.Kind} {e.Data.Position.X},{e.Data.Position.Y}");
                Recognizer.PointerReleased += (_, e) => Log.Add($"up {e.Data.Kind}");
                Recognizer.Tapped += (_, e) => { Taps.Add(e.Data); Log.Add($"tap {e.Data.Count}"); };
                Recognizer.Held += (_, e) => Log.Add($"hold {e.Data.Kind}");
                Recognizer.DragStarted += (_, e) => { Log.Add($"drag-start {e.Data.Kind} {e.Data.Start.X},{e.Data.Start.Y}"); e.Cancel = CancelNextDrag; };
                Recognizer.DragMoved += (_, e) => Log.Add($"drag-move {e.Data.Position.X},{e.Data.Position.Y}");
                Recognizer.DragCompleted += (_, e) => { Completed = e.Data; Log.Add("drag-end"); };
                Recognizer.DragCanceled += (_, e) => Log.Add("drag-cancel");
                Recognizer.PinchStarted += (_, e) => Log.Add($"pinch-start {e.Data.Center.X},{e.Data.Center.Y}");
                Recognizer.PinchChanged += (_, e) => { LastPinch = e.Data; Log.Add("pinch-change"); };
                Recognizer.PinchCompleted += (_, e) => Log.Add("pinch-end");
            }

            public FakeInputSystem Input { get; }

            public GestureRecognizer Recognizer { get; }

            public List<string> Log { get; } = [];

            public List<TapInfo> Taps { get; } = [];

            public DragInfo? Completed { get; private set; }

            public PinchInfo? LastPinch { get; private set; }

            public bool CancelNextDrag { get; set; }

            public void Touch(params TouchContact[] contacts) => Touch(Frame, contacts);

            public void Touch(TimeSpan elapsed, params TouchContact[] contacts)
            {
                Input.Touch!.Contacts.Clear();
                Input.Touch.Contacts.AddRange(contacts);
                Recognizer.Update(elapsed);
            }

            public void Mouse(int x, int y, TimeSpan? elapsed = null)
            {
                Input.Mouse.MouseInfo = new MouseInfo(new Point(x, y));
                Input.Touch!.Contacts.Clear();
                Recognizer.Update(elapsed ?? Frame);
            }
        }

        private static TouchContact Down(int id, int x, int y) => new(id, new Point(x, y), TouchContactState.Pressed);

        private static TouchContact Move(int id, int x, int y) => new(id, new Point(x, y), TouchContactState.Moved);

        private static TouchContact Up(int id, int x, int y) => new(id, new Point(x, y), TouchContactState.Released);

        [Fact]
        public void TouchTap_InsideSlop_RaisesPressTapRelease()
        {
            var h = new Harness();

            h.Touch(Down(1, 100, 100));
            h.Touch(Move(1, 109, 100));
            h.Touch(Up(1, 109, 100));

            Assert.Equal(new[] { "down Touch 100,100", "up Touch", "tap 1" }, h.Log);
        }

        [Fact]
        public void TouchMovePastSlop_StartsDrag_AndNeverTaps()
        {
            var h = new Harness();

            h.Touch(Down(1, 100, 100));
            h.Touch(Move(1, 111, 100));
            h.Touch(Move(1, 120, 100));
            h.Touch(Up(1, 120, 100));

            Assert.Equal(new[] { "down Touch 100,100", "drag-start Touch 100,100", "drag-move 120,100", "up Touch", "drag-end" }, h.Log);
        }

        [Fact]
        public void Slop_IsMeasuredInDips()
        {
            var h = new Harness();
            h.Recognizer.Settings.DisplayScaleSource = () => 2f;

            h.Touch(Down(1, 100, 100));
            h.Touch(Move(1, 115, 100)); // 15 px = 7.5 DIP at 200 %: still inside the 10 DIP slop
            h.Touch(Up(1, 115, 100));

            Assert.Single(h.Taps);
        }

        [Fact]
        public void MultiTap_CountsWithinDelayAndSlop_AndResetsAfterwards()
        {
            var h = new Harness();

            h.Touch(Down(1, 50, 50));
            h.Touch(Up(1, 50, 50));
            h.Touch(Down(2, 52, 50));
            h.Touch(Up(2, 52, 50));
            h.Touch(TimeSpan.FromMilliseconds(400));
            h.Touch(Down(3, 52, 50));
            h.Touch(Up(3, 52, 50));

            Assert.Equal(new[] { 1, 2, 1 }, h.Taps.Select(t => t.Count));
        }

        [Fact]
        public void Hold_FiresOnceOnTimeout_AndSuppressesTap()
        {
            var h = new Harness();

            h.Touch(Down(1, 10, 10));
            h.Touch(TimeSpan.FromMilliseconds(300), Move(1, 10, 10));
            h.Touch(TimeSpan.FromMilliseconds(300), Move(1, 10, 10));
            h.Touch(TimeSpan.FromMilliseconds(300), Move(1, 10, 10));
            h.Touch(Up(1, 10, 10));

            Assert.Equal(new[] { "down Touch 10,10", "hold Touch", "up Touch" }, h.Log);
        }

        [Fact]
        public void DragCompleted_ReportsReleaseVelocity()
        {
            var h = new Harness();

            h.Touch(Down(1, 0, 0));
            for (int x = 10; x <= 200; x += 10)
                h.Touch(Move(1, x, 0)); // 10 px per 10 ms = 1000 px/s
            h.Touch(Up(1, 210, 0));

            Assert.NotNull(h.Completed);
            Assert.InRange(h.Completed!.Value.Velocity.X, 950f, 1050f);
            Assert.InRange(h.Completed.Value.Velocity.Y, -1f, 1f);
        }

        [Fact]
        public void Velocity_AfterPause_IsNearZero()
        {
            var h = new Harness();

            h.Touch(Down(1, 0, 0));
            for (int x = 10; x <= 100; x += 10)
                h.Touch(Move(1, x, 0));
            for (int i = 0; i < 15; i++)
                h.Touch(Move(1, 100, 0)); // 150 ms motionless
            h.Touch(Up(1, 100, 0));

            Assert.InRange(h.Completed!.Value.Velocity.Length(), 0f, 1f);
        }

        [Fact]
        public void DragStarted_Canceled_SuppressesTheRestOfTheDrag()
        {
            var h = new Harness { CancelNextDrag = true };

            h.Touch(Down(1, 0, 0));
            h.Touch(Move(1, 50, 0));
            h.Touch(Move(1, 80, 0));
            h.Touch(Up(1, 80, 0));

            Assert.Equal(new[] { "down Touch 0,0", "drag-start Touch 0,0", "up Touch" }, h.Log);
        }

        [Fact]
        public void MouseLeft_DragsAndTaps_LikeToday()
        {
            var h = new Harness();
            h.Input.Mouse.MouseInfo = new MouseInfo(new Point(5, 5));
            h.Input.Mouse.RaiseButtonPressed(MouseButtons.LeftButton);
            h.Mouse(5, 5);
            h.Mouse(40, 5);
            h.Input.Mouse.RaiseButtonReleased(MouseButtons.LeftButton);
            h.Mouse(40, 5);

            Assert.Equal(new[] { "down MouseLeft 5,5", "drag-start MouseLeft 5,5", "up MouseLeft", "drag-end" }, h.Log);
        }

        [Fact]
        public void MouseClick_WithinOneFrame_StillTaps()
        {
            var h = new Harness();
            h.Input.Mouse.MouseInfo = new MouseInfo(new Point(5, 5));
            h.Input.Mouse.RaiseButtonPressed(MouseButtons.LeftButton);
            h.Input.Mouse.RaiseButtonReleased(MouseButtons.LeftButton);

            h.Mouse(5, 5);

            Assert.Equal(new[] { "down MouseLeft 5,5", "up MouseLeft", "tap 1" }, h.Log);
        }

        [Fact]
        public void MouseRight_HoldsOnRelease_NeverDrags()
        {
            var h = new Harness();
            h.Input.Mouse.MouseInfo = new MouseInfo(new Point(5, 5));
            h.Input.Mouse.RaiseButtonPressed(MouseButtons.RightButton);
            h.Mouse(60, 5);
            h.Input.Mouse.RaiseButtonReleased(MouseButtons.RightButton);
            h.Mouse(60, 5);

            Assert.Equal(new[] { "down MouseRight 5,5", "up MouseRight", "hold MouseRight" }, h.Log);
        }

        [Fact]
        public void MouseMiddle_DragIsTaggedMiddle_AndNeverTaps()
        {
            var h = new Harness();
            h.Input.Mouse.MouseInfo = new MouseInfo(new Point(5, 5));
            h.Input.Mouse.RaiseButtonPressed(MouseButtons.MiddleButton);
            h.Input.Mouse.RaiseButtonReleased(MouseButtons.MiddleButton);
            h.Mouse(5, 5);
            h.Input.Mouse.RaiseButtonPressed(MouseButtons.MiddleButton);
            h.Mouse(5, 5);
            h.Mouse(50, 5);

            Assert.DoesNotContain(h.Log, l => l.StartsWith("tap", StringComparison.Ordinal));
            Assert.Contains("drag-start MouseMiddle 5,5", h.Log);
        }

        [Fact]
        public void SecondFinger_CancelsDrag_AndStartsPinch()
        {
            var h = new Harness();

            h.Touch(Down(1, 100, 100));
            h.Touch(Move(1, 130, 100));
            h.Touch(Move(1, 130, 100), Down(2, 230, 100));

            Assert.Equal(new[] { "down Touch 100,100", "drag-start Touch 100,100", "down Touch 230,100", "drag-cancel", "pinch-start 180,100" }, h.Log);
        }

        [Fact]
        public void Pinch_ReportsScaleAndCenter_AndEndsWhenAFingerLifts()
        {
            var h = new Harness();
            h.Touch(Down(1, 100, 100), Down(2, 200, 100));

            h.Touch(Move(1, 50, 100), Move(2, 250, 100));

            Assert.Equal(2f, h.LastPinch!.Value.Scale, 3);
            Assert.Equal(2f, h.LastPinch.Value.ScaleDelta, 3);
            Assert.Equal(new Vector2(150, 100), h.LastPinch.Value.Center);

            h.Touch(Move(1, 50, 100), Up(2, 250, 100));
            h.Log.Clear();
            h.Touch(Move(1, 10, 100));
            h.Touch(Up(1, 10, 100));

            Assert.Equal(new[] { "up Touch" }, h.Log); // the remaining finger stays inert: no drag, no tap
        }

        [Fact]
        public void ThirdFinger_IsIgnored()
        {
            var h = new Harness();
            h.Touch(Down(1, 100, 100), Down(2, 200, 100));
            h.Log.Clear();

            h.Touch(Move(1, 100, 100), Move(2, 200, 100), Down(3, 400, 400));
            h.Touch(Move(1, 100, 100), Move(2, 200, 100), Up(3, 400, 400));

            Assert.Equal(new[] { "down Touch 400,400", "up Touch" }, h.Log);
        }

        [Fact]
        public void VanishedContact_CancelsDrag_AndReleasesPointer()
        {
            var h = new Harness();
            h.Touch(Down(1, 0, 0));
            h.Touch(Move(1, 40, 0));
            h.Log.Clear();

            h.Touch(); // contact 1 missing: vanished

            Assert.Equal(new[] { "up Touch", "drag-cancel" }, h.Log);
        }

        [Fact]
        public void VanishedContact_BeforeMoving_NeverTaps()
        {
            var h = new Harness();
            h.Touch(Down(1, 0, 0));

            h.Touch();

            Assert.Empty(h.Taps);
        }

        [Fact]
        public void ReusedContactId_ResetsThePointer()
        {
            var h = new Harness();
            h.Touch(Down(7, 0, 0));
            h.Touch(Move(7, 40, 0));

            h.Touch(Down(7, 300, 300));
            h.Touch(Up(7, 300, 300));

            Assert.Contains("drag-cancel", h.Log);
            Assert.Single(h.Taps);
            Assert.Equal(new Point(300, 300), h.Taps[0].Position);
        }

        [Fact]
        public void MouseReleasedWhileTouchActive_StillEndsTheMouseTrack()
        {
            var h = new Harness();
            h.Input.Mouse.MouseInfo = new MouseInfo(new Point(5, 5));
            h.Input.Mouse.RaiseButtonPressed(MouseButtons.LeftButton);
            h.Mouse(5, 5);
            h.Touch(Down(1, 300, 300));
            h.Input.Mouse.RaiseButtonReleased(MouseButtons.LeftButton);
            h.Touch(Move(1, 300, 300));
            h.Touch(Up(1, 300, 300));
            h.Log.Clear();

            h.Mouse(200, 200, TimeSpan.FromMilliseconds(700)); // no button held: must not hold or drag

            Assert.Empty(h.Log);
        }

        [Fact]
        public void LateEmulatedClick_AfterFingerLifts_IsIgnored()
        {
            var h = new Harness();
            h.Input.Mouse.MouseInfo = new MouseInfo(new Point(100, 100));
            h.Touch(Down(1, 100, 100));
            h.Touch(Up(1, 100, 100));

            h.Input.Mouse.RaiseButtonPressed(MouseButtons.LeftButton);
            h.Input.Mouse.RaiseButtonReleased(MouseButtons.LeftButton);
            h.Mouse(100, 100); // the OS promotes the tap to a click only after the finger lifted

            Assert.Equal(new[] { "down Touch 100,100", "up Touch", "tap 1" }, h.Log);
        }

        [Fact]
        public void RealMouseClick_WellAfterTouch_StillTaps()
        {
            var h = new Harness();
            h.Touch(Down(1, 100, 100));
            h.Touch(Up(1, 100, 100));
            h.Mouse(100, 100, TimeSpan.FromMilliseconds(600));

            h.Input.Mouse.RaiseButtonPressed(MouseButtons.LeftButton);
            h.Input.Mouse.RaiseButtonReleased(MouseButtons.LeftButton);
            h.Mouse(100, 100);

            Assert.Equal(PointerKind.MouseLeft, h.Taps[^1].Kind);
        }

        [Fact]
        public void EmulatedMouse_WhileTouchActive_IsIgnored()
        {
            var h = new Harness();
            h.Input.Mouse.MouseInfo = new MouseInfo(new Point(100, 100));

            h.Input.Mouse.RaiseButtonPressed(MouseButtons.LeftButton);
            h.Touch(Down(1, 100, 100));
            h.Input.Mouse.RaiseButtonReleased(MouseButtons.LeftButton);
            h.Touch(Up(1, 100, 100));

            Assert.Equal(new[] { "down Touch 100,100", "up Touch", "tap 1" }, h.Log);
        }
    }
}
