# Phase 10.3: the editor overlay (`EditorFrame`)

> Design spec, discussed with Ivan on 2026-10-05. It's the third sub-project of Phase 10 and builds on 10.1
> (`docs/superpowers/specs/2026-10-04-markup-design-document-design.md`) and 10.2
> (`docs/superpowers/specs/2026-10-05-markup-hot-reload-design.md`).

## Context

10.1 can edit a tracked page through typed operations, and 10.2 keeps the live pages in step with the markup text.
Neither has a user interface. 10.3 adds the first one: an overlay that **attaches to any live page**, lets the developer
pick elements with the pointer, and turns pointer gestures and keyboard commands into 10.1 edit operations. Undo,
lossless save and hot reload come from the layers below.

It serves both of Phase 10's hosts:
- the **in-game utility**: edit a page inside the real game scene and save after the playtest;
- the **designer app** (10.5), which will host the same frame.

**Success looks like this:** on a live page, the developer selects an element with a click, drags it to another place
in its panel or into another panel, drags its handles to resize it, and nudges it with the arrow keys. The page follows
live, each gesture is one undo step, and the saved file's diff contains only the attributes and elements the gestures
touched.

### What already exists

- Every pointer path in `Canvas` (tap, press, hover, gesture drags, scroll) starts from `Canvas.HitTest`, which checks
  `Canvas.Overlays` first. A full-surface, hit-testable overlay therefore already receives all of IcyUI's pointer input:
  the page's controls never see a click while the editor owns it.
- `IInputEventSystem.RegisterCommand(ICommand, KeyGesture, argument)` maps key gestures to plain `ICommand`s.
- `Canvas.IsMouseOverGUI` exists as the host-facing "is the pointer over the UI" signal, but it is a get-only
  auto-property that is never set.

## Decisions

| Decision | Choice |
|---|---|
| What a move means | **Placement depends on the container.** A StackPanel takes an insertion index; a Grid takes a row and column; any other panel takes a `Margin` offset. Pixel offsets are never written into flow layouts. |
| What a resize means | `Width`/`Height`. Inside a Grid, the edge also grows or shrinks `RowSpan`/`ColumnSpan` once it passes 25 % of the next (or the last) track. |
| Fine-tuning | Arrow keys nudge `Margin`, alignment-aware, in every container. |
| Grid spans | **Added to core `Grid`** as part of 10.3 (`Grid.RowSpan`, `Grid.ColumnSpan`). |
| Using the page vs. editing it | An **Edit/Interact mode toggle** on the attached frame (hotkey and toolbar button). In Interact mode the page and game behave normally and the selection stays visible. |
| Selection | **Single selection** in 10.3. Multi-select and clipboard move to 10.4, where the outline panel shows them. |
| Keyboard | **Commands, not key handlers.** Every action is an `ICommand` in `EditorCommands`; `EditorBindings` maps gestures to them and is rebindable. Gamepad and touch hosts can invoke the same commands. |
| Command collisions | **First-wins dispatch in core:** commands registered with `handlesGesture: true` run before the others and stop dispatch when they execute. |
| Architecture | A **headless `EditorSession`** (selection, modes, hit resolution, commands, placement) plus a **thin visual `EditorFrame`**. 10.4's panels bind to the session. |
| Where the code lives | `IcyUI.Design`, plus five small engine-neutral changes in core `IcyUI`. |

## Detailed design

### 1. Core changes (`IcyUI`)

