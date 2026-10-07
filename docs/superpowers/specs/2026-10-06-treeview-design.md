# TreeView: a flattened, virtualizing tree control

> Design spec, discussed with Ivan on 2026-10-06. It's the first of three sub-projects that came out of the
> "samples navigation sidebar" request: **TreeView** (this spec) → **samples shell** (a sidebar built on it, plus a
> `ScrollViewer` per demo) → **editor scope** (`EditorFrame` restricted to a subtree, with scroll forwarding).

## Context

The sample hosts have grown to ~22 demos, navigated only by PageUp/PageDown. Ivan asked for a sidebar with grouped,
selectable samples. `ListBox` can't skip header items, and grouped navigation is really a tree, so the sidebar needs a
**TreeView**. That control is useful well beyond the samples. Phase 10.4's outline panel shows the markup document as a
tree with selection synced to the canvas, and games use trees for settings menus, inventories and scene hierarchies.

**Success looks like this:**
- A developer writes a static tree in markup with `TreeViewNode`s, or binds any object graph through
  `ChildrenSelector`.
- Expanding, collapsing, scrolling and live collection changes keep the right content on every row, including recycled
  containers.
- Arrow keys and the D-pad move through the tree and leave it at every boundary.
- A 10,000-node tree costs only what's on screen.

### What already exists

- `ItemsControl` virtualizes a flat `items` list. It has variable-height estimation, scroll anchoring, per-template
  container pooling, and incremental `InsertItems`/`RemoveItems`/`ReplaceItems`/`MoveItems` paths driven by
  `INotifyCollectionChanged`. On reuse, `RentContainer` only rebinds `DataContext`; everything else is stamped in
  `AttachContainer`.
- `SelectingItemsControl` adds `SelectedIndex`/`SelectedItem`/`SelectionChanged`, and click-to-select through
  `SelectorItem.Tapped`. Every item is selectable.
- `SelectorItem` has `IsSelected` and `IsHighlighted`, themed through `CommonStates` in `DefaultTheme.xml`, including
  the compound Selected+Hovered and Highlighted+Hovered states.
- `Selector` handles keyboard and gamepad through the **focus-gate pattern**. While its gate is focused, it subscribes
  to `INavigationEvents.FocusChanging`/`SelectElement`/`CloseModal` and sets `Handled` to keep focus.
- There is **no `ScrollIntoView`** anywhere in `ItemsControl`, and **no keyboard navigation** in `ListBox`.

## Decisions

| Decision | Choice |
|---|---|
| Architecture | **Flattened and virtualizing.** The tree keeps a flat list of the visible rows and renders it with `ItemsControl`'s existing virtualization and pooling. Expand/collapse are range inserts/removes. |
| Public shape | **A composite `TreeView : Control`** with template part `PART_List` (an internal `TreeViewList : SelectingItemsControl`). `TreeView.ItemsSource` means the roots, and `SelectedItem` is the data item, not a row. |
| How children are found | **Both code and markup:** `ChildrenSelector`, then `HierarchicalDataTemplate.ChildrenPath`, then `TreeViewNode.Children`. |
| Static trees | A **`TreeViewNode`** data class, so a tree can be written directly in markup. |
| Branch clicks | **Per-item selectability.** Branches are selectable by default. `IsItemSelectable` or `TreeViewNode.IsSelectable` opts items out, and tapping a non-selectable branch toggles it. |
| Live data | **Observe the children collections of expanded nodes** and translate their changes into flattened ranges. |
| Navigation | **Selection follows focus**, Explorer-style, through the Navigation events, so the D-pad works the same as the arrow keys. **Focus leaves the tree at every boundary.** |
| Reveal | A **core `ItemsControl.ScrollIntoView(int)`**, plus `TreeView.Reveal(item)`, which expands ancestors. Setting `SelectedItem` reveals. |
| Where the code lives | Core `IcyUI` (`Icy.UI.Controls`, and `Icy.UI.Styles` for the template), plus a `DefaultTheme.xml` entry. No engine code. |

## Detailed design

### Types

