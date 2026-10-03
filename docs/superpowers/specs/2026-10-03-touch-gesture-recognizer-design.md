# Touch gesture recognizer (3a)

> Design spec, brainstormed with Ivan on 2026-10-02/03. It is part 1 of two of "real touch scrolling", which grew out
> of a DPI follow-up: touch swipes scrolled 2× too far at 200 %. Part 2 (3b: `ScrollViewer` pan, inertia and gesture
> arbitration) builds on this spec and gets its own.

## Context

Touch input doesn't really work today, and the DPI symptom sits on top of that:

- `ITouchInput` (the device layer each engine implements) is **gesture-level**: `Tap`, `Hold`, `Swipe`, `Drag` carrying
  `TranslationInfo`.
  - MonoGame fills it from XNA's `TouchPanel.ReadGesture()`. Its "Swipe" is XNA's `Flick`, whose `Delta` is a
    **velocity in px/s**. `ScrollEvents` uses that velocity as a one-off scroll *distance*.
  - Stride's `TouchInput` is a stub. Gesture recognition was deferred in v1, so Stride has no touch at all.
- Core already contains half a recognizer, for the **mouse only**. `TouchEvents` emulates tap, multi-tap and hold from
  mouse buttons (`MaxMultiTapDelay`, `MinHoldDelay`, `HoldAreaSize`) and starts drags through
  `DragEvents.OnMouseMove`.
- Touch never raises `TouchDown`/`TouchUp`, so a finger never gives a control its Pressed state.
- The 10 px tap/drag slop is measured in **physical pixels**, so at 200 % it is half as forgiving.

**Cross-engine impact.**
- Core `IcyUI` gets the recognizer and the new gesture events.
- `IcyUI.MonoGame` and `IcyUI.Stride` shrink to raw contact connectors. Stride gains touch for the first time.
- `IcyUI.FNA` (still a stub) later needs only the same `TouchPanel.GetState()` connector as MonoGame.

## Decisions

| Decision | Choice |
|---|---|
| Overall scope of #3 | Real touch scrolling, split into **3a** (this spec: gesture input) and **3b** (scrolling behavior). Ivan's choice over a minimal unit fix. |
| Where recognition lives | **One engine-agnostic recognizer in core**, fed raw pointer data. Ivan's choice, matching "engines are thin connectors". |
| Pointers that drive it | **Touch + middle mouse.** Left/right mouse keep today's semantics (they go to controls); middle-button drag is tagged *Pan*. |
| Multi-touch | **Includes pinch** (two-finger scale and center). Extra contacts beyond two are tracked by ID and ignored. |
| Structure | **Approach A:** `ITouchInput` becomes a raw polled contact snapshot, and gestures are produced only by the core recognizer. This is a breaking change to `ITouchInput`/`TranslationInfo`. Acceptable pre-MVP, because only our two engines implement it. |
| Threshold units | **DIPs:** slop = configured value × `IRenderContext.DisplayScale`. Deliberately not `Canvas.EffectiveScale`: a user-scale preference shouldn't change how far a finger may wobble. |
| Compensation scope (applies in 3b) | Pan follows the finger 1:1 through the scrolling element's **full** transform (DPI, canvas zoom, `RenderScale`). 3a delivers physical-pixel positions and velocities; 3b maps them. |

## Detailed design

### 1. Device contract and engine connectors

```csharp
namespace Icy.Input.Devices
{
    public interface ITouchInput : IInputDeviceListener, IInitializable
    {
        /// Refreshed once per input update.
        IReadOnlyList<TouchContact> Contacts { get; }
    }

    public readonly record struct TouchContact(int Id, Point Position, TouchContactState State); // physical px
    public enum TouchContactState { Pressed, Moved, Released }
}
```

- `Tap`, `Hold`, `Swipe`, `Drag` and `TranslationInfo` are **removed** from the device layer.
- A `Released` contact appears in exactly one snapshot, so a tap that starts and ends between two updates is not lost.
  Pressing and releasing within a single update produces a `Pressed` snapshot followed by a `Released` one.