1. **`Grid.RowSpan` and `Grid.ColumnSpan`.** Attached `int` properties, default 1. Values below 1 are treated as 1.
   - Accessors follow the existing `GetRow`/`SetRow` pattern: `GetRowSpan`, `SetRowSpan`, `GetColumnSpan`,
     `SetColumnSpan`. Markup: `<Border Grid.Row="1" Grid.ColumnSpan="2"/>`.
   - **Arrange:** a child's cell is the sum of the tracks it spans, starting at its clamped `Row`/`Column` and clamped
     to the grid's track count.
   - **Auto sizing** runs in two passes:
     1. Auto tracks size from the children that span exactly one track in that direction (today's
        `MaxChildTrackSize`, filtered).
     2. Each spanning child whose desired size (plus margin) exceeds the sum of its spanned tracks adds the deficit
        **evenly to the Auto tracks in its span**. Pixel and Star tracks never grow for a spanning child. A span with no
        Auto track ignores the deficit; the child is limited by its cell, as today.
2. **`Canvas.IsMouseOverGUI` becomes real.** `UpdateHover` already hit-tests the mouse every frame; it now also stores
   `hit != null`. The docs describe it as the host's "should the game ignore the mouse" check. While an `EditorFrame` is
   in Edit mode, its full-surface layer makes it `true` everywhere.
3. **`IInputEventSystem.UnregisterCommand(ICommand command, KeyGesture gesture)`** removes one registration and leaves
   the gesture's other commands in place. The existing `UnregisterCommand(KeyGesture)` stays.
4. **First-wins dispatch.** `RegisterCommand(ICommand command, KeyGesture gesture, object? argument = null,
   bool handlesGesture = false)`.
   - Registrations with `handlesGesture: true` are dispatched **before** the ordinary ones for the same gesture;
     among themselves, the **latest registration goes first**, so the most recently attached tool wins.
   - The first of them whose `CanExecute` is `true` executes, and dispatch stops.
   - When none of them can execute, the ordinary registrations all run, exactly as today.
   - Existing callers see no change. The implementers to update are core's `InputEventSystem` and the tests'
     `FakeInputEventSystem`; the engine projects only expose the interface.
5. **`Canvas.IsKeyboardNavigationEnabled`** (`bool`, default `true`). When `false`, the canvas ignores the navigation
   events (`FocusNext`/`FocusPrevious`, directional moves, `SelectElement`, `CloseModal`), so Tab, arrows, Enter and
   Esc stop moving page focus and closing modals. Games can also use it, for cutscenes say.

### 2. `EditorSession` (headless)

```csharp
public sealed class EditorSession : IDisposable
{
    public static EditorSession Attach(DesignSession design, Canvas canvas);

    public EditorMode Mode { get; set; }                  // Edit or Interact
    public EditorSelection? Selection { get; }
    public bool IsBlocked { get; }                        // the selection's document isn't in sync
    public string? BlockedReason { get; }
    public EditorCommands Commands { get; }
    public EditorBindings Bindings { get; }
    public PlacementRegistry Placement { get; }

    public event EventHandler? ModeChanged;
    public event EventHandler? SelectionChanged;
    public event EventHandler? BlockedChanged;

    public UIElement? HitTest(Point screenPoint);         // skips the editor's own layers
    public UIElement? ResolveSelectable(UIElement? hit);
    public void Select(DesignDocument document, NodeId node);
    public void Clear();
}

public sealed record EditorSelection(DesignDocument Document, NodeId Node, UIElement Instance);
```

**Lifetime.** `Attach` works on one canvas, across every tracked document whose pages are on it. `Dispose` restores
everything it changed.

**Modes.**
- **Entering Edit:** remembers and clears the canvas's focused element (so no TextBox takes typing), sets
  `Canvas.IsKeyboardNavigationEnabled = false`, and enables the commands.
- **Entering Interact, and `Dispose`:** restore the focus (if the element is still on the canvas) and the navigation
  flag.
- The session starts in Edit mode.

**Hit resolution.** `ResolveSelectable` walks from the hit element up through `UIElement.Parent` and returns the first
element that `DesignSession.FindDocument` tracks and whose node `DesignDocument.IsEditable` accepts.
- Template parts and generated item containers are either untracked or tracked under property elements, which
  `IsEditable` rejects, so a click inside a Button's template selects the Button, and a click on a ListBox item selects
  the ListBox.