| Type | Visibility | Role |
|---|---|---|
| `TreeView : Control` | public | Owns the roots, child resolution, expansion state, the flattened list, live subscriptions, navigation and reveal. Template part `PART_List`. |
| `TreeViewList : SelectingItemsControl` | internal | Virtualizes the flattened rows. Creates `TreeViewItem` containers and re-stamps their row state in `AttachContainer`. |
| `TreeViewItem : SelectorItem` | public | The row container: `Depth`, `IsExpanded`, `HasChildren`, `IsSelectable` (all read-only to users, set by the tree), and template part `PART_Expander` (the chevron). |
| `TreeViewNode` | public | Data class for static trees: `Header` (`object?`; `ToString()` returns its text), `Children` (`ObservableCollection<TreeViewNode>`, the content property), `IsExpanded`, `IsSelectable` (default `true`), `Tag`. Implements `INotifyPropertyChanged`. |
| `HierarchicalDataTemplate : DataTemplate` | public | Adds `ChildrenPath` (a `PropertyPath` string) naming the item's children collection. |

### `TreeView` members

- `IEnumerable? ItemsSource`: the root items. When it implements `INotifyCollectionChanged`, it's observed.
- `DataTemplate? ItemTemplate`, `Func<object, DataTemplate>? ItemTemplateSelector`: forwarded to `PART_List`. A
  `TreeViewNode` with no template renders its `Header` (the default template shows the item's text, and
  `TreeViewNode.ToString()` returns `Header`'s text).
- `Func<object, IEnumerable?>? ChildrenSelector`.
- `Func<object, bool>? IsItemSelectable`.
- `object? SelectedItem` (get/set), `event EventHandler? SelectionChanged`.
- `bool IsExpanded(object item)`, `void Expand(object item)`, `void Collapse(object item)`,
  `bool Reveal(object item)`.
- `event EventHandler<TreeViewItemEventArgs>? ItemExpanded`, `ItemCollapsed` (the args carry the item).
- `float Indent` (themable, default `16`).
- An inline-items content property, so `<TreeView><TreeViewNode …/></TreeView>` works. Inline items become the roots
  when `ItemsSource` isn't set, and setting both throws, as in WPF.

**Child resolution** (first match wins):
1. `ChildrenSelector(item)`
2. the `ChildrenPath` of the item's `HierarchicalDataTemplate` (from `ItemTemplateSelector`, then `ItemTemplate`)
3. `((TreeViewNode)item).Children`
4. none

`HasChildren` is "the resolved collection is non-null and non-empty". An expanded node whose children collection
becomes empty keeps `IsExpanded` but shows no chevron, and it shows the chevron again when a child arrives.

**Selectability:** `IsItemSelectable(item)`, then `TreeViewNode.IsSelectable`, then `true`.

### Expansion state and flattening

- Expansion state lives in the `TreeView`: a `HashSet<object>` of expanded items, compared by reference
  (`ReferenceEqualityComparer`). It never lives on containers, because pooling would lose it.
- `TreeViewNode.IsExpanded` gives the initial state and stays synced both ways while the node is in the tree. Plain
  data items start collapsed.
- The flattened list is an internal `RangeObservableCollection<FlatRow>` (see Expand), assigned as
  `PART_List.ItemsSource`. Each
  `FlatRow` is a small class `(object Item, int Depth, FlatRow? Parent)`. The tree keeps a
  `Dictionary<object, FlatRow>` from item to its visible row (first occurrence), so `Reveal` and live changes don't
  have to scan.
- Every expanded row caches **`VisibleDescendants`**, the number of visible rows below it. The cache is updated on the
  way up the parent chain whenever a range is inserted or removed. A row's flattened index plus 1 plus the
  `VisibleDescendants` of its preceding siblings gives the insertion offset for a child change.
- **Expand(item):** build the visible subtree (the children, plus recursively any children that are already in the
  expanded set) and insert it after the row as **one multi-item `Add`**. The flattened list is an internal
  `RangeObservableCollection<FlatRow>` that raises a single `Add`/`Remove` carrying the whole range.
  `ItemsControl.InsertItems`/`RemoveItems` already take `NewItems`/`OldItems` as lists, so `ItemsControl` needs no
  change. Subscribe to the children collection. Raise `ItemExpanded`.
- **Collapse(item):** remove the row's `VisibleDescendants` range, unsubscribe from every collection in that subtree,
  and raise `ItemCollapsed`. The expanded flags of descendants are **kept**, so re-expanding restores the subtree as it
  was (Explorer behavior).
- Calling `Expand`/`Collapse` on an item that isn't visible updates the set only. The rows appear when an ancestor
  expands.

### Live changes

The tree subscribes to `INotifyCollectionChanged` on the roots and on the children collection of each **visible,
expanded** row. It unsubscribes on collapse, on removal, when `ItemsSource`/`ChildrenSelector` are retargeted, and in
`OnDetached`.

| Action | Flattened effect |
|---|---|
| Add | Insert the new items' visible subtrees at the computed offset. |
| Remove | Remove their rows plus `VisibleDescendants`, unsubscribing from that subtree. |
| Replace | Remove plus insert. The old item's expansion state is dropped. |
| Move | Remove plus insert, keeping the expansion state. |
| Reset | Re-flatten that parent's subtree. |

- If the **selected item** leaves the visible tree because it was removed (directly or with an ancestor), the selection
  clears and `SelectionChanged` is raised. A **collapse** keeps the selection.
- **The same item under two parents** displays in both places, and expansion, compared by reference, is shared. This
  is documented, not prevented.
- **Cycles:** flattening walks only expanded nodes, and `Reveal`'s DFS keeps a visited set.

### Containers and pooling

`TreeViewList.AttachContainer(container, index)` calls the base, then **re-stamps every row-dependent property** from
`FlatRow` and the tree's state, because a pooled container only has its `DataContext` rebound:
- `Depth`, which sets the indent spacer width to `Depth × Indent`
- `IsExpanded` and `HasChildren`, which set the chevron glyph and its visibility
- `IsSelectable`
- `IsSelected` and `IsHighlighted` (the base already stamps `IsSelected` from `SelectedIndex`; the tree maps the
  selected item to its flattened index)

`DetachContainer` clears the chevron's tap subscription.

### Interaction

**Pointer and touch:**
- **Tapping the chevron** toggles expansion and never selects. The chevron handles its own tap, so the row's `Tapped`
  doesn't fire.
- **Tapping a row:**
  - If the row is selectable, it gets selected.
  - If not, it toggles expansion. A non-selectable leaf does nothing.
- **Double-tapping a selectable branch** toggles it.
- The current row (see below) moves to the tapped row.

**Keyboard and gamepad** (focus-gate pattern, like `Selector`). `TreeView` is focusable. While it's focused, it
handles `INavigationEvents.FocusChanging` (the direction vector) and `SelectElement`. The **current row** is shown with
`SelectorItem.IsHighlighted`.

| Input | Behavior |
|---|---|
| Up | Previous row. **On the first row: not handled**, so focus leaves the tree. |
| Down | Next row. **On the last row: not handled.** |
| Right | Collapsed branch: expand it. Expanded branch: move to its first child. **Leaf: not handled**, so spatial navigation leaves the tree. *(Amended 2026-10-07 by the samples shell spec: Right used to walk to the next row in flattened order, which kept focus from leaving a sidebar tree.)* |
| Left | Expanded branch: collapse it. Otherwise: the parent. **On a root with nothing to collapse: not handled.** |
| Enter / A (`SelectElement`) | Toggle expansion of the current row. |

- Moving to a selectable row selects it (**selection follows focus**). Moving to a non-selectable row makes it current
  without changing the selection.
- When the tree gains focus with no current row, the current row starts at the selected row, or else the first row.

### Reveal and `ScrollIntoView`

- **`ItemsControl.ScrollIntoView(int index)`** (core, public):
  - It computes the item's top from known and estimated heights, the same math as `LocateViewportStart`.
  - If the item isn't fully inside the viewport, it requests a new vertical offset that aligns its nearer edge.
  - The request goes through a new `IVirtualizingScrollInfo` event, `ScrollToOffsetRequested` (`float` absolute
    offset), next to `VerticalOffsetCorrectionRequested`, and `ScrollViewer` applies it.
  - Once the item is realized and its real height differs from the estimate, the existing correction path settles it.
  - It's a no-op when the item is already fully visible, or when no scroll owner is attached.
  - It throws `ArgumentOutOfRangeException` for a bad index.
- **`TreeView.Reveal(item)`:**
  - If the item is visible, it calls `ScrollIntoView` on its row.
  - Otherwise it runs a DFS from the roots through child resolution (with a visited set), expands every ancestor on
    the found path, then calls `ScrollIntoView`.
  - It returns `false` if the item isn't found.
- **Setting `SelectedItem`:**
  - If the item is selectable, it's revealed and selected.
  - If it's not found or not selectable, the selection clears (`SelectingItemsControl`'s "not present → clear" rule).
  - `null` clears.

