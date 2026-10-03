# Touch Gesture Recognizer (3a) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace engine-specific touch gestures with one core gesture recognizer. It is fed raw touch contacts plus the
mouse, and emits tap, hold, drag (with velocity) and pinch identically on every engine.

**Architecture:**
- `ITouchInput` becomes a raw, polled snapshot of `TouchContact`s.
- An internal `GestureRecognizer` (`Icy.Input.Gestures`) runs first in `InputEventSystem.Update` and is exposed as
  `IInputEventSystem.Gestures`.
- `TouchEvents` and `DragEvens` become thin adapters over it.
- The MonoGame and Stride `TouchInput` classes shrink to contact connectors.

**Tech Stack:** C# / .NET 10, xUnit v2, MonoGame 3.8.5 DesktopGL (`TouchPanel.GetState`), Stride 4.3
(`InputManager.PointerEvents`).

**Spec:** `docs/superpowers/specs/2026-10-03-touch-gesture-recognizer-design.md`

## Global Constraints

- Every public API gets complete XML documentation (`<see cref>`, `<see langword>`, `<list>`, `<para>`). The DocFX site
  is generated from it.
- Files start with the repo copyright header and use block-scoped `namespace X { }`. StyleCop applies to `IcyUI`,
  `IcyUI.MonoGame` and `IcyUI.Stride`. Keep one type per file, and member order public → internal → protected → private,
  static before instance.