- `HitTest` checks every overlay except the editor's own layers, then the root elements, so dropdowns and dialogs the
  page opened stay selectable.

**Selection.**
- `Instance` is the live copy that was picked. Edits still apply to every copy of the file, as in 10.1.
- On `DesignDocument.SubtreeReplaced` for the selected node, `Instance` is re-resolved in the same load scope.
- After every `Changed`, the selection clears if its node no longer exists (removed by an edit, undo or applied text).
  Ids survive `ApplyText` for matched elements (10.2), so the selection survives saves.
- `Select` and `Clear` are public, so 10.4's outline panel drives the same selection.

**Commands.** `EditorCommands` holds one `ICommand` per action. `CanExecute` is `false` in Interact mode, without a
selection (except ToggleMode, Undo and Redo), and while `IsBlocked` for the editing commands only (Delete and the
nudges). ToggleMode, the selection commands, Undo and Redo keep working while blocked: 10.2's undo goes through text, so
it's the way back out of a broken state.

`EditorBindings` holds the default gestures, registered with `handlesGesture: true` while the session is attached:

| Command | Default gesture | Effect |
|---|---|---|
| ToggleMode | Ctrl+Shift+E | Edit ↔ Interact. |
| Deselect | Esc | Clears the selection. |
| SelectParent / SelectFirstChild | Alt+Up / Alt+Down | Moves the selection to the nearest editable ancestor / first editable content child. |
| SelectPrevious / SelectNext | Alt+Left / Alt+Right | Moves to the previous / next editable sibling. |
| Delete | Delete | `RemoveElement` (refused for the root). |
| Undo / Redo | Ctrl+Z / Ctrl+Y, Ctrl+Shift+Z | The selected document's `UndoStack`; with no selection, the document last edited through this session. |
| Nudge (4 directions) | Arrows | `Margin` by 1 unit, alignment-aware (section 3). |
| NudgeLarge (4 directions) | Shift+Arrows | The same by 10 units. |

The default toggle avoids F1/F2, which the sample hosts bind to debug tools. Hosts change bindings through
`EditorBindings` before or after attaching; a change re-registers the affected gesture.

**Blocked documents.** While the selected document isn't `IsInSync` (10.2), selection, Undo and Redo still work, but
Delete, the nudges and the move/resize gestures are refused, and `BlockedReason` is the first `LiveErrors` message or "The markup has errors; fix them
first."

### 3. Placement strategies

```csharp
public interface IPlacementStrategy
{
    PlacementTarget? GetDropTarget(UIElement container, UIElement dragged, Point surfacePoint);
    IReadOnlyList<AttributeEdit> GetResize(UIElement container, UIElement child, ResizeHandle handle, Vector2 delta);
}
```

- `PlacementTarget` carries the content index for `MoveElement`, the attribute edits to apply with it (such as
  `Grid.Row`), and an indicator shape (a line or a rectangle in surface space) for the frame to draw.
- `AttributeEdit` is a name and a value, or a removal.
- `ResizeHandle` is one of the eight handles (corners and edge midpoints).
- `delta` is the drag offset from the gesture's start in surface units; strategies compute from the element's
  size and attributes at the gesture's start, which the session captures, so rounding never accumulates.

**The registry.** `PlacementRegistry` maps a container type to a strategy; the most-derived registered type wins. The
defaults are `StackPanel`, `Grid`, and `Panel` (the fallback). A game registers its own panel's strategy the same way.

**Choosing the drop container.** The deepest editable container under the pointer that isn't the dragged element or
inside it. Single-child containers (`Border`, `ContentControl`) accept a drop only when empty. When nothing accepts
the drop, the move does nothing.

**StackPanel.**
- **Drop:** the index of the nearest gap between children along the panel's orientation, decided by each child's
  midpoint. The indicator is an insertion line across the panel.
- **Resize:** `Width`/`Height`. A child of a stack is anchored, so a start-edge handle (left or top) changes only the
  size.