- **MonoGame:** `TouchPanel.GetState()` gives `TouchLocation` (`Id`, `Position`, `State`), which maps 1:1.
  `TouchLocationState.Invalid` is skipped. The `TouchPanel.EnabledGestures` setup and `ReadGesture()` are removed.
- **Stride:** fold each frame's `InputManager.PointerEvents` into contacts by `PointerId`.
  - Only touch/finger pointer devices are used. Mouse pointers also appear in `PointerEvents` and must be filtered out,
    because the mouse already comes through `IMouseInput`.
  - `PointerEvent.Position` is **normalized (0–1)**. Multiply it by the back-buffer size to get physical pixels.
  - `PointerEventType.Canceled` maps to a contact that vanishes without a release (see Edge cases).
- **FNA (later):** the same connector as MonoGame.

### 2. Core recognizer

`Icy.Input.Gestures.GestureRecognizer` is internal and owned by `InputEventSystem`.
- It ticks once per input update, **before** the other event providers.
- Each tick it reads the touch contact snapshot, the mouse position and buttons, the elapsed time, and the display
  scale (through a `Func<float>` wired to `IRenderContext.DisplayScale`).
- Its output is exposed through the new public **`IInputEventSystem.Gestures : IGestureEvents`**.

**Pointer kinds:**