### Visuals and theme

New `DefaultTheme.xml` entries:
- **`TreeViewItem` template:** a horizontal row with an indent spacer, then `PART_Expander` (a `▸`/`▾` text glyph; it
  keeps its width when `HasChildren` is false, so leaves line up), then a `ContentPresenter`. The style is based on the
  `SelectorItem` style, so `CommonStates` (Hovered, Selected, Highlighted and their compounds) and padding match
  `ListBox`. The glyph follows `IsExpanded`. Whether that's a new `ControlState` flag or a `TreeViewItem`-local state
  group is decided in the plan, after checking how `Expander` does it.
- **`TreeView` template:** a `ScrollViewer` around `PART_List`, like `ListBox`. `Indent` defaults to `16`.

### Markup

- `TreeViewNode` and `HierarchicalDataTemplate` are registered with the markup type system the same way as
  `DataTemplate`.
- `TreeViewNode.Children` is the `[ContentProperty]`, so nested nodes need no wrapper element.
- `ChildrenPath` takes a `PropertyPath` string, resolved per item.
- `TreeViewNode` is a data object, not a `UIElement`. If the 10.x `DesignSession`/`EditorSession` needs anything to
  track or select inside a tree, that belongs to the editor-outline phase, not this one.

## Performance

- **Rendering and realization:** unchanged from `ItemsControl`. Only rows in the viewport, plus its margin, are
  realized, whatever the tree's size or depth.