**Grid.**
- **Drop:** the cell under the pointer. `Grid.Row`/`Grid.Column` are written when non-zero and removed when they become
  0, to keep the markup minimal. The element keeps its spans, clamped so it fits. The indicator is the cell area the
  element will cover.
- **Resize:** `Width`/`Height`, and the span follows the dragged edge. When the edge passes
  `SpanThreshold` (default 0.25) of the next track, the span grows to include it; when it pulls back below the
  threshold of the last spanned track, the span shrinks (minimum 1). On a start edge, `Row`/`Column` move back as the
  span grows. `RowSpan`/`ColumnSpan` are written when above 1 and removed when they return to 1.

**The fallback: `Margin` offsets.** Any other panel moves an element by the drag delta without changing its size:

| Alignment | Horizontal move by dx |
|---|---|
| Left | `Margin.Left += dx` |
| Right | `Margin.Right -= dx` |
| Center, Stretch | `Margin.Left += dx`, `Margin.Right -= dx` |

Vertical moves work the same way with Top/Bottom. Arrow-key nudging uses this math in every container. A start-edge
resize in the fallback also moves the margin, so the opposite edge stays where it was.

**Values.** Surface units (DIPs), rounded to whole units, invariant culture. `Margin` is written in its shortest form:
`"4"`, `"4,8"` or `"l,t,r,b"`.

**One gesture, one undo step.** Every move, resize and nudge runs inside one `MarkupEditor.BeginTransaction`.

### 4. `EditorFrame` (visual)

```csharp
public sealed class EditorFrame : IDisposable
{
    public static EditorFrame Attach(Canvas canvas, DesignSession design);
    public EditorSession Session { get; }
    public EditorToolbarPlacement ToolbarPlacement { get; set; }   // corner, top-left by default
    // Adorner colors and HandleSize (7 by default) as settable properties.
}
```

**Two overlays.**
1. **The capture layer:** full-surface; draws every adorner. Hit-testable only in Edit mode.
2. **The toolbar:** small and always hit-testable, so the mode can be toggled with the mouse in Interact mode. It shows
   the Edit/Interact toggle, the selection label (`Button "okButton"`, or the type name when unnamed), and a red line
   with `BlockedReason` while blocked.

A hit-test-invisible element skips its whole subtree, so the toolbar is a separate overlay, not a child of the capture
layer.

**Staying on top.** A popup the page opens later is added as a newer overlay and would sit above the capture layer. In
Edit mode the frame checks after each layout pass whether its layers are the last overlays, and re-adds them when they
aren't.

**Adorners**, all in surface space so they stay crisp at any `EffectiveScale`:
- **Hover** (Edit mode): a thin outline of the element a click would select. One extra hit test per frame.
- **Selection:** an outline and eight handles. In Interact mode the outline is dimmed and the handles are hidden.
- **Drop indicator** while moving: the strategy's line or area.
- **Ghost** while moving: an outline of the dragged element's size following the pointer. There is no render-to-texture
  in core, so it isn't a picture of the element.
- **Canvas transforms:** corners are mapped through the canvas's content-to-surface transform. Pan and scale keep them
  axis-aligned. Under a rotated canvas the outline is the axis-aligned bounds of the mapped corners.

**Gestures (Edit mode)**, through the capture layer's tap and drag callbacks; the layer claims both drag axes.
- **Tap:** selects the resolved element; a tap on nothing selectable clears the selection.
- **Drag from a handle:** resize. Each move applies the strategy's attribute edits through the 10.1 fast path, so the
  element follows live.
- **Drag from an element's body:** move. An unselected element is selected first, so press-and-drag works in one
  motion. Nothing is written until the drop; then the target's edits are committed. A drop where nothing accepts the
  element, or onto its current place, changes nothing.
- **Cancel** (`DragCanceled`): a move commits nothing; a resize closes its transaction and undoes it once.
- **Touch** behaves the same through the gesture recognizer.

