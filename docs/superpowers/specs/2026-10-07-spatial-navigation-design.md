# Spatial focus navigation: arrows, D-pad and stick move focus between controls

> Design spec, discussed with Ivan on 2026-10-07. It came out of the TreeView smoke test: focus couldn't leave the tree
> with the arrows, and after clicking a button the arrows couldn't get back into it.

## Context

IcyUI has two ways to move focus with input devices, and only one of them is finished:

- **Order-based (Tab / Shift+Tab, gamepad thumb clicks):** `NavigationEvents` raises `FocusNext`/`FocusPrevious`, and
  `Canvas.MoveFocus(bool)` steps through the focusable elements of the current focus scope. This works and stays as it
  is.
- **Directional (arrow keys, D-pad, left stick):** `NavigationEvents.OnDirectionalFocus` raises `FocusChanging`. A
  focused control may set `Handled` (TreeView, Selector). An unhandled press does nothing: the branch that would move
  focus is a `// TODO`. So no control can be left or entered with arrows or a gamepad.

**Success looks like this:**
- Arrows, D-pad and stick move focus to the nearest control in the pressed direction, inside the current focus scope.
- Controls that use arrows themselves (TreeView, an open Dropdown, a TextBox caret) still get them first and let them go
  at their edges.
- Enter / gamepad A activates the focused control: a Button clicks, a CheckBox toggles.
- Focus moved by keyboard or gamepad is never left scrolled out of view.
- The stick is usable: one step per push, a comfortable repeat while held, the right vertical direction.

### What already exists

- `INavigationEvents` / `NavigationEvents` turn keyboard, gamepad and mouse buttons into `FocusChanging(Vector2)`,
  `SelectElement`, `CloseModal`, `FocusNext`/`FocusPrevious`, `NavigateBack`/`NavigateForward` and
  `AltKeyNavigation`. `RepeatDelay`/`RepeatStartDelay`/`MinimalFocusChangeDistance` exist but are unused (repeat) or
  default to `0` (dead zone). `Update(TimeSpan)` is called every frame by `InputEventSystem` and is empty.
- `TextEvents` already implements a held-key repeat in `Update` (start delay, then interval); the navigation repeat
  follows the same pattern.
