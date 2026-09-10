# Tier-2 Phase 5 — Selector, Dropdown, ComboBox

> Design spec for Phase 5 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`).
> The plan calls Phase 5 out as a scope note only ("ComboBox"), needing its own dedicated design
> discussion before implementation starts - this is that discussion, written up. Confirmed with Ivan
> partway through this discussion: the phase actually delivers **two** controls, not one - see Scope
> below.

## Context

`ItemsControl` (Phase 2) deliberately shipped with no selection concept - `ItemContainer`'s own remarks
say a future `Selector`/`ListBox` "introduces a real `ListBoxItem` with selection state on top of this
mechanism," naming this exact phase as where that happens. Separately, `Canvas.Overlays` (Phase 1) was
built with "a future `ComboBox`'s dropdown" as one of its three named justifications, but its own remarks
are explicit that overlay content is **not** part of `HitTest`/focus traversal - "a caller wires that up
itself if it needs any." This phase is the first to actually need that wiring.

**No cross-engine impact.** Like `SplitPane`/`ItemsControl`/`Expander`, this is pure core-`IcyUI` layout,
input, and styling work - nothing in `IcyUI.MonoGame`/`IcyUI.Stride` changes, and `IcyUI.FNA` (still an
unimplemented stub) needs no equivalent work as a result of this phase. `TextBox.InsertText`'s
control-character filtering (the one place with engine-specific commentary, re: `MonoGame.Window.TextInput`
being backed by Windows' `WM_CHAR`) is already deliberately engine-agnostic - `ComboBox` reusing that
input path introduces no new cross-engine risk.

## Decisions

| Decision | Choice |
|---|---|
| Scope | Two controls, not one: `Dropdown` (fixed-text display, selection-only) and `ComboBox` (editable text, `Contains`-based case-insensitive filter/search). Both derive from a new abstract `Selector : ItemsControl`. |
| `Selector` properties | `SelectedIndex`/`SelectedItem`, `SelectedValuePath`/`SelectedValue` (get-only, derived), `DisplayMemberPath` - all path-based properties resolved via `DynamicPropertyPath` (runtime-typed, tolerant of heterogeneous/unknown item types), not the statically-typed `PropertyPath` markup binding otherwise uses. |
| Container | New `SelectorItem : ItemContainer` with `IsSelected`/`IsHighlighted`, driving new `ControlState.Selected`/`ControlState.Highlighted` flags through the existing `VisualState` machinery. |
| Container creation | `ItemsControl.RentContainer`'s hardcoded `new ItemContainer` becomes a call to a new `protected virtual ItemContainer CreateContainer(DataTemplate, object)` factory method with that exact default body; `Selector` overrides it to return a `SelectorItem`. Everything else in `ItemsControl` (pooling, virtualization, the `Dictionary<..., ItemContainer>` fields) is untouched - `Selector` downcasts when it needs selection state. |
| Overlay interactivity | `Canvas.HitTest` checks `Overlays` first (topmost/last-added wins), before falling back to today's `rootElements` scan. General framework capability - `OnTap`/`OnTouchDown`/`UpdateHover` all route through `HitTest`, so this one change makes overlay content hoverable/pressable/tappable for free, with no changes needed to those methods themselves. Reusable by `Dialog`'s modal backdrop later. |
| Outside-click dismiss | Falls out of the `HitTest` change for free, no special-casing needed: a click that misses the popup simply resolves to whatever's underneath via `Canvas`'s existing fallback, and gets dispatched to it normally. `Selector` itself is responsible for noticing "a press landed outside my own popup while it's open" (by subscribing to the same raw touch-down event `Canvas` does) and closing - it does not need to intercept or redirect the click itself. |
| Popup layout | Anchored below the control by default, flips above on viewport-bottom overflow; width matched to the control's own width; a `MaxDropDownHeight` property, with the realized `SelectorItem`s hosted inside a `ScrollViewer` once content exceeds it - keeps `ItemsControl`'s existing virtualization working. |
| Composition | `Chrome` hosts only `PART_ToggleButton` (+ `PART_TextBox` for `ComboBox`), mirroring `Expander`/`SplitPane`'s "chrome hosts the small bit" precedent. A separately-managed **popup host** (`PART_Popup`, wrapping a `PART_PopupScrollViewer`) holds the realized `SelectorItem`s; it is `AddOverlay`'d/`RemoveOverlay`'d from the owning `Canvas` as the popup opens/closes. `RentContainer`/pooling target the popup host's children, never `Content`/`Chrome`. |
| Focus model | `Selector` keeps a single `Canvas`-level focus point for the whole interaction - it never moves actual `UIElement` focus onto individual `SelectorItem`s. That one focus point is `Selector` itself for `Dropdown`, or `PART_TextBox` for `ComboBox` (§8 - a real `TextBox` needs actual keyboard focus to receive typed text). Whichever node holds it tracks a `HighlightedIndex` internally and subscribes directly to `INavigationEvents.FocusChanging`/`SelectElement`/`CloseModal` while focused (these are unified keyboard+gamepad events already, but today have zero framework-level consumers for directional movement - `Canvas` only wires `FocusNext`/`FocusPrevious`/`CloseModal`). No changes needed to `Canvas`'s own focus-traversal system. |
| `ComboBox` value model | Selection-only (searchable/filterable), not free text entry. Typing live-filters via `Contains` (case-insensitive) against `DisplayMemberPath`; Enter with a highlighted filtered match commits it; Enter with no match, or Escape, reverts the text to the last valid `SelectedItem`'s display text. |
| `Dropdown` typeahead | Letter-key presses (while focused, open or closed) jump `HighlightedIndex`/`SelectedIndex` to the next item whose `DisplayMemberPath` text starts with that letter (case-insensitive), cycling on repeated presses of the same key. |

## Detailed design

### 1. `Selector` properties

New file `sources/IcyUI/UI/Controls/Selector.cs`, `public abstract class Selector : ItemsControl`:

- `int SelectedIndex` (default `-1`) - setting it updates `SelectedItem`/`SelectedValue`, the affected
  `SelectorItem.IsSelected` flags (old index cleared, new index set, only if realized - see §2's
  "selection state survives de-realization" note), and raises `SelectionChanged`. Out-of-range values
  (`< -1` or `>= Count`) throw `ArgumentOutOfRangeException`, matching `DefaultEstimatedItemHeight`'s own
  `Guard.IsGreaterThan` precedent for validated setters elsewhere in this hierarchy.
- `object? SelectedItem` (default `null`) - setter resolves the item's index via `items` (the private
  list `ItemsControl` already maintains) and forwards to `SelectedIndex`'s setter; the two are always kept
  in sync, `SelectedIndex` is the source of truth internally.
- `string? SelectedValuePath` / `object? SelectedValue` (get-only) - `SelectedValuePath` is a
  `[RegisterReference]` string property; when set, wraps `new DynamicPropertyPath(value)` and re-resolves
  `SelectedValue` from `SelectedItem` whenever either changes. `SelectedValue` is `null` when
  `SelectedItem`/`SelectedValuePath` is `null`, or when the path fails to resolve (`DynamicPropertyPath`
  already returns `null` on failure rather than throwing).
- `string? DisplayMemberPath` - same `DynamicPropertyPath` shape. `protected string GetDisplayText(object
  item)` resolves it (falling back to `item?.ToString() ?? string.Empty` when unset or resolution fails)
  - the one shared helper `ComboBox`'s filter, `Dropdown`'s typeahead, and both controls' closed-state
  display text all call into.
- `event EventHandler? SelectionChanged`.
- `protected int HighlightedIndex` (default `-1`) - not a public property this phase (no confirmed use
  case for external code reading/setting it); purely the popup's "currently arrow-key-highlighted, not
  yet committed" cursor, described fully in §5.

`DynamicPropertyPath` (not the statically-typed `PropertyPath` markup binding otherwise uses) is the
right tool here specifically because `Selector.ItemsSource` items are `object` - their concrete type
isn't known when `DisplayMemberPath`/`SelectedValuePath` is set (often before `ItemsSource` even is), and
may be heterogeneous. `DynamicPropertyPath` resolves per-instance via reflection at each call and
tolerates a path that doesn't resolve for a given item (catches and returns `null`), exactly matching
this use case's needs.

### 2. `SelectorItem` and the `CreateContainer` factory hook

New file `sources/IcyUI/UI/Controls/SelectorItem.cs`:

```csharp
public class SelectorItem : ItemContainer
{
    public bool IsSelected { get; set; } // sets/clears ControlState.Selected, mirrors ToggleButton.IsChecked's shape
    public bool IsHighlighted { get; set; } // sets/clears ControlState.Highlighted
}
```

Two new flags on `ControlState` (`sources/IcyUI/UI/Styles/VisualState.cs`): `Selected = 1 << 7`,
`Highlighted = 1 << 8`.

`ItemsControl.RentContainer` (`ItemsControl.cs:428`) changes its one `new ItemContainer { Content =
template.Build(item) }` call site (`ItemsControl.cs:440`) to `CreateContainer(template, item)`, backed by:

```csharp
protected virtual ItemContainer CreateContainer(DataTemplate template, object item)
    => new() { Content = template.Build(item) };
