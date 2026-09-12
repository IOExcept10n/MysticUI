# Tier-2 Phase 6 — WrapGrid

> Design spec for Phase 6 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`).
> The roadmap calls this phase "`GridView`" and scopes it as a WPF-`ListView`+`GridView`-style tabular
> data browser (columns, headers, sort). Confirmed with Ivan at the start of this discussion: that's not
> actually what's needed here - the real ask is an auto-flowing grid *layout* (items wrap into rows/columns
> automatically, like WPF's `WrapPanel`/`UniformGrid`, sized for an icon/inventory grid), which is smaller,
> more foundational, and unrelated to tabular data. Renamed to **`WrapGrid`** so a genuine future tabular
> phase can still use the name "`GridView`" for itself without collision. The tabular browser itself is
> explicitly deferred, not part of this phase.

## Context

`ItemsControl` (Phase 2) virtualizes a single-column vertical list via variable-height estimation
(`knownHeights`, running-average, anchor-walk - see `ItemsControl.cs`'s `RealizeRange`/`LocateViewportStart`/
`RecordHeight`). None of that machinery is pluggable today beyond *where* a realized container is
positioned and *what type* it is (`RealizationBounds`/`CreateContainer`/`AttachContainer`/`DetachContainer`,
added in Phase 5) - the actual "which indices are visible, what rect does each get" computation is private
and tightly coupled to the single-column variable-height model.

A uniform-cell grid (fixed `ItemWidth`/`ItemHeight`, needed for the inventory-grid use case this phase
targets) is a **simpler** virtualization problem than what `ItemsControl` already solves - row/column
placement is exact arithmetic, no height estimation or anchor-walk needed - but it's a genuinely different
algorithm, not a variant of the existing one. Confirmed directly with Ivan during this discussion: rather
than refactor `ItemsControl`'s private internals to make the geometry step pluggable (real regression risk
to the `Selector`/`Dropdown`/`ComboBox` stack already built on it, for not much reuse payoff since the
geometry math needs a full override either way), `WrapGrid` is built **standalone** - its own
`IVirtualizingScrollInfo` implementation, its own realize/pool/derealize loop, `ItemsControl` untouched.

**No cross-engine impact.** Pure core-`IcyUI` layout/input/styling work, same as every prior Tier-2 phase -
`IcyUI.MonoGame`/`IcyUI.Stride` are untouched, `IcyUI.FNA` (still an unimplemented stub) needs no
equivalent work.

## Decisions

| Decision | Choice |
|---|---|
| Scope | An auto-flowing, virtualizing grid *layout* control (`WrapGrid : Control`), not a tabular data browser. No columns, headers, sorting, or per-column typed data - a genuine future "`GridView`"/`DataGrid` phase, if ever built, is unrelated to this one. |
| Relationship to `ItemsControl` | Standalone, not a subclass. Own `ItemsSource`/`ItemTemplate`/`ItemTemplateSelector`/`PoolingEnabled` surface (same shape as `ItemsControl`'s, independently implemented), own `IVirtualizingScrollInfo` implementation, own pooling. Zero changes to `ItemsControl.cs`. |
| Cell sizing | Explicit `ItemWidth`/`ItemHeight` (both `float`, validated `> 0`) - no auto-measure-to-discover-size bootstrapping. Every cell is exactly this size; content that doesn't fit is the item template's own problem (e.g. clip, or size itself to fit), same as any other fixed-size layout slot in this framework. |
| Flow layout | Row-major auto-flow only (fills left-to-right, wraps to the next row below) - matches the vertical-scroll-centric shape every other virtualizing surface in this codebase already has (`ItemsControl`, `ScrollViewer`). No `Orientation` property, no explicit `Columns` count override, in v1 - `columnsPerRow` is always derived from available width. |
| Container | Realizes `SelectorItem` (reused as-is from Phase 5 - already has `IsSelected`/`ControlState.Selected`, no popup coupling). No `CreateContainer`-style extension point - container type is fixed, no known need for a `WrapGrid` subclass yet (YAGNI, unlike `ItemsControl`/`Selector`'s more open-ended hook). |
| Selection | `SelectedIndex`/`SelectedItem`, click/tap-to-select only. **No keyboard/gamepad grid navigation in v1** - 2D directional movement (which neighbor does "Up" mean when row lengths can differ at the very end of the list?) is a harder, separate problem from `Selector`'s 1D popup-list nav and wasn't part of what was actually asked for; flagged as a deferred future addition, not attempted here. |
| Tap-to-select mechanics | New `SelectorItem.Tapped` event, raised from a `protected internal override void OnTap()` - mirrors `Button`'s own `OnTap`-to-`Click` shape. `WrapGrid` subscribes per-container on realize, unsubscribes on de-realize. Since realized items live in `WrapGrid`'s own normal visual tree (not an overlay), this reaches them through `Canvas`'s existing per-element `OnTap` dispatch directly - no manual re-`HitTest` workaround needed (unlike `Selector`'s popup items, which need that workaround specifically because overlay content isn't reachable through the normal tree-rooted dispatch). |
| Drag/drop | Out of scope for `WrapGrid` itself. An item's own content implements `IDragSource`/`IDropTarget` (Phase 1's existing framework) directly, same pattern `Slider`'s thumb/`SplitPane`'s divider already use - `WrapGrid` needs no special knowledge of dragging. Tap-vs-drag-start disambiguation is the existing touch/gesture pipeline's job (a tap is a press+release within a small movement threshold; a drag is a press+move past it), already proven to coexist correctly elsewhere in the framework - worth a quick behavioral confirmation at implementation time, not a design fork. |
| Collection-change handling | Any `INotifyCollectionChanged` notification (`Add`/`Remove`/`Replace`/`Move`/`Reset`) triggers a full re-snapshot + derealize-everything-and-recompute, rather than `ItemsControl`'s careful incremental index-shifting. Deliberate simplification: `WrapGrid` caches no per-item state (no heights to preserve across a splice, unlike `ItemsControl`'s `knownHeights`), so there's nothing incremental handling would actually save - pooling already makes a full recompute just as cheap as an index-preserving update would be. |
| Theming | No `ControlTemplate` needed for `WrapGrid` itself - a plain layout+virtualization container, same tier as `Panel`/`Grid` today. Only `SelectorItem`'s existing `Selected`-state visuals apply (already themed from Phase 5). |

## Detailed design

### 1. Properties

New file `sources/IcyUI/UI/Controls/WrapGrid.cs`, `public class WrapGrid : Control, IVirtualizingScrollInfo`:

- `IEnumerable? ItemsSource` - own implementation, same live-reactive-to-`INotifyCollectionChanged` shape
  `ItemsControl.ItemsSource` has (see Decisions: collection-change handling differs, see §4).
- `DataTemplate? ItemTemplate`, `Func<object, DataTemplate>? ItemTemplateSelector` - same resolution order
  (`ItemTemplateSelector` first, falling back to `ItemTemplate`) as `ItemsControl`.
- `bool PoolingEnabled` (default `true`) - same shape as `ItemsControl.PoolingEnabled`.
- `float ItemWidth`, `float ItemHeight` - both `[RegisterReference]`, `Guard.IsGreaterThan(value, 0f)`
  validated setters (matching `DefaultEstimatedItemHeight`'s own precedent), default `64f` each. Changing
  either invalidates measure/arrange and forces a full re-realize (column count and every item's target
  rect both depend on these).
- `int SelectedIndex` (default `-1`) - validated (`Guard.IsGreaterThanOrEqualTo(-1)`,
  `Guard.IsLessThan(ItemCount)` when `!= -1`, matching `Selector.SelectedIndex`'s own precedent exactly).
  Setting it clears the old realized container's `SelectorItem.IsSelected` (if realized) and sets the new
  one's (if realized), and raises `SelectionChanged`.
- `object? SelectedItem` (default `null`) - setter resolves the item's index and forwards to
  `SelectedIndex`; not in the list resolves to `-1`/`null` rather than throwing (matches
  `Selector.SelectedItem`'s own precedent).
- `event EventHandler? SelectionChanged`.

No `DisplayMemberPath`/`SelectedValuePath` - those exist on `Selector` to derive display text for a
single-line closed-state summary, which has no equivalent concept here (every item's own `ItemTemplate`
already renders its own full content).

### 2. Virtualization: `IVirtualizingScrollInfo`

```csharp
public float ExtentWidth => viewportWidth; // mirrors ItemsControl.ExtentWidth - no horizontal scrolling supported
public float ExtentHeight => RowCount * ItemHeight;
public event EventHandler<float>? VerticalOffsetCorrectionRequested; // declared to satisfy the interface, never raised - see below
```

`VerticalOffsetCorrectionRequested` exists on the interface to let content whose item sizes change after
being realized (`ItemsControl`'s whole reason for having it) correct for that without a visible jump. Every
`WrapGrid` cell is a fixed, known-upfront size - nothing here ever needs that correction, so the event is
never invoked. Interface-compliance-without-use, same as any interface member a given implementer
legitimately has nothing to do for.

`columnsPerRow` is recomputed from the actual arranged width, not the raw viewport parameter (mirrors how
`ItemsControl.RealizationBounds` - `ContentBounds` - is what item rects are actually placed against, while
`verticalOffset`/`viewportHeight` drive range math separately):

```csharp
private int ColumnsPerRow => Math.Max(1, (int)(ContentBounds.Width / ItemWidth));
private int RowCount => (int)Math.Ceiling(ItemCount / (float)ColumnsPerRow);
```

`OnViewportChanged(horizontalOffset, verticalOffset, viewportWidth, viewportHeight)` stores the offset/
viewport fields (same bookkeeping shape as `ItemsControl.OnViewportChanged`), then computes the visible row
band and realizes every index in it:

```csharp
const float ScrollAheadBuffer = 100f; // same constant/precedent as ItemsControl.RealizeRange
int columnsPerRow = ColumnsPerRow;
int firstRow = (int)(verticalOffset / ItemHeight);
int lastRow = (int)((verticalOffset + viewportHeight + ScrollAheadBuffer) / ItemHeight);