**Detaching.** `Dispose` removes both layers, disposes the session (which unregisters its bindings and restores focus
and navigation), and leaves no handlers on the canvas or the documents.

### 5. Edge cases

- **Nothing tracked on the canvas:** the frame attaches, nothing is selectable, and the toolbar says so.
- **The selected element is removed** by any route: the selection clears.
- **The root element:** selectable; Delete and moves are refused, resizing works.
- **An element in a page loaded twice:** the adorner follows the copy that was picked; edits update both copies.
- **A drag that starts in Interact mode** belongs to the page, and toggling to Edit mode mid-drag doesn't take it over.
- **The editor's own overlays are never selectable**, and neither is anything inside them.

## Performance

- **No editor attached:** zero cost. The frame and session live in `IcyUI.Design`, which release games don't reference.
  The core additions are an attached-property read per Grid child (the same as `Row`/`Column`), one bool assignment in
  `UpdateHover`, and a flag check in navigation and command dispatch.
- **Attached, Interact mode:** the dimmed selection outline only.
- **Edit mode:** one extra hit test per frame for hover, adorners for one selection and one hover target, and the
  topmost check after layout.
- **During a drag:** a move asks for a drop target per pointer move, which looks at one panel's children or tracks and
  writes nothing until the drop. A resize writes attributes through the fast path, without re-parsing.
- **Budget:** the frame's overhead in Edit mode is **≤ 0.2 ms per frame on a 500-element page**. A test measures it
  and reports it (it isn't asserted).

## Testing

All in `IcyUI.Tests`, with simulated input through the existing fake input system.

| Area | Tests |
|---|---|
| Core | Span arrange; Auto deficit spread over spanned Auto tracks; Pixel/Star tracks don't grow; span clamping; `IsMouseOverGUI` over roots, overlays and empty space; single-command unregister keeps the others; `handlesGesture` wins, falls through when it can't execute, and the latest registration goes first; `IsKeyboardNavigationEnabled` blocks Tab, arrows, Enter and Esc. |
| Session | Template parts and item containers resolve to their owner; opaque nodes are skipped; a page popup is selectable; the selection follows `SubtreeReplaced`, survives `ApplyText`, and clears when its node vanishes; Edit mode clears and restores focus and the navigation flag; commands are refused while blocked; each command's effect. |
| Strategies | StackPanel gap index in both orientations, at both ends and nested; Grid cell under the pointer; writing and removing `Row`/`Column`; span grow and shrink at the threshold on both edges; margin math for every alignment; shortest `Margin` form; a strategy registered for a derived panel wins. |
| Frame | Tap selects; a body drag moves to a new StackPanel index as one undo step with a minimal text change; a handle drag writes `Width` and `ColumnSpan`; cancel rolls back; the frame stays on top of a later popup; Interact mode lets a click reach a Button; detaching leaves no overlays, bindings or flags. |
| Performance | Edit-mode frame overhead on a 500-element page, reported. |

**Manual check:** a new **`EditorDemo`** in `Shared Samples`, registered in both hosts' `SampleGame.cs`. Its page has a
StackPanel, a Grid with some spans, a plain `Panel` for margin moves, a TextBox (focus handling) and a ComboBox (the
popup case), with the frame attached and a Save button that writes the markup to a temp file and shows the path. Ivan
runs it on both engines.

## Cross-engine impact

None in the engine projects. The frame is a plain `UIElement` drawn through `IRenderContext` and fed by core gestures
and commands. `IcyUI.MonoGame` and `IcyUI.Stride` don't change, and `IcyUI.FNA` needs nothing.

Touch editing won't work on MonoGame DesktopGL, which has no touch input at all (see CLAUDE.md's known issues). Mouse
editing works on both engines.

## Out of scope

- Multi-select, clipboard, duplicate, and inserting new elements from a toolbox (10.4).
- Snapping and alignment guides.
- Handles that follow a rotated canvas.
- Default gamepad bindings (the commands are ready for them).