```

`Selector` overrides it: `new SelectorItem { Content = template.Build(item) }`. Nothing else in
`ItemsControl` changes - pooling (`pools`), `realizedContainers`, `containerTemplates` all stay typed
`ItemContainer`; `Selector` downcasts (`(SelectorItem)realizedContainers[index]`) where it needs
`IsSelected`/`IsHighlighted`.

**Selection state survives de-realization.** Because virtualization means a selected item's
`SelectorItem` can be pooled/discarded and rebuilt later, `Selector` does not treat `IsSelected` as the
source of truth - `SelectedIndex` is. Whenever a container becomes realized (`Selector` hooks the same
point `EnsureRealized` calls into, or simpler: `Selector` overrides `CreateContainer` to also stamp
`IsSelected`/`IsHighlighted` from the current `SelectedIndex`/`HighlightedIndex` at creation time), its
`IsSelected`/`IsHighlighted` are set to match current selection/highlight state immediately, not left to
catch up later.

### 3. `Canvas.HitTest` overlay extension

`Canvas.HitTest` (`Canvas.cs:301`) currently only scans `rootElements`. Extended to check `overlayElements`
first, in reverse (last-added = topmost = checked first, matching draw order - the last entry in the list
draws on top per `Overlays`' own doc comment):

```csharp
public UIElement? HitTest(Point screenPoint)
{
    for (int i = overlayElements.Count - 1; i >= 0; i--)
    {
        UIElement? hit = overlayElements[i].HitTest(screenPoint); // screen space directly - no ScreenToCanvasSpace, overlays ignore the canvas transform (see UpdateLayout/RenderVisual's own reset)
        if (hit != null)
            return hit;
    }

    Vector2 canvasLocalPoint = ScreenToCanvasSpace(screenPoint);
    foreach (UIElement element in rootElements.OrderByDescending(e => e.ZIndex))
    {
        UIElement? hit = element.HitTest(canvasLocalPoint);
        if (hit != null)
            return hit;
    }

    return null;
}
```

Overlay elements are tested in **screen space directly** - unlike `rootElements`, they're arranged
against the raw viewport with the canvas transform reset (`RenderVisual`/`UpdateLayout` already do this
for drawing/layout; hit-testing must match). This one change is sufficient to make overlay content
participate in `OnTap`, `OnTouchDown`/`OnTouchUp` (press-state tracking), and `UpdateHover` - all three
already call `HitTest` and dispatch/track state generically off whatever it returns, with no
overlay-specific code of their own.

No change to `OnTap`/`OnTouchDown`/`UpdateHover` themselves, and no change to outside-click dismiss
handling at the `Canvas` level - see the Decisions table entry above for why that falls out for free.

### 4. Composition: `Chrome` vs. the popup host

Same `Chrome`-can-only-hold-one-child constraint `SplitPane`/`Expander` already documented
(`Control.Background`/`BorderBrush`/`BorderThickness`/`Padding` hard-cast `Chrome` to `Border`). `Selector`
needs two things live at once - the closed-state chrome (toggle, + text box for `ComboBox`) and the
popup's item list - so only one lives inside `Chrome`:

- **`PART_ToggleButton` (+ `PART_TextBox` for `ComboBox`) lives inside `Chrome`** - default-constructed in
  `Selector`'s (and `ComboBox`'s) constructor, `((Border)Chrome).Child = ...`, same pattern `SplitPane`'s
  divider and `Expander`'s header use.
- **The popup host lives outside `Chrome` and outside the normal visual tree entirely while closed** - a
  new internal panel (`PART_Popup`) wrapping a `ScrollViewer` (`PART_PopupScrollViewer`) whose `Content`
  is a plain container `Selector`'s own `CreateContainer`-built `SelectorItem`s get added to. This panel
  is never a child of `Chrome` or of `Selector` itself in the normal `GetVisualChildren` sense - `Selector`
  overrides `GetVisualChildren`/`OnRender` to yield/draw it only while it's open (see §5), and its actual
  screen presence comes entirely from `Canvas.AddOverlay`/`RemoveOverlay`, not from being parented into
  `Selector`'s own layout subtree.

`RentContainer`'s realized `SelectorItem`s get `Parent`/`Canvas` wired to the popup host (not to
`Selector` itself) - mirrors exactly how `ItemsControl.EnsureRealized` already wires `container.Parent =
this` today, just retargeted to the popup host instead of `this`.

### 5. Popup lifecycle and positioning

`Selector` gains `bool IsOpen` (default `false`), settable both by the toggle button's click and
programmatically:

- **Opening** (`IsOpen` set `true`): the popup host is measured (against `ItemsControl`'s existing
  `MeasureContent`/`ExtentHeight` machinery, so its natural height already accounts for the realized
  items); available space below the control (`Canvas` viewport height minus the control's own screen-space
  bottom edge) is compared against that natural height (clamped to `MaxDropDownHeight`) - placed below if
  it fits, above otherwise. Width is set to the control's own `ActualBounds.Width`. `PART_PopupScrollViewer`
  gets an explicit `Height` when the natural content height exceeds `MaxDropDownHeight`, otherwise sizes to
  content. `Canvas.AddOverlay(popupHost)` is called, and `OnViewportChanged` is invoked once immediately
  (mirroring how a `ScrollViewer` host normally drives it) so realization/virtualization starts working
  right away rather than waiting for the next scroll/resize.
- **Closing** (`IsOpen` set `false`, via toggle click, `SelectElement` commit, `CloseModal`, or an
  outside-click miss - see below): `Canvas.RemoveOverlay(popupHost)`. Realized `SelectorItem`s are **not**
  eagerly de-realized here - the existing `ItemsControl` pooling/virtualization mechanism already handles
  that the next time `OnViewportChanged` fires (e.g. on next open), consistent with "closing doesn't
  discard state you'll need again immediately" already true elsewhere in this codebase (pooled containers
  reused across opens are cheaper than rebuilding every time).
- **Outside-click dismiss**: while `IsOpen`, `Selector` subscribes directly to
  `Configuration.Input.Events.Touch.TouchDown` (the same raw event `Canvas.OnTouchDown` itself listens to
  - `Canvas.cs:489`), and on each press checks whether `Canvas.HitTest(point)`'s result is `this`, a
  descendant of `Chrome`, or a descendant of the popup host; if none of those, sets `IsOpen = false`. Since
  `Canvas`'s own dispatch (`OnTouchDown`/`OnTap`) runs its normal `HitTest` independent of `Selector`'s
  closing side-effect, whatever was actually pressed still receives its own press/tap handling in the same
  gesture - "dismiss + pass through" happens automatically, not through any explicit hand-off.

### 6. Focus and keyboard/gamepad interaction

`Selector` (or `ComboBox`'s `PART_TextBox`, which delegates back up - see §8) is the sole `Canvas`-focused
element throughout; `SelectorItem`s are never individually focused. While `Selector.IsFocused`, it
subscribes to `Configuration.Input.Events.Navigation`:

- **`FocusChanging`** (`AcceptableEventArgs<Vector2>`, Up/Down from arrow keys or gamepad stick/d-pad):
  reads the dominant axis; a negative-Y or negative-X direction decrements `HighlightedIndex`, positive
  increments it (clamped to the *currently filtered* item range for `ComboBox` - see §8), opening the
  popup first if it's closed. Sets `args.Handled = true` so nothing else reacts to the same press (no
  framework-level directional focus-traversal exists yet to conflict with regardless - see the Decisions
  table - but this keeps `Selector` correct if one's ever added).
- **`SelectElement`** (Enter/gamepad `A`): if closed, opens the popup. If open with `HighlightedIndex >=
  0`, commits it to `SelectedIndex` and closes. If open with no valid highlight (e.g. `ComboBox`'s filter
  matched nothing), no-ops (does not close) - the popup stays open showing the empty/no-match state so
  the user can keep typing or press Escape.
- **`CloseModal`** (Escape/gamepad `B`): closes the popup without changing `SelectedIndex`, discarding any
  in-progress highlight change. `ComboBox` additionally reverts its text (see §8).

Click/tap on `PART_ToggleButton` toggles `IsOpen` directly (via its own `OnTap`/`IsCheckedChanged`, no
navigation-event involvement).

### 7. `SelectorItem` mouse interaction

Beyond keyboard/gamepad, a direct tap on a realized `SelectorItem` (reachable via §3's `HitTest` extension)
sets `HighlightedIndex` to its index on hover/press (so keyboard/mouse stay in sync if the user switches
input mid-interaction) and commits it as `SelectedIndex` + closes the popup on tap-release, same effective
outcome as `SelectElement`.

### 8. `ComboBox` specifics

New file `sources/IcyUI/UI/Controls/ComboBox.cs`, `public class ComboBox : Selector`. `Chrome` hosts both
`PART_ToggleButton` and `PART_TextBox` (the toggle button typically rendered as a small arrow glyph beside
the text field, exact layout is a theming detail - see §10).

- **Filtering**: `PART_TextBox.TextChanged` re-evaluates a filtered view over `items` -
  `GetDisplayText(item).Contains(PART_TextBox.Text, StringComparison.OrdinalIgnoreCase)`. The popup host
  realizes only filtered-in items (their original indices preserved for `SelectedIndex`/`HighlightedIndex`
  purposes - the filter changes *which* indices are eligible for realization/highlight, not the underlying
  `items` list itself). Typing while closed both sets `IsOpen = true` and applies the filter from the
  first keystroke.
- **Commit**: `SelectElement`/Enter with a highlighted filtered match sets `SelectedItem` and closes,
  and `PART_TextBox.Text` is then set to that item's `GetDisplayText` (may differ from what was typed,
  e.g. differing case or a partial match). No match highlighted (filter yielded nothing, or nothing's been
  arrow-key-selected within the filtered results): Enter reverts `PART_TextBox.Text` to the last valid
  `SelectedItem`'s display text (or empty, if nothing was ever selected) and closes without changing
  `SelectedIndex` - **selection-only**, per the Decisions table: `ComboBox` never leaves `SelectedItem`
  pointing at nothing while `Text` holds an unmatched string.
- **`CloseModal`/Escape**: same revert-text-and-close behavior as a non-matching Enter, whether or not
  anything was filtered/highlighted at the time.
- **Focus delegation**: `PART_TextBox` itself is a real focusable `UIElement` (needs actual keyboard text
  input, which only a focused `TextBox` receives) - so `ComboBox`'s "single focus point" (§6) is
  `PART_TextBox`, not `ComboBox` itself. `ComboBox` subscribes to `Configuration.Input.Events.Navigation`
  gated on `PART_TextBox.IsFocused` instead of its own `IsFocused`, and `Canvas.Focus(comboBox)` calls (via
  tap on the closed control, or `MoveFocus`/Tab traversal landing on it) redirect to
  `Canvas.Focus(PART_TextBox)` - `ComboBox` itself is not independently focusable when it has a
  `PART_TextBox`.

### 9. `Dropdown` specifics

New file `sources/IcyUI/UI/Controls/Dropdown.cs`, `public class Dropdown : Selector`. `Chrome` hosts only
`PART_ToggleButton` - its `Content` is set to (or its own `ContentPresenter`-based template renders) the
current `SelectedItem`'s `GetDisplayText`, updated whenever `SelectedItem` changes.

- **Typeahead**: while focused, `Dropdown` also subscribes to raw `Configuration.Input.Events.Keyboard`
  text input (the same `TextInput`-style event `TextBox` itself consumes for typed characters, not
  `Keys.Up`/`Down` navigation) - a letter key sets `HighlightedIndex` (and, since `Dropdown` has no
  separate "commit" text field, immediately `SelectedIndex` too - selecting *is* the display) to the next
  item (wrapping) whose `GetDisplayText` starts with that letter, case-insensitive. Repeated presses of
  the same letter within a short window (matching typical native typeahead debounce, exact timing a
  theming/implementation-time detail) cycle to the next match rather than restarting the search string.
  Does not open the popup - typeahead works whether `IsOpen` is `true` or `false`, mirroring native
  `<select>`/listbox behavior.

### 10. Templating & theming

`OnApplyTemplate` (mirroring `Expander`/`SplitPane`'s own): `GetTemplateChild<ToggleButton>("PART_ToggleButton")`
and (for `ComboBox`) `GetTemplateChild<TextBox>("PART_TextBox")`, `GetTemplateChild<Panel>("PART_Popup")`,
`GetTemplateChild<ScrollViewer>("PART_PopupScrollViewer")` re-point the default-constructed parts when a
`Template` supplies them, falling back to the constructor-built defaults otherwise.

`DefaultTheme.xml` gains implicit styles/`ControlTemplate`s for `Dropdown` and `ComboBox`, each with
`Hovered`/`Pressed`/`Focused` states (existing pattern) plus a themed popup border. Exact chrome layout
(arrow glyph placement, `ComboBox`'s toggle-vs-text-box split) is a theming implementation detail worked
out when `DefaultTheme.xml` is actually written, not a design-level decision - same deferral `Expander`'s
own spec used for its chevron rotate-vs-swap mechanism. `SelectorItem` gets an implicit style with
`Selected`/`Highlighted` `VisualState`s (e.g. background tint), following `CheckBox`'s `Checked`-state
precedent.

### 11. Sample

A `SelectorDemo` (or split `DropdownDemo`/`ComboBoxDemo`) added to Shared Samples, same precedent as
`SplitPaneDemo`/`ExpanderDemo` - both controls bound to a list of demo items, exercising open/close,
keyboard nav, `ComboBox` filtering, and `Dropdown` typeahead.

## Testing

- `Selector` defaults: `SelectedIndex == -1`, `SelectedItem == null`, `SelectedValue == null`,
  `HighlightedIndex == -1`, `IsOpen == false`.
- `SelectedIndex`/`SelectedItem` setters stay in sync both directions; out-of-range `SelectedIndex` throws.
- `SelectedValuePath`/`DisplayMemberPath` resolve correctly against heterogeneous item types (a
  `DynamicPropertyPath` resolution test, not just a single fixed item type) and degrade to `null`/
  `ToString()` respectively when unresolvable.
- `CreateContainer` override: `Selector`-derived controls realize `SelectorItem`s (not bare
  `ItemContainer`s), verified via the existing `ItemsControl` realization test harness.
- Selection state survives a pool-and-reuse cycle: select an item, scroll it out of the realized range and
  back (or otherwise force de-realize/re-realize), confirm `IsSelected` is still correct on the
  re-realized container without re-selecting.
- `Canvas.HitTest`: a point over overlay content resolves to the overlay element even when it visually
  overlaps `rootElements` content beneath it; a point over neither resolves via the existing fallback path
  unchanged (regression coverage for the pre-existing behavior).
- Outside-click dismiss: simulated `TouchDown` outside both `Chrome` and the popup host while `IsOpen`
  closes the popup AND the same simulated press still reaches whatever it actually hit (a control placed
  underneath receives its own tap/focus).
- Keyboard/gamepad: simulated `FocusChanging`/`SelectElement`/`CloseModal` raises drive `HighlightedIndex`/
  `SelectedIndex`/`IsOpen` exactly as §6 describes, for both a raw keyboard-backed and gamepad-backed
  raise of the same events (confirming the unified-events assumption actually holds, not just keyboard).
- `ComboBox`: `Contains`-filter case-insensitivity; Enter-with-match commits and updates `Text`;
  Enter/Escape-with-no-match reverts `Text` without touching `SelectedIndex`; typing while closed opens
  and filters from the first keystroke.
- `Dropdown`: typeahead cycles through multiple matches on repeated same-letter presses; wraps at the end
  of the list; works both while closed and open.
- Manual smoke test (both engines, per `[[feedback_smoke_test_notification]]`): popup opens/closes/flips
  correctly near viewport edges, mouse and keyboard/gamepad navigation both drive the same highlighted
  state consistently, `ComboBox` filtering and `Dropdown` typeahead both behave as designed.

## Critical files

**New:** `sources/IcyUI/UI/Controls/Selector.cs`, `sources/IcyUI/UI/Controls/SelectorItem.cs`,
`sources/IcyUI/UI/Controls/Dropdown.cs`, `sources/IcyUI/UI/Controls/ComboBox.cs`,
`sources/IcyUI.Tests/Controls/SelectorTests.cs` (+ `DropdownTests.cs`/`ComboBoxTests.cs`).

**Modified:** `sources/IcyUI/UI/Controls/ItemsControl.cs` (`CreateContainer` factory hook),
`sources/IcyUI/UI/Canvas.cs` (`HitTest` overlay extension), `sources/IcyUI/UI/Styles/VisualState.cs` (new
`ControlState.Selected`/`Highlighted` flags), `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (new
`Dropdown`/`ComboBox`/`SelectorItem` styles/templates), Shared Samples (new demo).

## Open items for implementation planning (not blocking spec approval)

- Exact `PART_` layout inside the default `ComboBox`/`Dropdown` templates (arrow glyph placement, text box
  sizing) - cosmetic theming specifics, not architecturally significant. Ivan confirmed `PART_`-prefixed
  placeholder names are fine for now; final names can still change freely pre-1.0.
- Typeahead's same-letter-repeat cycling window duration - an implementation-time constant, not a design
  fork.
- Whether `MaxDropDownHeight`'s default value should be a fixed pixel constant or itself themable -
  small, non-architectural implementation-time call.
- The precise mechanics of "clamped to the currently filtered item range" for `ComboBox`'s
  `HighlightedIndex` (§6) when the filter changes out from under an in-progress highlight (e.g. the
  highlighted item gets filtered out by a further keystroke) - needs a concrete resolution (clamp to
  nearest remaining match vs. reset to first match) at implementation time, not a design-level fork.