int firstIndex = Math.Max(0, firstRow * columnsPerRow);
int lastIndex = Math.Min(ItemCount - 1, ((lastRow + 1) * columnsPerRow) - 1);

var stillRealized = new HashSet<int>();
for (int index = firstIndex; index <= lastIndex; index++)
{
    EnsureRealized(index); // resolves template, rents/creates a SelectorItem, wires Tapped
    stillRealized.Add(index);

    int row = index / columnsPerRow;
    int column = index % columnsPerRow;
    var targetRect = new Rectangle(
        ContentBounds.X + (column * (int)ItemWidth),
        ContentBounds.Y + (row * (int)ItemHeight) - (int)verticalOffset,
        (int)ItemWidth,
        (int)ItemHeight);

    ItemContainer container = realizedContainers[index];
    container.InvalidateArrange();
    container.Arrange(targetRect);
}

foreach (int realizedIndex in realizedContainers.Keys.ToList())
{
    if (!stillRealized.Contains(realizedIndex))
        Derealize(realizedIndex);
}
```

No anchor-walk, no height cache, no estimation - every index's row/column/rect is exact, O(1) arithmetic.
`EnsureRealized`/`Derealize`/pooling (`RentContainer`, `Stack<ItemContainer>` per `DataTemplate`) mirror
`ItemsControl`'s own shape closely (same proven pattern) but with no `knownHeights`/`RecordHeight`
bookkeeping at all - there's nothing variable to track.

**Worked example**: `ItemWidth = ItemHeight = 64`, `ContentBounds.Width = 400` → `columnsPerRow = 6`
(`400 / 64 = 6.25` → floors to `6`). `ItemCount = 100` → `RowCount = ceil(100 / 6) = 17` →
`ExtentHeight = 17 * 64 = 1088`. With `verticalOffset = 300`, `viewportHeight = 500`:
`firstRow = 300 / 64 = 4` (floors), `lastRow = (300 + 500 + 100) / 64 = 900 / 64 = 14` (floors) → realizes
rows 4 through 14 inclusive (11 rows × 6 columns), i.e. indices `4*6=24` through
`min(99, 15*6-1) = min(99, 89) = 89`.

### 3. Container realization, pooling, and tap-to-select

```csharp
private ItemContainer EnsureRealized(int index)
{
    if (realizedContainers.TryGetValue(index, out ItemContainer? existing))
        return existing;

    object item = items[index];
    DataTemplate template = ResolveTemplate(item); // ItemTemplateSelector, falling back to ItemTemplate
    SelectorItem container = RentContainer(template, item); // pool lookup, or new SelectorItem { Content = template.Build(item) }

    container.IsSelected = index == SelectedIndex;
    container.Tapped += Container_Tapped;
    containerIndices[container] = index; // small Dictionary<SelectorItem, int>, mirrors Selector's own containerIndices

    container.Parent = this;
    container.Canvas = Canvas;
    realizedContainers[index] = container;
    return container;
}

