# Tier-2 Phase 3 — SplitPane

> Design spec for Phase 3 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`).
> The plan calls Phase 3 out as a scope note only, needing its own dedicated design discussion before
> implementation starts - this is that discussion, written up.

## Context

Nothing in IcyUI today lets an app split a region into two resizable, user-adjustable areas - `Grid`
requires fixed/star row-and-column definitions set up front, with no interactive divider. `Slider` is
the only existing control with a draggable, position-clamped "thumb" concept; `SplitPane`'s divider
reuses that exact mechanism rather than the Phase 1 cross-container `IDragSource`/`IDropTarget`
framework, confirmed directly with Ivan during brainstorming - a divider drag is a single-target
"grab and reposition" gesture with no drop target elsewhere, structurally identical to `Slider`'s thumb,
not a cross-container payload carry. This resolves an inconsistency between the Tier-2 roadmap memory
(which described `SplitPane` as an early cross-container-framework consumer) and `IDragSource.cs`'s own
doc comment (which already used "a future `SplitPane`'s divider" as the example of the *single-target*
pattern) - the doc comment was right.

**Scope for this phase:** exactly two panes (`First`/`Second`) and one `Orientation`, matching
`StackPanel`'s existing `Orientation.Horizontal`/`Orientation.Vertical` semantics (side-by-side vs.
stacked). A 3+ region layout is achieved by nesting `SplitPane`s, not by a native N-pane mode - confirmed
with Ivan over the alternative (a single `SplitPane` managing N panes/N-1 dividers natively), which was
rejected as unnecessary surface area this phase and a duplicate of concerns `ItemsControl`'s collection
management already covers should it ever be needed.

**No cross-engine impact.** Like `ItemsControl`/`Slider`, this is pure core-`IcyUI` layout and input -
nothing in `IcyUI.MonoGame`/`IcyUI.Stride` changes, and `IcyUI.FNA` (still an unimplemented stub) needs
no equivalent work as a result of this phase.

## Decisions

| Decision | Choice |
|---|---|
| Divider drag mechanism | Single-target `UIElement.OnDragStarted`/`OnDragPerforming`/`OnDragEnded`, reusing `Slider`'s exact pattern - **not** the Phase 1 `IDragSource`/`IDropTarget` cross-container framework. |
| Pane count / nesting | Exactly two panes (`First`/`Second`) + one `Orientation` per `SplitPane` instance. 3+ regions are built by nesting `SplitPane`s inside each other. |
| Sizing model | `SplitterPosition` is a ratio (`float`, 0-1) of available space along the orientation axis. Both panes resize proportionally when the `SplitPane` itself resizes - not a fixed-pixel-plus-remainder model. |
| Minimum pane size | `MinFirstSize`/`MinSecondSize` (`float` pixels, default 0) clamp where the divider can land, mirroring `Slider.Minimum`/`Maximum`'s clamped-property precedent. |
| Divider templating | Named template part (`PART_Divider`), mirroring `Slider`'s `PART_Thumb` exactly. |
| Divider hit-test vs. visible size | The divider's hit-test band (`DividerSize`) is wider than its visible line - achieved with zero new hit-test mechanism, since `UIElement.HitTest` already tests directly against `ActualBounds`. |

## Detailed design

### 1. Composition: why `Chrome` can host the divider but not the panes

`Control.Background`/`BorderBrush`/`BorderThickness`/`Padding` all hard-cast `Chrome` to `Border` when
untemplated (`(Border)Chrome`) - so the default (and templated-root) `Chrome` must stay a single `Border`
whose one `Child` slot can hold at most one more element. `Slider` uses that one slot for its thumb.
`SplitPane` needs *three* additional visuals (`First`, divider, `Second`), so only one of them can live
inside `Chrome`'s own subtree:

- **The divider lives inside `Chrome`**, exactly like `Slider`'s thumb: `Chrome` is an outer decorative
  `Border` (`Background`/`BorderBrush`/`BorderThickness` - a border around the whole `SplitPane`, largely
  invisible in practice since `First`/`Second`/divider tile the entire content area with no gaps) whose
  `Child` is the divider `Border`. `ArrangeContent` sets the divider's `Width`/`Height` (whichever is the
  orientation axis) and `Margin` to position it - the same "set thumb.Margin, then let normal layout
  finish the job" pattern `Slider.ArrangeContent` already uses - before calling `Chrome.Arrange(ActualBounds)`.
- **`First`/`Second` live outside `Chrome`**, managed directly by `SplitPane` the same way `ItemsControl`
  manages its realized containers: `GetVisualChildren` yields `Chrome`, then `First`, then `Second`
  (order doesn't matter for correctness - their regions never overlap - but matches `ItemsControl`'s own
  "chrome first" convention); `OnRender` draws `Chrome` then each pane explicitly instead of relying on
  `Control`'s single-`Chrome.Draw` default; `ArrangeContent` arranges `First`/`Second` into their computed
  rectangles directly, not through `Panel`-style margin/alignment resolution.

### 2. Properties

New file `sources/IcyUI/UI/Controls/SplitPane.cs`, extending `Control`:

- `Orientation Orientation` (default `Orientation.Horizontal`) - reuses the existing `Icy.UI.Orientation`
  enum (`StackPanel`'s own), same semantics: `Horizontal` places `First`/`Second` side-by-side (a
  vertical-line divider); `Vertical` stacks them (a horizontal-line divider).
- `UIElement? First`, `UIElement? Second` - plain properties, each wiring/unwiring `Parent`/`Canvas`
  directly (no dual templated/untemplated storage split needed, unlike `Content`/`Background`/etc. -
  these never route through `Chrome`, templated or not, so there's nothing for a template to intercept).
  Two nested property-element tags in markup (`<SplitPane.First>`/`<SplitPane.Second>`), since
  `[ContentProperty]` only supports one slot and this control needs two.
- `float SplitterPosition` (default `0.5`) - clamped every time it, `MinFirstSize`, `MinSecondSize`, or
  the control's size changes (see §3). Setting it invalidates arrange and raises...
- `event EventHandler? SplitterPositionChanged` - mirrors `Slider.ValueChanged`.
- `float MinFirstSize`, `float MinSecondSize` (default `0`) - pixel minimums for `First`/`Second`
  respectively, along the orientation axis.
- `float DividerSize` (default `6`) - both the divider's hit-test band width *and* the outer bound of
  its visible-line child (see §4); themable, not a template-part-only concern.
- `IBrush DividerBrush` - forwards to the divider's *visible inner line* child's `Background` when
  untemplated (mirrors `Slider.ThumbBrush`'s `get => thumb.Background; set => thumb.Background = value;`
  pattern exactly, just one level deeper - see §4).

### 3. Layout algorithm

- `MeasureContent`: measure `First`/`Second` for their natural size, sum along the orientation axis with
  `DividerSize`, take the max of the two on the cross axis - the same natural-size fallback shape as
  `Panel.MeasureContent`/`StackPanel.MeasureContent`. (In practice `SplitPane` is almost always given an
  explicit size or stretched by its own parent, same as `Slider`/`ProgressBar` - this measurement mostly
  matters when it isn't.)
- `ArrangeContent`:
  1. `available` = `ContentBounds`'s `Width` (Horizontal) or `Height` (Vertical).
  2. `rawDividerPos` = `SplitterPosition * (available - DividerSize)`.
  3. Clamp `rawDividerPos` to `[MinFirstSize, available - DividerSize - MinSecondSize]` (when
     `MinFirstSize + MinSecondSize + DividerSize > available`, clamp to whatever range is left rather
     than throwing - degrade gracefully under an undersized container, same spirit as `Slider`'s own
     `Math.Max(0, ...)` roaming-space guard).
  4. `First.Arrange(...)` from the content origin to `dividerPos`; divider sized/positioned via
     `Width`/`Margin` (Horizontal) or `Height`/`Margin` (Vertical), exactly like `Slider.ArrangeContent`
     positions `thumb`; `Second.Arrange(...)` from `dividerPos + DividerSize` to the end. All three
     stretch the full cross-axis extent.
  5. `Chrome.Arrange(ActualBounds)` last (lets the divider's own `Margin`/`Width` set in step 4 actually
     take effect through normal layout, same ordering `Slider.ArrangeContent` uses).

### 4. Default divider visual & hit-test band

The built-in (untemplated) divider is two nested `Border`s, constructed once (mirroring `Slider`'s
`defaultThumb` field):

- **Outer** (`defaultDivider`, wired to `((Border)Chrome).Child` in the constructor exactly like
  `Slider` wires `defaultThumb`): `Width`/`Height` = `DividerSize` along the orientation axis, stretches
  the cross axis, `Background` = transparent. This is what `ArrangeContent` positions via `Margin`
  (§3.4) - and, since `UIElement.HitTest` tests directly against `ActualBounds`, this outer `Border`'s
  bounds *are* the grabbable hit-test band, automatically wider than whatever's actually visible inside
  it. No new hit-test mechanism needed.
- **Inner** (a plain `Border` child of the outer one, not independently named/templatable): a thin
  strip (e.g. 2px) centered within the outer band via `Margin`, `Background` bound to `DividerBrush`.
  This is the part a user actually sees as "the divider line."

`OnApplyTemplate` (mirroring `Slider.OnApplyTemplate` exactly): `GetTemplateChild<Border>("PART_Divider")`
repoints the outer divider reference when a `Template` supplies one; a template's `PART_Divider` is free
to skip the inner-thin-line composition entirely (e.g. make the whole band visibly colored) - that's an
author choice, not a constraint this control enforces.

### 5. Divider drag

`SplitPane` overrides `OnDragStarted`/`OnDragPerforming`/`OnDragEnded` directly (like `Slider` does, not
via a separate per-part handler), converting the screen point to a local content-relative offset along
the orientation axis and deriving `SplitterPosition = clamp(offset / available, minRatio, maxRatio)`
(the same `MinFirstSize`/`MinSecondSize`-derived bounds as §3.3, expressed as ratios). Since `Canvas`
hit-tests once at drag-start and routes the whole gesture to whatever was hit there (the single-target
pattern's own established behavior - see `IDragSource.cs`'s remarks), dragging past either edge of the
`SplitPane` still works mid-gesture, same as dragging below/above a `Slider`'s track already does.

### 6. Templating & theming

- `DefaultTheme.xml` gains an implicit `SplitPane` style + `ControlTemplate`, following the exact
  `Slider`/`ProgressBar` shape already in that file:
  ```xml
  <ControlTemplate x:Key="IcyDefaultSplitPaneTemplate" TargetType="SplitPane">
    <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
      <Border x:Name="PART_Divider" Background="Transparent">
        <Border Margin="..." Background="{TemplateBinding DividerBrush}"/>
      </Border>
    </Border>
  </ControlTemplate>
  ```
  with `Hovered`/`Pressed` `VisualState`s on `DividerBrush`, mirroring `Slider`'s `Hovered` state on
  `ThumbBrush`.

### 7. Sample

A `SplitPaneDemo` added to Shared Samples (same precedent as `ItemsControl`'s Task 11 sample) - nested
`SplitPane`s forming a 3-pane layout:

```xml
<SplitPane Orientation="Horizontal">
  <SplitPane.First><TreeView/></SplitPane.First>
  <SplitPane.Second>
    <SplitPane Orientation="Vertical">
      <SplitPane.First><Editor/></SplitPane.First>
      <SplitPane.Second><Output/></SplitPane.Second>
    </SplitPane>
  </SplitPane.Second>
