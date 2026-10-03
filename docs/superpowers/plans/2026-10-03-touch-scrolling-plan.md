# Touch Scrolling (3b) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Touch and middle-mouse pan scrollable areas 1:1 with flick inertia, while a single, direction-locked owner
receives each drag.

**Architecture:**
- `Canvas` routes drags from `IGestureEvents`. It resolves one owner per gesture by asking each ancestor of the hit
  element `GetDragAxes`; the innermost element claiming the main axis wins, and otherwise the innermost element claiming
  any axis.
- `ScrollViewer` claims for `Touch`/`MouseMiddle`, pans through its own transform, and coasts with constant deceleration
  through an internal per-frame tick that `Dispatcher.UpdateAnimations` drives.

**Tech Stack:** C# / .NET 10, xUnit v2.

**Spec:** `docs/superpowers/specs/2026-10-03-touch-scrolling-design.md` (builds on
`docs/superpowers/specs/2026-10-03-touch-gesture-recognizer-design.md`).

## Global Constraints

- Every public API gets complete XML documentation. Files start with the repo copyright header and use block-scoped
  namespaces. StyleCop applies to `IcyUI`: one type per file; member order public → internal → protected → private,
  static before instance; properties before methods.
- `ScrollViewer` defaults: `PanningMode = Auto`, `PanningDeceleration = 1500` (units/s² in its own space; `0` disables
  inertia).
- Inertia: no fling below **50** units/s; speed capped at **8000** units/s; duration `T = |v| / d`, distance `v · T / 2`,
  `EaseOutQuad` over `T`.
- **The left mouse never pans.** `ScrollViewer` claims only `Touch` and `MouseMiddle`. Built-in controls never claim
  `MouseMiddle`.
- Edges are a hard stop (the offset setters clamp). Nested areas follow the per-gesture rule: an area that can't move in
  the gesture's direction doesn't claim it.
- Build: `dotnet build "sources/IcyUI.sln" --no-incremental`. No new warnings against the baseline of **82**;
  `IcyUI.Stride` stays at 0.
- Tests: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. **Check the `Total:` line**: a crashed host still
  prints `Passed!`. The baseline is **1019**.
- Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Plan-level decisions

1. **Inertia does not use `Animation`.** `Animation` writes at the Animation precedence tier, and `Stop()` clears that
   tier, reverting the property to the value captured at the first tier write. On a "progress" property that would fire
   a spurious revert, and on the offsets themselves it would undo the scroll. Instead, `Dispatcher` gains an internal
   `IFrameTicker` registry, ticked inside `UpdateAnimations` after the animations. That's the same timing source, so
   tests drive inertia with `UpdateAnimations`. An internal `InertiaDriver` applies relative `EaseOutQuad` deltas, as
   the spec requires.
2. **Touch-to-stop without tapping.** The recognizer raises `PointerReleased` (→ `TouchUp` clears Pressed) **before**
   `Tapped`. A `ScrollViewer` whose fling is stopped by a press therefore keeps its children un-hittable for the rest of
   that press, and re-enables them through `Dispatcher.Invoke` (queued and run at the end of the frame), after the tap
   has been dispatched. The hook is a new `protected virtual bool CanHitTestChildren()` on `UIElement`.
   `UIElement.HitTest` isn't virtual today.
3. **The owner catches up on the start frame.** After `OnDragStarted(drag.Start)`, `Canvas` calls
   `OnDragPerforming(drag.Position)` when the position differs from the start. This fixes 3a's deferred minor
   "a Slider lags one slop behind on the drag-start frame". `ScrollViewer` uses its first `OnDragPerforming` only as the
   pan anchor, so the content doesn't jump by the slop distance.