private void Derealize(int index)
{
    if (!realizedContainers.Remove(index, out ItemContainer? container))
        return;

    var selectorItem = (SelectorItem)container;
    selectorItem.Tapped -= Container_Tapped;
    containerIndices.Remove(selectorItem);

    container.Parent = null;
    container.Canvas = null;

    if (PoolingEnabled) /* return to the per-template pool, same as ItemsControl.Derealize */;
}

private void Container_Tapped(object? sender, EventArgs e)
{
    if (sender is SelectorItem item && containerIndices.TryGetValue(item, out int index))
        SelectedIndex = index;
}
```

`SelectorItem` gains (in `sources/IcyUI/UI/Controls/SelectorItem.cs`, additive - `Selector`/`Dropdown`/
`ComboBox` are unaffected):

```csharp
public event EventHandler? Tapped;

protected internal override void OnTap()
{
    base.OnTap();
    Tapped?.Invoke(this, EventArgs.Empty);
}
```

### 4. `ItemsSource` and collection-change handling

Own `items`/`ResetItems`/`OnSourceCollectionChanged`, same *shape* as `ItemsControl`'s (snapshot
`IEnumerable` into a `List<object>`, subscribe to `INotifyCollectionChanged` when the source implements it,
unsubscribe on `OnDetached`/reassignment, re-subscribe on `OnAttached` for the same `Page.KeepAlive`
re-attach reason `ItemsControl.OnAttached`'s own remarks document) - but every branch of
`OnSourceCollectionChanged` (`Add`/`Remove`/`Replace`/`Move`/`Reset`) does the same thing: re-snapshot
`items`, derealize every currently-realized container (pooled, not discarded), `InvalidateMeasure()`/
`InvalidateArrange()`. The next `OnViewportChanged` re-realizes from scratch against the new `items`/
`ItemCount`. No incremental index-shifting - see the Decisions table for why that's a deliberate
simplification, not a missed optimization.

### 5. Composition and layout plumbing

`WrapGrid : Control` - `Chrome` (the default `Border`) renders `Background`/`BorderBrush`/`BorderThickness`/
`Padding` only, same as `ItemsControl`'s own use of `Chrome`; realized `SelectorItem`s are managed as extra
children outside `Chrome`'s single-child slot, positioned directly within `ContentBounds` (`Chrome`'s own
inset content area), mirroring `ItemsControl.RealizationBounds => ContentBounds` exactly (no override point
needed here - `WrapGrid` doesn't have Phase 5's popup-redirection use case).

```csharp
protected override void ArrangeContent()
{
    Chrome.InvalidateArrange();
    Chrome.Arrange(ActualBounds);
}