- Core `IcyUI` must not reference engine types. Engine projects are connectors only.
- Defaults (copied from today's `TouchEvents`): `HoldDelay = 0.5 s`, `MultiTapDelay = 0.3 s`, `Slop = 10` DIP.
  Slop in pixels = `Slop × IRenderContext.DisplayScale`. Never use `Canvas.EffectiveScale` for it.
- The velocity window is 100 ms (least squares). A degenerate window gives `Vector2.Zero`, never NaN or ∞.
- Build: no new warnings against the baseline of **82** (`dotnet build "sources/IcyUI.sln" --no-incremental`).
  `IcyUI.Stride` stays at 0.
- Tests: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. **Always check the `Total:` count**, because a
  crashed test host still prints `Passed!`. The baseline is 977.
- Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Plan-level decisions

1. **How the recognizer learns `DisplayScale`.** Through `GestureSettings.DisplayScaleSource` (`Func<float>`, default
   `() => 1f`). The `IcyConfiguration` constructor wires it to `() => renderContext.DisplayScale`. The input system keeps
   no reference to the renderer.
2. **The Stride spike is already resolved** (checked against the `Stride.Input` 4.3.0.2507 XML docs):
   - `PointerEvent.AbsolutePosition` is in surface pixels, the same space as `MouseInput`'s `AbsoluteMousePosition`.
     There's no need to multiply the normalized `Position`.
   - `PointerEvent.Pointer` is an `IPointerDevice`. Mouse pointers are skipped with `is IMouseDevice`.
   - `PointerEventType.Canceled` exists.
   - `InputManager.PointerEvents` holds exactly one frame of events.
3. **Snapshot completeness.** Every active contact appears in **every** snapshot (as `Moved` when it didn't move) until
   its single `Released`. The recognizer treats a tracked contact that is missing from a snapshot as vanished (cancel).
   MonoGame's `TouchPanel.GetState()` already behaves this way. The Stride connector keeps its own active set to
   provide it.
4. **Mouse buttons are queued.** `MouseButtonPressed`/`Released` fire synchronously from `MouseInput.Update`, before
   `Events.Update`. The recognizer queues them together with the cursor position and processes them in its own
   `Update`, so a click shorter than one frame still registers.
5. **`IDragEvents.DragCanceled` is new.** A canceled drag (second finger, vanished contact) must not be forwarded as
   `DragEnded`, because `Canvas.OnDragEnded` would drop a drag-drop payload onto the target under the finger. `Canvas`
   handles it as "release the dragged element, no drop".
6. **Hold now fires on timeout for touch and left mouse** (the spec state machine). Before, mouse Hold fired on release.
   The only consumers are the diagnostics aggregators. Right mouse keeps Hold-on-release. Middle mouse never
   taps or holds, and no longer raises `TouchDown`/`TouchUp`. Before, middle raised `TouchDown` but never `TouchUp`.
7. **MonoGame touch was never enabled.** The old `TouchInput.IsListening` defaulted to `false` and nothing called
   `EnableListening()`. The new connectors default to `true`, like `MouseInput`.
8. **Right-button drags are dropped.** Before, a right press moved past the slop started a drag. The spec table gives
   right-mouse no drag.
9. **`DragEvensTests` are rewritten** to drive the adapter through `FakeGestureEvents`, because `DragEvens.OnMouseMove`
   is removed. The assertions (absolute positions, one per update) stay the same.

## Review Focus

1. **A drag-drop drag canceled by a second finger** must not drop the payload. The preview overlay is removed and the
   target gets `OnDragLeave`. Test: Task 2 (`DragDropTests.DragCanceled_DoesNotDrop_AndRemovesPreview`).
2. **A finger lifted while touch-emulated mouse events also arrive** (Windows/SDL synthesize mouse input from touch) must
   produce exactly one tap, not two. Test: Task 1 (`EmulatedMouse_WhileTouchActive_IsIgnored`).
3. **A press and release of the mouse within one frame** must still produce a tap. Test: Task 1
   (`MouseClick_WithinOneFrame_StillTaps`).
4. **A contact that vanishes mid-drag** (focus loss) must raise `PointerReleased`, so the control's Pressed state
   clears, plus `DragCanceled`, and never a tap. Test: Task 1 (`VanishedContact_CancelsDrag_AndReleasesPointer`).
5. **A stationary finger released after a fast move** must report velocity ≈ 0 rather than the stale fast speed, so a
   list doesn't get thrown in 3b. Test: Task 1 (`Velocity_AfterPause_IsNearZero`).

## File map

| File | Task | Responsibility |
|---|---|---|
| `IcyUI/Input/Devices/TouchContact.cs`, `TouchContactState.cs` (new) | 1 | Raw contact types |
| `IcyUI/Input/Devices/ITouchInput.cs` | 1, 3 | Task 1 adds `Contacts`; Task 3 removes the legacy gesture events and `TranslationInfo` |
| `IcyUI/Input/Gestures/PointerKind.cs`, `PointerInfo.cs`, `TapInfo.cs`, `DragInfo.cs`, `PinchInfo.cs`, `GestureSettings.cs`, `IGestureEvents.cs` (new) | 1 | Public gesture contract |
| `IcyUI/Input/Gestures/VelocityTracker.cs`, `GestureRecognizer.cs` (new, internal) | 1 | Recognition |
| `IcyUI.Tests/Input/FakeInputSystem.cs` | 1, 2 | `FakeTouchInput`, mouse raise helpers, `FakeGestureEvents` |
| `IcyUI.Tests/Input/GestureRecognizerTests.cs` (new) | 1 | Recognizer tests |
| `IcyUI/Input/IInputEventSystem.cs`, `InputEventSystem.cs`, `Configuration/IcyConfiguration.cs` | 2 | Wiring |
| `IcyUI/Input/Events/ITouchEvents.cs`, `TouchEvents.cs`, `IDragEvents.cs`, `DragEvens.cs`, `ScrollEvents.cs` | 2 | Adapters |
| `IcyUI/UI/Canvas.cs` | 2 | `DragCanceled` handling |
| `IcyUI.Tests/Input/DragEvensTests.cs`, `TouchEventsTests.cs` (new), `DragDropTests.cs` | 2 | Adapter tests |
| `IcyUI/Input/Diagnostics/DeviceEventsAggregator.cs` | 3 | Drop the legacy touch subscriptions |
| `IcyUI.MonoGame/Input/Devices/TouchInput.cs`, `IcyUI.Stride/Input/Devices/TouchInput.cs`, `IcyUI.Stride/Input/InputSystem.cs` | 3 | Connectors |
| `Shared Samples/ScalingDemo.cs`, `IcyUI.Tests/Samples/ScalingDemoTests.cs` | 4 | Gesture readout |
| `.claude/skills/engine-integration/SKILL.md` | 4 | Connector note |

---

### Task 1: Gesture contract and core recognizer

**Files:**
- Create: `sources/IcyUI/Input/Devices/TouchContact.cs`, `TouchContactState.cs`
- Create: `sources/IcyUI/Input/Gestures/PointerKind.cs`, `PointerInfo.cs`, `TapInfo.cs`, `DragInfo.cs`, `PinchInfo.cs`,
  `GestureSettings.cs`, `IGestureEvents.cs`, `VelocityTracker.cs`, `GestureRecognizer.cs`
- Modify: `sources/IcyUI/Input/Devices/ITouchInput.cs` (add `Contacts`; the legacy events stay until Task 3)
- Modify: `sources/IcyUI.MonoGame/Input/Devices/TouchInput.cs`, `sources/IcyUI.Stride/Input/Devices/TouchInput.cs`
  (temporary empty `Contacts`)
- Modify: `sources/IcyUI.Tests/Input/FakeInputSystem.cs` (`FakeTouchInput`, settable `Touch`, mouse raise helpers)
- Test: `sources/IcyUI.Tests/Input/GestureRecognizerTests.cs`

**Interfaces:**
- Produces: `public readonly record struct TouchContact(int Id, Point Position, TouchContactState State)`;
  `public enum TouchContactState { Pressed, Moved, Released }`; `ITouchInput.Contacts : IReadOnlyList<TouchContact>`.
- Produces (namespace `Icy.Input.Gestures`):
  - `enum PointerKind { Touch, MouseLeft, MouseRight, MouseMiddle }`
  - `readonly record struct PointerInfo(PointerKind Kind, Point Position)`
  - `readonly record struct TapInfo(PointerKind Kind, Point Position, int Count)`
  - `readonly record struct DragInfo(PointerKind Kind, Point Start, Point Position, Vector2 Delta, Vector2 Velocity)`
  - `readonly record struct PinchInfo(Vector2 Center, float Scale, float ScaleDelta, Vector2 CenterDelta)`
  - `class GestureSettings { TimeSpan HoldDelay; TimeSpan MultiTapDelay; float Slop; Func<float> DisplayScaleSource; }`
  - `interface IGestureEvents : IInputEventProvider` with:
    - `PointerPressed`, `PointerReleased`, `Held` (`GenericEventArgs<PointerInfo>`)
    - `Tapped` (`GenericEventArgs<TapInfo>`)
    - `DragStarted` (`AcceptableEventArgs<DragInfo>`); `DragMoved`, `DragCompleted`, `DragCanceled`
      (`GenericEventArgs<DragInfo>`)
    - `PinchStarted`, `PinchChanged`, `PinchCompleted` (`GenericEventArgs<PinchInfo>`)
    - `GestureSettings Settings { get; }`
  - `internal sealed class GestureRecognizer : IGestureEvents` with the constructor `GestureRecognizer(IInputSystem)`.
- Produces (tests):
  - `FakeTouchInput` with `List<TouchContact> Contacts`.
  - `FakeInputSystem.Touch` is settable (`FakeTouchInput?`).
  - `FakeMouseInput.RaiseButtonPressed(MouseButtons)` / `RaiseButtonReleased(MouseButtons)`.

- [ ] **Step 1: Add the raw contact types and `ITouchInput.Contacts`**

`sources/IcyUI/Input/Devices/TouchContactState.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Devices
{
    /// <summary>
    /// Specifies the state of a <see cref="TouchContact"/> in one <see cref="ITouchInput.Contacts"/> snapshot.
    /// </summary>
    public enum TouchContactState
    {
        /// <summary>
        /// The contact touched down since the previous snapshot.
        /// </summary>
        Pressed,

        /// <summary>
        /// The contact is still down. It is reported in every snapshot, even when it didn't move.
        /// </summary>
        Moved,

        /// <summary>
        /// The contact lifted since the previous snapshot. It is reported exactly once and is absent afterwards.
        /// </summary>
        Released,
    }
}
```

`sources/IcyUI/Input/Devices/TouchContact.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Input.Devices
{
    /// <summary>
    /// Describes one touch contact (finger) in an <see cref="ITouchInput.Contacts"/> snapshot.
    /// </summary>
    /// <param name="Id">The contact identifier, stable from its <see cref="TouchContactState.Pressed"/> to its <see cref="TouchContactState.Released"/> snapshot.</param>
    /// <param name="Position">The contact position, in physical (back-buffer) pixels.</param>
    /// <param name="State">The contact state in this snapshot.</param>
    public readonly record struct TouchContact(int Id, Point Position, TouchContactState State);
}
```

In `sources/IcyUI/Input/Devices/ITouchInput.cs`, add this member to the interface, above the existing events:

```csharp
        /// <summary>
        /// Gets the touch contacts of the current input update.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The snapshot is refreshed once per input update. Every active contact appears in every snapshot - as
        /// <see cref="TouchContactState.Moved"/> when it didn't move - until the single snapshot that reports it
        /// <see cref="TouchContactState.Released"/>. A tracked contact missing from a snapshot is treated as canceled.
        /// </para>
        /// <para>
        /// Gestures (tap, hold, drag, pinch) are recognized in core from this snapshot - see
        /// <see cref="Gestures.IGestureEvents"/>.
        /// </para>
        /// </remarks>
        IReadOnlyList<TouchContact> Contacts { get; }
```

In **both** engine `TouchInput` classes (`IcyUI.MonoGame/Input/Devices/TouchInput.cs`,
`IcyUI.Stride/Input/Devices/TouchInput.cs`), add a temporary member. Task 3 replaces both classes entirely:

```csharp
        /// <inheritdoc/>
        public IReadOnlyList<TouchContact> Contacts => [];
```

- [ ] **Step 2: Add the test fakes**

In `sources/IcyUI.Tests/Input/FakeInputSystem.cs`:

Replace `public ITouchInput? Touch => null;` with:

```csharp
        public FakeTouchInput? Touch { get; set; }

        ITouchInput? IInputSystem.Touch => Touch;
```

Add these to `FakeMouseInput`:

```csharp
        public void RaiseButtonPressed(MouseButtons button) => MouseButtonPressed?.Invoke(this, new GenericEventArgs<MouseButtons>(button));

        public void RaiseButtonReleased(MouseButtons button) => MouseButtonReleased?.Invoke(this, new GenericEventArgs<MouseButtons>(button));
```

Add a new class after `FakeMouseInput`:

```csharp
    /// <summary>
    /// A fake <see cref="ITouchInput"/> whose contact snapshot tests script directly, one input update at a time.
    /// </summary>
    public sealed class FakeTouchInput : ITouchInput
    {
        public List<TouchContact> Contacts { get; } = [];

        IReadOnlyList<TouchContact> ITouchInput.Contacts => Contacts;

        public bool IsListening => true;

        public bool IsInitialized { get; private set; }

        public event EventHandler<GenericEventArgs<TranslationInfo>>? Drag;

        public event EventHandler<GenericEventArgs<Point>>? Hold;

        public event EventHandler<GenericEventArgs<TranslationInfo>>? Swipe;

        public event EventHandler<GenericEventArgs<Point>>? Tap;

        public void Initialize() => IsInitialized = true;

        public bool DisableListening() => true;

        public bool EnableListening() => true;
    }
```

(The four legacy events keep the fake compiling until Task 3 removes them from `ITouchInput`. Task 3 deletes them from
the fake as well.)

- [ ] **Step 3: Write the failing recognizer tests**

`sources/IcyUI.Tests/Input/GestureRecognizerTests.cs`:

```csharp
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
```

- [ ] **Step 4: Run the tests to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~GestureRecognizerTests"`
Expected: build FAILS (`Icy.Input.Gestures` doesn't exist).

- [ ] **Step 5: Add the public gesture contract**

`sources/IcyUI/Input/Gestures/PointerKind.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Gestures
{
    /// <summary>
    /// Specifies which pointer produced a gesture.
    /// </summary>
    public enum PointerKind
    {
        /// <summary>
        /// A touch contact (finger).
        /// </summary>
        Touch,

        /// <summary>
        /// The left mouse button. Taps, holds and drags like a finger.
        /// </summary>
        MouseLeft,

        /// <summary>
        /// The right mouse button. Its release raises <see cref="IGestureEvents.Held"/> (a context click). It never taps or drags.
        /// </summary>
        MouseRight,

        /// <summary>
        /// The middle mouse button. It only drags, and those drags are meant for panning content rather than for controls.
        /// </summary>
        MouseMiddle,
    }
}
```

`sources/IcyUI/Input/Gestures/PointerInfo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Describes a pointer press, release or hold.
    /// </summary>
    /// <param name="Kind">The pointer that produced the gesture.</param>
    /// <param name="Position">The pointer position, in physical pixels.</param>
    public readonly record struct PointerInfo(PointerKind Kind, Point Position);
}
```

`sources/IcyUI/Input/Gestures/TapInfo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Describes a tap.
    /// </summary>
    /// <param name="Kind">The pointer that tapped.</param>
    /// <param name="Position">The tap position, in physical pixels.</param>
    /// <param name="Count">
    /// The number of consecutive taps: <c>2</c> for a double tap. A tap continues the sequence when it lands within
    /// <see cref="GestureSettings.MultiTapDelay"/> and <see cref="GestureSettings.Slop"/> of the previous one.
    /// </param>
    public readonly record struct TapInfo(PointerKind Kind, Point Position, int Count);
}
```

`sources/IcyUI/Input/Gestures/DragInfo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Describes one step of a drag.
    /// </summary>
    /// <param name="Kind">The pointer that drags.</param>
    /// <param name="Start">The position where the pointer was pressed, in physical pixels.</param>
    /// <param name="Position">The current pointer position, in physical pixels.</param>
    /// <param name="Delta">The movement since the previous drag event, in physical pixels.</param>
    /// <param name="Velocity">
    /// The release velocity in physical pixels per second - set on <see cref="IGestureEvents.DragCompleted"/> only,
    /// otherwise <see cref="Vector2.Zero"/>.
    /// </param>
    public readonly record struct DragInfo(PointerKind Kind, Point Start, Point Position, Vector2 Delta, Vector2 Velocity);
}
```

`sources/IcyUI/Input/Gestures/PinchInfo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Describes one step of a two-finger pinch.
    /// </summary>
    /// <param name="Center">The midpoint between the two fingers, in physical pixels.</param>
    /// <param name="Scale">The finger distance relative to the distance when the pinch started (<c>2</c> = twice as far apart).</param>
    /// <param name="ScaleDelta">The scale change since the previous pinch event, as a factor.</param>
    /// <param name="CenterDelta">The movement of <paramref name="Center"/> since the previous pinch event, in physical pixels.</param>
    public readonly record struct PinchInfo(Vector2 Center, float Scale, float ScaleDelta, Vector2 CenterDelta);
}
```

`sources/IcyUI/Input/Gestures/GestureSettings.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Gestures
{
    /// <summary>
    /// Holds the thresholds of gesture recognition (see <see cref="IGestureEvents"/>).
    /// </summary>
    public class GestureSettings
    {
        /// <summary>
        /// Gets or sets how long a touch or left-mouse press must stay inside <see cref="Slop"/> to raise
        /// <see cref="IGestureEvents.Held"/>. Defaults to 0.5 seconds.
        /// </summary>
        public TimeSpan HoldDelay { get; set; } = TimeSpan.FromSeconds(0.5);

        /// <summary>
        /// Gets or sets the longest gap between two taps that still continues a multi-tap sequence
        /// (see <see cref="TapInfo.Count"/>). Defaults to 0.3 seconds.
        /// </summary>
        public TimeSpan MultiTapDelay { get; set; } = TimeSpan.FromSeconds(0.3);

        /// <summary>
        /// Gets or sets how far a pointer may move, in device-independent pixels, before a press becomes a drag.
        /// Defaults to <c>10</c>.
        /// </summary>
        /// <remarks>
        /// Converted to physical pixels through <see cref="DisplayScaleSource"/>, so a finger may wobble the same physical
        /// distance at every display scale. The UI scale (<see cref="UI.Canvas.EffectiveScale"/>) deliberately doesn't apply.
        /// </remarks>
        public float Slop { get; set; } = 10f;

        /// <summary>
        /// Gets or sets the source of the display scale used to convert <see cref="Slop"/> to physical pixels.
        /// </summary>
        /// <remarks>
        /// <see cref="Configuration.IcyConfiguration"/> wires it to <see cref="Rendering.IRenderContext.DisplayScale"/>.
        /// Defaults to a constant <c>1</c>. Non-finite or non-positive results are treated as <c>1</c>.
        /// </remarks>
        public Func<float> DisplayScaleSource { get; set; } = () => 1f;
    }
}
```

`sources/IcyUI/Input/Gestures/IGestureEvents.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Input.Events;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Represents the gestures recognized from touch contacts and mouse buttons - the single source of pointer gestures
    /// on every engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Engines only report raw contacts (<see cref="Devices.ITouchInput.Contacts"/>) and mouse buttons. Recognition happens
    /// here, so thresholds and behavior are identical everywhere. All positions are in physical pixels.
    /// </para>
    /// <para>
    /// Per pointer kind (see <see cref="PointerKind"/>):
    /// <list type="bullet">
    /// <item><description><see cref="PointerKind.Touch"/> and <see cref="PointerKind.MouseLeft"/> tap, hold and drag.</description></item>
    /// <item><description><see cref="PointerKind.MouseRight"/> raises <see cref="Held"/> on release.</description></item>
    /// <item><description><see cref="PointerKind.MouseMiddle"/> only drags.</description></item>
    /// <item><description>Two touch contacts pinch. A second finger cancels the first finger's drag; after a pinch, the
    /// remaining fingers stay inert until all are lifted.</description></item>
    /// </list>
    /// Mouse buttons are ignored while any touch contact is active, because the OS synthesizes mouse input from touch.
    /// </para>
    /// </remarks>
    public interface IGestureEvents : IInputEventProvider
    {
        /// <summary>
        /// Occurs when a pointer is pressed.
        /// </summary>
        event EventHandler<GenericEventArgs<PointerInfo>>? PointerPressed;

        /// <summary>
        /// Occurs when a pointer is released, or its contact vanished.
        /// </summary>
        event EventHandler<GenericEventArgs<PointerInfo>>? PointerReleased;

        /// <summary>
        /// Occurs when a pointer is released inside <see cref="GestureSettings.Slop"/> before <see cref="GestureSettings.HoldDelay"/>.
        /// </summary>
        event EventHandler<GenericEventArgs<TapInfo>>? Tapped;

        /// <summary>
        /// Occurs when a touch or left-mouse press stays inside <see cref="GestureSettings.Slop"/> for
        /// <see cref="GestureSettings.HoldDelay"/>, or when the right mouse button is released.
        /// </summary>
        event EventHandler<GenericEventArgs<PointerInfo>>? Held;

        /// <summary>
        /// Occurs when a press moves past <see cref="GestureSettings.Slop"/>. Set <see cref="System.ComponentModel.CancelEventArgs.Cancel"/>
        /// to reject the drag: no further drag events are raised for that press.
        /// </summary>
        event EventHandler<AcceptableEventArgs<DragInfo>>? DragStarted;

        /// <summary>
        /// Occurs when a dragging pointer moves.
        /// </summary>
        event EventHandler<GenericEventArgs<DragInfo>>? DragMoved;

        /// <summary>
        /// Occurs when a dragging pointer is released. <see cref="DragInfo.Velocity"/> holds the release velocity.
        /// </summary>
        event EventHandler<GenericEventArgs<DragInfo>>? DragCompleted;

        /// <summary>
        /// Occurs when a drag ends without a release - a second finger started a pinch, or the contact vanished.
        /// </summary>
        event EventHandler<GenericEventArgs<DragInfo>>? DragCanceled;

        /// <summary>
        /// Occurs when a second touch contact lands and a pinch begins.
        /// </summary>
        event EventHandler<GenericEventArgs<PinchInfo>>? PinchStarted;

        /// <summary>
        /// Occurs when a pinching finger moves.
        /// </summary>
        event EventHandler<GenericEventArgs<PinchInfo>>? PinchChanged;

        /// <summary>
        /// Occurs when either pinching finger is lifted or vanishes.
        /// </summary>
        event EventHandler<GenericEventArgs<PinchInfo>>? PinchCompleted;

        /// <summary>
        /// Gets the recognition thresholds.
        /// </summary>
        GestureSettings Settings { get; }
    }
}
```

- [ ] **Step 6: Add the velocity tracker and the recognizer**

`sources/IcyUI/Input/Gestures/VelocityTracker.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Estimates a pointer's velocity by a least-squares fit over its most recent samples.
    /// </summary>
    internal sealed class VelocityTracker
    {
        private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(100);
        private readonly List<(double Time, Vector2 Position)> samples = [];

        /// <summary>
        /// Records a position, discarding samples older than the 100 ms window.
        /// </summary>
        /// <param name="time">The recognizer clock.</param>
        /// <param name="position">The pointer position, in physical pixels.</param>
        public void Add(TimeSpan time, Point position)
        {
            double now = time.TotalSeconds;
            samples.Add((now, new Vector2(position.X, position.Y)));
            samples.RemoveAll(sample => now - sample.Time > Window.TotalSeconds);
        }

        /// <summary>
        /// Gets the velocity in physical pixels per second, or <see cref="Vector2.Zero"/> when the window holds fewer than
        /// two samples or no time span.
        /// </summary>
        /// <returns>The estimated velocity.</returns>
        public Vector2 GetVelocity()
        {
            if (samples.Count < 2)
                return Vector2.Zero;

            double meanTime = 0, meanX = 0, meanY = 0;
            foreach (var (time, position) in samples)
            {
                meanTime += time;
                meanX += position.X;
                meanY += position.Y;
            }

            meanTime /= samples.Count;
            meanX /= samples.Count;
            meanY /= samples.Count;

            double denominator = 0, numeratorX = 0, numeratorY = 0;
            foreach (var (time, position) in samples)
            {
                double dt = time - meanTime;
                denominator += dt * dt;
                numeratorX += dt * (position.X - meanX);
                numeratorY += dt * (position.Y - meanY);
            }

            return denominator < 1e-12 ? Vector2.Zero : new Vector2((float)(numeratorX / denominator), (float)(numeratorY / denominator));
        }
    }
}
```

`sources/IcyUI/Input/Gestures/GestureRecognizer.cs`:

```csharp
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
        private readonly Dictionary<MouseButtons, PointerTrack> mouseTracks = [];
        private readonly Queue<(MouseButtons Button, bool IsPress, Point Position)> pendingMouse = new();
        private readonly Dictionary<int, PointerTrack> touchTracks = [];
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
            bool touchActive = ProcessTouches(slop);
            ProcessMouse(slop, touchActive);
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
                || !touchTracks.TryGetValue(pinch.IdA, out PointerTrack? a)
                || !touchTracks.TryGetValue(pinch.IdB, out PointerTrack? b))
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

            if (pinch != null && (pinch.IdA == id || pinch.IdB == id))
            {
                PinchInfo last = new(pinch.LastCenter, pinch.LastScale, 1f, Vector2.Zero);
                pinch = null;
                PinchCompleted?.Invoke(this, last);
                return;
            }

            Finish(track, canceled, slop);
        }

        private void ProcessMouse(float slop, bool touchActive)
        {
            while (pendingMouse.TryDequeue(out var pending))
            {
                // The OS synthesizes mouse input from touch; one finger must never count as two pointers.
                if (touchActive || ToKind(pending.Button) is not PointerKind kind)
                    continue;

                if (pending.IsPress)
                {
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

        private sealed class Pinch(int idA, int idB, float startDistance)
        {
            public int IdA { get; } = idA;

            public int IdB { get; } = idB;

            public float StartDistance { get; } = startDistance;

            public float LastScale { get; set; } = 1f;

            public Vector2 LastCenter { get; set; }
        }
    }
}
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~GestureRecognizerTests"`
Expected: all 19 PASS. Then run the full suite. Expected: `Total:` = 977 + 19 = 996, all passing.

If `TouchMovePastSlop_StartsDrag_AndNeverTaps` shows an extra `drag-move 111,100`, that's wrong: the move that crosses
the slop raises `DragStarted` only. The `return` after `DragStarted` in `Move` must stay.

- [ ] **Step 8: Build and commit**

Run: `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"`
Expected: `0 Error(s)`, warnings ≤ 82.

```bash
git add sources/IcyUI/Input sources/IcyUI.MonoGame/Input/Devices/TouchInput.cs sources/IcyUI.Stride/Input/Devices/TouchInput.cs sources/IcyUI.Tests/Input
git commit -m "Add the core touch gesture recognizer" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Wire the recognizer in and turn the touch/drag providers into adapters

**Files:**
- Modify: `sources/IcyUI/Input/IInputEventSystem.cs`, `InputEventSystem.cs`, `sources/IcyUI/Configuration/IcyConfiguration.cs`
- Modify: `sources/IcyUI/Input/Events/ITouchEvents.cs`, `TouchEvents.cs`, `IDragEvents.cs`, `DragEvens.cs`, `ScrollEvents.cs`
- Modify: `sources/IcyUI/UI/Canvas.cs`
- Modify: `sources/IcyUI.Tests/Input/FakeInputSystem.cs` (`FakeGestureEvents`; `FakeDragEvents.RaiseDragCanceled`;
  remove the three settings from `FakeTouchEvents`)
- Test: `sources/IcyUI.Tests/Input/DragEvensTests.cs` (rewritten), `sources/IcyUI.Tests/Input/TouchEventsTests.cs` (new),
  `sources/IcyUI.Tests/Input/DragDropTests.cs` (one new test)

**Interfaces:**
- Consumes: `IGestureEvents`, `GestureRecognizer`, `DragInfo`, `PointerInfo`, `TapInfo` (Task 1).
- Produces:
  - `IInputEventSystem.Gestures : IGestureEvents`
  - `IDragEvents.DragCanceled : EventHandler<GenericEventArgs<Point>>`
  - `FakeGestureEvents` with `RaisePointerPressed(PointerInfo)`, `RaisePointerReleased(PointerInfo)`,
    `RaiseTapped(TapInfo)`, `RaiseHeld(PointerInfo)`, `RaiseDragStarted(DragInfo)` (returns the args),
    `RaiseDragMoved(DragInfo)`, `RaiseDragCompleted(DragInfo)`, `RaiseDragCanceled(DragInfo)`.
  - `FakeDragEvents.RaiseDragCanceled(Point)`

- [ ] **Step 1: Extend the fakes**

In `FakeInputSystem.cs`, add to `FakeInputEventSystem`'s constructor `Gestures = new FakeGestureEvents(inputSystem);`
and these members:

```csharp
        public FakeGestureEvents Gestures { get; }

        IGestureEvents IInputEventSystem.Gestures => Gestures;
```

Add the class:

```csharp
    /// <summary>
    /// A fake <see cref="IGestureEvents"/> that lets tests raise recognized gestures directly.
    /// </summary>
    public sealed class FakeGestureEvents(IInputSystem inputSystem) : FakeInputEventProviderBase(inputSystem), IGestureEvents
    {
        public event EventHandler<GenericEventArgs<PointerInfo>>? PointerPressed;

        public event EventHandler<GenericEventArgs<PointerInfo>>? PointerReleased;

        public event EventHandler<GenericEventArgs<TapInfo>>? Tapped;

        public event EventHandler<GenericEventArgs<PointerInfo>>? Held;

        public event EventHandler<AcceptableEventArgs<DragInfo>>? DragStarted;

        public event EventHandler<GenericEventArgs<DragInfo>>? DragMoved;

        public event EventHandler<GenericEventArgs<DragInfo>>? DragCompleted;

        public event EventHandler<GenericEventArgs<DragInfo>>? DragCanceled;

        public event EventHandler<GenericEventArgs<PinchInfo>>? PinchStarted;

        public event EventHandler<GenericEventArgs<PinchInfo>>? PinchChanged;

        public event EventHandler<GenericEventArgs<PinchInfo>>? PinchCompleted;

        public GestureSettings Settings { get; } = new();

        public void RaisePointerPressed(PointerInfo info) => PointerPressed?.Invoke(this, info);

        public void RaisePointerReleased(PointerInfo info) => PointerReleased?.Invoke(this, info);

        public void RaiseTapped(TapInfo info) => Tapped?.Invoke(this, info);

        public void RaiseHeld(PointerInfo info) => Held?.Invoke(this, info);

        public AcceptableEventArgs<DragInfo> RaiseDragStarted(DragInfo info)
        {
            var args = new AcceptableEventArgs<DragInfo> { Data = info };
            DragStarted?.Invoke(this, args);
            return args;
        }

        public void RaiseDragMoved(DragInfo info) => DragMoved?.Invoke(this, info);

        public void RaiseDragCompleted(DragInfo info) => DragCompleted?.Invoke(this, info);

        public void RaiseDragCanceled(DragInfo info) => DragCanceled?.Invoke(this, info);

        public void RaisePinchChanged(PinchInfo info) => PinchChanged?.Invoke(this, info);

        public void RaisePinchStarted(PinchInfo info) => PinchStarted?.Invoke(this, info);

        public void RaisePinchCompleted(PinchInfo info) => PinchCompleted?.Invoke(this, info);
    }
```

In `FakeDragEvents`, add `public event EventHandler<GenericEventArgs<Point>>? DragCanceled;` and
`public void RaiseDragCanceled(Point point) => DragCanceled?.Invoke(this, new GenericEventArgs<Point>(point));`.

In `FakeTouchEvents`, delete `MaxMultiTapDelay`, `MinHoldDelay` and `HoldAreaSize`.

Add `using Icy.Input.Gestures;` at the top of the file.

- [ ] **Step 2: Write the failing adapter and canvas tests**

Replace the body of `sources/IcyUI.Tests/Input/DragEvensTests.cs` with:

```csharp
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
```

`sources/IcyUI.Tests/Input/TouchEventsTests.cs`:

```csharp
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
```

In `sources/IcyUI.Tests/Input/DragDropTests.cs`, add after `Preview_FollowsTheCursorInSurfaceSpace_AtDisplayScale2`:

```csharp
        [Fact]
        public void DragCanceled_DoesNotDrop_AndRemovesPreview()
        {
            // A second finger (pinch) or a vanished contact cancels the drag - the payload must not land on the target
            // under the finger.
            var (canvas, input) = CreateCanvas();
            var preview = new UIElement();
            AddSource(canvas, new object(), preview);
            var target = AddTarget(canvas, accepts: true, new Rectangle(200, 0, 100, 100));
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(50, 50));
            input.Events.Drag.RaiseDragPerforming(new Point(250, 50));
            input.Events.Drag.RaiseDragCanceled(new Point(250, 50));

            Assert.False(target.DroppedOn);
            Assert.Equal(1, target.LeaveCount);
            Assert.Empty(canvas.Overlays);
        }
```

- [ ] **Step 3: Run the tests to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DragEvensTests|FullyQualifiedName~TouchEventsTests|FullyQualifiedName~DragDropTests"`
Expected: build FAILS (`IInputEventSystem.Gestures` and `IDragEvents.DragCanceled` don't exist).

- [ ] **Step 4: Wire the recognizer**

`sources/IcyUI/Input/IInputEventSystem.cs`: add `using Icy.Input.Gestures;` and, after `Touch`:

```csharp
        /// <summary>
        /// Gets the gestures recognized from touch contacts and mouse buttons. They update before every other event provider,
        /// and the touch and drag providers are built on top of them.
        /// </summary>
        IGestureEvents Gestures { get; }
```

`sources/IcyUI/Input/InputEventSystem.cs`:
- Add `using Icy.Input.Gestures;`.
- Add the constructor parameter `IGestureEvents? gestures = null` (last) with the matching `<param>` doc: "The gesture
  recognizer, or <see langword="null"/> for the built-in one.".
- In the body, add `Gestures = gestures ?? new GestureRecognizer(InputSystem);`.
- Add the property `/// <inheritdoc/> public IGestureEvents Gestures { get; }`.
- In `Initialize()`, call `Gestures.Initialize();` first, right after `listener.Initialize();`.
- In `Update()`, call `Gestures.Update(deltaTime);` first.

`sources/IcyUI/Configuration/IcyConfiguration.cs`: in the constructor, after `Input = input;`:

```csharp
            // Gesture slop is measured in DIPs: it follows the OS display scale, never the UI scale.
            input.Events.Gestures.Settings.DisplayScaleSource = () => renderContext.DisplayScale;
```

- [ ] **Step 5: Turn the providers into adapters**

`sources/IcyUI/Input/Events/IDragEvents.cs`: add after `DragEnded`:

```csharp
        /// <summary>
        /// Occurs when a drag ends without being released - a second finger started a pinch, or the contact vanished.
        /// Handlers must undo the drag rather than complete it (no drop).
        /// </summary>
        event EventHandler<GenericEventArgs<Point>>? DragCanceled;
```

Replace `sources/IcyUI/Input/Events/DragEvens.cs`'s class body. Keep the header, `internal class DragEvens : IDragEvents`,
the XML summary, and the three existing event docs:

```csharp
        private bool active;

        /// <summary>
        /// Initializes a new instance of the <see cref="DragEvens"/> class.
        /// </summary>
        /// <param name="inputSystem">The input system whose gestures are forwarded.</param>
        public DragEvens(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? DragEnded;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? DragCanceled;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? DragPerforming;

        /// <inheritdoc/>
        public event EventHandler<AcceptableEventArgs<Point>>? DragStarted;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            IGestureEvents gestures = InputSystem.Events.Gestures;
            gestures.DragStarted += OnGestureDragStarted;
            gestures.DragMoved += OnGestureDragMoved;
            gestures.DragCompleted += OnGestureDragCompleted;
            gestures.DragCanceled += OnGestureDragCanceled;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
        }

        // Middle-mouse drags are pans for scrolling content (see PointerKind.MouseMiddle), never control drags.
        private static bool IsControlDrag(PointerKind kind) => kind is PointerKind.Touch or PointerKind.MouseLeft;

        private void OnGestureDragStarted(object? sender, AcceptableEventArgs<DragInfo> e)
        {
            if (!IsControlDrag(e.Data.Kind))
                return;

            var args = new AcceptableEventArgs<Point> { Data = e.Data.Start };
            DragStarted?.Invoke(this, args);
            e.Cancel = args.Cancel;
            e.Handled = args.Handled;
            active = !args.Cancel;
        }

        private void OnGestureDragMoved(object? sender, GenericEventArgs<DragInfo> e)
        {
            if (active && IsControlDrag(e.Data.Kind))
                DragPerforming?.Invoke(this, e.Data.Position);
        }

        private void OnGestureDragCompleted(object? sender, GenericEventArgs<DragInfo> e)
        {
            if (!active || !IsControlDrag(e.Data.Kind))
                return;
            active = false;
            DragEnded?.Invoke(this, e.Data.Position);
        }

        private void OnGestureDragCanceled(object? sender, GenericEventArgs<DragInfo> e)
        {
            if (!active || !IsControlDrag(e.Data.Kind))
                return;
            active = false;
            DragCanceled?.Invoke(this, e.Data.Position);
        }
```

Update the usings to `System.Drawing`, `Icy.Data` and `Icy.Input.Gestures`, and remove `Icy.Rendering` if it's now
unused.

`sources/IcyUI/Input/Events/ITouchEvents.cs`: delete `MaxMultiTapDelay`, `MinHoldDelay` and `HoldAreaSize`, together
with their docs. Add a `<remarks>` to the interface: "Built on <see cref=\"Gestures.IGestureEvents\"/>; its thresholds
live in <see cref=\"Gestures.IGestureEvents.Settings\"/>."

Replace `sources/IcyUI/Input/Events/TouchEvents.cs`'s class body. Keep the header, class declaration, summary and the four
event docs:

```csharp
        /// <summary>
        /// Initializes a new instance of the <see cref="TouchEvents"/> class.
        /// </summary>
        /// <param name="inputSystem">The input system whose gestures are forwarded.</param>
        public TouchEvents(IInputSystem inputSystem)
        {
            InputSystem = inputSystem;
        }

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? Hold;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<TouchInfo>>? Tap;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? TouchDown;

        /// <inheritdoc/>
        public event EventHandler<GenericEventArgs<Point>>? TouchUp;

        /// <inheritdoc/>
        public IInputSystem InputSystem { get; }

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public void Initialize()
        {
            if (IsInitialized) return;
            IsInitialized = true;

            IGestureEvents gestures = InputSystem.Events.Gestures;
            gestures.PointerPressed += OnPointerPressed;
            gestures.PointerReleased += OnPointerReleased;
            gestures.Tapped += OnTapped;
            gestures.Held += OnHeld;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
        }

        // Middle mouse only pans; it never presses, taps or holds controls.
        private static bool IsTouchLike(PointerKind kind) => kind != PointerKind.MouseMiddle;

        private void OnPointerPressed(object? sender, GenericEventArgs<PointerInfo> e)
        {
            if (IsTouchLike(e.Data.Kind))
                TouchDown?.Invoke(this, e.Data.Position);
        }

        private void OnPointerReleased(object? sender, GenericEventArgs<PointerInfo> e)
        {
            if (IsTouchLike(e.Data.Kind))
                TouchUp?.Invoke(this, e.Data.Position);
        }

        private void OnTapped(object? sender, GenericEventArgs<TapInfo> e) =>
            Tap?.Invoke(this, new TouchInfo(e.Data.Position, e.Data.Count));

        private void OnHeld(object? sender, GenericEventArgs<PointerInfo> e) =>
            Hold?.Invoke(this, e.Data.Position);
```

Fix the usings (`System.Drawing`, `Icy.Data`, `Icy.Input.Gestures`) and drop `System.Runtime.CompilerServices` and
`Icy.Input.Devices` if they become unused.

`sources/IcyUI/Input/Events/ScrollEvents.cs`: delete the `Touch_Swipe` method, the `InputSystem.Touch.Swipe +=`
subscription in `Initialize`, and the `case ITouchInput touch:` branches in both `Devices_DeviceConnected` and
`Devices_DeviceDisconnected`. Middle-button autoscroll stays untouched (3b replaces it). Update the `Scroll` remarks in
`IScrollEvents.cs`: "Raised for the mouse wheel, middle-button autoscroll and the gamepad right stick. Touch scrolling
is built on <see cref=\"Gestures.IGestureEvents\"/>."

- [ ] **Step 6: Handle `DragCanceled` in `Canvas`**

In `sources/IcyUI/UI/Canvas.cs`, `EnsureInputRoutingInitialized`: add `events.Drag.DragCanceled += OnDragCanceled;` after
the `DragEnded` subscription. Add next to `OnDragEnded`:

```csharp
        private void OnDragCanceled(object? sender, GenericEventArgs<Point> e)
        {
            // Release the dragged element like a normal end, so it drops any capture - but never complete a drag-drop:
            // the payload must not land on whatever target happens to be under the finger.
            foreach (UIElement element in SelfAndAncestors(draggedElement))
                element.OnDragEnded(e.Data);
            draggedElement = null;

            if (dragDropSession is { } session)
            {
                session.CurrentTarget?.OnDragLeave(session);
                if (session.Preview != null)
                    RemoveOverlay(session.Preview);
                dragDropSession = null;
            }
        }
```

- [ ] **Step 7: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected:
- All pass.
- `Total:` = 996 − 2 (old `DragEvensTests`) + 4 (new `DragEvensTests`) + 4 (`TouchEventsTests`) + 1 (`DragDropTests`)
  = **1003**.
- `grep -rn "MinHoldDelay\|MaxMultiTapDelay\|HoldAreaSize\|OnMouseMove" sources --include=*.cs` prints nothing.

- [ ] **Step 8: Build and commit**

Run: `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"`
Expected: `0 Error(s)`, warnings ≤ 82.

```bash
git add sources/IcyUI sources/IcyUI.Tests
git commit -m "Route touch, tap and drag events through the gesture recognizer" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Raw contact connectors; remove the legacy device gestures

**Files:**
- Modify: `sources/IcyUI/Input/Devices/ITouchInput.cs` (remove `Tap`, `Hold`, `Swipe`, `Drag`, `TranslationInfo`)
- Modify: `sources/IcyUI/Input/Diagnostics/DeviceEventsAggregator.cs`
- Modify: `sources/IcyUI.Tests/Input/FakeInputSystem.cs` (`FakeTouchInput` loses its four legacy events)
- Rewrite: `sources/IcyUI.MonoGame/Input/Devices/TouchInput.cs`, `sources/IcyUI.Stride/Input/Devices/TouchInput.cs`
- Modify: `sources/IcyUI.Stride/Input/InputSystem.cs` (`new TouchInput(input)`)

**Interfaces:**
- Consumes: `TouchContact`, `TouchContactState`, `ITouchInput.Contacts` (Task 1).
- Produces: the final `ITouchInput` (only `Contacts` plus the inherited listener/initializable members).

- [ ] **Step 1: Remove the legacy API**

In `ITouchInput.cs`:
- Delete the `Tap`, `Hold`, `Swipe` and `Drag` events and the whole `TranslationInfo` struct.
- Keep the `Contacts` doc remarks from Task 1, and update the interface `<summary>` to "Represents a touch device
  listener that reports raw touch contacts."

In `DeviceEventsAggregator.cs`:
- Delete the `case ITouchInput touch:` blocks in both the subscribe and unsubscribe switches.
- Delete the `Touch_Tap`, `Touch_Hold`, `Touch_Swipe` and `Touch_Drag` handlers.
- Delete the `TouchTap`, `TouchHold`, `TouchSwipe` and `TouchDrag` members of the nested `DeviceEventType` enum, with
  their docs.

In `FakeInputSystem.cs`, delete the four legacy events from `FakeTouchInput`.

Run: `grep -rn "TranslationInfo\|\.Swipe\b\|TouchSwipe" sources --include=*.cs`
Expected: matches only in the two engine `TouchInput.cs` files, which Steps 2–3 rewrite.

- [ ] **Step 2: MonoGame connector**

Replace `sources/IcyUI.MonoGame/Input/Devices/TouchInput.cs` with:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Input;
using Icy.Input.Devices;
using Microsoft.Xna.Framework.Input.Touch;

namespace Icy.MonoGame.Input.Devices
{
    /// <summary>
    /// MonoGame touch device listener: reports raw contacts from <see cref="TouchPanel.GetState()"/>.
    /// </summary>
    /// <remarks>
    /// Gestures are recognized in core (<see cref="Icy.Input.Gestures.IGestureEvents"/>), not by XNA's gesture recognizer, so
    /// <see cref="TouchPanel.EnabledGestures"/> is left unused. <see cref="TouchPanel.GetState()"/> reports every active
    /// contact each frame, which satisfies <see cref="ITouchInput.Contacts"/>'s snapshot contract directly.
    /// </remarks>
    internal class TouchInput : ITouchInput, IUpdateableInput
    {
        private readonly List<TouchContact> contacts = [];

        /// <inheritdoc/>
        public IReadOnlyList<TouchContact> Contacts => contacts;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public bool IsListening { get; private set; } = true;

        /// <inheritdoc/>
        public bool DisableListening()
        {
            IsListening = false;
            return true;
        }

        /// <inheritdoc/>
        public bool EnableListening()
        {
            IsListening = true;
            return true;
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            contacts.Clear();
            if (!IsListening)
                return;

            foreach (TouchLocation location in TouchPanel.GetState())
            {
                TouchContactState? state = location.State switch
                {
                    TouchLocationState.Pressed => TouchContactState.Pressed,
                    TouchLocationState.Moved => TouchContactState.Moved,
                    TouchLocationState.Released => TouchContactState.Released,
                    _ => null,
                };

                if (state is { } contactState)
                    contacts.Add(new TouchContact(location.Id, new Point((int)location.Position.X, (int)location.Position.Y), contactState));
            }
        }
    }
}
```

- [ ] **Step 3: Stride connector**

Replace `sources/IcyUI.Stride/Input/Devices/TouchInput.cs` with:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Input;
using Icy.Input.Devices;
using TInputManager = Stride.Input.InputManager;
using TMouseDevice = Stride.Input.IMouseDevice;
using TPointerEventType = Stride.Input.PointerEventType;

namespace Icy.Stride.Input.Devices
{
    /// <summary>
    /// Stride touch device listener: folds <see cref="TInputManager.PointerEvents"/> into raw contacts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stride reports pointer <em>events</em>, while <see cref="ITouchInput.Contacts"/> is a snapshot that lists every active
    /// contact each frame. This listener therefore keeps its own set of active contacts: an event-less contact is reported
    /// as <see cref="TouchContactState.Moved"/> at its last position. Mouse pointers are skipped, because the mouse comes
    /// through <see cref="MouseInput"/>.
    /// </para>
    /// <para>
    /// <see cref="TPointerEventType.Canceled"/> removes the contact without a <see cref="TouchContactState.Released"/>;
    /// core recognition treats the missing contact as canceled.
    /// </para>
    /// </remarks>
    internal class TouchInput(TInputManager input) : ITouchInput, IUpdateableInput
    {
        private readonly Dictionary<int, Point> active = [];
        private readonly List<TouchContact> contacts = [];
        private Dictionary<int, Point> carriedReleases = [];
        private Dictionary<int, Point> pendingReleases = [];

        /// <inheritdoc/>
        public IReadOnlyList<TouchContact> Contacts => contacts;

        /// <inheritdoc/>
        public bool IsInitialized { get; private set; }

        /// <inheritdoc/>
        public bool IsListening { get; private set; } = true;

        /// <inheritdoc/>
        public bool DisableListening()
        {
            IsListening = false;
            return true;
        }

        /// <inheritdoc/>
        public bool EnableListening()
        {
            IsListening = true;
            return true;
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            IsInitialized = true;
        }

        /// <inheritdoc/>
        public void Update(TimeSpan deltaTime)
        {
            contacts.Clear();
            if (!IsListening)
            {
                active.Clear();
                return;
            }

            var pressed = new HashSet<int>();
            var released = new Dictionary<int, Point>();
            foreach (var pointerEvent in input.PointerEvents)
            {
                if (pointerEvent.Pointer is TMouseDevice)
                    continue;

                int id = pointerEvent.PointerId;
                var position = new Point((int)pointerEvent.AbsolutePosition.X, (int)pointerEvent.AbsolutePosition.Y);
                switch (pointerEvent.EventType)
                {
                    case TPointerEventType.Pressed:
                        active[id] = position;
                        pressed.Add(id);
                        break;

                    case TPointerEventType.Moved:
                        if (active.ContainsKey(id))
                            active[id] = position;
                        break;

                    case TPointerEventType.Released:
                        if (active.Remove(id))
                            released[id] = position;
                        break;

                    case TPointerEventType.Canceled:
                        active.Remove(id);
                        pressed.Remove(id);
                        break;
                }
            }

            foreach (var (id, position) in active)
                contacts.Add(new TouchContact(id, position, pressed.Contains(id) ? TouchContactState.Pressed : TouchContactState.Moved));

            // A press and release within one frame: report the press now and keep the release for the next snapshot, so the
            // tap isn't lost.
            foreach (var (id, position) in released)
            {
                if (pressed.Contains(id))
                {
                    contacts.Add(new TouchContact(id, position, TouchContactState.Pressed));
                    pendingReleases[id] = position;
                }
                else
                {
                    contacts.Add(new TouchContact(id, position, TouchContactState.Released));
                }
            }

            foreach (var (id, position) in carriedReleases)
                contacts.Add(new TouchContact(id, position, TouchContactState.Released));
            carriedReleases.Clear();
            (carriedReleases, pendingReleases) = (pendingReleases, carriedReleases);
        }
    }
}
```

In `sources/IcyUI.Stride/Input/InputSystem.cs`, change `Touch = new TouchInput();` to `Touch = new TouchInput(input);`.

- [ ] **Step 4: Build and test**

Run: `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"`
Expected: `0 Error(s)`, warnings ≤ 82. `IcyUI.Stride`: 0 warnings
(`... | grep "IcyUI.Stride.*warning" | wc -l` prints 0).

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: `Total:` 1003, all pass.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI sources/IcyUI.MonoGame sources/IcyUI.Stride sources/IcyUI.Tests
git commit -m "Report raw touch contacts from MonoGame and Stride; drop device-level gestures" -m "MonoGame reads TouchPanel.GetState() instead of XNA gestures (its touch listener was also never enabled); Stride gains touch for the first time by folding InputManager.PointerEvents into contacts." -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Gesture readout for the smoke test; docs

**Files:**
- Modify: `sources/Shared Samples/ScalingDemo.cs`
- Test: `sources/IcyUI.Tests/Samples/ScalingDemoTests.cs`
- Modify: `.claude/skills/engine-integration/SKILL.md`

**Interfaces:**
- Consumes: `IInputEventSystem.Gestures`, `FakeGestureEvents` (Task 2).

- [ ] **Step 1: Write the failing test**

Add to `ScalingDemoTests`:

```csharp
        [Fact]
        public void GestureReadout_ShowsTheLastGesture()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            UIElement root = ScalingDemo.Build(configuration, "Airfool");

            input.Events.Gestures.RaiseTapped(new Icy.Input.Gestures.TapInfo(Icy.Input.Gestures.PointerKind.Touch, new System.Drawing.Point(5, 6), 2));
            Assert.Equal("Last gesture: Touch tap x2 at 5,6", root.FindRequiredControl<TextBlock>("GestureReadout").Text);

            input.Events.Gestures.RaisePinchChanged(new Icy.Input.Gestures.PinchInfo(new System.Numerics.Vector2(10, 20), 1.5f, 1.1f, System.Numerics.Vector2.Zero));
            Assert.Equal("Last gesture: pinch x1.50 at 10,20", root.FindRequiredControl<TextBlock>("GestureReadout").Text);
        }
```

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ScalingDemoTests"`
Expected: FAIL (`No element named 'GestureReadout'`).

- [ ] **Step 2: Add the readout**

In `ScalingDemo.Build`, after `var readout = new TextBlock { Text = "(not attached)" };`:

```csharp
            // Touch smoke-test aid: shows what the core gesture recognizer saw last.
            var gestureReadout = new TextBlock { Name = "GestureReadout", Text = "Last gesture: (none)" };
            Icy.Input.Gestures.IGestureEvents gestures = configuration.Input.Events.Gestures;
            gestures.Tapped += (_, e) => gestureReadout.Text = $"Last gesture: {e.Data.Kind} tap x{e.Data.Count} at {e.Data.Position.X},{e.Data.Position.Y}";
            gestures.Held += (_, e) => gestureReadout.Text = $"Last gesture: {e.Data.Kind} hold at {e.Data.Position.X},{e.Data.Position.Y}";
            gestures.DragCompleted += (_, e) => gestureReadout.Text = $"Last gesture: {e.Data.Kind} drag, {e.Data.Velocity.Length():0} px/s";
            gestures.PinchChanged += (_, e) => gestureReadout.Text = $"Last gesture: pinch x{e.Data.Scale:0.00} at {e.Data.Center.X:0},{e.Data.Center.Y:0}";
```

and add `root.Children.Add(gestureReadout);` right after `root.Children.Add(readout);`.

Run the `ScalingDemoTests` filter again. Expected: all 7 PASS.

- [ ] **Step 3: Docs**

In `.claude/skills/engine-integration/SKILL.md`, extend the "OS-specific code lives in core..." bullet with:
"Touch works the same way: an engine only fills `ITouchInput.Contacts` (raw contacts, every active one in every
snapshot); tap, hold, drag and pinch are recognized in core (`Icy.Input.Gestures`). A future `IcyUI.FNA` reuses the
MonoGame `TouchPanel.GetState()` connector."

- [ ] **Step 4: Build, test, commit**

Run the build. Expected: `0 Error(s)`, warnings ≤ 82. Run the suite. Expected: `Total:` 1004, all pass.

```bash
git add "sources/Shared Samples/ScalingDemo.cs" sources/IcyUI.Tests/Samples/ScalingDemoTests.cs .claude/skills/engine-integration/SKILL.md
git commit -m "Add a gesture readout to ScalingDemo for touch smoke tests" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Whole-branch verification

- [ ] **Step 1:** `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"`.
  Expected: 0 errors, warnings ≤ 82, `IcyUI.Stride` 0.
- [ ] **Step 2:** `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. Expected: `Total:` 1004, all pass, no
  "Test Run Aborted".
- [ ] **Step 3:** Whole-branch review of every commit from this plan, covering the Review Focus list and the
  recognizer's state transitions (`Pressed`/`Held`/`Dragging`/`Inert`).
- [ ] **Step 4: Hand over the manual smoke test (Ivan runs it; never claim it).** On the touch laptop, in both engines,
  using the Scaling Demo's "Last gesture" line:
  1. Finger tap, double tap and hold are reported as `Touch`. A mouse click still works and is reported as `MouseLeft`,
     and a single finger tap does **not** also show a mouse tap.
  2. A pressed button highlights under a finger.
  3. Dragging a `Slider` and a `SplitPane` with a finger works.
  4. A finger flick reports a drag with a plausible px/s velocity. Two fingers report a pinch scale.
  5. Drag-drop (Drag & Drop demo) with a finger, then putting a second finger down mid-drag: nothing is dropped and the
     ghost disappears.
  6. Middle-button autoscroll still scrolls lists (unchanged until 3b).
