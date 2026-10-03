# Touch scrolling (3b)

> Design spec, brainstormed with Ivan on 2026-10-03. Part 2 of 2 of "real touch scrolling". It builds on 3a, the core
> gesture recognizer (`docs/superpowers/specs/2026-10-03-touch-gesture-recognizer-design.md`).

## Context

3a gave every engine one gesture source: `IInputEventSystem.Gestures`, with tap, hold, drag (pointer kind, direction,
release velocity) and pinch. Scrolling doesn't use any of it yet:

- **No drag ownership.** `Canvas` sends `OnDragStarted`/`OnDragPerforming`/`OnDragEnded` to *every* ancestor of the
  touched element. That is harmless only because leaf controls (`Slider`, `SplitPane`, `HsvSquare`) are the sole drag
  handlers. Once `ScrollViewer` handles drags, a `Slider` inside a list would move its thumb *and* scroll the list.
- **`ScrollViewer` is minimal:** clamped `HorizontalOffset`/`VerticalOffset`, `ScrollChanged`, and virtualization hand-off.
  It has no drag handling, no inertia and no scrollbars.
- **Middle-button "scrolling" is an autoscroll in `ScrollEvents`.** Its speed grows with the cursor's distance from the
  press point, measured in physical pixels.
- `ScrollViewer` sits inside `ListBox`, `ComboBox` popups, `PropertyGrid` and others, so these rules apply to all of them.

**Cross-engine impact:** core only. The engines already deliver gestures through 3a. Touch works on Stride; MonoGame
desktop has no touch yet (see `CLAUDE.md` Known issues), but middle-mouse panning works on both engines.

## Goals

- A finger, or the middle mouse button, on a scrollable area moves its content **1:1 under the pointer**, through DPI,
  `Canvas` zoom and `RenderScale` (the full transform).
- A flick keeps the content moving with **inertia** that decelerates to a stop.
- Mouse wheel, gamepad and left-mouse behavior are unchanged. **The left mouse never pans.**

## Decisions

| Decision | Choice |
|---|---|
| Who owns a drag over an interactive child | **Direction lock.** The gesture's first movement past the slop picks the axis; the innermost element that claims that axis wins. |
| Touch drag-and-drop (`IDragSource`) | **Press-and-hold** (the existing Held gesture) picks the item up. A plain finger drag scrolls. The mouse keeps today's immediate drag-drop. |
| Edges | **Hard stop.** Content stops exactly at the edge; inertia ends there. No bounce, no edge glow. |
| Nested scroll areas | **Per gesture.** The area chosen at the start keeps the whole gesture and its fling. An area already at its edge in the gesture's direction doesn't claim it, so the next area out gets it. No hand-off mid-gesture. |
| `ScrollViewer` knobs | **WPF-like:** `PanningMode` and `PanningDeceleration`, plus `StopInertia()`. |
| Architecture | **One owner per gesture, chosen by `Canvas`** through a per-element axis query. Rejected: broadcasting with "handled" flags (the rules end up spread across controls) and a separate manipulation-event family (a large API for no current need). |

## Detailed design

### 1. Gesture ownership

**New API on `UIElement`:**

```csharp
[Flags] public enum DragAxes { None = 0, Horizontal = 1, Vertical = 2, Both = Horizontal | Vertical }

public readonly record struct DragClaimContext(PointerKind Kind, Point ScreenStart, Vector2 LocalDirection, bool StartedFromHold);

protected internal virtual DragAxes GetDragAxes(in DragClaimContext context) => DragAxes.None;
```

- `ScreenStart` is the press point in physical pixels, so a control can claim only part of itself (a `SplitPane`
  claims only when the press is on its splitter).
- `LocalDirection` is the movement that crossed the slop, mapped into the element's own space. Axis decisions therefore
  hold under rotation and zoom.
- `StartedFromHold` is `true` when the drag grew out of a Held press.

**Resolution at `DragStarted`.** `Canvas` hit-tests `ScreenStart` and walks up the ancestor chain, calling `GetDragAxes`
on each element. The main axis is horizontal when `|LocalDirection.X| ≥ |LocalDirection.Y|`, otherwise vertical.
1. The **innermost** element whose answer contains the main axis owns the gesture.
2. Otherwise, the **innermost** element with any non-`None` answer owns it. This keeps today's mouse feel: a slightly
   diagonal drag on a horizontal `Slider` still moves it, and nothing changes outside scroll areas.