protected override IEnumerable<UIElement> GetVisualChildren()
{
    yield return Chrome;
    foreach (int index in realizedContainers.Keys.OrderBy(i => i))
        yield return realizedContainers[index];
}

protected override Size MeasureContent() => new((int)ExtentWidth, (int)ExtentHeight);

protected override void OnRender(IRenderContext context)
{
    Chrome.Draw(context);
    foreach (int index in realizedContainers.Keys.OrderBy(i => i))
        realizedContainers[index].Draw(context);
}
```

Identical in shape to `ItemsControl`'s own four overrides of the same names - this is boilerplate every
`IVirtualizingScrollInfo`-implementing, `Control`-based container needs, not something worth a shared base
for on its own (see the Decisions discussion on why a shared base wasn't chosen for this phase).

Used the same way `ItemsControl` is - as a `ScrollViewer.Content` (its `ArrangeContent`'s virtualizing
branch already delegates to any `IVirtualizingScrollInfo` generically, confirmed against `ScrollViewer.cs`
directly - no `ScrollViewer` change needed). A `WrapGrid` placed directly inside a `Grid`/`StackPanel`
instead never receives `OnViewportChanged` calls and silently renders empty, exactly like a bare
`ItemsControl` would - worth the same doc-comment warning `ItemsControl`'s own class remarks already carry.

### 6. Theming

No `ControlTemplate`/implicit `Style` needed for `WrapGrid` itself in `DefaultTheme.xml` - it has no visual
identity beyond its (optional) `Background`/`BorderBrush`, already themable via `Control`'s existing
properties with no new markup required. `SelectorItem`'s existing `Selected`-state style (from Phase 5)
already applies to `WrapGrid`'s realized items with no changes.

### 7. Sample

A `WrapGridDemo` added to Shared Samples (same precedent as `SplitPaneDemo`/`ExpanderDemo`/`SelectorDemo`) -
a scrollable grid of a few dozen demo items (enough to exercise virtualization, e.g. colored icon-style
tiles), demonstrating resizing (column count changing live) and click-to-select highlighting.

## Testing

- Defaults: `SelectedIndex == -1`, `SelectedItem == null`, `ItemWidth`/`ItemHeight` at their `64f` default.
- `ItemWidth`/`ItemHeight` setters: `Guard.IsGreaterThan` rejects `<= 0`; changing either triggers a full
  re-realize with correctly recomputed positions.
- `ColumnsPerRow`/`RowCount`/`ExtentHeight` arithmetic across several `ContentBounds.Width`/`ItemCount`
  combinations, including the exact worked example in §2 (a concrete regression anchor, not just a formula
  restated as a test).
- `OnViewportChanged` realizes exactly the expected index range for a given offset/viewport (matching §2's
  worked example precisely) and de-realizes everything outside it on scroll.
- Resizing `ContentBounds.Width` (e.g. simulating a container resize) changes `ColumnsPerRow` and correctly
  re-flows already-realized items to their new row/column without requiring a scroll.
- Selection: `SelectedIndex`/`SelectedItem` stay in sync both directions; `SelectorItem.IsSelected` is set/
  cleared correctly on both the old and new selected container when realized, and survives a
  derealize/re-realize (pool-and-reuse) cycle without re-selecting, matching `Selector`'s own precedent
  test shape.
- Tap-to-select: a simulated tap on a realized `SelectorItem` (via `Canvas`'s normal per-element `OnTap`
  dispatch, not a manual `HitTest` workaround) sets `SelectedIndex` to that item's index.
- `SelectorItem.Tapped`: fires on `OnTap()`, doesn't fire for an unrelated element's tap.
- `ItemsSource` reactivity: `Add`/`Remove`/`Replace`/`Move`/`Reset` on an `ObservableCollection` source each
  trigger a correct full re-realize (right `ItemCount`, right items at each index afterward) - deliberately
  not testing "did it avoid re-realizing unaffected items," since it deliberately doesn't.
- `PoolingEnabled`: a de-realized-then-re-realized-with-the-same-template item reuses a pooled container
  rather than building a fresh one (same assertion shape `ItemsControlTests`' own pooling test uses).
- Integration: hosted inside a real `ScrollViewer`, virtualization actually engages (only the visible range
  is ever realized for a large `ItemsSource`), matching `ItemsControl`'s own `ScrollViewer`-hosted test
  precedent.
- Manual smoke test (both engines, per `[[feedback_smoke_test_notification]]`): scrolling a large item
  count stays smooth with only the visible band realized, resizing the host reflows columns live, tap
  selection highlights correctly, no visual seams/gaps between cells.

## Critical files

**New:** `sources/IcyUI/UI/Controls/WrapGrid.cs`, `sources/IcyUI.Tests/Controls/WrapGridTests.cs`,
`sources/Shared Samples/WrapGridDemo.cs`.

**Modified:** `sources/IcyUI/UI/Controls/SelectorItem.cs` (new `Tapped` event + `OnTap()` override -
additive, no behavior change for existing `Selector`/`Dropdown`/`ComboBox` consumers).

**Untouched (confirmed, not just assumed):** `sources/IcyUI/UI/Controls/ItemsControl.cs`,
`sources/IcyUI/UI/Controls/ScrollViewer.cs` (its existing generic `IVirtualizingScrollInfo` delegation
already covers `WrapGrid` with no change), `sources/IcyUI/UI/Canvas.cs`.

## Open items for implementation planning (not blocking spec approval)

- `ItemWidth`/`ItemHeight`'s `64f` default is a reasonable icon-grid-ish placeholder, not a carefully
  chosen constant - fine to adjust freely at implementation time, same status `DefaultEstimatedItemHeight`'s
  `40f` and `MaxDropDownHeight`'s `200f` had in their own specs.
- The tap-vs-drag-start gesture disambiguation (Decisions table) should get a quick behavioral check once
  a real `IDragSource`-implementing item exists to test against - expected to already work correctly given
  the existing gesture pipeline, but not empirically confirmed as part of this design.
- Keyboard/gamepad grid navigation (Decisions table) is explicitly deferred, not designed here - if a
  concrete need shows up later, it gets its own design discussion rather than being retrofitted from this
  spec's assumptions.
