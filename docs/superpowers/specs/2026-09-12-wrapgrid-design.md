# Tier-2 Phase 6 — WrapGrid, ListBox, and the `SelectingItemsControl` extraction

> Design spec for Phase 6 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`).
> The roadmap's own scope note named this phase "`GridView`" and described a WPF-tabular data browser
> (columns, headers, sort). Through this discussion that scope corrected twice: first to an auto-flowing
> item-grid layout (**`WrapGrid`**, unrelated to tabular data - a genuine future tabular-browser phase, if
> ever built, is free to use the name "`GridView`" for itself), then Ivan added a second control - a plain
> WPF-`ListBox`-style always-visible selectable list (**`ListBox`**), initially misnamed "`ListView`" before
> being corrected. Ivan also directed a class-hierarchy change beyond what either control needs on its own:
> **every control that realizes multiple items should be an `ItemsControl`**, including `WrapGrid` (originally
> scoped standalone) - and since `WrapGrid`/`ListBox` both need the same `SelectedIndex`/`SelectedItem`
> selection state `Selector` already has, that state is extracted into a new shared base,
> **`SelectingItemsControl`**, rather than duplicated three times.

## Context

Three real findings shaped this design, each verified against the actual current source, not assumed:

1. **`ItemsControl`'s virtualization geometry (`RealizeRange`/`LocateViewportStart`/`ExtentHeight`) is
   private and specific to single-column variable-height estimation** - but it's already cleanly separable
   from the genuinely reusable 90% of the class (pooling, `CreateContainer`, `AttachContainer`/
   `DetachContainer`, `INotifyCollectionChanged` incremental handling). Widening those three members (plus
   `EnsureRealized`/`Derealize` and the `verticalOffset`/`viewportHeight`/`viewportWidth`/`horizontalOffset`
   fields they and a subclass's own geometry both need) from `private` to `protected`/`protected virtual` is
   a **mechanical, additive visibility change** - the base class's own runtime behavior doesn't change at
   all, so the existing 744 tests should stay green unmodified. `OnViewportChanged` is already `public
   virtual` and already just calls `LocateViewportStart()` then `RealizeRange(...)`, so neither `WrapGrid`
   nor `ListBox` needs to override it.
2. **`Selector.cs`'s actual shipped shape** (read in full, not from the older plan document, which predates
   several since-shipped fixes: `PopupBackground`/`PopupBorderBrush`/`PopupBorderThickness`,
   `containerIndices`, the nested `PopupItemsHost : Panel, IVirtualizingScrollInfo` forwarding shim) shows
   the `SelectedIndex`/`SelectedItem`/`SelectionChanged`/`CreateContainer` extraction is a **pure hoist**:
   every other line in `Selector.cs` that references `SelectedIndex` (`AttachContainer`, `OnPopupItemTap`,
   etc.) keeps working completely unchanged once it's inherited instead of locally declared - accessibility
   (`protected`) is preserved, nothing about how those members are *used* changes.
3. **`ListBox` needs no geometry override at all** - a plain always-visible vertical selectable list is
   exactly what `ItemsControl`'s *default* single-column virtualization already does. It only adds selection
   (via the new `SelectingItemsControl` base) and click-to-select wiring on top - the smallest of the three
   new/changed classes here by a wide margin.

**No cross-engine impact.** Pure core-`IcyUI` work, same as every prior Tier-2 phase - `IcyUI.MonoGame`/
`IcyUI.Stride` untouched, `IcyUI.FNA` (still an unimplemented stub) needs no equivalent work.

## Decisions

| Decision | Choice |
|---|---|
| Scope | Two controls: `WrapGrid` (auto-flowing virtualizing grid layout, e.g. an icon/inventory grid) and `ListBox` (plain always-visible virtualizing selectable list, WPF-`ListBox`-shaped). No tabular columns/headers/sort/resize for either - a genuine future tabular "`GridView`" phase, if ever needed, is unrelated and unscoped here. |
| Class hierarchy | `ItemsControl` → **`SelectingItemsControl`** (new, abstract - `SelectedIndex`/`SelectedItem`/`SelectionChanged`, `CreateContainer` → `SelectorItem`) → `Selector` (existing, now derives from `SelectingItemsControl` instead of `ItemsControl` directly; keeps popup lifecycle/`HighlightedIndex`/nav/`DisplayMemberPath`/`SelectedValuePath`, none of which move) → `Dropdown`/`ComboBox` (unchanged); and, as siblings of `Selector` under `SelectingItemsControl`: `WrapGrid` (own grid-flow geometry override) and `ListBox` (no geometry override - inherits the default vertical-list behavior as-is). |
| `ItemsControl` changes | Widen `RealizeRange`/`LocateViewportStart` to `protected virtual`; wrap `ExtentHeight`'s body in a new `protected virtual float ComputeExtentHeight()`; widen `EnsureRealized`/`Derealize` and the `verticalOffset`/`viewportHeight`/`viewportWidth`/`horizontalOffset` fields to `protected`. No other change - `ExtentWidth`'s existing `=> viewportWidth` body already matches what `WrapGrid`/`ListBox` want, needs no override. |
| `Selector.cs` changes | `SelectedIndex`/`SelectedItem` (+ their two backing fields), `SelectionChanged`, and the `CreateContainer` override move up into `SelectingItemsControl`, verbatim. `Selector`'s base type changes to `SelectingItemsControl`. Every other member (popup lifecycle, `HighlightedIndex`, nav, `AttachContainer`/`DetachContainer`, `PopupItemsHost`, etc.) is untouched, including its own references to `SelectedIndex` (still resolves via inheritance). |
| Cell sizing (`WrapGrid`) | Explicit `ItemWidth`/`ItemHeight` (both `float`, validated `> 0`, default `64f`) - no auto-measure-to-discover-size bootstrapping. |
| Flow layout (`WrapGrid`) | Row-major auto-flow only (fills left-to-right, wraps down) - `columnsPerRow` always derived from `ContentBounds.Width`. No `Orientation`/explicit `Columns` override in v1. |
| Container | Both realize `SelectorItem` (inherited via `SelectingItemsControl.CreateContainer` - no per-control override needed). |
| Selection | Both get `SelectedIndex`/`SelectedItem` (inherited), single-select only, click/tap-to-select. **No keyboard/gamepad navigation in v1** for either - `Selector`'s 1D popup-list nav doesn't translate cleanly to `WrapGrid`'s 2D layout or generalize obviously to `ListBox` either; deferred as a future addition if a concrete need shows up. |
| Tap-to-select mechanics | New `SelectorItem.Tapped` event, raised from a `protected internal override void OnTap()` (mirrors `Button`'s own `OnTap`-to-`Click` shape). `WrapGrid`/`ListBox` each wire it in their own `AttachContainer`/`DetachContainer` override (small, identical shape in both - duplicated rather than pulled into `SelectingItemsControl`, since `Selector`'s popup items are hosted outside the normal visual tree and use a different, already-shipped mechanism that shouldn't be entangled with this). Confirmed via reading `Canvas.OnTap`'s actual dispatch (`HitTest` then `.OnTap()` on the hit element and every ancestor) that this reaches `WrapGrid`/`ListBox` items directly - no manual re-`HitTest` workaround needed, since (unlike `Selector`'s popup) their items live in the normal tree. |
| Drag/drop | Out of scope for both controls. An item's own content implements `IDragSource`/`IDropTarget` (Phase 1's existing framework) directly, same pattern `Slider`'s thumb/`SplitPane`'s divider already use. |
| Collection-change handling (`WrapGrid`) | Any `INotifyCollectionChanged` notification triggers a full derealize-and-recompute rather than incremental index-shifting - `WrapGrid` caches no per-item state worth preserving across a splice (unlike `ItemsControl`'s `knownHeights`), and pooling already makes a full recompute as cheap as an incremental update would be. `ListBox` needs no special handling at all - it's just `ItemsControl`'s own existing incremental logic, inherited and unchanged. |
| Theming | No `ControlTemplate` needed for either control - plain layout+virtualization containers, same tier as `Panel`/`Grid`/`ItemsControl` today. Only `SelectorItem`'s existing `Selected`-state visuals apply (already themed from Phase 5). |

## Detailed design

### 1. `ItemsControl` visibility widening

`sources/IcyUI/UI/Controls/ItemsControl.cs` - every change below is a visibility/structure widening only,
verified to preserve the class's own existing runtime behavior exactly:

```diff
- private float horizontalOffset;
- private float verticalOffset;
- private float viewportWidth;
- private float viewportHeight;
+ protected float horizontalOffset;
+ protected float verticalOffset;
+ protected float viewportWidth;
+ protected float viewportHeight;
```

```diff
- public float ExtentHeight => sumOfKnownHeights + ((items.Count - knownCount) * AverageHeight);
+ public float ExtentHeight => ComputeExtentHeight();
+
+ /// <summary>
+ /// Computes <see cref="ExtentHeight"/> - the running-average single-column estimate by default. A
+ /// subclass with different virtualization geometry (e.g. a uniform grid) overrides this with its own
+ /// formula instead.
+ /// </summary>
+ protected virtual float ComputeExtentHeight() => sumOfKnownHeights + ((items.Count - knownCount) * AverageHeight);
```

```diff
- private (int Index, float Offset) LocateViewportStart()
+ protected virtual (int Index, float Offset) LocateViewportStart()
```

```diff
- private void RealizeRange(int firstIndex, float firstOffset)
+ protected virtual void RealizeRange(int firstIndex, float firstOffset)
```

```diff
- private void EnsureRealized(int index)
+ protected void EnsureRealized(int index)
```

```diff
- private void Derealize(int index)
+ protected void Derealize(int index)
```

`ExtentWidth => viewportWidth` is untouched - already exactly what `WrapGrid`/`ListBox` want, no subclass
override needed. `anchorIndex`/`anchorOffset` stay `private` - only `RecordHeight`'s own (harmless-for-fixed-
size-content, see §3) above-viewport correction reads them, no subclass needs to.

### 2. `SelectingItemsControl` extraction

New file `sources/IcyUI/UI/Controls/SelectingItemsControl.cs`, `public abstract class
SelectingItemsControl : ItemsControl` - moved **verbatim** out of `Selector.cs` (confirmed against the
current shipped file, not the older plan document):

```csharp
public abstract class SelectingItemsControl : ItemsControl
{
    private int selectedIndex = -1;
    private object? selectedItem;