- **The stick is unusable as a navigation source today.** Both `GamepadInput` connectors raise `LeftStickMove` on every
  frame the analog value changes, so a pushed stick floods `FocusChanging`. Devices report **+Y as up** (XNA and Stride
  both), while UI space is **+Y down**, so stick up/down are swapped (Dropdown's highlight already moves the wrong way).
- Controls receive navigation through **focus-gated global subscriptions**: `Selector` (via `HookFocusGate`; ComboBox
  gates on its inner TextBox) and `TreeView` subscribe to `FocusChanging`/`SelectElement` while focused. TreeView also
  re-subscribes in `OnAttached` because focus survives `Canvas.Remove`. Canvas subscribes to `FocusNext`,
  `FocusPrevious` and `CloseModal` once, lazily, so it would run *before* any control that subscribed later. Ordering by
  subscription can't give controls the first look.
- `Canvas.OnTap` and `Canvas.OnScroll` already route through `SelfAndAncestors(...)` and stop at the first element that
  claims the input.
- `TextBox` moves its caret on plain Left/Right in its own raw `Keyboard.KeyDown` handler. Nothing stops the same key
  from also raising `FocusChanging`. It's single-line; there are no Ctrl/Shift editing keys yet.
- Enter / gamepad A does nothing on a focused `Button`: only `Selector`, `ComboBox` and `TreeView` listen to
  `SelectElement`.
- `ItemsControl.ScrollIntoView(int)` exists; there's no general bring-into-view for an element.

## Decisions

| Decision | Choice |
|---|---|
| Tab / Shift+Tab | **Order-based**, unchanged (`Canvas.MoveFocus(bool)`). |
| Arrows / D-pad / stick | **Spatial**: the nearest focusable control in the pressed direction. |
| Routing | **Through the focused element.** Canvas is the only subscriber to `FocusChanging`/`SelectElement`. It calls new routed virtuals on the focused element, then its ancestors; the first `true` claims the press. Unclaimed directional presses move focus spatially. |
| Activation | New routed `OnActivate()`; `Button` clicks. Unclaimed activation does nothing. |
| Direction | Snapped to the dominant axis (4-way) for every source, the stick included. |
| Scope | The focused element's focus scope (as Tab); the whole canvas without one. No wrap-around. |
| Scrolled-out controls | **Reachable** when inside a ScrollViewer whose viewport is on screen; the new focus is scrolled into view. |
| TextBox | **Edge escape**: Left/Right move the caret and escape only at the text edge; Up/Down always escape (single-line). |
| Closed Dropdown/ComboBox | Arrows **navigate away**; Enter/A opens. Open: arrows move the highlight and stay inside. |
| Stick | In scope: dead zone, edge trigger, hysteresis, Y flip, hold-to-repeat. |
| `CloseModal` (Escape/B) | Out of scope; keeps its current subscriptions. |

## Detailed design

### Routing

Two new virtuals on `UIElement`, both `protected internal` like `OnTap`, both returning `bool` ("claimed"):

```csharp
/// Handles a directional navigation press while this element or a descendant has focus.
protected internal virtual bool OnNavigate(Vector2 direction) => false;

/// Handles an activation press (Enter, gamepad A) while this element or a descendant has focus.
protected internal virtual bool OnActivate() => false;
```

- `direction` is always one of the four unit vectors, in UI space (+Y down).
- Canvas subscribes once (in `EnsureInputRoutingInitialized`, next to `FocusNext`) to `FocusChanging` and
  `SelectElement`, and only acts while `IsKeyboardNavigationEnabled` and while it holds the `FocusedElement`.
  - `FocusChanging`: snap `e.Data` to an axis; walk `SelfAndAncestors(FocusedElement)` calling `OnNavigate`; on the
    first `true` set `e.Handled` and stop. Otherwise call `MoveFocusSpatial(direction)`; set `e.Handled` when focus moved.
  - With **no focused element**, a directional press focuses the first element in Tab order (and brings it into view).
  - `SelectElement`: walk `SelfAndAncestors(FocusedElement)` calling `OnActivate` until one returns `true`.
- `INavigationEvents` is unchanged. Apps that subscribe to `FocusChanging` themselves still see every press, but setting
  `Handled` there no longer influences focus movement (it never did anything before, either).
- New public `Canvas.MoveFocus(Vector2 direction)` overload: the spatial move, for apps driving navigation from their own
  input (e.g. a custom button mapping). Returns `bool` (moved).

### The spatial move

`Canvas.MoveFocusSpatial(Vector2 direction)` (the body of `MoveFocus(Vector2)`):

**Candidates.** The same base set as `MoveFocus(bool)`: elements in the focused element's focus scope
(`FindEnclosingFocusScope`), else all root elements, that are `IsFocusable` and visible. Additionally excluded:

- the focused element itself and its ancestors;
- elements whose effective visibility is off (an ancestor invisible or at `Opacity <= 0`) or that are disabled;
- **clipped-away** elements. A candidate's screen bounds are intersected with each clipping ancestor's screen bounds
  (`ClipToBounds`), walking up. A `ScrollViewer` ancestor doesn't clip the candidate (its content is reachable by
  scrolling), but the walk continues *from the ScrollViewer's own bounds*, so a ScrollViewer that is itself clipped
  away or off screen hides everything in it. A candidate with an empty result is excluded. The canvas viewport is the
  outermost clip.

**Geometry.** Screen-space rectangles, computed outside rendering from the element's composed layout and render
transforms (the same chain `Draw` and hit-testing use), so scrolling (`LayoutOffset`), canvas scale and DPI are
accounted for. A small internal helper, `UIElement.GetScreenBounds()`, provides it.

**Qualification and scoring.** With `F` the focused rectangle, `C` a candidate, and the axis given by the direction:

1. *Ahead:* `C`'s near edge must be at or past `F`'s far edge, minus a tolerance of 4 layout units scaled to screen.
   (Pressing Down: `C.Top >= F.Bottom - tolerance`.)
2. *Beam:* `C` is in `F`'s beam when their ranges on the cross axis overlap. Any in-beam candidate beats every
   out-of-beam one.
3. *Score* inside each group: `major + 2 * minor`, where `major` is the edge gap along the axis (`max(0, …)`) and
   `minor` is the gap between the cross-axis ranges (`0` when they overlap). Lowest wins.
4. *Ties:* smaller distance between centres, then earlier Tab order. The result is deterministic.