</SplitPane>
```

## Testing

- Layout math: `ArrangeContent` produces the expected `First`/divider/`Second` rectangles for a range of
  `SplitterPosition`/`Orientation`/`MinFirstSize`/`MinSecondSize` combinations, including the
  undersized-container clamp-degradation case (§3.3).
- `SplitterPosition`'s own setter clamps and raises `SplitterPositionChanged` exactly once per real
  change (no-op re-assignment of the same value raises nothing - matches `Slider.Value`'s own guard via
  `SetProperty`).
- Drag: simulated `OnDragStarted`/`OnDragPerforming` sequences at various screen points produce the
  expected `SplitterPosition`, including past-the-edge points clamping correctly (mirrors `Slider`'s own
  drag tests).
- Hit-testing: a point within `DividerSize`'s band but outside the inner visible line still resolves to
  the divider element via `HitTest`.
- Templating: `PART_Divider` swap via `Template` re-wires drag logic to the new element (mirrors
  `Slider`'s own `OnApplyTemplate` test coverage); a `Template` with no `PART_Divider` falls back to the
  default (mirrors `Slider`'s "template with no PART_Thumb still applies" case).
- Manual smoke test (both engines, per [[feedback_smoke_test_notification]]): the sample's nested
  `SplitPane`s resize interactively, the divider grabs cleanly within its hit-test band, and proportional
  resize behaves correctly when the window itself is resized.

## Critical files

**New:** `sources/IcyUI/UI/Controls/SplitPane.cs`,
`sources/IcyUI.Tests/Controls/SplitPaneTests.cs`.

**Modified:** `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (new `SplitPane` style/template),
Shared Samples (new `SplitPaneDemo`).

## Open items for implementation planning (not blocking spec approval)

- Exact default for `DividerSize`/inner-line thickness/`VisualState` colors - cosmetic theming
  specifics, not architecturally significant.
- Whether `SplitterPosition`'s clamp range, when `MinFirstSize + MinSecondSize + DividerSize > available`,
  should prefer honoring `MinFirstSize` or `MinSecondSize` first (a tie-breaking tuning detail, not a
  design-level decision) - reasonable default: clamp symmetrically around the midpoint of whatever range
  remains.
- Keyboard resizing (arrow-key nudge while the divider is focused) is out of scope for this phase,
  consistent with `Slider` itself having no keyboard support today either - not a regression relative to
  the established baseline.