| Kind | Tap / Hold | Drag | Pinch |
|---|---|---|---|
| Touch contact | Tap with multi-tap count; Hold | yes, with release velocity | two contacts |
| Mouse left | as today | as today | no |
| Mouse right | release raises Hold (today's context-click semantics) | no | no |
| Mouse middle | no | always tagged `Pan` | no |

**Per-pointer state machine:** `Pressed → Dragging → Released`.
- Moving past the slop starts a drag.
- Staying inside the slop for `HoldDelay` raises **Hold**.
- Releasing inside the slop without a hold raises **Tap**. Its count increments when the tap lands within
  `MultiTapDelay` and within the slop of the previous tap; otherwise it restarts at 1.
- A **second touch contact** cancels the first contact's pending tap or drag (`DragCanceled`) and starts **Pinch**.
  - `PinchChanged` reports the center, the scale relative to the start distance, and the center delta.
  - The pinch ends when either finger lifts.
  - Remaining fingers stay inert until all contacts are up (no jumpy drag resume).
- **Emulated mouse suppression:** while any touch contact is active, mouse button input is ignored. Windows synthesizes
  mouse messages from touch, so one finger must never count as two pointers.

**`IGestureEvents`.** All positions are physical pixels. Every args type carries the pointer `Kind`.
- `PointerPressed` / `PointerReleased` (position)
- `Tapped(position, count)`, `Held(position)`
- `DragStarted`: `AcceptableEventArgs`, so a handler can claim the drag. This is 3b's arbitration hook.
- `DragMoved(position, delta)`, `DragCompleted(position, velocity px/s)`, `DragCanceled`
- `PinchStarted` / `PinchChanged(center, scaleDelta, centerDelta)` / `PinchCompleted`
- `Settings`: `HoldDelay` (0.5 s), `MultiTapDelay` (0.3 s), `Slop` (10 DIP). These are today's defaults. They move
  here from `ITouchEvents` (`MinHoldDelay`, `MaxMultiTapDelay`, `HoldAreaSize`).

**Velocity** is a least-squares fit over the samples from the last ~100 ms. A window with zero time span gives
velocity 0, never NaN or ∞.

**Existing providers become adapters:**
- `TouchEvents` re-raises Tap, Hold, Down and Up from gestures (touch, mouse left, mouse right). Its own mouse emulation
  code is deleted. For the first time, touch raises `TouchDown`/`TouchUp`.
- `DragEvents` re-raises drag gestures from touch and mouse left, keeping `AcceptableEventArgs`. `OnMouseMove` goes away.
- `ScrollEvents` keeps wheel and gamepad and loses the `Touch_Swipe` path. **Middle-button autoscroll stays as it is**
  until 3b replaces it with a real pan, so nothing regresses between the two parts.
- `DeviceEventsAggregator` (diagnostics) drops its subscriptions to the removed `ITouchInput` events.

### 3. Edge cases

- **A contact vanishes without `Released`** (focus loss, device disconnect, Stride `Canceled`): treat it as a cancel.
  A pending tap is discarded, an active drag gets `DragCanceled`, and an active pinch gets `PinchCompleted`.
- **The OS reuses a contact `Id`:** a `Pressed` for an `Id` that is already tracked resets that pointer.
- **Mouse and touch at the same time:** they are independent pointers, apart from emulated-mouse suppression. Pinch only
  involves touch contacts.
- **A third or later finger:** tracked so its `Released` can't corrupt state, but otherwise ignored.

## Testing

Unit tests (core, no engine). A fake `ITouchInput` with a scriptable contact list and a fake clock drive the recognizer
tick by tick.
- Tap vs drag at the slop boundary, at `DisplayScale` 1 and 2.
- Multi-tap counting and reset. Hold timing.
- Drag start → move → complete; release velocity on a synthetic constant-speed track. An `Accept`-ed `DragStarted` is
  honored.
- A second finger raises `DragCanceled` and `PinchStarted` with the correct scale and center. Lifting either finger ends
  the pinch; the remaining finger stays inert.
- Mouse right raises Hold. Mouse middle raises a `Pan`-tagged drag. Neither raises Tap.
- Emulated-mouse suppression while a touch contact is active.
- The edge cases above.
- **Regression:** the existing mouse-driven tap, hold, drag and drag-drop tests pass unchanged through the adapters.
- Touch raises `TouchDown`/`TouchUp` and gives a control its Pressed state.

Build: `dotnet build "sources/IcyUI.sln"` with no new warnings (the baseline is 82). `IcyUI.Stride` stays at 0.

Manual (Ivan runs it on the touch laptop, both engines):
- Tap, hold and multi-tap.
- Drag a `Slider` with a finger.
- A pressed button highlights under a finger.
- Pinch: there's no consumer until 3b or Phase 10, so a small gesture readout is added to an existing demo.

## Critical files

- `sources/IcyUI/Input/Devices/ITouchInput.cs` (rewritten), plus new `TouchContact` and `TouchContactState`.
- `sources/IcyUI/Input/Gestures/*` (new): `GestureRecognizer`, `IGestureEvents`, the args types, `GestureSettings`,
  and the velocity estimator.
- `sources/IcyUI/Input/IInputEventSystem.cs`, `InputEventSystem.cs` (the `Gestures` member; recognizer tick order).
- `sources/IcyUI/Input/Events/TouchEvents.cs`, `ITouchEvents.cs`, `DragEvens.cs`, `ScrollEvents.cs`,
  `Diagnostics/DeviceEventsAggregator.cs` (adapters and cleanup).
- `sources/IcyUI.MonoGame/Input/Devices/TouchInput.cs`, `sources/IcyUI.Stride/Input/Devices/TouchInput.cs`
  (connectors).
- `sources/IcyUI.Tests/Input/...` (fake touch input; recognizer tests).
- One demo for the gesture readout, registered in both hosts as usual.

## Open items for implementation planning

- **Spike first (Stride):** confirm the `PointerEvents` API in Stride 4.3: the event types, telling touch apart from
  mouse devices, and that positions are normalized. If it differs, adapt the connector. The core contract doesn't change.
- How the recognizer gets `DisplayScale`. `InputEventSystem` has no reference to the render context today. Choose
  between a `Func<float>` passed in when `IcyConfigurationBuilder.Build()` wires the input system, and a setter. Decided
  in the plan.
- Whether `FakeInputSystem` gains a `FakeTouchInput`, or the test fake lives next to the recognizer tests.