**Reverse-step memory.** After a spatial move from `A` to `B` in direction `d`, Canvas remembers `(A, d)`. If the next
directional press is `-d` while `B` is still focused and `A` is still a valid candidate, focus returns to `A` directly.
Any other focus change (Tab, tap, programmatic `Focus`, a different direction) clears the memory. It's a weak reference,
so it never keeps `A` alive.

**After the move** Canvas calls `Focus(target)` and then `target.BringIntoView()`.

### `UIElement.BringIntoView()`

Public. Scrolls every enclosing `ScrollViewer` just enough to show this element, innermost first.

- For each `ScrollViewer` ancestor: compare the element's tracked screen rectangle with the viewer's viewport screen
  rectangle (its `ContentBounds`). On each axis, if the element is above/left, align its leading edge; if below/right,
  align its trailing edge; if it's larger than the viewport, align the leading edge. Convert the screen delta to layout
  units through the viewer's transform scale and add it to `VerticalOffset`/`HorizontalOffset` (their setters clamp).
- The tracked rectangle then shifts by the applied delta before the next (outer) viewer is considered, so nested viewers
  work without a layout pass in between.
- Working in screen space covers both plain content and virtualizing content (`ItemsControl` arranges realized
  containers relative to the viewport), so a realized row inside a list can be brought into view too.
- Canvas calls it after a spatial move and after `MoveFocus(bool)` (Tab). Not after taps or programmatic `Focus()`.

### Input layer (`NavigationEvents`)

A single **held-direction state machine** feeds `FocusChanging` from all three sources:

- **Press:** an arrow key (`KeyDown`), a D-pad button (`ButtonPressed`), or the stick crossing its threshold raises
  `FocusChanging` once, immediately, and becomes the held direction (the latest press wins).
- **Repeat:** `Update(deltaTime)` re-raises the held direction after `RepeatStartDelay`, then every `RepeatDelay`.
- **Release:** `KeyUp`, `ButtonReleased`, or the stick falling below its release threshold clears the held direction
  if it's the one released.
- **Stick specifics:**
  - The direction is snapped to the dominant axis; **Y is negated** (devices report +Y up).
  - Fires when the magnitude rises above `MinimalFocusChangeDistance`; releases below `0.7 *` that value (hysteresis).
  - Changing the snapped axis while pushed counts as a new press. Jitter within the same snapped direction raises
    nothing.
- **Modifiers:** Ctrl+arrow and Shift+arrow don't raise `FocusChanging` (reserved for editing). Alt already routes to
  `AltKeyNavigation`.
- **New defaults** (documented behaviour change): `MinimalFocusChangeDistance` `0` → `0.5`, `RepeatStartDelay` `2 s` →
  `0.4 s`, `RepeatDelay` `1 s` → `0.1 s`.

No engine code changes: both connectors already pass raw stick values through.

### Control changes

- **`Button.OnActivate`**: clicks through the same path as a tap (respects `IsEnabled`) and returns `true`.
  `ToggleButton` and `CheckBox` inherit it.