- **Memory:** one `FlatRow` per **visible** row, one dictionary entry per visible item, one `VisibleDescendants`
  counter per expanded row, and one collection subscription per expanded row. Collapsed subtrees are never walked.
- **Expand/collapse:** O(size of the visible subtree) to build or remove, plus **one** range notification. ItemsControl's
  incremental path de-realizes from the insertion index onward once, and only the viewport is re-realized.
- **`Reveal`:** O(1) when the item is visible. Otherwise an O(n) DFS over the data, documented as such.
- **No per-frame work:** the tree does nothing in `OnRender`/`Update` beyond what `ItemsControl` already does.
- **Benchmark-style test:** expand a 10,000-node tree level by level. Assert that the realized container count stays
  within the viewport bound, and that the time of one full expand fits a budget, measured first and then fixed in the
  plan.

## Testing

All behavioral, in `IcyUI.Tests/Controls/TreeView*Tests.cs`:

- **Flattening:** expand and collapse ranges at every depth. Collapse keeps descendants' flags, and re-expand restores
  the subtree.
- **Child resolution order** and **selectability** rules, including the non-selectable-branch tap toggling it.
- **Navigation:** every row of the table above, including **leaving the tree** at the first row (Up), at the last row
  (Down and Right), and at a collapsed root (Left). Also selection-follows-focus over non-selectable rows.
- **Live changes:** Add, Remove, Replace, Move and Reset at the root and at depth. Removing the selected item clears
  the selection. Collapsing keeps it. Collapse and detach unsubscribe (no leaked handlers).
