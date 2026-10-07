# Editor scope

> Design spec, discussed with Ivan on 2026-10-08. This is sub-project 3 of the samples work
> (`2026-10-07-samples-shell-design.md`): restrict the editor to one subtree, and let the wheel and the middle button
> scroll the page while editing. Builds on the editor frame (`2026-10-05-editor-frame-design.md`).

## Context

`EditorFrame` adds a full-surface `EditorCaptureLayer` and a toolbar to the canvas. In Edit mode the layer takes every
pointer gesture, so inside the samples shell it also covers the sidebar: you must press "Stop editing" before you can
switch demos. Two more problems follow from the capture layer being a parentless overlay:

- **Wheel:** `Canvas.OnScroll` walks `SelfAndAncestors(hoveredElement)`. Over the capture layer that chain is just the
  layer, so the page's `ScrollViewer` never sees the wheel.
- **Middle drag:** `Canvas.ResolveDragOwner` walks the chain from `HitTest(drag.Start)`. The layer declines
  `PointerKind.MouseMiddle`, so nobody owns the pan.

`UIElement.OnScroll` and `GetDragAxes` are `protected internal`, so `IcyUI.Design` can't forward to another element
without reimplementing scrolling. That needs a small core seam.

## Decisions

| Question | Decision |
|---|---|
| What is a scope | One `UIElement` subtree; `null` keeps today's whole-canvas behavior. |
| Keyboard in Edit mode | Stays canvas-wide, as today: focus cleared, keyboard navigation off, editor bindings global. The scope only limits pointer capture, hit-testing and selection. |
| Wheel / middle-pan forwarding | A core opt-in "fall-through" flag on overlays: unclaimed scrolls and drags continue below the overlay. |
| Where the scope lives | On `EditorSession` (`Session.Scope`), so every tool sharing the session shares the boundary. |
| Capture region | The overlay is positioned and sized to the scope's ancestor-clipped bounds; `UIElement.HitTest` isn't virtual. |
| Toolbar | `ToolbarPlacement` corners are measured from the scope's region. |
| Page overlays (popups, dialogs) | Geometric rule: inside the region the editor sees whatever is topmost, overlays included. No core owner link. |
| Samples | Both: `EditorDemo` scopes to its own page; the shell gets an "Edit demo" toggle (F4) scoped to its content area. |
| Sessions per canvas | One. A second `Attach` on the same canvas throws. |

## Design

### Core: `UIElement.PassesUnclaimedInput`

A new public property on `UIElement`:

```csharp
public bool PassesUnclaimedInput { get; set; }
```

It is consulted only on canvas overlays. When it is `true`, a wheel scroll or a drag that nothing in the overlay's
chain takes falls through to the elements beneath it.

- **`Canvas.OnScroll`:** walk the hovered chain as today. If no element consumes the scroll and the chain's root is an
  overlay with the flag set, hit-test again at the mouse position with that overlay excluded (the existing
  `HitTest(point, includeOverlay)` filter) and walk the new chain. Repeat for stacked flagged overlays; the excluded
  set grows each time.
- **Drags:** `OnGestureDragStarted` applies the same rule to `ResolveDragOwner`. If the chain from
  `HitTest(drag.Start)` finds no owner and its root is a flagged overlay, resolve again below it. The element that
  claims the drag owns it for the rest of the gesture, as today.
- **Untouched:** hover, click and tap targeting, drag-and-drop sources and focus. Only the two "unclaimed" paths fall
  through, so unflagged overlays such as a modal dialog's backdrop keep blocking.
- No other core API changes.

**Engines:** a routing change above the connectors. MonoGame, Stride and a future FNA host need no work; touch drags
take the same `ResolveDragOwner` path.

**Known limit, kept on purpose:** in Edit mode a one-finger touch drag still edits, because the capture layer claims it.
Touch users pan in Interact mode. Two-finger pan is out of scope.

### `EditorSession`

- `EditorSession.Attach(DesignSession design, Canvas canvas, UIElement? scope)`. The existing two-argument overload
  stays and means `scope: null`. A non-null scope that isn't on `canvas` throws `ArgumentException`.
- `UIElement? Scope { get; }`, fixed for the session's lifetime. To re-scope, dispose and attach again.
- **Region:** the scope's surface bounds, intersected with the bounds of every ancestor that has `ClipToBounds` and with
  the canvas surface. With no scope, the whole surface. Empty when the scope is off the canvas or fully clipped.
- **`HitTest(point)`** returns `null` outside the region. Inside it, the behavior is unchanged: the topmost element past
  the editor's own layers, page overlays included.
- **`Select(instance)`** returns `false`, changing nothing, unless `instance` is in the scope subtree or lives in an
  overlay. Click selection goes through `HitTest`, so it's limited to the region automatically, and so are move drop
  targets: a drag can't drop into the sidebar.
- **One session per canvas:** `Attach` throws `InvalidOperationException` while another undisposed session is attached
  to the same canvas. Two sessions would both register key bindings and fight over focus and navigation state.
  Tracked in a `ConditionalWeakTable<Canvas, EditorSession>`; `Dispose` removes the entry.
