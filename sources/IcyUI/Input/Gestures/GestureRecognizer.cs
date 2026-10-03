// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Data;
using Icy.Input.Devices;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Recognizes taps, holds, drags and pinches from <see cref="ITouchInput.Contacts"/> and mouse buttons.
    /// </summary>
    /// <remarks>
    /// Mouse button events fire synchronously while devices update, before <see cref="Update"/>. They are queued with the
    /// cursor position and processed in <see cref="Update"/>, so a click shorter than one frame still registers and mouse
    /// and touch share one timeline.
    /// </remarks>
    internal sealed class GestureRecognizer : IGestureEvents
    {
        // How long after the last touch contact mouse presses are still treated as OS-synthesized: Windows promotes a
        // tap to a click only after the finger lifts.
        private static readonly TimeSpan EmulatedMouseGrace = TimeSpan.FromMilliseconds(500);
        private readonly Dictionary<MouseButtons, PointerTrack> mouseTracks = [];
        private readonly Queue<(MouseButtons Button, bool IsPress, Point Position)> pendingMouse = new();
        private readonly Dictionary<int, PointerTrack> touchTracks = [];
        private TimeSpan? lastTouchActiveTime;
        private int lastTapCount;
        private Point lastTapPosition;
        private TimeSpan lastTapTime;
        private TimeSpan now;
        private Pinch? pinch;
        private bool touchLocked;

        /// <summary>
        /// Initializes a new instance of the <see cref="GestureRecognizer"/> class.
        /// </summary>
        /// <param name="inputSystem">The input system whose touch contacts and mouse buttons are recognized.</param>
        public GestureRecognizer(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<PointerInfo>>? PointerPressed;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<PointerInfo>>? PointerReleased;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<TapInfo>>? Tapped;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<PointerInfo>>? Held;

        /// <inheritdoc/>
        public event EventHandler<AcceptableEventArgs<DragInfo>>? DragStarted;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<DragInfo>>? DragMoved;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<DragInfo>>? DragCompleted;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<DragInfo>>? DragCanceled;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<PinchInfo>>? PinchStarted;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<PinchInfo>>? PinchChanged;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<PinchInfo>>? PinchCompleted;

        private enum TrackState
        {
            Pressed,
            Held,
            Dragging,
            Inert,
        }

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public GestureSettings Settings { get; } = new();

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;
            InputSystem.Mouse.MouseButtonPressed += OnMouseButtonPressed;
            InputSystem.Mouse.MouseButtonReleased += OnMouseButtonReleased;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            now += deltaTime;
            float slop = GetSlopPixels();
            if (ProcessTouches(slop))
                lastTouchActiveTime = now;
            bool suppressMousePresses = lastTouchActiveTime is { } touchTime && now - touchTime < EmulatedMouseGrace;
            ProcessMouse(slop, suppressMousePresses);
            RaiseHolds();
        }

        private static float Distance(Point a, Point b) => Vector2.Distance(new Vector2(a.X, a.Y), new Vector2(b.X, b.Y));

        private static PointerKind? ToKind(MouseButtons button) => button switch
        {
            MouseButtons.LeftButton => PointerKind.MouseLeft,
            MouseButtons.RightButton => PointerKind.MouseRight,
            MouseButtons.MiddleButton => PointerKind.MouseMiddle,
            _ => null,
        };

        private static bool ContainsContact(IReadOnlyList<TouchContact> contacts, int id)
        {
            foreach (TouchContact contact in contacts)
            {
                if (contact.Id == id)
                    return true;
            }

            return false;
        }

        private float GetSlopPixels()
        {
            float scale = Settings.DisplayScaleSource();
            if (!float.IsFinite(scale) || scale <= 0)
                scale = 1f;
            return Settings.Slop * scale;
        }

        private bool ProcessTouches(float slop)
        {
            IReadOnlyList<TouchContact> contacts = InputSystem.Touch?.Contacts ?? [];

            if (touchTracks.Count > 0)
            {
                List<int>? vanished = null;
                foreach (int id in touchTracks.Keys)
                {
                    if (!ContainsContact(contacts, id))
                        (vanished ??= []).Add(id);
                }

                if (vanished != null)
                {
                    foreach (int id in vanished)
                        EndTouch(id, canceled: true, slop);
                }
            }

            foreach (TouchContact contact in contacts)
            {
                switch (contact.State)
                {
                    case TouchContactState.Pressed:
                        if (touchTracks.ContainsKey(contact.Id))
                            EndTouch(contact.Id, canceled: true, slop);
                        PressTouch(contact);
                        break;

                    case TouchContactState.Moved:
                        if (touchTracks.TryGetValue(contact.Id, out PointerTrack? moved))
                            Move(moved, contact.Position, slop);
                        break;

                    case TouchContactState.Released:
                        if (touchTracks.TryGetValue(contact.Id, out PointerTrack? released))
                        {
                            Move(released, contact.Position, slop);
                            EndTouch(contact.Id, canceled: false, slop);
                        }

                        break;
                }
            }

            UpdatePinch();
            if (touchTracks.Count == 0)
                touchLocked = false;
            return contacts.Count > 0 || touchTracks.Count > 0;
        }

        private void PressTouch(TouchContact contact)
        {
            var track = new PointerTrack(PointerKind.Touch, contact.Position, now);
            touchTracks[contact.Id] = track;
            PointerPressed?.Invoke(this, new PointerInfo(PointerKind.Touch, contact.Position));

            if (touchLocked)
                track.State = TrackState.Inert;
            else if (touchTracks.Count == 2)
                StartPinch();
        }

        private void StartPinch()
        {
            touchLocked = true;
            int[] ids = [.. touchTracks.Keys];
            foreach (PointerTrack track in touchTracks.Values)
            {
                if (track.State == TrackState.Dragging)
                    DragCanceled?.Invoke(this, new DragInfo(track.Kind, track.Start, track.Position, Vector2.Zero, Vector2.Zero));
                track.State = TrackState.Inert;
            }

            Point a = touchTracks[ids[0]].Position;
            Point b = touchTracks[ids[1]].Position;
            var center = new Vector2((a.X + b.X) / 2f, (a.Y + b.Y) / 2f);
            pinch = new Pinch(ids[0], ids[1], MathF.Max(Distance(a, b), 1f)) { LastCenter = center };
            PinchStarted?.Invoke(this, new PinchInfo(center, 1f, 1f, Vector2.Zero));
        }

        private void UpdatePinch()
        {
            if (pinch == null
                || !touchTracks.TryGetValue(pinch.FirstId, out PointerTrack? a)
                || !touchTracks.TryGetValue(pinch.SecondId, out PointerTrack? b))
            {
                return;
            }

            var center = new Vector2((a.Position.X + b.Position.X) / 2f, (a.Position.Y + b.Position.Y) / 2f);
            float scale = Distance(a.Position, b.Position) / pinch.StartDistance;
            if (scale == pinch.LastScale && center == pinch.LastCenter)
                return;

            var info = new PinchInfo(center, scale, scale / pinch.LastScale, center - pinch.LastCenter);
            pinch.LastScale = scale;
            pinch.LastCenter = center;
            PinchChanged?.Invoke(this, info);
        }

        private void EndTouch(int id, bool canceled, float slop)
        {
            PointerTrack track = touchTracks[id];
            touchTracks.Remove(id);
            PointerReleased?.Invoke(this, new PointerInfo(track.Kind, track.Position));

            if (pinch != null && (pinch.FirstId == id || pinch.SecondId == id))
            {
                PinchInfo last = new(pinch.LastCenter, pinch.LastScale, 1f, Vector2.Zero);
                pinch = null;
                PinchCompleted?.Invoke(this, last);
                return;
            }

            Finish(track, canceled, slop);
        }

        private void ProcessMouse(float slop, bool suppressPresses)
        {
            while (pendingMouse.TryDequeue(out var pending))
            {
                if (ToKind(pending.Button) is not PointerKind kind)
                    continue;

                if (pending.IsPress)
                {
                    // The OS synthesizes mouse input from touch; one finger must never count as two pointers. Releases
                    // always go through, so a track pressed before the touch began can't get stuck.
                    if (suppressPresses)
                        continue;

                    if (mouseTracks.Remove(pending.Button, out PointerTrack? stale))
                    {
                        PointerReleased?.Invoke(this, new PointerInfo(stale.Kind, stale.Position));
                        Finish(stale, canceled: true, slop);
                    }

                    mouseTracks[pending.Button] = new PointerTrack(kind, pending.Position, now);
                    PointerPressed?.Invoke(this, new PointerInfo(kind, pending.Position));
                }
                else if (mouseTracks.Remove(pending.Button, out PointerTrack? track))
                {
                    Move(track, pending.Position, slop);
                    PointerReleased?.Invoke(this, new PointerInfo(track.Kind, track.Position));
                    Finish(track, canceled: false, slop);
                }
            }

            if (mouseTracks.Count > 0)
            {
                Point position = InputSystem.Mouse.MouseInfo.Position;
                foreach (PointerTrack track in mouseTracks.Values)
                    Move(track, position, slop);
            }
        }

        private void Move(PointerTrack track, Point position, float slop)
        {
            Point previous = track.Position;
            track.Position = position;
            track.Velocity.Add(now, position);

            if (track.State is TrackState.Pressed or TrackState.Held
                && track.Kind != PointerKind.MouseRight
                && Distance(track.Start, position) > slop)
            {
                track.State = TrackState.Dragging;
                var args = new AcceptableEventArgs<DragInfo>
                {
                    Data = new DragInfo(track.Kind, track.Start, position, new Vector2(position.X - track.Start.X, position.Y - track.Start.Y), Vector2.Zero),
                };
                DragStarted?.Invoke(this, args);
                if (args.Cancel)
                    track.State = TrackState.Inert;
                return;
            }

            if (track.State == TrackState.Dragging && position != previous)
                DragMoved?.Invoke(this, new DragInfo(track.Kind, track.Start, position, new Vector2(position.X - previous.X, position.Y - previous.Y), Vector2.Zero));
        }

        private void Finish(PointerTrack track, bool canceled, float slop)
        {
            switch (track.State)
            {
                case TrackState.Dragging:
                    if (canceled)
                        DragCanceled?.Invoke(this, new DragInfo(track.Kind, track.Start, track.Position, Vector2.Zero, Vector2.Zero));
                    else
                        DragCompleted?.Invoke(this, new DragInfo(track.Kind, track.Start, track.Position, Vector2.Zero, track.Velocity.GetVelocity()));
                    break;

                case TrackState.Pressed when !canceled:
                    if (track.Kind == PointerKind.MouseRight)
                        Held?.Invoke(this, new PointerInfo(track.Kind, track.Position));
                    else if (track.Kind is PointerKind.Touch or PointerKind.MouseLeft)
                        RaiseTap(track.Kind, track.Position, slop);
                    break;
            }
        }

        private void RaiseTap(PointerKind kind, Point position, float slop)
        {
            bool continues = lastTapCount > 0
                && now - lastTapTime <= Settings.MultiTapDelay
                && Distance(lastTapPosition, position) <= slop;
            lastTapCount = continues ? lastTapCount + 1 : 1;
            lastTapTime = now;
            lastTapPosition = position;
            Tapped?.Invoke(this, new TapInfo(kind, position, lastTapCount));
        }

        private void RaiseHolds()
        {
            foreach (PointerTrack track in touchTracks.Values.Concat(mouseTracks.Values))
            {
                if (track.State == TrackState.Pressed
                    && track.Kind is PointerKind.Touch or PointerKind.MouseLeft
                    && now - track.PressTime >= Settings.HoldDelay)
                {
                    track.State = TrackState.Held;
                    Held?.Invoke(this, new PointerInfo(track.Kind, track.Position));
                }
            }
        }

        private void OnMouseButtonPressed(object? sender, GenericEventArgs<MouseButtons> e) =>
            pendingMouse.Enqueue((e.Data, true, InputSystem.Mouse.MouseInfo.Position));

        private void OnMouseButtonReleased(object? sender, GenericEventArgs<MouseButtons> e) =>
            pendingMouse.Enqueue((e.Data, false, InputSystem.Mouse.MouseInfo.Position));

        private sealed class PointerTrack(PointerKind kind, Point start, TimeSpan pressTime)
        {
            public PointerKind Kind { get; } = kind;

            public Point Start { get; } = start;

            public TimeSpan PressTime { get; } = pressTime;

            public Point Position { get; set; } = start;

            public TrackState State { get; set; } = TrackState.Pressed;

            public VelocityTracker Velocity { get; } = CreateTracker(start, pressTime);

            private static VelocityTracker CreateTracker(Point start, TimeSpan time)
            {
                var tracker = new VelocityTracker();
                tracker.Add(time, start);
                return tracker;
            }
        }

        private sealed class Pinch(int firstId, int secondId, float startDistance)
        {
            public int FirstId { get; } = firstId;

            public int SecondId { get; } = secondId;

            public float StartDistance { get; } = startDistance;

            public float LastScale { get; set; } = 1f;

            public Vector2 LastCenter { get; set; }
        }
    }
}