4. **Test migration through forwarding.** `FakeDragEvents.Raise*` forward to `FakeGestureEvents` as `MouseLeft`
   gestures (`Position == Start` on start, so no catch-up call). The 59 existing call sites keep working. Only elements
   that receive drags must now claim an axis (`CanvasEventBubblingTests`' recording element).
5. **`DragInfo.StartedFromHold`** is added as an optional last positional parameter (`= false`), so existing
   `new DragInfo(...)` calls still compile.
6. **`ScrollViewer` remembers its own claim.** `Canvas` calls `GetDragAxes` on every ancestor before picking the owner.
   `ScrollViewer` keeps the axes it returned last (`pendingClaim`) and locks panning to them in `OnDragStarted`.

## Review Focus

1. **A press on a list mid-fling, then lift:** exactly zero taps reach the item under the finger, and the next tap
   (after a frame) works normally. Test: Task 4 (`PressDuringFling_StopsIt_AndSwallowsOnlyThatTap`).
2. **A fling across a virtualized list whose row heights get corrected mid-flight:** no jump, and the correction is
   kept. Test: Task 4 (`OffsetCorrectionDuringFling_IsKept`).
3. **A custom control that only overrides `OnDragStarted` (legacy):** it no longer receives drags. This is documented on
   `OnDragStarted`; no test, it's a stated breaking change.
4. **A drag owner removed from the tree mid-gesture** (e.g. its dialog closes): no exception, and no further calls.
   Test: Task 2 (`OwnerDetachedMidDrag_ReceivesNothingMore`).
5. **A vertical swipe starting on a horizontal slider inside a list that is already at its top edge:** the list can't
   move that way, so the slider takes the gesture through the fallback rule rather than nothing reacting. Test: Task 3
   (`SliderInListAtEdge_FallsBackToTheSlider`).

---

### Task 1: `DragInfo.StartedFromHold` and a per-frame ticker

**Files:**
- Modify: `sources/IcyUI/Input/Gestures/DragInfo.cs`, `sources/IcyUI/Input/Gestures/GestureRecognizer.cs`
- Create: `sources/IcyUI/Data/Markup/IFrameTicker.cs`
- Modify: `sources/IcyUI/Data/Markup/Dispatcher.cs`
- Test: `sources/IcyUI.Tests/Input/GestureRecognizerTests.cs` (2 new), `sources/IcyUI.Tests/Data/DispatcherFrameTickerTests.cs` (new)

**Interfaces:**
- Produces: `DragInfo(..., Vector2 Velocity, bool StartedFromHold = false)`
- Produces: `internal interface IFrameTicker { void Tick(TimeSpan delta); }` (namespace `Icy.Data.Markup`)
- Produces: `internal void Dispatcher.RegisterFrameTicker(IFrameTicker)` and `internal void Dispatcher.UnregisterFrameTicker(IFrameTicker)`

- [ ] **Step 1: Write the failing tests**

Add to `GestureRecognizerTests` (after `DragCompleted_ReportsReleaseVelocity`):

```csharp
        [Fact]
        public void DragGrowingOutOfAHold_IsMarkedStartedFromHold()
        {
            var h = new Harness();
            bool? fromHold = null;
            h.Recognizer.DragStarted += (_, e) => fromHold = e.Data.StartedFromHold;

            h.Touch(Down(1, 10, 10));
            h.Touch(TimeSpan.FromMilliseconds(600), Move(1, 10, 10));
            h.Touch(Move(1, 60, 10));

            Assert.True(fromHold);
        }

        [Fact]
        public void PlainDrag_IsNotMarkedStartedFromHold()
        {
            var h = new Harness();
            bool? fromHold = null;
            h.Recognizer.DragStarted += (_, e) => fromHold = e.Data.StartedFromHold;

            h.Touch(Down(1, 10, 10));
            h.Touch(Move(1, 60, 10));

            Assert.False(fromHold);
        }
```

`sources/IcyUI.Tests/Data/DispatcherFrameTickerTests.cs`:

```csharp
using Icy.Data.Markup;
using Xunit;

namespace Icy.Tests.Data
{
    public class DispatcherFrameTickerTests
    {
        private sealed class CountingTicker : IFrameTicker
        {
            public TimeSpan Total { get; private set; }

            public void Tick(TimeSpan delta) => Total += delta;
        }

        [Fact]
        public void RegisteredTicker_IsTickedByUpdateAnimations_UntilUnregistered()
        {
            Dispatcher dispatcher = Dispatcher.GetCurrentThreadDispatcher();
            var ticker = new CountingTicker();

            dispatcher.RegisterFrameTicker(ticker);
            dispatcher.UpdateAnimations(TimeSpan.FromMilliseconds(16));
            dispatcher.UnregisterFrameTicker(ticker);
            dispatcher.UpdateAnimations(TimeSpan.FromMilliseconds(16));

            Assert.Equal(TimeSpan.FromMilliseconds(16), ticker.Total);
        }
    }
}
```

- [ ] **Step 2: Run them to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~GestureRecognizerTests|FullyQualifiedName~DispatcherFrameTickerTests"`
Expected: build FAILS (`StartedFromHold`, `IFrameTicker` and `RegisterFrameTicker` don't exist).

- [ ] **Step 3: Implement**

`DragInfo.cs`: add the parameter and its doc:

```csharp
    /// <param name="StartedFromHold">
    /// <see langword="true"/> when the drag grew out of a press that had already raised <see cref="IGestureEvents.Held"/>
    /// (press-and-hold, then move) - the touch gesture that picks up drag-and-drop sources.
    /// </param>
    public readonly record struct DragInfo(PointerKind Kind, Point Start, Point Position, Vector2 Delta, Vector2 Velocity, bool StartedFromHold = false);
```

`GestureRecognizer.Move`: replace the `DragStarted` block's start so the flag is captured before the state changes:

```csharp
                bool startedFromHold = track.State == TrackState.Held;
                track.State = TrackState.Dragging;
                var args = new AcceptableEventArgs<DragInfo>
                {
                    Data = new DragInfo(track.Kind, track.Start, position, new Vector2(position.X - track.Start.X, position.Y - track.Start.Y), Vector2.Zero, startedFromHold),
                };
```

`sources/IcyUI/Data/Markup/IFrameTicker.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Data.Markup
{
    /// <summary>
    /// Represents an object advanced once per frame by <see cref="Dispatcher.UpdateAnimations(TimeSpan)"/>, after the
    /// running animations - for motion that applies relative steps instead of writing an animated property value.
    /// </summary>
    internal interface IFrameTicker
    {
        /// <summary>
        /// Advances the ticker by one frame.
        /// </summary>
        /// <param name="delta">The time elapsed since the previous frame.</param>
        void Tick(TimeSpan delta);
    }
}
```

`Dispatcher.cs`:
- Add the field `private readonly HashSet<IFrameTicker> frameTickers = [];` after `frameBindings`.
- Add these methods after `UnregisterAnimation`:

```csharp
        /// <summary>
        /// Registers a ticker to be advanced on every <see cref="UpdateAnimations(TimeSpan)"/> call.
        /// </summary>
        /// <param name="ticker">The ticker to register.</param>
        internal void RegisterFrameTicker(IFrameTicker ticker)
        {
            lock (lockObj)
            {
                frameTickers.Add(ticker);
            }
        }

        /// <summary>
        /// Stops advancing a ticker registered through <see cref="RegisterFrameTicker(IFrameTicker)"/>.
        /// </summary>
        /// <param name="ticker">The ticker to unregister.</param>
        internal void UnregisterFrameTicker(IFrameTicker ticker)
        {
            lock (lockObj)
            {
                frameTickers.Remove(ticker);
            }
        }
```

Replace the body of `UpdateAnimations` (keep its docs). Add one sentence to its remarks: "Registered frame tickers are
advanced after the animations."

```csharp
        public void UpdateAnimations(TimeSpan delta)
        {
            Animation[] animations;
            IFrameTicker[] tickers;
            lock (lockObj)
            {
                if (runningAnimations.Count == 0 && frameTickers.Count == 0)
                    return;
                animations = [.. runningAnimations];
                tickers = [.. frameTickers];
            }

            foreach (Animation animation in animations)
                animation.Update(delta);
            foreach (IFrameTicker ticker in tickers)
                ticker.Tick(delta);
        }
```

The `Dispatcher` class and its `UpdateAnimations` are public while `IFrameTicker` is internal. The internal methods are
fine; just check that the public XML docs don't `cref` the internal interface.

- [ ] **Step 4: Run the tests**

Run the Step 2 filter. Expected: PASS. Then run the full suite. Expected: `Total:` **1022**, all passing.

- [ ] **Step 5: Build and commit**

Build with no new warnings (≤ 82).

```bash
git add sources/IcyUI/Input/Gestures sources/IcyUI/Data/Markup sources/IcyUI.Tests/Input/GestureRecognizerTests.cs sources/IcyUI.Tests/Data/DispatcherFrameTickerTests.cs
git commit -m "Mark hold-initiated drags and add a per-frame ticker to Dispatcher" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Single-owner drag routing in `Canvas`

**Files:**
- Create: `sources/IcyUI/UI/DragAxes.cs`, `sources/IcyUI/UI/DragClaimContext.cs`
- Modify: `sources/IcyUI/UI/UIElement.cs` (`GetDragAxes`, `OnDragFling`, `CanHitTestChildren`, `HitTest`, drag docs)
- Modify: `sources/IcyUI/UI/Canvas.cs` (owner resolution and gesture routing)
- Modify: `sources/IcyUI/UI/Controls/Slider.cs`, `SplitPane.cs`, `HsvSquare.cs` (claims)
- Modify: `sources/IcyUI.Tests/Input/FakeInputSystem.cs` (forwarding), `sources/IcyUI.Tests/UI/CanvasEventBubblingTests.cs` (claim)
- Create: `sources/IcyUI.Tests/UI/AxisElement.cs`, `sources/IcyUI.Tests/UI/CanvasDragOwnershipTests.cs`

**Interfaces:**
- Consumes: `DragInfo.StartedFromHold` (Task 1).
- Produces:
  - `[Flags] public enum DragAxes { None = 0, Horizontal = 1, Vertical = 2, Both = 3 }` (namespace `Icy.UI`)
  - `public readonly record struct DragClaimContext(PointerKind Kind, Point ScreenStart, Vector2 LocalDirection, bool StartedFromHold)`
  - `protected internal virtual DragAxes UIElement.GetDragAxes(in DragClaimContext context)` (default `None`)
  - `protected internal virtual void UIElement.OnDragFling(Vector2 screenVelocity)`
  - `protected virtual bool UIElement.CanHitTestChildren()` (default `true`)
  - Test helpers: `AxisElement(DragAxes)` and `AxisBorder(DragAxes)` with a `Log` list.

- [ ] **Step 1: Write the test helpers and failing tests**

`sources/IcyUI.Tests/UI/AxisElement.cs`:

```csharp
using System.Drawing;
using System.Numerics;
using Icy.UI;

namespace Icy.Tests.UI
{
    /// <summary>
    /// A leaf element that claims fixed drag axes and records every drag call it receives.
    /// </summary>
    internal sealed class AxisElement(DragAxes axes) : UIElement
    {
        public List<string> Log { get; } = [];

        protected internal override DragAxes GetDragAxes(in DragClaimContext context) => axes;

        protected internal override void OnDragStarted(Point screenPoint) => Log.Add($"start {screenPoint.X},{screenPoint.Y}");

        protected internal override void OnDragPerforming(Point screenPoint) => Log.Add($"move {screenPoint.X},{screenPoint.Y}");

        protected internal override void OnDragEnded(Point screenPoint) => Log.Add("end");

        protected internal override void OnDragFling(Vector2 screenVelocity) => Log.Add("fling");
    }

    /// <summary>
    /// A container counterpart of <see cref="AxisElement"/>.
    /// </summary>
    internal sealed class AxisBorder(DragAxes axes) : Border
    {
        public List<string> Log { get; } = [];

        protected internal override DragAxes GetDragAxes(in DragClaimContext context) => axes;

        protected internal override void OnDragStarted(Point screenPoint) => Log.Add("start");

        protected internal override void OnDragPerforming(Point screenPoint) => Log.Add("move");

        protected internal override void OnDragEnded(Point screenPoint) => Log.Add("end");
    }
}
```

`sources/IcyUI.Tests/UI/CanvasDragOwnershipTests.cs`:

```csharp
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.DragDrop;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasDragOwnershipTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) Create()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            return (canvas, input);
        }

        private static T Place<T>(T element, int x, int y, int width, int height)
            where T : UIElement
        {
            element.Margin = new Thickness(x, y, 0, 0);
            element.Width = width;
            element.Height = height;
            element.HorizontalAlignment = HorizontalAlignment.Left;
            element.VerticalAlignment = VerticalAlignment.Top;
            return element;
        }

        private static DragInfo Drag(PointerKind kind, Point start, Point position, bool fromHold = false) =>
            new(kind, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero, fromHold);

        [Fact]
        public void InnermostClaimantOfTheMainAxis_OwnsTheDrag_AndItsParentGetsNothing()
        {
            var (canvas, input) = Create();
            var child = new AxisElement(DragAxes.Horizontal) { Width = 100, Height = 50 };
            var parent = Place(new AxisBorder(DragAxes.Both) { Child = child }, 0, 0, 200, 200);
            canvas.Add(parent);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 12)));

            Assert.Equal(new[] { "start 10,10", "move 40,12" }, child.Log);
            Assert.Empty(parent.Log);
        }

        [Fact]
        public void CrossAxisDrag_GoesToTheAncestorClaimingThatAxis()
        {
            var (canvas, input) = Create();
            var child = new AxisElement(DragAxes.Horizontal) { Width = 100, Height = 50 };
            var parent = Place(new AxisBorder(DragAxes.Vertical) { Child = child }, 0, 0, 200, 200);
            canvas.Add(parent);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(12, 40)));

            Assert.Empty(child.Log);
            Assert.Equal(new[] { "start", "move" }, parent.Log);
        }

        [Fact]
        public void NoMainAxisClaimant_FallsBackToTheInnermostClaimant()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.Horizontal), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.MouseLeft, new Point(10, 10), new Point(14, 40)));

            Assert.Equal("start 10,10", element.Log[0]);
        }

        [Fact]
        public void ElementsThatClaimNothing_ReceiveNoDrag()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.None), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 10)));
            input.Events.Gestures.RaiseDragCompleted(Drag(PointerKind.Touch, new Point(10, 10), new Point(60, 10)));

            Assert.Empty(element.Log);
        }

        [Fact]
        public void CompletedDrag_FlingsThenEnds_OnTheOwnerOnly()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.Both), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(10, 10)));
            input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));
            input.Events.Gestures.RaiseDragCompleted(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));

            Assert.Equal(new[] { "start 10,10", "move 30,10", "fling", "end" }, element.Log);
        }

        [Fact]
        public void CanceledDrag_EndsWithoutFling()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.Both), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(10, 10)));
            input.Events.Gestures.RaiseDragCanceled(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));

            Assert.Equal(new[] { "start 10,10", "end" }, element.Log);
        }

        [Fact]
        public void OwnerDetachedMidDrag_ReceivesNothingMore()
        {
            var (canvas, input) = Create();
            var element = Place(new AxisElement(DragAxes.Both), 0, 0, 100, 50);
            canvas.Add(element);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(10, 10)));
            canvas.Remove(element);
            input.Events.Gestures.RaiseDragMoved(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));
            input.Events.Gestures.RaiseDragCompleted(Drag(PointerKind.Touch, new Point(10, 10), new Point(30, 10)));

            Assert.Equal(new[] { "start 10,10" }, element.Log);
        }

        [Fact]
        public void TouchDragDrop_StartsOnlyAfterAHold()
        {
            var (canvas, input) = Create();
            var source = Place(new PreviewSource(), 0, 0, 100, 100);
            canvas.Add(source);
            canvas.Render();

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 10)));
            input.Events.Gestures.RaiseDragCanceled(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 10)));
            Assert.Empty(canvas.Overlays);

            input.Events.Gestures.RaiseDragStarted(Drag(PointerKind.Touch, new Point(10, 10), new Point(40, 10), fromHold: true));
            Assert.Single(canvas.Overlays);
        }

        [Fact]
        public void BuiltInControls_ClaimTheirAxes_AndNeverMiddleMouse()
        {
            var touch = new DragClaimContext(PointerKind.Touch, Point.Empty, new Vector2(10, 0), false);
            var middle = touch with { Kind = PointerKind.MouseMiddle };

            Assert.Equal(DragAxes.Horizontal, new Slider().GetDragAxes(touch));
            Assert.Equal(DragAxes.None, new Slider().GetDragAxes(middle));
            Assert.Equal(DragAxes.Both, new HsvSquare().GetDragAxes(touch));
            Assert.Equal(DragAxes.None, new HsvSquare().GetDragAxes(middle));
        }

        private sealed class PreviewSource : UIElement, IDragSource
        {
            public bool TryBeginDrag(Point screenPoint, out object? payload, out UIElement? preview)
            {
                payload = new object();
                preview = new UIElement();
                return true;
            }
        }
    }
}
```

(Check `IDragSource`'s exact signature against `DragDropTests.FakeDragSource` and match it. Check that `HsvSquare`
has a public parameterless constructor; if it doesn't, construct it the way `HsvSquareTests` does.)

In `CanvasEventBubblingTests`, give the drag-recording element a claim. Add to its nested class:

```csharp
            protected internal override DragAxes GetDragAxes(in DragClaimContext context) => DragAxes.Both;
