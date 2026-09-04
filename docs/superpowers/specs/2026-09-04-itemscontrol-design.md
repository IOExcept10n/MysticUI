# Tier-2 Phase 2 — ItemsControl

> Design spec for Phase 2 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`).
> The plan calls Phase 2 out as a scope note only, needing its own dedicated design discussion before
> implementation starts - this is that discussion, written up.

## Context

Nothing in IcyUI today generates UI from a data collection. `Panel.Children`/`Grid`'s definition lists
are hand-populated; nothing reacts to `INotifyCollectionChanged`. `ScrollViewer.ArrangeContent` always
fully measures/arranges its single `Content`, with no concept of "don't lay out what isn't visible."
`ControlTemplate.LoadContent` (Phase 9 M5) builds a fresh visual tree via a fresh `MarkupLoader` call
every time, with zero reuse - a precedent for building a tree from a template, but not a precedent for
pooling one.

Later phases depend on this one: `ComboBox` (Phase 5) needs a dropdown list, `GridView` (Phase 6) needs
a row repeater, `PropertyGrid` (Phase 9) needs a property-row list whose rows can individually resize
(each row may contain an `Expander`-style collapsible section). That last dependency is why this spec
commits to variable-height virtualization now rather than deferring it - retrofitting a uniform-height
assumption once `PropertyGrid` exists would be far more expensive than building the general case first.

**What this phase does not cover, by explicit scope decision:** selection state. `ItemsControl` has no
concept of a "selected item" and its container (`ItemContainer`) carries no selection visuals. A future
`Selector`/`ListBox` (Phase 5, alongside `ComboBox`) adds a real `ListBoxItem` with selection on top of
the mechanism this phase builds - deferred because no concrete consumer needs it yet and it's a clean
additive layer, not a restructuring.

## Decisions

| Decision | Choice |
|---|---|
| Collection reactivity | Live-reactive when the source implements `INotifyCollectionChanged`; a plain `IEnumerable` is snapshotted once at assignment. |
| Templating concept | New `DataTemplate` type, not a reuse of `ControlTemplate`. No `TargetType` check (works against any data object's runtime type), no `{TemplateBinding}` (there's no "templated control" to bind back to) - instead the built tree's root gets `DataContext` set to the data item. |
| Container tiering | `ItemsControl` uses a minimal `ItemContainer` (a bare `ContentControl`, no selection logic). A future `Selector`/`ListBox` (Phase 5) introduces a real `ListBoxItem` on top, not a change to `ItemContainer` itself. |
| Pooling | On by default (`PoolingEnabled`), configurable per `ItemsControl` instance. Pooled unit is the container element plus its `DataTemplate`-built visual tree - reuse only rebuilds `Content` if the resolved template differs from what the pooled container already has; otherwise just rebinds `DataContext`. |
| Virtualization | Full virtualization now: only realize items intersecting the viewport (+ a small scroll-ahead buffer), not the whole collection. |
| Item sizing model | Variable-height, two-pass estimation - not a uniform-size assumption. Justified by `Expander`/`Accordion` (Phase 4), `SplitPane` (Phase 3), and especially `PropertyGrid` (Phase 9), all of which need items whose real height differs from each other and can change after the item is first realized. |
| Scroll anchoring | A minimal, targeted form ships in this phase: when an already-realized item's known height changes and its index is above the current top-visible index, the scroll offset is adjusted by the same delta so on-screen content doesn't jump. General anchor-correction for big-jump landing-estimate error (e.g. dragging the scrollbar thumb through mostly-unrealized territory) remains a deferred fast-follow - see "Deferred: general anchor-correction" below. |

## Detailed design

### 1. `DataTemplate`

- `sources/IcyUI/UI/Styles/DataTemplate.cs` (new, alongside `ControlTemplate.cs`): holds the same kind
  of markup-loadable content `ControlTemplate` does (a `MarkupLoader`-built subtree), but with no
  `TargetType` and no `{TemplateBinding}` support - there's no host control to bind back to, only a
  data item.
- `DataTemplate.Build(object dataItem) : UIElement`: loads the template's content fresh (or reuses a
  pooled tree - see pooling below), sets the resulting root element's `DataContext = dataItem`, and
  returns it. Bindings inside the template (`{Binding PropertyName}`) resolve against that
  `DataContext` through the existing binding machinery - no new binding mechanism needed.
- Markup shape: `<ItemsControl.ItemTemplate><DataTemplate>...</DataTemplate></ItemsControl.ItemTemplate>`
  for a one-off template, assigned directly like any other property-element content (`Border.Child`'s
  existing pattern). A `<DataTemplate x:Key="...">` in a `Resources` dictionary plus
  `{StaticResource ...}` also works for a template reused across multiple places - no special-casing
  needed since `DataTemplate` is just another resource-dictionary-storable object.
- `ItemTemplateSelector : Func<object, DataTemplate>?` on `ItemsControl` lets an app pick a different
  template per data item (e.g. `PropertyGrid` choosing an editor template by property type) without a
  new selector type - a plain delegate is enough, matching IcyUI's existing preference for delegates
  over ceremony where a single method suffices (see `Binding`'s converter delegates for precedent).

### 2. `ItemContainer`

- `sources/IcyUI/UI/Controls/ItemContainer.cs` (new): a minimal `ContentControl` - `Content` is
  whatever `DataTemplate.Build` returned. No selection state, no `IsSelected`, no selection-driven
  visual states. Exists as its own type (rather than using a bare `ContentControl` directly) purely so
  the pool and any future `Selector` layer have a distinct, themable type to key off (`GetImplicitStyleKey(typeof(ItemContainer))`).

### 3. `ItemsControl`

- `sources/IcyUI/UI/Controls/ItemsControl.cs` (new): extends `Control`.
- `ItemsSource : IEnumerable?` - when set, if the value implements `INotifyCollectionChanged`,
  subscribes to `CollectionChanged` and incrementally adds/removes containers and height-cache entries
  in response (`Add`/`Remove`/`Replace`/`Reset` actions each map to a bounded local update, not a full
  rebuild). A plain `IEnumerable` is enumerated once at assignment into an internal list snapshot; later
  external mutation of that source is not observed (matches the "live-reactive when possible" decision -
  no polling, no `IEnumerable`-level diffing).
- `ItemTemplate : DataTemplate?`, `ItemTemplateSelector : Func<object, DataTemplate>?` - if both are
  set, the selector wins per item; `ItemTemplate` is the fallback (and the common case: most
  `ItemsControl` usages need exactly one template).
- `PoolingEnabled : bool = true`.
- Implements `IVirtualizingScrollInfo` directly (see below) - no separate virtualizing-panel type.
  `ItemsControl` already owns both "what the items are" and "how they're laid out," and nothing else in
  the roadmap needs a virtualizing panel independent of an items-list, so a dedicated `Panel` subtype
  would be pure ceremony (YAGNI).

### 4. Pooling

- `Dictionary<DataTemplate, Stack<ItemContainer>> pools` - one pool per resolved template, since a
  container built from template A can't cheaply become one built from template B (different visual
  tree shape).
- On realize: pop a container from the pool for the resolved template if one's available, rebind
  `DataContext` to the new data item, and use it as-is - no `Content` rebuild. If the pool is empty (or
  `PoolingEnabled = false`), build a fresh container via `DataTemplate.Build`.
- On de-realize (item scrolls out of the realized range): push the container back onto its template's
  pool. **The container's cached height entry (see below) is not discarded when the container is
  pooled** - the height tracker is keyed by data index, not by container instance, so a de-realized
  item's last-known height stays authoritative until that index either scrolls back into view and gets
  re-measured, or is removed from the collection.

### 5. Height tracking & estimation

- `List<float?> knownHeights` - one slot per item in `ItemsSource`, `null` until the item has been
  realized and measured at least once. Resized (insert/remove `null`/entries at the right position) in
  response to `CollectionChanged`.
- `float sumOfKnownHeights`, `int knownCount` - running totals, updated incrementally (not recomputed
  from scratch) on every height write:
  - **First measurement** (`knownHeights[i]` was `null`): `sumOfKnownHeights += newHeight; knownCount++;`
  - **Re-measurement** (`knownHeights[i]` already had a value - e.g. an `Expander` inside the item's
    template toggled): `sumOfKnownHeights += newHeight - knownHeights[i];` (`knownCount` unchanged).
    This is the correction to the original design's "permanent once measured" framing - a known height
    is authoritative until the item's real size changes again, not a one-time write.
  - `averageHeight = knownCount > 0 ? sumOfKnownHeights / knownCount : DefaultEstimatedItemHeight` (a
    configurable constant, used only before anything has ever been realized).
- `ExtentHeight = sumOfKnownHeights + (itemCount - knownCount) * averageHeight` - O(1), recomputed
  whenever `sumOfKnownHeights`/`knownCount`/`itemCount` change.
- **Realize-range lookup (viewport → item index range)**: an anchor (an index + its known cumulative
  offset) is kept from the last layout pass. A small scroll delta walks forward/backward from the
  anchor index, accumulating each item's known height (if measured) or `averageHeight` (if not) until
  the walk covers the new viewport range - O(realized-range-delta), not O(collection size). A **big
  jump** (e.g. dragging the scrollbar thumb far from the current anchor) instead estimates a landing
  index directly (`targetOffset / averageHeight`), jumps the anchor there, and realizes from that new
  position - avoiding an O(distance) walk for large jumps.
- Newly realized items are measured for real immediately, folding their true height into
  `sumOfKnownHeights`/`knownCount` (and thus `averageHeight`/`ExtentHeight`) as part of the same layout
  pass that realized them.

### 6. Minimal above-viewport anchoring

- When step 5's re-measurement path fires (a realized item's known height changed) **and that item's
  index is above the current top-visible index**, adjust `VerticalOffset` by the same delta
  (`newHeight - oldHeight`) before the layout pass finishes. This keeps on-screen content visually
  stationary when something the user has already scrolled past resizes (the common `PropertyGrid`
  case: collapsing a section above where you're currently looking).
- Re-measurement **within or below** the viewport does not adjust the offset - the resize is visible
  and expected (the user is looking at the thing that just changed size), matching how every other
  expand/collapse UI behaves.
- This is deliberately narrow: it only corrects for a *known* item changing size, using an exact delta.
  It does not correct for estimation error accumulated while jumping through *unrealized* territory
  (see below) - that requires tracking a stable visual anchor across a big jump and re-deriving the
  offset from it once real heights are discovered, which is a materially larger mechanism.

### 7. Deferred: general anchor-correction

Landing on an estimated index via a big jump (§5) can be off by however much the average has drifted
from the true distribution in that stretch of the collection. Without general anchor-correction, the
visible effect is the scrollbar thumb's size/position nudging slightly as previously-unrealized items
get measured and folded into the average - small and generally imperceptible for near-uniform content,
more noticeable the more item heights vary. This remains an explicit fast-follow, not built in Phase 2.
The §6 mechanism above is intentionally a special case of this general problem (exact delta from a
*known* change) rather than a partial implementation of it - the general version needs a proper stable
anchor (e.g. "keep item N pinned at pixel Y") maintained across arbitrary scroll input, not just
delta-propagation from a single re-measurement event.

### 8. `IVirtualizingScrollInfo` & `ScrollViewer` integration

- New interface, `sources/IcyUI/UI/IVirtualizingScrollInfo.cs`:
  - `float ExtentWidth { get; }`, `float ExtentHeight { get; }` (get-only - the content computes its
    own extent; the scroll viewer never sets it).
  - `void OnViewportChanged(float horizontalOffset, float verticalOffset, float viewportWidth, float viewportHeight)`.
- `ItemsControl` implements this directly (§3) - no separate virtualizing-panel type.
- `ScrollViewer` (`sources/IcyUI/UI/Controls/ScrollViewer.cs`) changes:
  - `ExtentWidth`/`ExtentHeight` branch on `Content is IVirtualizingScrollInfo vsi` - read `vsi.ExtentWidth`/`vsi.ExtentHeight` instead of measuring `Content` for its natural size.
  - `ArrangeContent`/`UpdateContentOffset` call `vsi.OnViewportChanged(...)` instead of fully
    measuring/arranging `Content` when it implements the interface - mirrors WPF's real
    `ScrollViewer` → `IScrollInfo` → `VirtualizingStackPanel` delegation.
  - Non-virtualizing content (anything not implementing the interface) keeps today's full
    measure/arrange behavior unchanged - this is purely additive.

## Worked example

See the markup + numeric walkthrough already discussed inline in conversation (item realization,
extent estimation and correction, a big scrollbar-thumb jump, and the small-delta anchor walk) -
reproduced here for the record:

```xml
<ScrollViewer>
  <ItemsControl ItemsSource="{Binding People}">
    <ItemsControl.ItemTemplate>
      <DataTemplate>
        <Border Padding="8" BorderBrush="#FF56566A" BorderThickness="0,0,0,1">
          <StackPanel Orientation="Vertical">
            <TextBlock FontSize="16" Text="{Binding Name}"/>
            <TextBlock FontSize="12" Foreground="Gray" Text="{Binding Bio}"/>
          </StackPanel>
        </Border>
      </DataTemplate>
    </ItemsControl.ItemTemplate>
  </ItemsControl>