3. Otherwise, no element owns the gesture.

Each element computes its own `LocalDirection`; the main axis is computed per element from that.

**Routing.** Only the owner receives `OnDragStarted`, `OnDragPerforming`, `OnDragEnded` and the new `OnDragFling`
(section 2). The owner is fixed for the whole gesture. If it detaches from the canvas mid-gesture, routing stops
silently.

**Built-in claims:**

| Control | `GetDragAxes` |
|---|---|
| `Slider` | `Horizontal` (it has no orientation). |
| `SplitPane` | Its splitter axis (`Horizontal` when `Orientation` is `Horizontal`), and only when `ScreenStart` is on the splitter. |
| `HsvSquare` | `Both`. |
| `ScrollViewer` | The axes `PanningMode` allows, intersected with the axes it can still move in the gesture's direction. Only for `Touch` and `MouseMiddle`; always `None` for the left and right mouse buttons. |

The built-in controls don't claim `MouseMiddle` drags, which are meant only for panning.

**Drag-and-drop.** `Canvas` looks for an `IDragSource` in the hit element's chain as today, with two changes. For a
`Touch` gesture it does so only when `StartedFromHold` is set. `MouseMiddle` gestures never start drag-drop. Mouse-left
gestures keep today's immediate behavior.

**Inputs.** `Canvas` routes drags from `IGestureEvents` (`DragStarted`, `DragMoved`, `DragCompleted`, `DragCanceled`)
instead of `IDragEvents`, because it needs the pointer kind, the direction and the hold flag. `IDragEvents` stays public
for applications. `DragInfo` gains `bool StartedFromHold`, set by `GestureRecognizer` when a drag starts from the Held
state.

**Breaking change (accepted pre-MVP).** A custom control that only overrides `OnDragStarted` must also override
`GetDragAxes`, or it stops receiving drags. The built-in controls are updated, and `OnDragStarted`'s documentation
states the requirement.

### 2. `ScrollViewer` panning and inertia

**New public API:**

| Member | Default | Notes |
|---|---|---|
| `PanningMode PanningMode` | `Auto` | `enum PanningMode { Auto, None, Vertical, Horizontal, Both }`. `Auto` means the axes whose extent exceeds the viewport. |
| `float PanningDeceleration` | `1500` | Units per second², in the `ScrollViewer`'s own space. `0` disables inertia. Must be finite and ≥ 0. |
| `void StopInertia()` | — | Ends a running fling immediately. |

**Panning (1:1).**
- The owner keeps `PointToLocal` of the last pointer position. Each `OnDragPerforming` subtracts the local movement from
  the offsets, on the claimed axes only: a vertical claim never drifts sideways.
- Content follows from the point where the gesture became a pan. There's no jump by the slop distance.

**Inertia.**
- `Canvas` calls `protected internal virtual void OnDragFling(Vector2 screenVelocity)` on the owner when the gesture
  completes, right before `OnDragEnded`. It isn't called when the gesture is canceled. The default does nothing.
- `ScrollViewer` maps the velocity into its own space with the linear part of the inverse transform:
  `PointToLocal(p + v) − PointToLocal(p)`. It keeps only the claimed axes and caps the magnitude at 8000 units/s.
  Below 50 units/s, or when `PanningDeceleration` is 0, there is no inertia.
- Constant deceleration `d` from speed `|v|` lasts `T = |v| / d` and travels `v · T / 2`. The position follows exactly
  `EaseOutQuad` over `T`.
- **Inertia applies deltas, not absolute values.** An `Animation` drives a private progress value from 0 to 1 over
  `T` with `EaseOutQuad`. Each change adds `distance × (progress − previousProgress)` to the current offsets.
  Virtualized content corrects offsets while estimated row heights become real
  (`VerticalOffsetCorrectionRequested`); writing absolute values would overwrite those corrections and make the list
  jump.