```

- [ ] **Step 2: Run them to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CanvasDragOwnershipTests"`
Expected: build FAILS (`DragAxes`, `DragClaimContext` and `GetDragAxes` don't exist).

- [ ] **Step 3: Add the types and the `UIElement` hooks**

`sources/IcyUI/UI/DragAxes.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Specifies the axes an element wants to own a drag along (see <see cref="UIElement.GetDragAxes(in DragClaimContext)"/>).
    /// </summary>
    [Flags]
    public enum DragAxes
    {
        /// <summary>
        /// The element doesn't want the drag.
        /// </summary>
        None = 0,

        /// <summary>
        /// The element wants drags along its horizontal axis.
        /// </summary>
        Horizontal = 1,

        /// <summary>
        /// The element wants drags along its vertical axis.
        /// </summary>
        Vertical = 2,

        /// <summary>
        /// The element wants drags along both axes.
        /// </summary>
        Both = Horizontal | Vertical,
    }
}
```

`sources/IcyUI/UI/DragClaimContext.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using System.Numerics;
using Icy.Input.Gestures;

namespace Icy.UI
{
    /// <summary>
    /// Describes a starting drag to an element deciding whether to claim it (see <see cref="UIElement.GetDragAxes(in DragClaimContext)"/>).
    /// </summary>
    /// <param name="Kind">The pointer that drags.</param>
    /// <param name="ScreenStart">The press position, in physical pixels - lets an element claim only part of itself.</param>
    /// <param name="LocalDirection">The movement that crossed the drag threshold, mapped into the element's own space.</param>
    /// <param name="StartedFromHold">Whether the drag grew out of a press-and-hold.</param>
    public readonly record struct DragClaimContext(PointerKind Kind, Point ScreenStart, Vector2 LocalDirection, bool StartedFromHold);
}
```

In `UIElement.cs`:

1. Directly **before** `OnDragEnded`, add:

```csharp
        /// <summary>
        /// Returns the axes along which this element wants to own a drag that starts on it or a descendant.
        /// </summary>
        /// <param name="context">The starting drag.</param>
        /// <returns>The claimed axes; the base implementation returns <see cref="DragAxes.None"/>.</returns>
        /// <remarks>
        /// <para>
        /// When a drag starts, <see cref="UI.Canvas"/> asks the hit element and each ancestor in turn. The innermost element
        /// whose answer contains the drag's main axis (the larger component of
        /// <see cref="DragClaimContext.LocalDirection"/>) owns the gesture. If none does, the innermost element with any
        /// answer owns it. Only the owner receives <see cref="OnDragStarted"/>, <see cref="OnDragPerforming"/>,
        /// <see cref="OnDragFling"/> and <see cref="OnDragEnded"/>.
        /// </para>
        /// <para>
        /// An element that handles drags must override this method: overriding <see cref="OnDragStarted"/> alone receives nothing.
        /// </para>
        /// </remarks>
        protected internal virtual DragAxes GetDragAxes(in DragClaimContext context) => DragAxes.None;
```

2. Directly **after** `OnDragEnded`, add:

```csharp
        /// <summary>
        /// Invoked by <see cref="UI.Canvas"/> on the owner of a drag that completed (was released), right before
        /// <see cref="OnDragEnded"/>. It isn't invoked when the drag is canceled. The base implementation does nothing.
        /// </summary>
        /// <param name="screenVelocity">The release velocity, in physical pixels per second.</param>
        protected internal virtual void OnDragFling(Vector2 screenVelocity)
        {
        }
```

3. Update the `<summary>` of `OnDragEnded`, `OnDragPerforming` and `OnDragStarted`. Replace "that started on this
   element (or a descendant)" / "starts on this element or a descendant" with "owned by this element (see
   <see cref=\"GetDragAxes(in DragClaimContext)\"/>)". `OnDragPerforming`'s "every frame" becomes "whenever the pointer
   moves".

4. Directly **before** `GetVisualChildren`, add:

```csharp
        /// <summary>
        /// Gets a value indicating whether <see cref="HitTest(Vector2)"/> descends into this element's children.
        /// </summary>
        /// <returns><see langword="true"/> by default. Returning <see langword="false"/> makes the element itself the hit target
        /// for every point inside it - e.g. a scrolling list during a fling, so the press that stops it doesn't reach an item.</returns>
        protected virtual bool CanHitTestChildren() => true;
```

5. In `HitTest(Vector2)`, wrap the child loop: `if (CanHitTestChildren())` around the existing `foreach`.

- [ ] **Step 4: Route drags from gestures in `Canvas`**

In `Canvas.cs`:
- Add `using Icy.Input.Gestures;`.
- Rename the field `draggedElement` to `dragOwner`.
- In `EnsureInputRoutingInitialized`, replace the four `events.Drag.*` subscriptions with:

```csharp
            events.Gestures.DragStarted += OnGestureDragStarted;
            events.Gestures.DragMoved += OnGestureDragMoved;
            events.Gestures.DragCompleted += OnGestureDragCompleted;
            events.Gestures.DragCanceled += OnGestureDragCanceled;
```

Delete the old `OnDragStarted`, `OnDragPerforming`, `OnDragEnded` and `OnDragCanceled` handlers, and add (keeping
private-method order):

```csharp
        private UIElement? LiveDragOwner => dragOwner is { } owner && ReferenceEquals(owner.Canvas, this) ? owner : null;
```

(Place this private property with the other private members, before the methods, following StyleCop's
properties-before-methods rule.)

```csharp
        private UIElement? ResolveDragOwner(UIElement? hit, in DragInfo drag)
        {
            UIElement? fallback = null;
            foreach (UIElement element in SelfAndAncestors(hit))
            {
                Vector2 direction = element.PointToLocal(drag.Position) - element.PointToLocal(drag.Start);
                DragAxes axes = element.GetDragAxes(new DragClaimContext(drag.Kind, drag.Start, direction, drag.StartedFromHold));
                if (axes == DragAxes.None)
                    continue;

                DragAxes main = MathF.Abs(direction.X) >= MathF.Abs(direction.Y) ? DragAxes.Horizontal : DragAxes.Vertical;
                if ((axes & main) != 0)
                    return element;
                fallback ??= element;
            }

            return fallback;
        }

        private void OnGestureDragStarted(object? sender, AcceptableEventArgs<DragInfo> e)
        {
            DragInfo drag = e.Data;
            UIElement? hit = HitTest(drag.Start);
            dragOwner = ResolveDragOwner(hit, drag);
            if (dragOwner != null)
            {
                dragOwner.OnDragStarted(drag.Start);

                // The gesture already moved past the drag threshold: let the owner catch up to the pointer this frame.
                if (drag.Position != drag.Start)
                    dragOwner.OnDragPerforming(drag.Position);
            }

            // Touch picks up drag-and-drop sources only after a press-and-hold (a plain finger drag scrolls); the left
            // mouse starts it immediately; middle-mouse drags only pan.
            bool dragDropAllowed = drag.Kind == PointerKind.MouseLeft || (drag.Kind == PointerKind.Touch && drag.StartedFromHold);
            if (!dragDropAllowed)
                return;

            foreach (UIElement element in SelfAndAncestors(hit))
            {
                if (element is IDragSource source && source.TryBeginDrag(drag.Start, out object? payload, out UIElement? preview))
                {
                    // TryBeginDrag's own contract requires a non-null payload on a true return.
                    dragDropSession = new DragDropSession(element, payload!, drag.Start) { Preview = preview };
                    if (preview != null)
                    {
                        // The ghost visual must never be a drop target itself (see the original comment in the old
                        // OnDragStarted - keep it verbatim when moving this code).
                        preview.IsHitTestVisible = false;
                        AddOverlay(preview);
                        PositionOverlayAtScreenPoint(preview, drag.Start);
                    }

                    break;
                }
            }
        }

        private void OnGestureDragMoved(object? sender, GenericEventArgs<DragInfo> e)
        {
            Point position = e.Data.Position;
            LiveDragOwner?.OnDragPerforming(position);

            if (dragDropSession is { } session)
            {
                session.ScreenPoint = position;
                if (session.Preview != null)
                    PositionOverlayAtScreenPoint(session.Preview, position);
                UpdateDragDropTarget(session, position);
            }
        }

        private void OnGestureDragCompleted(object? sender, GenericEventArgs<DragInfo> e)
        {
            Point position = e.Data.Position;
            if (LiveDragOwner is { } owner)
            {
                owner.OnDragFling(e.Data.Velocity);
                owner.OnDragEnded(position);
            }

            dragOwner = null;

            if (dragDropSession is { } session)
            {
                session.ScreenPoint = position;
                if (session.CurrentTarget is { } target && target.CanDrop(session))
                    target.OnDrop(session);
                if (session.Preview != null)
                    RemoveOverlay(session.Preview);
                dragDropSession = null;
            }
        }

        private void OnGestureDragCanceled(object? sender, GenericEventArgs<DragInfo> e)
        {
            // Release the owner like a normal end, so it drops any capture - but never complete a drag-drop: the payload
            // must not land on whatever target happens to be under the finger.
            LiveDragOwner?.OnDragEnded(e.Data.Position);
            dragOwner = null;

            if (dragDropSession is { } session)
            {
                session.CurrentTarget?.OnDragLeave(session);
                if (session.Preview != null)
                    RemoveOverlay(session.Preview);
                dragDropSession = null;
            }
        }
```

When moving the drag-drop branch, keep the old `OnDragStarted`'s long comment about the preview and inclusive
hit-testing verbatim in place of the short one above.

- [ ] **Step 5: Claims on the built-in controls**

`Slider.cs`, before its `OnDragStarted` override:

```csharp
        /// <inheritdoc/>
        /// <remarks>A <see cref="Slider"/> is horizontal: it claims horizontal touch and left-mouse drags.</remarks>
        protected internal override DragAxes GetDragAxes(in DragClaimContext context) =>
            context.Kind is PointerKind.Touch or PointerKind.MouseLeft ? DragAxes.Horizontal : DragAxes.None;
```

`HsvSquare.cs`, before its `OnDragStarted` override:

```csharp
        /// <inheritdoc/>
        protected internal override DragAxes GetDragAxes(in DragClaimContext context) =>
            context.Kind is PointerKind.Touch or PointerKind.MouseLeft ? DragAxes.Both : DragAxes.None;
```

`SplitPane.cs`, before its `OnDragEnded` override:

```csharp
        /// <inheritdoc/>
        /// <remarks>Claims only drags that start on the divider, along the axis the divider moves.</remarks>
        protected internal override DragAxes GetDragAxes(in DragClaimContext context)
        {
            if (context.Kind is not (PointerKind.Touch or PointerKind.MouseLeft) || !IsPointOnDivider(context.ScreenStart))
                return DragAxes.None;
            return Orientation == Orientation.Horizontal ? DragAxes.Horizontal : DragAxes.Vertical;
        }
```

Add `using Icy.Input.Gestures;` to all three files. `SplitPane.OnDragStarted`'s remark about Canvas "bubbling a drag
gesture to every ancestor" becomes outdated: change it to "`GetDragAxes` already limits ownership to divider presses;
the check stays as a guard."

- [ ] **Step 6: Forward the drag fakes to gestures**

In `FakeInputSystem.cs`, construct `Gestures` **before** `Drag` in `FakeInputEventSystem`'s constructor, and pass it:
`Drag = new FakeDragEvents(inputSystem, Gestures);`. Change `FakeDragEvents`:

```csharp
    /// <summary>
    /// A fake <see cref="IDragEvents"/> that lets tests synthesize drag-sequence payloads directly. Each raise is also forwarded
    /// to <see cref="FakeGestureEvents"/> as a <see cref="PointerKind.MouseLeft"/> gesture, which is what <see cref="Canvas"/> routes.
    /// </summary>
    public sealed class FakeDragEvents(IInputSystem inputSystem, FakeGestureEvents gestures) : FakeInputEventProviderBase(inputSystem), IDragEvents
    {
        private Point start;
        private Point last;

        public event EventHandler<AcceptableEventArgs<Point>>? DragStarted;

        public event EventHandler<GenericEventArgs<Point>>? DragPerforming;

        public event EventHandler<GenericEventArgs<Point>>? DragEnded;

        public event EventHandler<GenericEventArgs<Point>>? DragCanceled;

        public void RaiseDragCanceled(Point point)
        {
            DragCanceled?.Invoke(this, new GenericEventArgs<Point>(point));
            gestures.RaiseDragCanceled(new DragInfo(PointerKind.MouseLeft, start, point, Vector2.Zero, Vector2.Zero));
        }

        public void RaiseDragEnded(Point point)
        {
            DragEnded?.Invoke(this, new GenericEventArgs<Point>(point));
            gestures.RaiseDragCompleted(new DragInfo(PointerKind.MouseLeft, start, point, Vector2.Zero, Vector2.Zero));
        }

        public void RaiseDragPerforming(Point point)
        {
            DragPerforming?.Invoke(this, new GenericEventArgs<Point>(point));
            gestures.RaiseDragMoved(new DragInfo(PointerKind.MouseLeft, start, point, new Vector2(point.X - last.X, point.Y - last.Y), Vector2.Zero));
            last = point;
        }

        public void RaiseDragStarted(Point point)
        {
            start = last = point;
            DragStarted?.Invoke(this, new AcceptableEventArgs<Point> { Data = point });
            gestures.RaiseDragStarted(new DragInfo(PointerKind.MouseLeft, point, point, Vector2.Zero, Vector2.Zero));
        }
    }
```

(Keep the existing member order and docs style of the file. If `FakeDragEvents` had other members, keep them.)

- [ ] **Step 7: Run the tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: all pass. `Total:` = 1022 + 9 = **1031**. The existing `SliderTests`, `SplitPaneTests`, `DragDropTests` and
`CanvasEventBubblingTests` pass unchanged apart from the one claim override.

If a `SplitPaneTests` case fails because it drags from a point `IsPointOnDivider` rejects, read the test. If the test
expects a resize from a non-divider press, it pinned the old broadcast. Record a ruling and change its expectation to the
spec (only divider presses resize).

- [ ] **Step 8: Build and commit**

Build with no new warnings (≤ 82).

```bash
git add sources/IcyUI/UI sources/IcyUI.Tests
git commit -m "Route each drag to a single, direction-locked owner" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `ScrollViewer` claims and 1:1 panning

**Files:**
- Create: `sources/IcyUI/UI/Controls/PanningMode.cs`
- Modify: `sources/IcyUI/UI/Controls/ScrollViewer.cs`
- Test: `sources/IcyUI.Tests/Controls/ScrollViewerPanningTests.cs`

**Interfaces:**
- Consumes: `GetDragAxes`, `DragClaimContext`, `DragAxes`, the Canvas routing (Task 2).
- Produces: `public enum PanningMode { Auto, None, Vertical, Horizontal, Both }` and `ScrollViewer.PanningMode`
  (default `Auto`).

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Controls/ScrollViewerPanningTests.cs`:

```csharp
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.Tests.UI;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ScrollViewerPanningTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) Create(float displayScale = 1f)
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext { DisplayScale = displayScale }, new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            return (canvas, input);
        }

        // A 200x200 viewport at the canvas origin over a 200x1000 column: the top 50 units are an AxisElement claiming
        // Horizontal (a slider stand-in).
        private static (ScrollViewer List, AxisElement Slider) AddList(Canvas canvas)
        {
            var slider = new AxisElement(DragAxes.Horizontal) { Height = 50 };
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(slider);
            column.Children.Add(new UIElement { Height = 950 });
            var list = new ScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = column };
            canvas.Add(list);
            canvas.Render();
            return (list, slider);
        }

        private static DragInfo Drag(PointerKind kind, Point start, Point position) =>
            new(kind, start, position, new Vector2(position.X - start.X, position.Y - start.Y), Vector2.Zero);

        private static void Pan(FakeInputSystem input, PointerKind kind, Point start, Point crossing, Point end)
        {
            input.Events.Gestures.RaiseDragStarted(Drag(kind, start, crossing));
            input.Events.Gestures.RaiseDragMoved(Drag(kind, start, end));
            input.Events.Gestures.RaiseDragCanceled(Drag(kind, start, end)); // end without a fling
        }

        [Fact]
        public void VerticalSwipeOnASlider_ScrollsTheList()
        {
            var (canvas, input) = Create();
            var (list, slider) = AddList(canvas);

            Pan(input, PointerKind.Touch, new Point(100, 40), new Point(100, 25), new Point(100, 5));

            Assert.Equal(20, list.VerticalOffset, 0.5);
            Assert.Empty(slider.Log);
        }

        [Fact]
        public void HorizontalSwipeOnASlider_MovesTheSlider_NotTheList()
        {
            var (canvas, input) = Create();
            var (list, slider) = AddList(canvas);

            Pan(input, PointerKind.Touch, new Point(50, 25), new Point(70, 25), new Point(120, 25));

            Assert.Equal(0, list.VerticalOffset);
            Assert.Equal("start 50,25", slider.Log[0]);
        }

        [Fact]
        public void SliderInListAtEdge_FallsBackToTheSlider()
        {
            var (canvas, input) = Create();
            var (list, slider) = AddList(canvas);

            // Finger moves down at the top edge: the list can't scroll that way, so it doesn't claim.
            Pan(input, PointerKind.Touch, new Point(100, 10), new Point(102, 30), new Point(104, 45));

            Assert.Equal(0, list.VerticalOffset);
            Assert.Equal("start 100,10", slider.Log[0]);
        }

        [Fact]
        public void InnerListAtItsEdge_PassesTheGestureToTheOuterList()
        {
            var (canvas, input) = Create();
            var inner = new ScrollViewer { Height = 100, Content = new UIElement { Height = 300 } };
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(inner);
            column.Children.Add(new UIElement { Height = 900 });
            var outer = new ScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = column };
            canvas.Add(outer);
            canvas.Render();
            inner.VerticalOffset = 200; // the inner list is at its bottom

            Pan(input, PointerKind.Touch, new Point(100, 80), new Point(100, 60), new Point(100, 30));

            Assert.Equal(200, inner.VerticalOffset);
            Assert.Equal(30, outer.VerticalOffset, 0.5);
        }

        [Fact]
        public void PanningModeNone_DoesNotPan()
        {
            var (canvas, input) = Create();
            var (list, _) = AddList(canvas);
            list.PanningMode = PanningMode.None;

            Pan(input, PointerKind.Touch, new Point(100, 150), new Point(100, 130), new Point(100, 100));

            Assert.Equal(0, list.VerticalOffset);
        }

        [Fact]
        public void AutoWithoutOverflow_ClaimsNothing()
        {
            var list = new ScrollViewer { Width = 200, Height = 200, Content = new UIElement { Height = 100 } };
            list.Arrange(new Rectangle(0, 0, 200, 200));

            Assert.Equal(DragAxes.None, list.GetDragAxes(new DragClaimContext(PointerKind.Touch, Point.Empty, new Vector2(0, -20), false)));
        }

        [Fact]
        public void LeftMouse_NeverPans_MiddleMouseDoes()
        {
            var (canvas, input) = Create();
            var (list, _) = AddList(canvas);

            Pan(input, PointerKind.MouseLeft, new Point(100, 150), new Point(100, 130), new Point(100, 100));
            Assert.Equal(0, list.VerticalOffset);

            Pan(input, PointerKind.MouseMiddle, new Point(100, 150), new Point(100, 130), new Point(100, 100));
            Assert.Equal(30, list.VerticalOffset, 0.5);
        }

        [Fact]
        public void Pan_IsOneToOne_AtDisplayScale2()
        {
            var (canvas, input) = Create(displayScale: 2f);
            var (list, _) = AddList(canvas);

            // Physical pixels: the 200-unit list spans 400 px. 150 px of finger travel = 75 units.
            Pan(input, PointerKind.Touch, new Point(200, 300), new Point(200, 250), new Point(200, 100));

            Assert.Equal(75, list.VerticalOffset, 0.5);
        }

        [Fact]
        public void Pan_IsOneToOne_UnderACanvasZoom()
        {
            var (canvas, input) = Create();
            canvas.Scale = new Vector2(2, 2);
            var (list, _) = AddList(canvas);

            Pan(input, PointerKind.Touch, new Point(200, 300), new Point(200, 250), new Point(200, 100));

            Assert.Equal(75, list.VerticalOffset, 0.5);
        }
    }
}
```

- [ ] **Step 2: Run them to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ScrollViewerPanningTests"`
Expected: build FAILS (`PanningMode` doesn't exist).

- [ ] **Step 3: Implement**

`sources/IcyUI/UI/Controls/PanningMode.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// Specifies along which axes a <see cref="ScrollViewer"/> pans with touch and the middle mouse button.
    /// </summary>
    public enum PanningMode
    {
        /// <summary>
        /// Pans along the axes whose content extent exceeds the viewport.
        /// </summary>
        Auto,

        /// <summary>
        /// Never pans; drags go to the next scroll area out.
        /// </summary>
        None,

        /// <summary>
        /// Pans vertically only.
        /// </summary>
        Vertical,

        /// <summary>
        /// Pans horizontally only.
        /// </summary>
        Horizontal,

        /// <summary>
        /// Pans along both axes.
        /// </summary>
        Both,
    }
}
```

In `ScrollViewer.cs`, add `using Icy.Input.Gestures;` and the fields:

```csharp
        private Vector2? panAnchor;
        private DragAxes panAxes;
        private PanningMode panningMode = PanningMode.Auto;
        private DragAxes pendingClaim;
```

(Keep fields alphabetical with the existing ones.) Add the property after `HorizontalOffset`:

```csharp
        /// <summary>
        /// Gets or sets the axes this <see cref="ScrollViewer"/> pans along with touch and the middle mouse button.
        /// Defaults to <see cref="Controls.PanningMode.Auto"/>.
        /// </summary>
        /// <remarks>
        /// A pan claims a drag only along axes where the content can still move in the drag's direction, so a list that is
        /// already at its edge lets the gesture reach the next scroll area out. The left mouse button never pans.
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(PanningMode.Auto)]
        [RegisterReference]
        public PanningMode PanningMode
        {
            get => panningMode;
            set => SetProperty(ref panningMode, value);
        }
```

Add the overrides after `OnScroll`:

```csharp
        /// <inheritdoc/>
        protected internal override DragAxes GetDragAxes(in DragClaimContext context)
        {
            pendingClaim = DragAxes.None;
            if (context.Kind is not (PointerKind.Touch or PointerKind.MouseMiddle))
                return DragAxes.None;

            DragAxes allowed = PanningMode switch
            {
                PanningMode.None => DragAxes.None,
                PanningMode.Vertical => DragAxes.Vertical,
                PanningMode.Horizontal => DragAxes.Horizontal,
                PanningMode.Both => DragAxes.Both,
                _ => (ExtentWidth > ViewportWidth ? DragAxes.Horizontal : DragAxes.None) | (ExtentHeight > ViewportHeight ? DragAxes.Vertical : DragAxes.None),
            };

            // Content follows the finger, so the offset moves opposite to the drag direction.
            if (allowed.HasFlag(DragAxes.Horizontal) && CanMove(horizontalOffset, ExtentWidth - ViewportWidth, -context.LocalDirection.X))
                pendingClaim |= DragAxes.Horizontal;
            if (allowed.HasFlag(DragAxes.Vertical) && CanMove(verticalOffset, ExtentHeight - ViewportHeight, -context.LocalDirection.Y))
                pendingClaim |= DragAxes.Vertical;
            return pendingClaim;
        }

        /// <inheritdoc/>
        protected internal override void OnDragStarted(Point screenPoint)
        {
            base.OnDragStarted(screenPoint);
            panAxes = pendingClaim;
            panAnchor = null;
        }

        /// <inheritdoc/>
        protected internal override void OnDragPerforming(Point screenPoint)
        {
            base.OnDragPerforming(screenPoint);
            Vector2 local = PointToLocal(screenPoint);

            // The first position only anchors the pan, so the content doesn't jump by the drag threshold.
            if (panAnchor is { } anchor)
            {
                Vector2 moved = local - anchor;
                if (panAxes.HasFlag(DragAxes.Horizontal))
                    HorizontalOffset -= moved.X;
                if (panAxes.HasFlag(DragAxes.Vertical))
                    VerticalOffset -= moved.Y;
            }

            panAnchor = local;
        }

        /// <inheritdoc/>
        protected internal override void OnDragEnded(Point screenPoint)
        {
            base.OnDragEnded(screenPoint);
            panAnchor = null;
            panAxes = DragAxes.None;
        }
```

and the private helper with the other private methods:

```csharp
        private static bool CanMove(float offset, float maxOffset, float offsetDirection)
        {
            if (maxOffset <= 0)
                return false;
            if (offsetDirection > 0)
                return offset < maxOffset;
            if (offsetDirection < 0)
                return offset > 0;
            return true;
        }
```

- [ ] **Step 4: Run the tests**

Run the Step 2 filter. Expected: 9 PASS. Then the full suite. Expected: `Total:` **1040**, all passing.

If `VerticalSwipeOnASlider_ScrollsTheList` reports 25 instead of 20, the start-frame catch-up moved the content by the
slop. Check that `OnDragPerforming`'s first call only sets `panAnchor`.

- [ ] **Step 5: Build and commit**

Build with no new warnings (≤ 82).

```bash
git add sources/IcyUI/UI/Controls/PanningMode.cs sources/IcyUI/UI/Controls/ScrollViewer.cs sources/IcyUI.Tests/Controls/ScrollViewerPanningTests.cs
git commit -m "Pan ScrollViewer 1:1 with touch and the middle mouse button" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Inertia

**Files:**
- Create: `sources/IcyUI/UI/Controls/InertiaDriver.cs`
- Modify: `sources/IcyUI/UI/Controls/ScrollViewer.cs`
- Test: `sources/IcyUI.Tests/Controls/ScrollViewerInertiaTests.cs`

**Interfaces:**
- Consumes: `IFrameTicker`, `Dispatcher.RegisterFrameTicker` (Task 1); `OnDragFling`, `CanHitTestChildren` (Task 2);
  panning (Task 3).
- Produces: `ScrollViewer.PanningDeceleration` (default `1500`), `ScrollViewer.StopInertia()`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Controls/ScrollViewerInertiaTests.cs`:

```csharp
using System.Drawing;
using System.Numerics;
using Icy.Assets;
using Icy.Configuration;
using Icy.Data.Markup;
using Icy.Input.Events;
using Icy.Input.Gestures;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ScrollViewerInertiaTests
    {
        private sealed class TapRecorder : UIElement
        {
            public int Taps { get; private set; }

            protected internal override void OnTap() => Taps++;
        }

        private sealed class TestScrollViewer : ScrollViewer
        {
            public bool RaiseScroll(ScrollInfo info) => OnScroll(info);
        }

        private static (Canvas Canvas, FakeInputSystem Input, TestScrollViewer List, TapRecorder Item) Create(int contentHeight = 5000)
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var item = new TapRecorder { Height = 100 };
            var column = new StackPanel { Orientation = Orientation.Vertical };
            column.Children.Add(item);
            column.Children.Add(new UIElement { Height = contentHeight - 100 });
            var list = new TestScrollViewer { Width = 200, Height = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = column };
            canvas.Add(list);
            canvas.Render();
            return (canvas, input, list, item);
        }

        // An upward touch flick released at velocity (0, vy) px/s: content scrolls down (offset increases).
        private static void Flick(FakeInputSystem input, float vy)
        {
            var start = new Point(100, 150);
            var end = new Point(100, 100);
            input.Events.Gestures.RaiseDragStarted(new DragInfo(PointerKind.Touch, start, new Point(100, 130), new Vector2(0, -20), Vector2.Zero));
            input.Events.Gestures.RaiseDragMoved(new DragInfo(PointerKind.Touch, start, end, new Vector2(0, -30), Vector2.Zero));
            input.Events.Gestures.RaiseDragCompleted(new DragInfo(PointerKind.Touch, start, end, Vector2.Zero, new Vector2(0, vy)));
        }

        private static void Advance(TimeSpan time) => Dispatcher.GetCurrentThreadDispatcher().UpdateAnimations(time);

        [Fact]
        public void Fling_CoastsWithConstantDeceleration()
        {
            var (_, input, list, _) = Create();
            Flick(input, -1000);
            float afterPan = list.VerticalOffset; // 30

            // v = 1000, d = 1500: T = 0.667 s, distance = 333.3. Halfway through T the eased position is 75 %.
            Advance(TimeSpan.FromSeconds(1d / 3d));
            Assert.Equal(afterPan + 250, list.VerticalOffset, 1);

            Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(afterPan + 333.3f, list.VerticalOffset, 1);
        }

        [Fact]
        public void Fling_StopsHardAtTheEdge()
        {
            var (_, input, list, _) = Create(contentHeight: 300); // max offset 100
            Flick(input, -3000);

            Advance(TimeSpan.FromSeconds(3));

            Assert.Equal(100, list.VerticalOffset);
        }

        [Theory]
        [InlineData(0f, -1000f)]  // inertia disabled
        [InlineData(1500f, -40f)] // below the 50 units/s threshold
        public void NoFling_WhenDisabledOrTooSlow(float deceleration, float vy)
        {
            var (_, input, list, _) = Create();
            list.PanningDeceleration = deceleration;
            Flick(input, vy);
            float afterPan = list.VerticalOffset;

            Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(afterPan, list.VerticalOffset);
        }

        [Fact]
        public void PressDuringFling_StopsIt_AndSwallowsOnlyThatTap()
        {
            var (canvas, input, list, item) = Create();
            list.VerticalOffset = 0;
            Flick(input, -1000);
            Advance(TimeSpan.FromMilliseconds(50));
            list.VerticalOffset = 0; // bring the item back under the finger while the fling still runs

            input.Events.Touch.RaiseTouchDown(new Point(100, 50));
            float stoppedAt = list.VerticalOffset;
            input.Events.Touch.RaiseTouchUp(new Point(100, 50));
            input.Events.Touch.RaiseTap(new TouchInfo(new Point(100, 50), 1));
            Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(stoppedAt, list.VerticalOffset);
            Assert.Equal(0, item.Taps);

            canvas.Render(); // end of frame: children are hit-testable again
            input.Events.Touch.RaiseTap(new TouchInfo(new Point(100, 50), 1));
            Assert.Equal(1, item.Taps);
        }

        [Fact]
        public void WheelScroll_StopsTheFling()
        {
            var (_, input, list, _) = Create();
            Flick(input, -1000);
            list.RaiseScroll(new ScrollInfo(-10, Orientation.Vertical));
            float afterWheel = list.VerticalOffset;

            Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(afterWheel, list.VerticalOffset);
        }

        [Fact]
        public void OffsetCorrectionDuringFling_IsKept()
        {
            var (_, input, list, _) = Create();
            Flick(input, -1000);
            float afterPan = list.VerticalOffset;
            Advance(TimeSpan.FromMilliseconds(100));

            list.VerticalOffset += 50; // what a virtualized list's height correction does mid-flight
            Advance(TimeSpan.FromSeconds(1));

            Assert.Equal(afterPan + 333.3f + 50, list.VerticalOffset, 1);
        }

        [Fact]
        public void StopInertia_And_PanningModeChange_StopTheFling()
        {
            var (_, input, list, _) = Create();
            Flick(input, -1000);
            list.StopInertia();
            float stopped = list.VerticalOffset;
            Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(stopped, list.VerticalOffset);

            Flick(input, -1000);
            list.PanningMode = PanningMode.Vertical;
            stopped = list.VerticalOffset;
            Advance(TimeSpan.FromSeconds(1));
            Assert.Equal(stopped, list.VerticalOffset);
        }

        [Theory]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void PanningDeceleration_RejectsInvalidValues(float value) =>
            Assert.ThrowsAny<ArgumentException>(() => new ScrollViewer().PanningDeceleration = value);

        [Fact]
        public void FlingAcrossAVirtualizedList_RealizesTheRowsItPassesOver()
        {
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            var template = (Icy.UI.Styles.DataTemplate)new Icy.Markup.MarkupLoader(configuration).LoadObject("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var items = new ItemsControl { ItemsSource = Enumerable.Range(0, 1000).Cast<object>().ToList(), ItemTemplate = template };
            var list = new ScrollViewer { Width = 300, Height = 400, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Content = items };
            canvas.Add(list);
            canvas.Render();

            Flick(input, -4000);
            Advance(TimeSpan.FromSeconds(3));
            canvas.Render();

            Assert.True(list.VerticalOffset > 1000, $"offset {list.VerticalOffset}");
            int firstVisible = (int)(list.VerticalOffset / 40);
            var realized = ((Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(items)!).Keys;
            Assert.Contains(firstVisible, realized);
            Assert.DoesNotContain(0, realized);
        }
    }
}
```

(The 40 px `Border` template matches `ItemsControlScrollViewerIntegrationTests`. Don't use the default `ToString` template
here: its `TextBlock` measures 0 tall without a font system, so the list wouldn't overflow and wouldn't claim the gesture.)

- [ ] **Step 2: Run them to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ScrollViewerInertiaTests"`
Expected: build FAILS (`PanningDeceleration` and `StopInertia` don't exist).

- [ ] **Step 3: Add the inertia driver**

`sources/IcyUI/UI/Controls/InertiaDriver.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Animations;
using Icy.Data.Markup;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Coasts a distance over a duration with constant deceleration (an <see cref="Easing.EaseOutQuad"/> curve), applying each
    /// frame's step as a relative delta.
    /// </summary>
    /// <remarks>
    /// Relative steps keep offset corrections made by others during the coast (e.g. a virtualized list's height corrections).
    /// The driver stops by itself at the end, or as soon as a non-zero step no longer moves anything (the edge).
    /// </remarks>
    /// <param name="apply">Applies one step; returns whether anything moved.</param>
    internal sealed class InertiaDriver(Func<Vector2, bool> apply) : IFrameTicker
    {
        private Dispatcher? dispatcher;
        private Vector2 distance;
        private TimeSpan duration;
        private TimeSpan elapsed;
        private float lastEased;

        /// <summary>
        /// Gets a value indicating whether a coast is running.
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// Starts a coast, replacing any running one.
        /// </summary>
        /// <param name="distance">The total distance to travel.</param>
        /// <param name="duration">The time the coast takes.</param>
        public void Start(Vector2 distance, TimeSpan duration)
        {
            Stop();
            this.distance = distance;
            this.duration = duration;
            elapsed = TimeSpan.Zero;
            lastEased = 0f;
            IsRunning = true;
            dispatcher = Dispatcher.GetCurrentThreadDispatcher();
            dispatcher.RegisterFrameTicker(this);
        }

        /// <summary>
        /// Stops the running coast, if any.
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
                return;
            IsRunning = false;
            dispatcher?.UnregisterFrameTicker(this);
        }

        /// <inheritdoc/>
        public void Tick(TimeSpan delta)
        {
            if (!IsRunning)
                return;

            elapsed += delta;
            float progress = duration <= TimeSpan.Zero ? 1f : MathF.Min(1f, (float)(elapsed / duration));
            float eased = Easing.EaseOutQuad(progress);
            Vector2 step = distance * (eased - lastEased);
            lastEased = eased;

            bool moved = apply(step);
            if (progress >= 1f || (!moved && step != Vector2.Zero))
                Stop();
        }
    }
}
```

- [ ] **Step 4: Add inertia to `ScrollViewer`**

Add `using Icy.Data.Markup;` and `using Icy.UI.Styles;`. Add constants and fields:

```csharp
        private const float MaxFlingSpeed = 8000f;
        private const float MinFlingSpeed = 50f;
        private readonly InertiaDriver inertia;
        private float panningDeceleration = 1500f;
        private Point lastDragScreenPoint;
        private bool swallowTap;
```

In the constructor, after `ClipToBounds = true;`:

```csharp
            inertia = new InertiaDriver(ApplyInertiaStep);

            // A press stops a fling; the rest of that press (including its tap) lands on this list, not on the item underneath.
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ControlState))
                    return;
                if (ControlState.HasFlag(ControlState.Pressed) && inertia.IsRunning)
                {
                    inertia.Stop();
                    swallowTap = true;
                }
                else if (!ControlState.HasFlag(ControlState.Pressed) && swallowTap)
                {
                    // Tapped is raised after the release; re-enable children once this input frame is done.
                    Dispatcher.GetCurrentThreadDispatcher().Invoke(() => swallowTap = false);
                }
            };
```

Add the property after `PanningMode`:

```csharp
        /// <summary>
        /// Gets or sets how quickly a fling slows down, in this control's units per second squared. Defaults to <c>1500</c>;
        /// <c>0</c> disables inertia.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or not finite.</exception>
        [Category("Behavior")]
        [DefaultValue(1500f)]
        [RegisterReference]
        public float PanningDeceleration
        {
            get => panningDeceleration;
            set
            {
                if (!float.IsFinite(value) || value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value), value, "The deceleration must be a finite, non-negative number.");
                SetProperty(ref panningDeceleration, value);
            }
        }
```

Change the `PanningMode` setter to stop a fling: `set { if (SetProperty(ref panningMode, value)) inertia.Stop(); }`.

Add the public method (before the protected overrides):

```csharp
        /// <summary>
        /// Stops a running fling immediately, leaving the offsets where they are.
        /// </summary>
        public void StopInertia() => inertia.Stop();
```

Update the overrides:
- `OnScroll`: call `inertia.Stop();` first.
- `OnDragStarted`: call `inertia.Stop();` and `swallowTap = false;`.
- `OnDragPerforming`: store `lastDragScreenPoint = screenPoint;`.

Add new overrides:

```csharp
        /// <inheritdoc/>
        protected internal override void OnDragFling(Vector2 screenVelocity)
        {
            base.OnDragFling(screenVelocity);
            if (panningDeceleration <= 0)
                return;

            // The linear part of the screen-to-local transform maps the velocity through DPI, canvas zoom and RenderScale.
            Point origin = lastDragScreenPoint;
            Vector2 local = PointToLocal(new Point(origin.X + (int)screenVelocity.X, origin.Y + (int)screenVelocity.Y)) - PointToLocal(origin);
            Vector2 offsetVelocity = new(panAxes.HasFlag(DragAxes.Horizontal) ? -local.X : 0f, panAxes.HasFlag(DragAxes.Vertical) ? -local.Y : 0f);

            float speed = offsetVelocity.Length();
            if (speed < MinFlingSpeed)
                return;
            if (speed > MaxFlingSpeed)
            {
                offsetVelocity *= MaxFlingSpeed / speed;
                speed = MaxFlingSpeed;
            }

            float seconds = speed / panningDeceleration;
            inertia.Start(offsetVelocity * (seconds / 2f), TimeSpan.FromSeconds(seconds));
        }

        /// <inheritdoc/>
        protected override bool CanHitTestChildren() => !inertia.IsRunning && !swallowTap;

        /// <inheritdoc/>
        protected override void OnDetached()
        {
            inertia.Stop();
            base.OnDetached();
        }
```

(If `ContentControl` or `Control` already overrides `OnDetached`, keep calling `base`.) Add the private method:

```csharp
        private bool ApplyInertiaStep(Vector2 step)
        {
            float before = horizontalOffset + verticalOffset;
            HorizontalOffset += step.X;
            VerticalOffset += step.Y;
            return horizontalOffset + verticalOffset != before;
        }
```

- [ ] **Step 5: Run the tests**

Run the Step 2 filter. Expected: all PASS (12 test cases including theory rows). Then the full suite. Expected: `Total:`
**1052**, all passing.

If `PressDuringFling_StopsIt_AndSwallowsOnlyThatTap` reaches the item on the first tap, check the event order: Canvas
`OnTouchUp` clears Pressed (queuing the re-enable) before `OnTap` runs, and the queued action must not run until
`canvas.Render()`.

- [ ] **Step 6: Build and commit**

Build with no new warnings (≤ 82).

```bash
git add sources/IcyUI/UI/Controls/InertiaDriver.cs sources/IcyUI/UI/Controls/ScrollViewer.cs sources/IcyUI.Tests/Controls/ScrollViewerInertiaTests.cs
git commit -m "Add fling inertia to ScrollViewer" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Retire middle-button autoscroll; verification

**Files:**
- Modify: `sources/IcyUI/Input/Events/ScrollEvents.cs`, `sources/IcyUI/Input/Events/IScrollEvents.cs`
- Test: `sources/IcyUI.Tests/Input/ScrollEventsTests.cs` (new)

- [ ] **Step 1: Write the failing test**

```csharp
using System.Drawing;
using Icy.Input.Devices;
using Icy.Input.Events;
using Xunit;

namespace Icy.Tests.Input
{
    public class ScrollEventsTests
    {
        [Fact]
        public void MiddleButtonDrag_NoLongerAutoscrolls()
        {
            // Middle-mouse drags pan scroll areas 1:1 through gestures now (ScrollViewer.PanningMode); the old autoscroll is gone.
            var input = new FakeInputSystem();
            var scroll = new ScrollEvents(input);
            scroll.Initialize();
            int raised = 0;
            scroll.Scroll += (_, _) => raised++;

            input.Mouse.MouseInfo = new MouseInfo(new Point(100, 100));
            input.Mouse.RaiseButtonPressed(MouseButtons.MiddleButton);
            for (int i = 1; i <= 10; i++)
            {
                input.Mouse.MouseInfo = new MouseInfo(new Point(100, 100 + (i * 20)));
                scroll.Update(TimeSpan.FromMilliseconds(50));
            }

            Assert.Equal(0, raised);
        }
    }
}
```

(Check whether `ScrollEvents` is `internal`; `InternalsVisibleTo` covers it either way.)

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ScrollEventsTests"`
Expected: FAIL (autoscroll raises `Scroll`).

- [ ] **Step 2: Remove the autoscroll**

In `ScrollEvents.cs`:
- Delete the fields `delay`, `lastMousePressPosition` and `deltaTranslation`.
- Delete the `else if (lastMousePressPosition != null)` branch of `Update`.
- Delete `Mouse_MouseButtonPressed`, `Mouse_MouseButtonReleased` and `OnEndTouchScroll`, plus their subscriptions in
  `Initialize` and in the device connect/disconnect switches.
- Keep the wheel, gamepad and `DirectionScroll` code, which the gamepad still uses.

Update `IScrollEvents.Scroll`'s remarks: "Raised for the mouse wheel and the gamepad right stick. Touch and middle-mouse
drags pan scroll areas through <see cref=\"Gestures.IGestureEvents\"/> and <see cref=\"UI.Controls.ScrollViewer.PanningMode\"/>."
Remove `RepeatDelay` only if nothing else uses it; it belongs to `IRepeatableInputEvents`, so it most likely stays.

Run the Step 1 filter. Expected: PASS. Then the full suite. Expected: `Total:` **1053**, all passing.

- [ ] **Step 3: Verification and commit**

Run `dotnet build "sources/IcyUI.sln" --no-incremental`. Expected: 0 errors, ≤ 82 warnings, `IcyUI.Stride` 0.

```bash
git add sources/IcyUI/Input/Events sources/IcyUI.Tests/Input/ScrollEventsTests.cs
git commit -m "Retire middle-button autoscroll in favor of gesture panning" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 4: Whole-branch review**

Review all commits of this plan against the spec and the Review Focus list.

- [ ] **Step 5: Hand over the manual smoke test (Ivan runs it; never claim it)**

Stride with touch, and both engines with the middle mouse:
1. Swiping vertically in the PropertyGrid demo scrolls the list, even when the swipe starts on a slider. A sideways
   swipe on a slider moves the slider.
2. A flick coasts and slows down. Touching the list stops it without pressing the item under the finger, and the next
   tap works.
3. A nested scroll area: swiping inside the inner list scrolls it; at its edge, the outer list takes the next gesture.
4. The ListBox demo: a fast flick through the long list renders rows continuously, with no blank band and no jump.
5. The middle mouse pans lists 1:1. The left mouse never pans, and its slider and divider drags work as before.
6. Drag-and-drop with a finger starts only after press-and-hold.