</ScrollViewer>
```

1000 items, 400px viewport. At `VerticalOffset = 0` with nothing realized yet, `ExtentHeight` starts at
`1000 × DefaultEstimatedItemHeight` (e.g. 40,000px at a 40px default). Realizing the first ~13 visible
items and measuring them for real (summing to, say, 500px) corrects `averageHeight` to ~38.5px and
`ExtentHeight` to ~38,500px - no visible jitter since nothing exists above item 0 to shift. Dragging the
scrollbar thumb to the middle (offset ≈19,250px) estimates a landing index of `19,250 / 38.5 ≈ 500`,
jumps the anchor there, and realizes ~495–512; those get measured for real and folded into the running
average, leaving indices 13–494 as a still-unrealized gap between two known clusters. A small scroll
near index 500 walks a short distance from the existing anchor (§5) rather than rescanning, and returns
de-realized containers to the pool without discarding their cached heights.

## Testing

- `DataTemplate.Build` - `DataContext` set correctly, bindings resolve against the data item, pooled
  reuse rebinds without rebuilding `Content` when the template matches.
- `ItemsControl` + `INotifyCollectionChanged` - `Add`/`Remove`/`Replace`/`Reset` each produce the
  expected incremental container/height-cache updates without a full rebuild (verify via a counter on
  a fake `DataTemplate.Build` implementation, asserting it's called only for genuinely new items).
- Height tracking - first-measurement vs re-measurement paths both update
  `sumOfKnownHeights`/`averageHeight`/`ExtentHeight` correctly; a `TheoryData` covering both directions
  of re-measurement (grow and shrink).
- Virtualization - only items intersecting the viewport (+ buffer) are realized at a given offset;
  small-delta scroll reuses the anchor walk (assert via a call-count/visited-index-range check, not
  timing); a big jump lands near the estimated index and does not walk the full collection.
- §6 anchoring - a fake item above the top-visible index growing/shrinking adjusts `VerticalOffset` by
  the exact delta; the same change within/below the viewport leaves `VerticalOffset` untouched.
- `IVirtualizingScrollInfo`/`ScrollViewer` integration - `ScrollViewer.ExtentHeight` reflects the
  virtualizing content's estimate, not a full measure; non-virtualizing `Content` is unaffected
  (regression guard for existing `ScrollViewer` tests).
- Manual smoke test (both engines, per [[feedback_smoke_test_notification]]): a long bound list scrolls
  smoothly, a scrollbar-thumb drag lands near the right spot, pooled containers are visibly reused (no
  flicker/rebuild) during ordinary scrolling.

## Critical files

**New:** `sources/IcyUI/UI/Styles/DataTemplate.cs`, `sources/IcyUI/UI/Controls/ItemContainer.cs`,
`sources/IcyUI/UI/Controls/ItemsControl.cs`, `sources/IcyUI/UI/IVirtualizingScrollInfo.cs`,
`sources/IcyUI.Tests/Controls/ItemsControlTests.cs` (and sibling test files for `DataTemplate`/pooling/
height-tracking).

**Modified:** `sources/IcyUI/UI/Controls/ScrollViewer.cs` (`ExtentWidth`/`ExtentHeight`,
`ArrangeContent`/`UpdateContentOffset` branching on `IVirtualizingScrollInfo`).

## Open items for implementation planning (not blocking spec approval)

- The exact threshold distinguishing a "small delta" (anchor walk, §5) from a "big jump" (direct
  landing-index estimate, §5) - a tuning heuristic (e.g. walk if the delta is under some multiple of
  the viewport height, jump otherwise), not a design-level decision.
- Exact shape of the `CollectionChanged`-to-height-cache-splice mapping for `Move`/`Replace` actions
  (index remapping details) - confirm against `List<T>`/`ObservableCollection<T>` semantics during
  planning, not a design-level decision.
- Whether `DefaultEstimatedItemHeight` is a fixed constant, an `ItemsControl` property, or themable via
  the default-theme mechanism (Phase 0) - a small decision, not architecturally significant either way.
- `ScrollViewer`'s existing scrollbar-thumb visuals (Phase 0 shipped a plain bordered look, deferring
  "a real scrollbar thumb" to this phase per the roadmap) - confirm the thumb-size/position math reads
  `ExtentHeight`/`ViewportHeight` the same way for virtualizing and non-virtualizing content (it should,
  since both go through the same `ExtentHeight` property either way).