- **`Selector`** (Dropdown, ComboBox):
  - `OnNavigate`: closed → `false` (focus moves on). Open → move `HighlightedIndex` as today, return `true`.
  - `OnActivate`: closed → open. Open → commit the highlight and close (today's `OnNavigationSelectElement`). `true`.
  - The `FocusChanging`/`SelectElement` subscriptions and `HookFocusGate` go away; ComboBox's inner TextBox is a
    descendant, so its unclaimed presses bubble to the ComboBox. `CloseModal` keeps its own subscription (out of scope),
    gated as today.
  - The existing virtuals `OnNavigationFocusChanging`/`OnNavigationSelectElement` (ComboBox overrides one) become the
    bodies of the new overrides; the plan settles their final names.
- **`TreeView`**:
  - `OnNavigate`: today's `OnNavigationFocusChanging` logic, returning its handled result. Edges still return `false`,
    and focus now actually moves on.
  - `OnActivate`: toggles the current row and returns `true` when it has children; `false` otherwise.
  - `SubscribeNavigation`/`UnsubscribeNavigation`, the `subscribedNavigation` field, and the "focus survives
    `Canvas.Remove`" re-subscribe in `OnAttached` go away.
- **`TextBox`**:
  - Plain Left/Right move from the raw `KeyDown` handler into `OnNavigate`: move the caret and return `true`, or return
    `false` at the start/end of the text.
  - Up/Down return `false`.
  - Home, End, Back and Delete stay in the raw handler.
  - Consequences: held Left/Right now repeats the caret, and the D-pad moves the caret before escaping.

## Performance

- **Per directional press:** one pass over the scope's elements (candidate filter, clip walk, score), reusing a pooled
  candidate list. Target: under 1 ms for a 1,000-element scope, checked by a test with a generous bound.
- **Per frame:** `NavigationEvents.Update` checks one held-state field; nothing else runs while idle.
- **Repeat** caps presses at about 10 per second while held.
- No dev-only parts, so nothing to gate for release builds.

## Testing

- **Routing:** claimed vs unclaimed `OnNavigate` on the focused element vs an ancestor; `OnActivate` clicks a Button and
  toggles a CheckBox; a disabled Button doesn't activate; an app-level `FocusChanging` subscriber still sees presses;
  nothing focused → the first Tab-order element.
- **Spatial algorithm** (laid-out panels with known geometry): four directions in a grid; beam beats a closer diagonal;
  overlap tolerance; deterministic ties; reverse-step memory returns to `A` and is cleared by Tab, a tap or another
  direction; the focus-scope (Window) boundary; hidden, zero-opacity, disabled and clipped-away candidates excluded; a
  candidate scrolled out of a visible ScrollViewer included, one in an off-screen ScrollViewer excluded; no candidate →
  no-op; correct under canvas scale.
- **BringIntoView:** minimal shifts on both axes; oversized element aligns its leading edge; nested ScrollViewers;
  a realized `ItemsControl` row; Tab into an off-screen element scrolls it into view.
- **Input state machine** (fake keyboard/gamepad + `Update`): one fire per press; repeat after start delay, then at
  interval; stop on release; latest direction wins; stick dead zone, hysteresis, axis snap, Y flip, jitter → no extra
  events; Ctrl/Shift+arrow ignored.
- **Migrations:** the existing Selector, ComboBox and TreeView navigation tests ported to routed presses with unchanged
  behaviour, plus: a closed Dropdown lets arrows go; TextBox caret moves, edge escape, Up/Down escape, Home/End stay put.
- **Behavioural (the original report):** in the TreeView demo layout, Down from the live tree's last row reaches the
  buttons, and Up from the Add button gets back into the tree. In the sample shell, D-pad down walks the whole sidebar
  and scrolls it along.

`FakeNavigationEvents` keeps its direct `Raise…` helpers so routing tests stay timing-free; the state machine is tested
on the real `NavigationEvents` with fake devices.

## Cross-engine impact

- All logic is in core `IcyUI`; MonoGame and Stride get it unchanged. FNA (stub) gets it once it has input connectors.
- The stick Y flip assumes both engines report +Y up. Verified for XNA/MonoGame by convention; **Stride's sign must be
  confirmed in the smoke test.**
- Related, already committed: Stride now honours `TextureRenderingOptions.Origin` like MonoGame (83176ca).

## Smoke test (Ivan, both engines, x64 and ARM64)

- D-pad and stick navigation through the TreeView and Controls demos, into and out of the trees.
- Stick up moves up on both engines.
- A / Enter presses buttons and toggles checkboxes.
- Dropdown: arrows pass by when closed; A opens; arrows move the highlight when open.
- TextBox: caret moves, escapes at the edges; Up/Down leave.
- The sample sidebar scrolls along with D-pad focus.
- Holding a direction repeats at a comfortable speed.

## Out of scope (follow-ups)

- Arrow handling in `Slider` (adjust value) and `TabControl` (switch tab).
- Keyboard/gamepad navigation inside `ListBox` and `WrapGrid`.
- Routing `CloseModal` (Escape/B) through the focused element, replacing Dialog's and Selector's subscriptions.
- Per-element navigation overrides (explicit up/down/left/right neighbours).
- Configurable button mapping.

## Plan-time revisions

- `Selector.HookFocusGate` **stays**: it still gates the `CloseModal` subscription, which is out of scope. Only the
  `FocusChanging`/`SelectElement` subscriptions go.
- The spec's "sample shell sidebar" behavioural test: the shell doesn't exist yet. Task 5 covers the same behaviour with
  a ScrollViewer of buttons (`DPadDown_WalksAScrolledListAndScrollsAlong`).
- ScrollViewer reveal is an internal virtual hook on `UIElement` (`RevealScreenRectangle`) overridden by `ScrollViewer`,
  so `UIElement` doesn't reference a control type.