    public event EventHandler? SelectionChanged;

    public int SelectedIndex
    {
        get => selectedIndex;
        set
        {
            Guard.IsGreaterThanOrEqualTo(value, -1);
            if (value != -1)
                Guard.IsLessThan(value, ItemCount);
            if (selectedIndex == value)
                return;

            if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? oldContainer))
                ((SelectorItem)oldContainer).IsSelected = false;

            selectedIndex = value;
            selectedItem = selectedIndex == -1 ? null : GetItemAt(selectedIndex);

            OnSelectionChanged();

            if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? newContainer))
                ((SelectorItem)newContainer).IsSelected = true;

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public object? SelectedItem
    {
        get => selectedItem;
        set => SelectedIndex = IndexOfItem(value);
    }

    protected override ItemContainer CreateContainer(DataTemplate template, object item)
        => new SelectorItem { Content = template.Build(item) };

    /// <summary>
    /// Called after <see cref="SelectedIndex"/>/<see cref="SelectedItem"/> have been updated, before the
    /// realized-container stamp and the public <see cref="SelectionChanged"/> event - empty by default. A
    /// subclass with derived state of its own that depends on the new selection (e.g. <see cref="Selector"/>'s
    /// <see cref="Selector.SelectedValue"/>) overrides this to refresh it, rather than reacting to its own
    /// <see cref="SelectionChanged"/> subscription (which would depend on subscriber-ordering relative to any
    /// external subscriber this control's own consumer adds).
    /// </summary>
    protected virtual void OnSelectionChanged()
    {
    }
}
```

(Same `[Category]`/`[DefaultValue]`/`[RegisterReference]` attributes and XML docs the originals carry -
omitted above for brevity, not omitted in the actual change.)

**`Selector.cs` changes**: delete the two moved fields, the `SelectionChanged` event, the `SelectedIndex`/
`SelectedItem` properties, and the `CreateContainer` override. Change the class declaration:

```diff
- public abstract class Selector : ItemsControl
+ public abstract class Selector : SelectingItemsControl
```

Add one small override to restore the old setter's `UpdateSelectedValue()` call (a `Selector`-only concept,
since `SelectedValue`/`SelectedValuePath` don't move):

```csharp
protected override void OnSelectionChanged() => UpdateSelectedValue();
```

**Everything else in `Selector.cs` is untouched** - `AttachContainer`/`DetachContainer` still reference
`SelectedIndex`/`HighlightedIndex` exactly as they do today (now resolving `SelectedIndex` via
inheritance), `OnPopupItemTap` still sets `SelectedIndex` the same way, `PositionPopup`/`OpenPopup`/
`ClosePopup`/the nav handlers/`PopupItemsHost` are all identical. `UpdateSelectedValue()`'s own call now
runs from `OnSelectionChanged()` instead of inline in the setter, at the exact same point in the sequence
(right after `selectedItem` is assigned, before the container is stamped or `SelectionChanged` fires) - a
`protected virtual` hook, not an event subscription, so there's no dependence on subscriber ordering.

### 3. `WrapGrid`

New file `sources/IcyUI/UI/Controls/WrapGrid.cs`, `public class WrapGrid : SelectingItemsControl`:

- `float ItemWidth`, `float ItemHeight` - `[RegisterReference]`, `Guard.IsGreaterThan(value, 0f)`, default
  `64f` each. Changing either invalidates measure/arrange and forces a re-realize.
- `private int ColumnsPerRow => Math.Max(1, (int)(ContentBounds.Width / ItemWidth));`
- Overrides `ComputeExtentHeight()`: `(int)Math.Ceiling(ItemCount / (float)ColumnsPerRow) * ItemHeight`.
- Overrides `LocateViewportStart()`: `int row = (int)(verticalOffset / ItemHeight); int index =
  Math.Clamp(row * ColumnsPerRow, 0, ItemCount - 1); return (index, row * ItemHeight);` - no anchor-walk,
  exact arithmetic.
- Overrides `RealizeRange(firstIndex, firstOffset)`: computes `lastRow`/`lastIndex` from `verticalOffset +
  viewportHeight + ScrollAheadBuffer` the same way, then for each `index` in `[firstIndex, lastIndex]` calls
  the (now-`protected`) inherited `EnsureRealized(index)`, computes `row = index / columnsPerRow; column =
  index % columnsPerRow;`, and arranges it at `(ContentBounds.X + column*ItemWidth, ContentBounds.Y +
  row*ItemHeight - verticalOffset, ItemWidth, ItemHeight)` - then calls the inherited `Derealize(index)` for
  anything realized but outside the new range. Same overall shape as the base's own `RealizeRange`, just
  exact-arithmetic instead of an accumulating walk.

  **Worked example**: `ItemWidth = ItemHeight = 64`, `ContentBounds.Width = 400` → `ColumnsPerRow = 6`
  (`400/64 = 6.25` floors to `6`). `ItemCount = 100` → `ExtentHeight = ceil(100/6)*64 = 17*64 = 1088`. With
  `verticalOffset = 300`, `viewportHeight = 500`: `firstRow = 300/64 = 4` (floors) → `firstIndex = 24`;
  `lastRow = (300+500+100)/64 = 14` (floors) → `lastIndex = min(99, 15*6-1) = 89`. Realizes indices 24-89
  (11 rows × 6 columns).
- `AttachContainer`/`DetachContainer`: call `base.AttachContainer`/`base.DetachContainer` (inherited
  default `Parent = this; Canvas = Canvas;` wiring from `ItemsControl`) then stamp `IsSelected`, wire/unwire
  `SelectorItem.Tapped` (§5), and maintain a small private `Dictionary<SelectorItem, int> containerIndices`
  (own copy, same shape as `Selector`'s - not shared, see the Decisions table).
- `Chrome` renders `Background`/`BorderBrush`/`BorderThickness`/`Padding` only (inherited `Control`
  behavior, no override needed) - realized `SelectorItem`s live outside `Chrome`'s single-child slot,
  positioned within `ContentBounds` directly, exactly like `ItemsControl`'s own default `RealizationBounds
  => ContentBounds` (no override needed there either - `WrapGrid` has no popup-redirection need).
- `GetVisualChildren`/`OnRender`/`MeasureContent`/`ArrangeContent`: **all inherited unchanged** from
  `ItemsControl` - they already do exactly what `WrapGrid` needs (yield `Chrome` + realized containers,
  measure to `(ExtentWidth, ExtentHeight)`, arrange `Chrome` to `ActualBounds`).

**Inherited-but-unused overhead, noted explicitly**: `EnsureRealized`/`Derealize`'s bodies still call
`RecordHeight(index, measuredHeight)` internally (they're shared, unmodified). For `WrapGrid`, every cell's
measured height is always exactly `ItemHeight`, so after the first measurement `RecordHeight`'s `delta ==
0` guard makes every subsequent call a no-op - harmless, slightly wasted bookkeeping into fields
`ComputeExtentHeight()`'s override ignores entirely, not a correctness concern.

### 4. `ListBox`

New file `sources/IcyUI/UI/Controls/ListBox.cs`, `public class ListBox : SelectingItemsControl`:

```csharp
public class ListBox : SelectingItemsControl
{
    private readonly Dictionary<SelectorItem, int> containerIndices = [];

