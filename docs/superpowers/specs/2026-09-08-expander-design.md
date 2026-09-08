# Tier-2 Phase 4 — Expander

> Design spec for Phase 4 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`).
> The plan calls Phase 4 out as a scope note only ("Expander/Accordion"), needing its own dedicated
> design discussion before implementation starts - this is that discussion, written up.

## Context

Nothing in IcyUI today lets an app show/hide a block of content behind a clickable header. The scope
note bundles "Expander/Accordion" as one line item, but only describes Expander's own behavior (a
header that toggles its content); Accordion (a group of Expanders where opening one auto-collapses the
others) is a distinct, separable concern with no concrete use case identified yet - **confirmed with
Ivan: Accordion is out of scope for this phase**, deferred to its own future design discussion if a
need arises.

**No cross-engine impact.** Like `SplitPane`/`ItemsControl`, this is pure core-`IcyUI` layout, input,
and styling - nothing in `IcyUI.MonoGame`/`IcyUI.Stride` changes, and `IcyUI.FNA` (still an unimplemented
stub) needs no equivalent work as a result of this phase.

## Decisions

| Decision | Choice |
|---|---|
| Scope | Expander only. Accordion (mutual-exclusion grouping) deferred to a future phase. |
| Expand direction | Down only (header on top, content revealed below). No `ExpandDirection` property this phase - YAGNI on the other 3 directions until something needs them. |
| Header content | `UIElement? Header`, not a plain string - matches `SplitPane.First`/`Second`'s "arbitrary content slot" precedent. Implicit `TextBlock` conversion still covers the plain-string case via markup. |
| `Content` markup binding | `[ContentProperty(nameof(Content))]`, matching `ContentControl` - a child element in markup goes to `Content` by default; `Header` is set via `<Expander.Header>` property-element syntax. Ivan's explicit call: doesn't change behavior, simplifies later markup authoring. |
| "Collapsed content not processed" vs. animation | Only true in the settled, idle-collapsed state. During an active expand/collapse transition, content is measured/arranged/rendered/clipped like normal so it can visibly grow or shrink - confirmed with Ivan as the intended reading. |
| Animation mechanism | **No bespoke `Animation`/`Timeline` object managed in C#.** A new `ExpansionProgress : float` (0-1) themable data-slot property, driven entirely by the existing `VisualState.Duration`/`Easing` state-transition machinery (`UIElement.AnimateStateSetter`) via a new `ControlState.Expanded` flag - the same mechanism `Hovered`/`Pressed`/`Checked` already use, just the first real usage of `Duration` in the shipped theme. |
| Composition | `Chrome` hosts only `PART_Header` (an internal `ToggleButton`), mirroring `SplitPane.Chrome` hosting only the divider. `Content` is a second, separately-managed child, arranged directly by `Expander.ArrangeContent`, not nested inside `Chrome`. |
| Header type | New `ExpanderHeader : ToggleButton` marker subtype (behaviorally identical to `ToggleButton`), mirroring `CheckBox`'s own documented rationale exactly - exists purely so `DefaultTheme.xml` can give it a distinct `ControlTemplate` (chevron + content) instead of picking up plain `ToggleButton`'s implicit style. |

## Detailed design

### 1. Properties

New file `sources/IcyUI/UI/Controls/Expander.cs`, extending `Control`:

- `UIElement? Header` - forwards to the internal header toggle's own `Content`
  (`headerToggle.Content = value`), the same forwarding shape `Control.Background`/etc. already use for
  `Chrome`. Wires/unwires nothing extra itself - `ExpanderHeader`/`ToggleButton`/`Button`'s own `Content`
  setter already handles `Parent`/`Canvas`.
- `UIElement? Content` (`[ContentProperty]`) - plain property, wiring/unwiring `Parent`/`Canvas` directly,
  same shape as `SplitPane.First`/`Second` (never routes through `Chrome`, templated or not - nothing for
  a template to intercept). Sets `ClipToBounds = true` once on assignment (needed for the partial-reveal
  clip during an active transition - harmless no-op when fully expanded).
- `bool IsExpanded` (default `false`) - mirrors `ToggleButton.IsChecked` exactly: setter sets/clears the
  new `ControlState.Expanded` flag and raises...
- `event EventHandler? IsExpandedChanged` - mirrors `ToggleButton.IsCheckedChanged`.
- `float ExpansionProgress` (default `0`, clamped `[0,1]`) - themable data-slot property
  (`[RegisterReference]`, `[AffectsMeasure]`, `[AffectsArrange]`), same shape as `CheckBox.CheckBrush`/
  `Slider.ThumbBrush`. Not normally set by application code directly (the theme's `Expanded`/`Collapsed`
  `VisualState` pair drives it) - but nothing prevents a custom theme or hand-rolled instant toggle from
  setting it directly, same as any other themable data slot.

New flag on the existing `[Flags] enum ControlState` (`sources/IcyUI/UI/Styles/VisualState.cs`):
`Expanded = 1 << 6`.

### 2. Composition: why `Chrome` can host the header but not the content

Same constraint `SplitPane`'s own spec documented: `Control.Background`/`BorderBrush`/`BorderThickness`/
`Padding` all hard-cast `Chrome` to `Border` when untemplated, so `Chrome`'s one `Child` slot holds at
most one more element. `Expander` needs two (`Header`-bearing toggle, `Content`), so only one lives
inside `Chrome`:

- **The header toggle lives inside `Chrome`**: `Chrome` is `((Border)Chrome).Child = headerToggle` in the
  constructor (mirrors `SplitPane`'s `((Border)Chrome).Child = divider`). `Expander`'s own
  `Background`/`BorderBrush`/`BorderThickness` therefore visually decorate the header row specifically,
  not a box around header+content together - consistent with how `SplitPane`'s own `Background` only
  really shows around its divider in practice. A user wanting a continuous box around the whole
  disclosure widget wraps the `Expander` itself in a `Border`, same as the `SplitPane` sample wraps
  `First`/`Second` panes in their own `Border`s for visual decoration.
- **`Content` lives outside `Chrome`**, managed directly: `GetVisualChildren` yields `Chrome`, then
  `Content` if non-null; `OnRender` draws `Chrome` then `Content` explicitly (only when
  `Content.IsVisible`, matching `Border.OnRender`'s own `if (Child?.IsVisible == true)` guard);
  `ArrangeContent` arranges `Content` into its own computed rectangle, not through `Panel`-style
  margin/alignment resolution.

### 3. `IsVisible` gating - reusing the existing convention, not inventing a new one

`UIElement.Draw`/`HitTest` already gate on `IsVisible` unconditionally in the base class (`UIElement.cs`
lines ~1120/1193) - a container doesn't need to do anything extra for an invisible child to be skipped by
rendering/hit-testing. Only `Measure`/`Arrange` need an explicit per-container check
(`StackPanel`/`Grid`/`Border` all already do `if (!child.IsVisible) continue`/`if (Child?.IsVisible ==
true)`).

`Expander` keeps `Content.IsVisible = ExpansionProgress > 0` (set whenever `ExpansionProgress` changes,
inside its own setter). This means:

- `ExpansionProgress == 0` (idle, fully collapsed): `Content.IsVisible == false` → `Draw`/`HitTest` skip
  it for free via the existing base-class gate; `Expander.MeasureContent`/`ArrangeContent` also skip
  calling `Measure()`/`Arrange()` on it via the same `if (Content?.IsVisible == true)` idiom
  `Border.ArrangeContent` already uses - fully "not processed", no new mechanism.
- `ExpansionProgress` in `(0,1)` (mid-transition) or `== 1` (fully expanded): `Content.IsVisible == true`,
  processed normally (see §4).

### 4. Layout algorithm

- `MeasureContent`:
  1. `headerSize = headerToggle.Measure()`.
  2. If `Content?.IsVisible != true`, return `headerSize` (`Content` skipped entirely - §3).
  3. Otherwise, `contentSize = Content.Measure()`; return
     `new Size(Math.Max(headerSize.Width, contentSize.Width), headerSize.Height + (int)(contentSize.Height * ExpansionProgress))`.
     Returning a height that scales with `ExpansionProgress` (not just clamping at Arrange time) is what
     makes the whole `Expander` grow/shrink smoothly as its own measured size, so a container holding
     several `Expander`s (e.g. a `StackPanel`) reflows in sync with the animation - a real accordion push
     effect, not just content clipped inside a fixed-size box.
- `ArrangeContent`:
  1. `headerToggle.InvalidateArrange(); headerToggle.Arrange(new Rectangle(ContentBounds.X, ContentBounds.Y, ContentBounds.Width, headerSize.Height));`
     (top row, full width).
  2. If `Content?.IsVisible == true`:
     `revealedHeight = (int)(Content.Measure().Height * ExpansionProgress)`;
     `Content.InvalidateArrange(); Content.Arrange(new Rectangle(ContentBounds.X, ContentBounds.Y + headerSize.Height, ContentBounds.Width, revealedHeight));`
     - `Content.ClipToBounds` (set `true` once at assignment, §1) clips its own natural (possibly taller)
       rendered content down to `revealedHeight` during a transition; a no-op crop when
       `ExpansionProgress == 1` and `revealedHeight` already equals `Content`'s full natural height.
  3. `Chrome.InvalidateArrange(); Chrome.Arrange(ActualBounds);` last - same ordering `SplitPane`/`Slider`
     use, so the header toggle's own `Arrange` call (step 1) has already taken effect before `Chrome`
     re-arranges around it.

  **Explicit carry-over from the `SplitPane` postmortem** (see `[[project_icyui_tier2_roadmap]]`):
  `InvalidateArrange()` is called on every managed child immediately before every `Arrange()` call in
  this method, unconditionally - `UIElement.Arrange(rect)` no-ops when the target's own
  `IsArrangeInvalid` is already `false`, regardless of whether `rect` changed. `SplitPane` shipped
  without this and needed a follow-up fix; `Expander` applies the `StackPanel`/`Grid` pattern from the
  start instead of rediscovering it via another bug report.

### 5. Header click → `IsExpanded`

`ExpanderHeader.IsCheckedChanged` (inherited from `ToggleButton`) is wired, in the `Expander` constructor,
to set `Expander.IsExpanded = headerToggle.IsChecked`. `IsExpanded`'s own setter mirrors that back
(`headerToggle.IsChecked = value`) guarded against re-entrant loops the same way `SetProperty`'s own
no-op-on-unchanged-value check already prevents (setting `IsChecked` to its own current value is a no-op,
so no infinite ping-pong). This reuses `ToggleButton`'s entire existing click/hover/press/checked
machinery for the header row - `Expander` itself never touches `OnTap`/pointer state.

### 6. Templating & theming

`OnApplyTemplate` (mirroring `SplitPane.OnApplyTemplate`): `GetTemplateChild<ExpanderHeader>("PART_Header")`
repoints `headerToggle` when a `Template` supplies one, falling back to the default
constructor-built `ExpanderHeader` otherwise.

`DefaultTheme.xml` gains:

- An implicit `ExpanderHeader` style + `ControlTemplate`, following `CheckBox`'s exact shape (chevron
  `Icon` + `ContentPresenter` in a `StackPanel`):
  ```xml
  <ControlTemplate x:Key="IcyDefaultExpanderHeaderTemplate" TargetType="ExpanderHeader">
    <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="{TemplateBinding Padding}">
      <StackPanel Orientation="Horizontal">
        <Icon x:Name="PART_Chevron" Kind="ChevronRight" Stroke="White" StrokeThickness="2" Margin="0,0,8,0"/>
        <ContentPresenter Content="{TemplateBinding Content}" VerticalAlignment="Center"/>
      </StackPanel>
    </Border>
  </ControlTemplate>
  ```
  (exact chevron reflect-state mechanism - rotate vs. `Kind` swap between `ChevronRight`/`ChevronDown` -
  is a theming implementation detail worked out when `DefaultTheme.xml` is actually written, not a
  design-level decision.)
- An implicit `Expander` style + `ControlTemplate`, following the `Slider`/`SplitPane` shape:
  ```xml
  <ControlTemplate x:Key="IcyDefaultExpanderTemplate" TargetType="Expander">
    <Border Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
      <ExpanderHeader x:Name="PART_Header"/>
    </Border>
  </ControlTemplate>
  ```
- An `Expanded`/`Collapsed` `VisualState` pair on `Expander`, targeting `ExpansionProgress`, with
  `Duration` set (e.g. 200ms) and an easing function - the first real exercise of `VisualState.Duration`
  in the shipped theme.

### 7. Sample

An `ExpanderDemo` added to Shared Samples (same precedent as `SplitPaneDemo`) - a couple of `Expander`s
with varied header/content, demonstrating the expand/collapse animation and a `Header` containing more
than plain text (confirming the "arbitrary `UIElement` header" decision actually matters).

## Testing

- Defaults: `IsExpanded == false`, `ExpansionProgress == 0`, `Header`/`Content` both `null`.
- `Header`/`Content` set wires `Parent` (mirrors `SplitPane.First_Set_WiresParent`-style tests).
- `IsExpanded` setter toggles `ControlState.Expanded` and raises `IsExpandedChanged` exactly once per
  real change (no-op re-assignment raises nothing).
- Simulated tap (`Events.Touch.RaiseTap`) on the header toggles `IsExpanded`; a tap elsewhere (on
  `Content`, once visible) does not.
- `MeasureContent`/`ArrangeContent` at `ExpansionProgress == 0`: `Content` not measured/arranged
  (`IsVisible == false`), `Expander`'s own measured height equals just the header's.
- Same at `ExpansionProgress == 0.5`: `Content` arranged to half its natural height, clipped;
  `Expander`'s own measured height reflects the partial reveal.
- Same at `ExpansionProgress == 1`: `Content` arranged to its full natural height, no clipping shortfall.
- Regression-style: `ArrangeContent` re-called after only `ExpansionProgress` changes (not `Content`
  itself) still re-arranges `Content` to the new revealed height (the `SplitPane`-postmortem pattern,
  written proactively rather than found via a bug report).
- Templating: `PART_Header` swap via `Template` re-wires the header (mirrors `SplitPane`'s own
  `Template_WithPartDivider_*` coverage); a `Template` with no `PART_Header` falls back to the default.
- Manual smoke test (both engines, per `[[feedback_smoke_test_notification]]`): the sample's `Expander`s
  animate smoothly on click, collapse fully out of the layout, and a `StackPanel` holding several
  `Expander`s reflows as each one expands/collapses.

## Critical files

**New:** `sources/IcyUI/UI/Controls/Expander.cs`, `sources/IcyUI/UI/Controls/ExpanderHeader.cs`,
`sources/IcyUI.Tests/Controls/ExpanderTests.cs`.

**Modified:** `sources/IcyUI/UI/Styles/VisualState.cs` (new `ControlState.Expanded` flag),
`sources/IcyUI/Resources/Themes/DefaultTheme.xml` (new `Expander`/`ExpanderHeader` styles/templates,
first real `VisualState.Duration` usage), Shared Samples (new `ExpanderDemo`).

## Open items for implementation planning (not blocking spec approval)

- Exact `Duration`/`Easing` values for the `Expanded`/`Collapsed` transition, and the chevron's
  rotate-vs-swap mechanism - cosmetic theming specifics, not architecturally significant.
- Whether `IsExpanded`↔`headerToggle.IsChecked` two-way sync needs an explicit re-entrancy guard beyond
  `SetProperty`'s existing no-op-on-unchanged-value check, or whether that's already sufficient - a small
  implementation-time verification, not a design fork.
- Keyboard toggling (Enter/Space while the header is focused) - `ToggleButton`/`Button` may already
  provide this for free via inherited `OnTap`-adjacent input handling; needs checking during
  implementation, not a new design decision either way.