- `static EditorSession? FindAttached(Canvas canvas)` returns the attached session or `null`, so tools can check before
  attaching.

### `EditorFrame`

- `EditorFrame.Attach(Canvas canvas, DesignSession design, UIElement? scope)`, passing the scope to the session. The
  existing overload stays.
- **Capture layer:** sets `PassesUnclaimedInput = true`. It keeps claiming left and touch drags in Edit mode; the wheel
  and middle drags fall through to the page.
- **Region tracking:** with a scope, the frame recomputes the region during render. When it changes, it sets the
  layer's Left/Top alignment, `Margin`, `Width` and `Height` and invalidates arrange. A layout change therefore shows
  up one frame late. Without a scope, the layer stretches over the canvas, as today.
- **Toolbar:** the `ToolbarPlacement` corner is measured from the region, and the toolbar is clamped onto the surface
  when the region is smaller than it.
- **Adorners:** scissored to the region, so an element scrolled partly out of view shows only its visible outline.
  Hover shows only while the mouse is inside the region.
- **Scope off the canvas or fully clipped:** the capture layer and the toolbar hide (`IsVisible = false`) and come back
  when the region is non-empty again. The keyboard lock and bindings stay, because the session is still attached;
  callers that detach their pages dispose the frame.
- **Unchanged:** the mode toggle, undo and redo, placement strategies, the touch tap handler and the `BringToTop`
  ordering.

### Samples

- **`EditorDemo`** attaches with `scope: page`, so its "Stop editing" and "Save" buttons stay clickable in Edit mode. If
  `EditorSession.FindAttached(canvas)` returns another session (the shell's), it shows "The shell's editor is on; press
  F4 to stop it" in its status line instead of attaching.
- **`SampleShell`:**
  - A footer under the sidebar's TreeView holds an "Edit demo" toggle button; its label flips to "Stop editing". F4
    toggles it too (the hosts use F1 for the bounds overlay and F2 for the diagnostics HUD).
  - The scope is the shell's `content` `ContentControl`, so the editor stays on across demo switches and the new page
    is editable at once.
  - On a demo switch the shell calls `Session.Clear()`, so nudges and Delete never act on a page that isn't shown.
  - The toggle and F4 refuse while a foreign session (`EditorDemo`'s) is attached, and the footer says why.
- The samples shell spec's "press Stop editing before switching demos" caveat no longer applies.

## Testing

Test-first, in `IcyUI.Tests`.

- **Core:**
  - A wheel over a flagged overlay scrolls the `ScrollViewer` beneath it; over an unflagged overlay it doesn't.
  - A middle drag over a flagged overlay pans the viewer beneath; a left drag the overlay claims stays with the
    overlay.
  - Two stacked flagged overlays both fall through.
  - A wheel that the overlay's own chain consumes doesn't fall through.
- **Session:**
  - `HitTest` returns `null` outside the region and in a part clipped by an ancestor.
  - `Select` refuses out-of-scope elements and accepts overlay elements.
  - A second `Attach` on the same canvas throws; attaching works again after `Dispose`.
  - `FindAttached` returns the session, then `null` after `Dispose`.
  - A scope that isn't on the canvas throws.
- **Frame:**
  - The capture layer's bounds equal the region and follow a viewport resize.
  - The toolbar sits in the region's corner and is clamped when the region is small.
  - The layer and toolbar hide when the scope detaches and return when it reattaches.
  - In Edit mode a click outside the region reaches the element there.
  - The wheel and a middle drag inside the region scroll the page.
- **Samples:**
  - In the shell with the editor on, a click on a sidebar row switches demos, and a click in the content selects.
  - Switching demos clears the selection.
  - `EditorDemo`'s buttons work in Edit mode.
  - `EditorDemo` refuses to attach while the shell's editor is on.
- **Hosts:** build only. Ivan smoke-tests both engines.

## Performance

- **Core:** the second hit test runs only when a wheel or drag went unclaimed over a flagged overlay. Everywhere else
  the cost is one `bool` check per unclaimed event.
- **Frame:** one region computation per rendered frame while attached: an ancestor walk plus rectangle intersections,
  O(depth). A re-arrange happens only when the rectangle changes.
- The editor is dev-only and opt-in; builds that never attach it pay nothing.
- `EditorFramePerformanceTests` gains a scoped attach on the 500-element page; the plan records the numbers.

## Documentation

Complete XML docs for `UIElement.PassesUnclaimedInput`, the new `Attach` overloads, `EditorSession.Scope` and
`EditorSession.FindAttached`. The `EditorFrame` class remarks get a scoped-use example.

## Out of scope

- Splitting the keyboard by focus (editor keys only while focus is in the scope).
- A core overlay-owner link (`AddOverlay(element, owner)`).
- Two-finger touch pan in Edit mode.
- Changing the scope of an attached session.
- Saving from inside the editor (Phase 10.5).