    protected override void AttachContainer(ItemContainer container, int index)
    {
        base.AttachContainer(container, index);
        var item = (SelectorItem)container;
        item.IsSelected = index == SelectedIndex;
        item.Tapped += Container_Tapped;
        containerIndices[item] = index;
    }

    protected override void DetachContainer(ItemContainer container)
    {
        var item = (SelectorItem)container;
        item.Tapped -= Container_Tapped;
        containerIndices.Remove(item);
        base.DetachContainer(container);
    }

    private void Container_Tapped(object? sender, EventArgs e)
    {
        if (sender is SelectorItem item && containerIndices.TryGetValue(item, out int index))
            SelectedIndex = index;
    }
}
```

That's the entire class. No geometry override (`RealizeRange`/`LocateViewportStart`/`ComputeExtentHeight`
all inherited as-is - a plain vertical list is exactly `ItemsControl`'s own default behavior), no new
properties beyond what `SelectingItemsControl`/`ItemsControl` already provide. This is the direct payoff of
the `SelectingItemsControl` extraction: the smallest possible realization of "an `ItemsControl` with
click-to-select."

### 5. `SelectorItem.Tapped`

`sources/IcyUI/UI/Controls/SelectorItem.cs` gains (additive - `Selector`/`Dropdown`/`ComboBox` are
unaffected, they don't subscribe to it):

```csharp
public event EventHandler? Tapped;