- **Pooling and stale content** (CLAUDE.md's recurring-bug rule):
  - scroll a deep tree, then a shallow one, through the same pool, and check indent, chevron and expanded state on
    every recycled row;
  - collapse and re-expand while scrolled;
  - retarget `ItemsSource` to a same-shape tree, and check no old headers survive;
  - change `ChildrenSelector` and check the rows are rebuilt.
- **Reveal** and **SelectedItem**: a deep item expands its ancestors and is scrolled into view; a missing item returns
  `false`; a cycle terminates.
- **`ScrollIntoView`** on `ListBox`: above the viewport, below it, already visible (no-op), and with estimated heights
  that get corrected.
- **Markup:** a `TreeViewNode` tree and a `HierarchicalDataTemplate` both load through `MarkupLoader`. `TreeViewDemo`'s
  markup is exercised by being linked into `IcyUI.Tests`.
- **The virtualization bound and the timing budget** from Performance.

## Demo

`Shared Samples/TreeViewDemo.cs` has two panes:
- **Static:** a markup `TreeViewNode` tree with a non-selectable category and nested groups.
- **Live:** a code-built tree through `ChildrenSelector`, with "Add child", "Remove", "Rename" and "Reveal a random deep
  node" buttons.
- **Status line:** the selection and expand/collapse events.

It's registered in **both** `MonoGame Sample` and `Stride Sample` `SampleGame.cs`, using the current per-host
mechanism (the samples shell comes in the next sub-project), and linked into `IcyUI.Tests`.

## Documentation

Every public type and member gets complete XML documentation (`<summary>`, `<remarks>` with `<list>`/`<para>`,
`<see cref>`/`<see langword>`, `<exception>`), so the DocFX site picks it up. `docfx/docs` has no per-control articles,
so no article is added.

## Cross-engine impact

- Core only. No engine types and no new rendering primitives (the chevron is text).
- Input arrives through the existing Navigation, Tap and Scroll paths, so `IcyUI.MonoGame`, `IcyUI.Stride` and the
  `IcyUI.FNA` stub need no changes.
- Touch taps on MonoGame DesktopGL remain subject to the known "no touch on MonoGame desktop" issue. Mouse and
  keyboard are unaffected.

## Out of scope

- Multi-select.
- Drag-and-drop reordering. The Phase 1 drag-drop infrastructure makes it a natural follow-up.
- Lazy or asynchronous child loading.
- Checkbox trees.
- The samples shell and the editor scope, which are the next two specs.

## Plan-time revisions

Found while reading the code for the implementation plan (`docs/superpowers/plans/2026-10-06-treeview-plan.md`). The
plan implements the design with these changes:

1. **No `ControlTemplate`/`PART_List` for `TreeView`.** `ScrollViewer` isn't template-safe yet (see the theme's own
   note), so `TreeView` composes its `ScrollViewer` and the internal list in code, like `TabControl` and `ColorPicker`.
   `TreeViewItem` does get a theme template.
2. **The chevron is a public `TreeViewExpander : Icon`** (`ChevronRight`/`ChevronDown`), not a text glyph that the
   sample fonts may not have. Core `OnTap` bubbles to every ancestor with no "handled" flag, so the expander raises its
   own `Tapped`, and `TreeViewItem` skips its row tap for that tap.
3. **No double-tap toggle.** The core has no double-tap gesture. The chevron, Enter and Right cover expansion.
4. **Subscriptions per visible item, reference-counted,** not per expanded node. Collapsed visible rows are observed
   too, so a chevron appears when a collapsed row gains its first child. `TreeViewNode.PropertyChanged` is observed the
   same way, so `IsExpanded`/`IsSelectable` set from outside are applied.
5. **No `VisibleDescendants` cache.** Subtree ranges are found by scanning depths (O(visible subtree)) and row indices
   by scanning (O(visible rows)). Simpler, and well within budget at 10,000 rows.
6. **Two small core additions:** `SelectingItemsControl.OnContainerTapped(int)` (the tap handler was private), and
   `MarkupLoader` reading `ChildrenPath` in its `DataTemplate` special case (which skips attribute resolution).
7. **`IVirtualizingScrollInfo.ScrollToVerticalOffsetRequested`** is the name of the new scroll request.
8. **The current-row highlight shows only while the tree is focused.** Setting `SelectedItem` from code also moves the
   current row.
9. **"Leaving the tree" means leaving the navigation event unhandled.** Core spatial focus navigation is still a
   `TODO` in `NavigationEvents.OnDirectionalFocus`. The tree already behaves correctly for when it lands.