- **Hard stop:** the offset setters clamp. A fling whose offsets no longer change on either axis ends early.

**Stopping a fling:**
- the `ScrollViewer` (or a descendant) is pressed;
- a new drag starts on it;
- an `OnScroll` (mouse wheel or gamepad) reaches it;
- `PanningMode` changes;
- `StopInertia()` is called;
- it detaches.

While a fling runs, `ScrollViewer.HitTest` returns the `ScrollViewer` itself for points inside it. The press that stops
the fling therefore lands on the list and doesn't activate the item under the finger, as on phones.

### 3. Middle mouse

`ScrollEvents` drops its middle-button autoscroll (the press/release tracking and the `DirectionScroll` from the cursor
offset). Mouse wheel and gamepad right-stick scrolling are unchanged. Middle-button drags reach scroll areas as
`MouseMiddle` gestures through section 1.

## Edge cases

- The owner detaches mid-drag: routing stops and nothing throws.
- A drag canceled mid-pan (a second finger starts a pinch, or a contact vanishes): `OnDragEnded` without `OnDragFling`,
  so the list stops where it is.
- `PanningMode.Auto` with no overflow claims nothing, so the gesture goes to the next scroll area out.
- A middle-mouse drag over non-scrollable content has no owner and does nothing.
- The content shrinks during a fling: the clamp stops the coast at the new edge.
- `PanningDeceleration` set to a negative or non-finite value throws `ArgumentOutOfRangeException`.

## Testing

Unit tests, with `FakeGestureEvents` driving `Canvas` and `Dispatcher.UpdateAnimations` driving inertia:
- **Ownership:**
  - A vertical swipe on a horizontal `Slider` inside a vertical `ScrollViewer` scrolls the list; a horizontal swipe moves
    the slider.
  - A `Slider` outside any scroll area still takes a diagonal drag.
  - An inner list already at its edge passes the gesture to the outer one.
  - `PanningMode.None`, and `Auto` without overflow, claim nothing.
  - A `SplitPane` claims only on its splitter.
  - Touch drag-drop starts only after a hold; the mouse starts it immediately.
  - Only the owner receives `OnDrag*`.
  - The left mouse never pans; the middle mouse pans.
- **Panning:** content moves exactly the pointer distance at `DisplayScale` 2 and with a `Canvas.Scale`.
- **Inertia:**
  - Coast distance and duration.
  - Hard stop at the edge.
  - Touching stops the fling without tapping the item underneath.
  - A mouse-wheel scroll stops it.
  - An offset correction made during the fling is kept.
  - A virtualized `ListBox` realizes the right items while flinging.
  - `PanningDeceleration = 0` gives no inertia.
- **Regression:** the existing `Slider`, `SplitPane`, `HsvSquare`, drag-drop and `ScrollViewer` tests pass through the
  new routing. Tests that used `FakeDragEvents` switch to `FakeGestureEvents` through a helper, with the same assertions.

Build: no new warnings (baseline 82); `IcyUI.Stride` stays at 0.

Manual (Ivan runs it): touch on Stride, and middle mouse on both engines. Check the `PropertyGrid` with sliders, a
nested scroll area, flick then touch to stop, and drag-drop after press-and-hold.

## Critical files

- `sources/IcyUI/UI/UIElement.cs` (`GetDragAxes`, `OnDragFling`), plus new `UI/DragAxes.cs` and `UI/DragClaimContext.cs`
- `sources/IcyUI/UI/Canvas.cs` (ownership resolution, gesture-based routing, hold-gated touch drag-drop)
- `sources/IcyUI/UI/Controls/ScrollViewer.cs` (claims, panning, inertia, `HitTest` during a fling), plus new
  `UI/Controls/PanningMode.cs`
- `sources/IcyUI/UI/Controls/Slider.cs`, `SplitPane.cs`, `HsvSquare.cs` (claims)
- `sources/IcyUI/Input/Gestures/DragInfo.cs`, `GestureRecognizer.cs` (`StartedFromHold`)
- `sources/IcyUI/Input/Events/ScrollEvents.cs` (middle autoscroll removed)
- `sources/IcyUI.Tests/...` (ownership, panning and inertia tests; migration of drag tests)