protected internal override void OnTap()
{
    base.OnTap();
    Tapped?.Invoke(this, EventArgs.Empty);
}
```

Verified against `Canvas.OnTap`'s actual dispatch (`Canvas.cs`): it `HitTest`s, then calls `.OnTap()` on
the hit element and every ancestor via `SelfAndAncestors(hit)`. Since `WrapGrid`/`ListBox` parent their
realized `SelectorItem`s into their own normal visual tree (via the inherited `AttachContainer` default,
`Parent = this`), a tapped item's own `OnTap()` fires directly through this existing mechanism - no manual
`HitTest` re-walk needed, unlike `Selector`'s popup items (which live outside the normal tree via
`Canvas.Overlays`, hence its own existing `OnPopupItemTap` workaround - left untouched, not retrofitted to
use `Tapped`, since it already works and this isn't its scope).

### 6. Theming & samples

No `ControlTemplate`/implicit `Style` needed for `WrapGrid`/`ListBox` themselves - `SelectorItem`'s existing
`Selected`-state style (Phase 5) already applies to both controls' realized items with no changes.

Two new samples added to Shared Samples (same precedent as `SplitPaneDemo`/`ExpanderDemo`/`SelectorDemo`):
`WrapGridDemo` (a scrollable grid of a few dozen icon-style tiles, exercising virtualization and resizing/
reflow) and `ListBoxDemo` (a scrollable list of selectable rows).

## Testing

- **`ItemsControl` regression**: the full existing `ItemsControlTests`/`SelectorTests`/`DropdownTests`/
  `ComboBoxTests` suites pass unchanged after the visibility-widening changes in §1 - this is the primary
  confirmation that §1 is truly behavior-preserving, not just an inspection claim.
- **`SelectingItemsControl`** (own new test file, via a `TestSelectingItemsControl : SelectingItemsControl`
  double mirroring `SelectorTests.TestSelector`'s existing pattern): defaults (`SelectedIndex == -1`,
  `SelectedItem == null`); validated range guard; `SelectedIndex`/`SelectedItem` sync both directions;
  `SelectionChanged` fires once per real change; `CreateContainer` realizes `SelectorItem`s; selection state
  survives a pool-and-reuse cycle.
- **`Selector`/`Dropdown`/`ComboBox`**: existing test suites re-run unchanged post-extraction (regression
  coverage that the hoist didn't alter behavior) - `UpdateSelectedValue`'s new `OnSelectionChanged()`-driven
  call path (§2) specifically covered by the existing `SelectedValuePath`-resolution tests still passing
  unmodified, confirming the hook fires at the same point in the sequence the old inline call did.
- **`WrapGrid`**: `ItemWidth`/`ItemHeight` validation; `ColumnsPerRow`/`ComputeExtentHeight` arithmetic
  across several width/count combinations including the exact §3 worked example; `LocateViewportStart`/
  `RealizeRange` realize exactly the expected index range for a given offset/viewport (matching the worked
  example); resizing `ContentBounds.Width` reflows already-realized items without requiring a scroll;
  selection sync + pool-survival; tap-to-select via `SelectorItem.Tapped`; `ItemsSource` `Add`/`Remove`/
  `Replace`/`Move`/`Reset` each trigger a correct full re-realize; `PoolingEnabled` reuse; hosted inside a
  real `ScrollViewer`, only the visible range is ever realized for a large source.
- **`ListBox`**: defaults; selection sync + pool-survival (inherited `SelectingItemsControl` behavior,
  confirmed working through `ListBox` specifically, not just assumed from `SelectingItemsControl`'s own
  tests); tap-to-select; `ItemsSource` reactivity uses `ItemsControl`'s existing *incremental* Add/Remove/
  Replace/Move handling correctly (unlike `WrapGrid`, this should NOT trigger a full re-realize - a
  regression test confirming `ListBox` actually inherits the cheaper incremental path, not a full recompute).
- Manual smoke test (both engines, per `[[feedback_smoke_test_notification]]`): `WrapGrid` scrolling/reflow/
  selection as before; `ListBox` scrolling/selection; confirm the whole `Selector`/`Dropdown`/`ComboBox`
  popup stack still behaves identically post-refactor (open/close, nav, filtering, typeahead).

## Critical files

**New:** `sources/IcyUI/UI/Controls/SelectingItemsControl.cs`, `sources/IcyUI/UI/Controls/WrapGrid.cs`,
`sources/IcyUI/UI/Controls/ListBox.cs`, `sources/IcyUI.Tests/Controls/SelectingItemsControlTests.cs`,
`sources/IcyUI.Tests/Controls/WrapGridTests.cs`, `sources/IcyUI.Tests/Controls/ListBoxTests.cs`,
`sources/Shared Samples/WrapGridDemo.cs`, `sources/Shared Samples/ListBoxDemo.cs`.

**Modified:** `sources/IcyUI/UI/Controls/ItemsControl.cs` (§1 visibility widening),
`sources/IcyUI/UI/Controls/Selector.cs` (§2 extraction - declarations removed, base type changed, nothing
else), `sources/IcyUI/UI/Controls/SelectorItem.cs` (new `Tapped` event + `OnTap()` override),
`sources/IcyUI.Tests/Controls/SelectorTests.cs` (regression re-verification, `SelectedValue`-timing test).

**Untouched (confirmed, not just assumed):** `sources/IcyUI/UI/Controls/Dropdown.cs`,
`sources/IcyUI/UI/Controls/ComboBox.cs`, `sources/IcyUI/UI/Controls/ItemContainer.cs`,
`sources/IcyUI/UI/Controls/ScrollViewer.cs` (its existing generic `IVirtualizingScrollInfo` delegation
already covers `WrapGrid`/`ListBox` with no change), `sources/IcyUI/UI/Canvas.cs`.

## Open items for implementation planning (not blocking spec approval)

- `ItemWidth`/`ItemHeight`'s `64f` default is a reasonable icon-grid-ish placeholder, not a carefully
  chosen constant - fine to adjust freely at implementation time, same status `DefaultEstimatedItemHeight`'s
  `40f` and `MaxDropDownHeight`'s `200f` had in their own specs.
- The tap-vs-drag-start gesture disambiguation for a future draggable grid item should get a quick
  behavioral check once a real `IDragSource`-implementing item exists to test against - expected to already
  work correctly given the existing gesture pipeline, not empirically confirmed as part of this design.
- Keyboard/gamepad navigation (Decisions table) is explicitly deferred for both controls - if a concrete
  need shows up later, it gets its own design discussion.
- Whether `ListBox` should later gain `SelectionMode` (multi-select) is unscoped - v1 is single-select only,
  matching what was actually asked for.
