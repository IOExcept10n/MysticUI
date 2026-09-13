# Tier-2 Phase 8 — ColorPicker

> Design spec for Phase 8 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`).
> The roadmap calls this phase out as a scope note only ("ColorPicker") - this discussion expanded it
> into five deliverables once the actual picker UI and its dependencies were worked through with Ivan:
> a framework-level `UIElement.IsEnabled`, a new reusable `TabControl`/`TabItem`, a new `GradientBrush`,
> `ColorPicker` itself, and a popup-trigger `ColorPickerButton`.

## Context

Nothing in IcyUI today lets a user pick an arbitrary color, nor switch between multiple named views in
one control, nor paint a multi-stop gradient. All three turned out to be needed for a real ColorPicker
UI, so this phase delivers them together rather than as separate future phases.

**No cross-engine-integration-project impact for `TabControl`/`ColorPicker`/`ColorPickerButton`** - like
`SplitPane`/`Expander`/`ItemsControl`, these are pure core-`IcyUI` composition, input, and styling.
**`GradientBrush` and `UIElement.IsEnabled` are also pure core `IcyUI`** - `GradientBrush` renders via the
existing `IRenderContext.CreateTexture`/`Draw` path (already implemented in both `IcyUI.MonoGame` and
`IcyUI.Stride`), and `IsEnabled` only composes two already-existing properties' getters. **Nothing in this
phase requires new `IcyUI.MonoGame`/`IcyUI.Stride` code, and `IcyUI.FNA` (still an unimplemented stub)
needs no equivalent work as a result of it** - it inherits all five deliverables for free whenever it's
actually implemented, the same way it would have inherited any other pure-core phase.

One deliberate exception, called out explicitly rather than left implicit: `GradientBrush` is designed so
that a *future*, separate piece of work - actually implementing the long-dead `EffectCode.GradientEffect`
enum value for real - would need genuine per-engine shader work in both `IcyUI.MonoGame` and
`IcyUI.Stride` (and, eventually, `IcyUI.FNA`). That work is explicitly **not** part of this phase (see
Decision table) - `GradientBrush` only calls `IRenderContext.GetBuiltInEffect` speculatively, to detect
whether such work has already landed, and falls back to its texture-based rendering when it hasn't (i.e.
always, today).

## Decisions

| Decision | Choice |
|---|---|
| Overall picker UI | All three of: swatches, HSV square + hue strip, RGBA sliders + hex field - switchable via tabs, not stacked. Ivan's explicit choice over simpler slider-only or stacked-always-visible alternatives. |
| Tab switching mechanism | A new, genuinely reusable `TabControl`/`TabItem` (not a `ColorPicker`-private mechanism) - Ivan's explicit ask, with icons and disabled tabs in scope from the start. |
| Disabled-tab mechanism | Discovered mid-discussion that IcyUI has **no `IsEnabled`/interaction-gating concept anywhere** - `ControlState.Disabled` is a dead, never-set enum flag. Ivan chose to build a real framework-level `UIElement.IsEnabled` now (see §1), not a `TabItem`-scoped workaround and not deferring disabled tabs out of scope. |
| Alpha channel | Full RGBA - `SelectedColor` carries a meaningful, user-settable alpha. |
| Swatch palette source | Caller-supplied (`ColorPicker.SwatchColors`), no non-overridable built-in default. |
| Gradient rendering | New `GradientBrush : IBrush` (linear + radial), texture-backed via the existing `CreateTexture`/`Draw` path - **not** a new shader effect this phase. Designed to opportunistically use `EffectCode.GradientEffect` if a future phase ever implements it for real (see §3.4), but building that shader work now was explicitly rejected as disproportionate scope for this phase. |
| Hue strip | No new control - a themed `Slider` (`Background` = a rainbow `GradientBrush`, thin custom `PART_Thumb`). `Control.Background` is already `IBrush`-typed, so this needs zero `Slider.cs` changes. |
| SV square | New, but **internal-only** (not a public reusable control) - a 2D bilinear HSV blend isn't expressible as `GradientBrush` stops, and nothing outside `ColorPicker` has asked to reuse it. |
| `ColorPickerButton` base class | `Control` (not `ToggleButton`) - mirrors `Selector`/`Dropdown`/`ComboBox`'s own precedent exactly: the outer control owns popup-lifecycle/`IsOpen`/`SelectedColor` state that doesn't belong on `ToggleButton` itself, and composes an internal `ToggleButton` as its `Chrome`/`PART_ToggleButton`, the same relationship `Selector` has with its own toggle. |
| `ColorPickerButton` popup mechanism | `Canvas.AddOverlay`/`RemoveOverlay`, anchored under the button, outside-click dismiss - the exact non-modal popup machinery `Dropdown`/`ComboBox` already established. Not a `Dialog`-hosted modal popup. |

## Detailed design

### 1. `UIElement.IsEnabled` (framework-level)

New property on `UIElement` (`sources/IcyUI/UI/UIElement.cs`), alongside the existing `IsVisible`/
`IsHitTestVisible`/`IsFocusable`:

```csharp
[Category("Behavior")]
[DefaultValue(true)]
[RegisterReference]
public bool IsEnabled
{
    get => isEnabled;
    set
    {
        if (SetProperty(ref isEnabled, value))
            State = value ? State & ~ControlState.Disabled : State | ControlState.Disabled;
    }
}
```

**Grounded in the actual current code, not assumed:**
- `ControlState.Disabled` (`sources/IcyUI/UI/Styles/VisualState.cs` line 31) exists in the `[Flags] enum`
  but is set/checked nowhere in the codebase today - confirmed via a full-repo search. This is the first
  real wiring of it, giving `DefaultTheme.xml` a `Disabled` `VisualState` to key off for the first time.
- `IsHitTestVisible`'s own doc comment (`UIElement.cs` ~line 370) already documents "the entire subtree is
  skipped" semantics, built for the drag-and-drop preview ghost - reused here, not re-invented.
- `Canvas.MoveFocus`'s `focusable` list (`Canvas.cs` line 401) and `Canvas.HitTest`'s ancestor-focus walk
  (line 632) both already gate on `IsFocusable`/`IsVisible` - reused via composition, not new `Canvas.cs`
  logic.

**Composition, not replacement**, so neither existing property's own independent semantics break:

```csharp
public bool IsHitTestVisible
{
    get => isHitTestVisible && IsEnabled;   // was: get => isHitTestVisible;
    set => SetProperty(ref isHitTestVisible, value);
}

public bool IsFocusable
{
    get => isFocusable && IsEnabled;   // was: get => isFocusable;
    set => SetProperty(ref isFocusable, value);
}
```

This gets "a disabled element and its whole subtree become unclickable" **for free**, through the exact
hit-test subtree-skip `Canvas.HitTest` already performs for `IsHitTestVisible` - zero new `Canvas.cs`
code. Likewise, `Canvas.MoveFocus`'s existing `.Where(e => e.IsFocusable && e.IsVisible)` filter excludes
disabled elements automatically once `IsFocusable`'s getter composes `IsEnabled`.

**Deliberately scoped narrow - confirmed with Ivan, not assumed:**
- **No cascading visual "disabled" look to descendants.** A disabled container's children still render
  normally (no dimming) - they simply become unreachable via the subtree-skip. The harder "effective
  IsEnabled inherited down the ancestor chain" problem (WPF's actual model) is real but isn't what this
  phase needs (only `TabItem` wants disabling) - deferred the same way this project has repeatedly
  deferred general/harder versions of a need in favor of the concrete one actually asked for (general
  spatial focus-traversal, general anchor-correction, 2D grid keyboard nav, Accordion).
- Focus-clearing when a currently-focused element becomes disabled mid-session is a small implementation-
  time detail (does `Canvas` need to notice and move focus away, or does nothing reach it anyway once
  hit-testing/focus-listing exclude it?) - not an architectural fork, verified during implementation.

### 2. `TabControl` / `TabItem`

New files `sources/IcyUI/UI/Controls/TabItem.cs`, `sources/IcyUI/UI/Controls/TabControl.cs`.

- **`TabItem : ContentControl`** - `Content` is the tab's own body (shown only while selected). New
  properties: `Header` (`object?`, mirrors `Expander.Header`'s "arbitrary content slot, implicit
  `TextBlock` conversion from markup" precedent) and `Icon` (`Icy.UI.Controls.Icon?`, nullable). Disabled
  tabs are just `TabItem.IsEnabled = false` (§1) - no `TabItem`-specific plumbing needed at all now that
  the framework has a real primitive for it.
- **`TabControl : SelectingItemsControl`.** `ItemsSource` holds `TabItem` objects. Grounded in
  `ItemsControl.ResolveTemplate` (`ItemsControl.cs` line 502): it throws when no `ItemTemplate`/
  `ItemTemplateSelector` is set - there's no "item is already a `UIElement`, skip templating" escape
  hatch anywhere in the base class, so `TabControl` must supply its own default `ItemTemplate`
  internally (built from each `TabItem`'s `Header`/`Icon` - an `Icon` beside a `ContentPresenter`,
  mirroring `CheckBox`'s/`ExpanderHeader`'s own icon-plus-content template shape), exactly like
  `Selector`'s subclasses rely on `DisplayMemberPath`/`ItemTemplate` to go from arbitrary data to a
  visual. A caller can still override the header's own look via `ItemTemplate`, same escape hatch every
  other `ItemsControl` already has.
- **`CreateContainer` override** (mirrors `SelectingItemsControl`'s own `CreateContainer`, which
  `TabControl` inherits and does not need to further override) realizes each `TabItem`'s header as a
  `SelectorItem`, click-to-select wired via the inherited `SelectorItem.Tapped` machinery - identical to
  `ListBox`, no new dispatch path.
- **Header-strip layout**: `TabControl` overrides the same virtualization geometry hooks `WrapGrid`
  already widened to `protected virtual` in Phase 6 (`LocateViewportStart`/`RealizeRange`/
  `ComputeExtentHeight`) to arrange realized headers in a single horizontal row instead of a vertical
  list or grid - virtualization is functionally moot at typical tab counts, but reusing the existing hook
  shape costs nothing and avoids a second, parallel non-virtualizing code path.
- **Content area**: a second, separately-managed child (outside `Chrome`, not going through the header
  strip's own realized-items area at all) - a `ContentPresenter` whose `Content` tracks
  `((TabItem?)SelectedItem)?.Content`, updated from `OnSelectionChanged` (the same override hook
  `SelectingItemsControl` already exposes for exactly this "derived state that depends on the new
  selection" purpose - see its own doc remarks). This mirrors `Expander.Content`/`SplitPane.First`+
  `Second`'s "arrange directly, not through `Chrome`" precedent.
- **Composition**: `Chrome` hosts the header strip only (mirrors `SplitPane`/`Expander`'s "`Chrome` hosts
  only the small bit" shape); the content-presenter child is the second, `Chrome`-external managed child.

### 3. `GradientBrush`

New file `sources/IcyUI/Rendering/Brushes/GradientBrush.cs`.

#### 3.1 Public shape

```csharp
public enum GradientKind { Linear, Radial }

public readonly record struct GradientStop(float Offset, Color Color);

public class GradientBrush : IBrush
{
    public GradientKind Kind { get; set; }
    public IList<GradientStop> GradientStops { get; }   // sorted by Offset for sampling
    public float Angle { get; set; }        // degrees, Linear only
    public Vector2 Center { get; set; }      // normalized 0-1 within the draw rect, Radial only
    public void Draw(IRenderContext context, in TextureRenderingOptions options) { ... }
}
```

#### 3.2 Texture-backed rendering (the path used today, unconditionally)

Mirrors `SolidColorBrush.Draw`'s own shape (`SolidColorBrush.cs` line 27): builds a full per-pixel
`Color[]` buffer sized to the brush's actual draw rect -
- `Linear`: project each pixel onto the `Angle`-rotated axis, interpolate between the two nearest
  `GradientStops` by the projected, normalized position.
- `Radial`: interpolate by each pixel's normalized distance from `Center` (clamped at the rect's own
  half-diagonal, so `Radius` isn't a separately exposed property - YAGNI until a real need for an
  explicit radius shows up).

Uploads via the existing `IRenderContext.CreateTexture<TColor>` (`IRenderContext.cs` line 67), then draws
through the same `context.Draw(texture, options)` call every other brush uses. The buffer is cached and
only rebuilt when size, `Kind`, `Angle`/`Center`, or `GradientStops` actually change - the same "cheap,
infrequent recompute" reasoning already agreed for the HSV square (§4).

#### 3.3 Effect-backed rendering (opportunistic, not built this phase)

Confirmed by reading the actual current render contexts, not assumed: `GetBuiltInEffect` unconditionally
`throw new NotImplementedException()` in `IcyUI.MonoGame`'s `RenderContext`, `IcyUI.Stride`'s
`RenderContext`, *and* the test `FakeRenderContext` - `EffectCode.GradientEffect` has never been
implemented anywhere.

`GradientBrush` still writes its `Draw` method to opportunistically detect if that ever changes, entirely
self-contained - **zero changes to `IRenderContext`'s own contract**:

```csharp
private IEffect? gradientEffect;
private bool effectProbed;

private IEffect? TryGetGradientEffect(IRenderContext context)
{
    if (!effectProbed)
    {
        effectProbed = true;
        try { gradientEffect = context.GetBuiltInEffect(EffectCode.GradientEffect); }
        catch (NotImplementedException) { gradientEffect = null; }
    }
    return gradientEffect;
}
```

Probed once per `GradientBrush` instance (not once per `Draw` call - avoids repeated exception overhead
against contexts that will never implement it), cached for the instance's lifetime. When a future engine
implementation actually returns a working effect, `GradientBrush.Draw` picks it up with no consumer-facing
API change on either side. **Actually implementing `GradientEffect` for real (real MonoGame `Effect`/
Stride shader work) is explicitly out of scope for this phase** - flagged in the Decision table so it
isn't mistaken for part of this phase's deliverables.

#### 3.4 Hue strip reuse

`DefaultTheme.xml` gains a themed `Slider` variant (or an implicit style keyed to a `HueSlider` marker
subtype, worked out during implementation the way `ExpanderHeader`'s own marker-subtype precedent was) -
`Background` set to a `GradientBrush` with 7 evenly-spaced rainbow stops, `Minimum`/`Maximum` = 0/360, and
a thin custom `PART_Thumb` (a narrow vertical bar rather than `Slider`'s default filled-block thumb).
**No `Slider.cs` changes at all** - `Control.Background` is already `IBrush`-typed, so this is purely a
`DefaultTheme.xml` addition consuming an already-general-purpose property.

### 4. `ColorPicker`

New file `sources/IcyUI/UI/Controls/ColorPicker.cs`.

- `Control` subclass. `Chrome` hosts an internal `TabControl` with three built-in `TabItem`s
  ("Swatches", "Picker", "Sliders") - `ColorPicker` itself owns no other visible chrome.
- `SelectedColor : Color` (plain notify property, `[RegisterReference]` only - no `Affects*` attribute
  needed, matching `Slider.ThumbBrush`'s own precedent: a color/brush-only property doesn't affect
  layout, and rendering isn't measure/arrange-cached the way `Affects*` attributes gate) +
  `ColorChanged` event, naming mirrors `Slider.Value`/`ValueChanged`.
- `SwatchColors : IList<Color>` (caller-supplied, §Decisions) - bound into the "Swatches" tab's `WrapGrid`
  of small swatch buttons (a `Border` per color, tapped to set `SelectedColor`).
- "Picker" tab: the hue `Slider` (§3.4) plus the new internal SV-square element (§4.1).
- "Sliders" tab: four `Slider`s (R/G/B/A, 0-255) plus a hex `TextBox` (`"#RRGGBBAA"`), all kept in sync
  with `SelectedColor` and each other.
- **Re-entrancy guard, flagged explicitly for the implementation plan, not resolved here**: four
  sub-widgets (SV square, hue slider, RGBA sliders, hex field) all read from and write to the same shared
  `SelectedColor`. Needs a suppress-while-updating guard (only push a value that's actually different) so
  one control's update doesn't bounce through all the others and back - the same class of guard other
  IcyUI controls already use for equivalent shared-state situations. Not a design fork, just something
  the implementation plan must get right from the start rather than discover as a bug later (matching this
  project's own repeated "properties that read-and-write the same shared state need this from day one"
  lesson).

#### 4.1 SV-square element

Internal-only (§Decisions) - a small `UIElement` (not a public `Control`) that:
- Renders a 2D bilinear HSV blend as its background: horizontally, white → the current hue's fully-
  saturated color; vertically, that → black. Built via the same `CreateTexture`-per-change technique
  `GradientBrush` uses internally (§3.2), but is **not** a `GradientBrush` instance or consumer - the
  blend is a genuine two-axis HSV formula, not expressible as linear/radial color-stop interpolation.
  Recomputed only when the current hue or the element's own size changes.
- Overlays a small circular marker at the current S/V position.
- Single-target drag (`OnDragStarted`/`OnDragPerforming`/`OnDragEnded`, exactly `Slider`'s own mechanism)
  updates S/V from the pointer position within the square.

### 5. `ColorPickerButton`

New file `sources/IcyUI/UI/Controls/ColorPickerButton.cs`.

- `Control` subclass (§Decisions - mirrors `Selector`'s own relationship to `ToggleButton`, not an
  inheritance of it). `Chrome` hosts an internal `ToggleButton` (`PART_ToggleButton`) whose own
  `Background` is a `SolidColorBrush(SelectedColor)` swatch preview, updated whenever `SelectedColor`
  changes.
- `SelectedColor`/`ColorChanged`/`SwatchColors` forwarded straight through to an internal `ColorPicker`
  instance, which is `Canvas.AddOverlay`'d (anchored under the button) when the toggle opens and
  `RemoveOverlay`'d when it closes - the exact `PART_Popup`/outside-click-dismiss shape `Dropdown`/
  `ComboBox` already established (`Selector.cs`), reused wholesale, not reinvented.

## Testing

- `IsEnabled`: defaults `true`; setting `false` sets `ControlState.Disabled` and clears it back on
  `true`; a disabled element (and a nested child under it) is excluded from `Canvas.HitTest` and from
  `MoveFocus`'s candidate list; an enabled sibling under the same disabled ancestor is unaffected in its
  own `IsEnabled`/`ControlState` (confirms the "no visual cascade" scoping decision).
- `TabControl`/`TabItem`: `SelectedIndex`/`SelectionChanged` behave like `ListBox`'s own coverage; tapping
  a disabled `TabItem`'s header does not change selection; the content-presenter child shows exactly the
  selected `TabItem.Content`, swapping on selection change; icon-bearing vs. icon-less `TabItem`s both
  realize correctly.
- `GradientBrush`: `Linear`/`Radial` pixel-buffer correctness at a few sample points/offsets; buffer cache
  invalidates on `GradientStops`/`Angle`/`Center`/`Kind`/size change and not otherwise; the effect-probe
  path catches `NotImplementedException` exactly once per instance (a test double `IRenderContext` that
  throws confirms no repeated probing).
- `ColorPicker`: changing any one of (SV square, hue slider, each RGBA slider, hex field) updates
  `SelectedColor` and every other sub-widget to match, with no re-entrant loop (a counting test double
  confirms each change settles in a bounded number of property-set cycles); `SwatchColors` tap sets
  `SelectedColor` to the tapped swatch exactly.
- `ColorPickerButton`: tap opens the popup anchored under the button; outside-click dismisses it (mirrors
  `Dropdown`'s own outside-click test); the preview swatch tracks `SelectedColor` live, including while
  the popup is open and a sub-widget inside it is being dragged.
- Manual smoke test (both engines, per `[[feedback_smoke_test_notification]]`): the sample's `ColorPicker`
  and `ColorPickerButton` render/interact correctly, tab switching is visually correct, the hue strip
  gradient renders (confirms `GradientBrush`'s actual rendering, not just its pixel-buffer math), disabled
  tabs visibly dim and don't respond to taps.

## Critical files

**New:** `sources/IcyUI/UI/Controls/TabItem.cs`, `TabControl.cs`, `ColorPicker.cs`, `ColorPickerButton.cs`,
`sources/IcyUI/Rendering/Brushes/GradientBrush.cs`, and matching new test files under
`sources/IcyUI.Tests/Controls/` and `sources/IcyUI.Tests/Rendering/Brushes/`.

**Modified:** `sources/IcyUI/UI/UIElement.cs` (new `IsEnabled`, composed `IsHitTestVisible`/`IsFocusable`
getters), `sources/IcyUI/UI/Styles/VisualState.cs` (no new flag - `ControlState.Disabled` already exists,
just gets wired up for the first time), `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (new
`TabControl`/`TabItem`/`ColorPicker`/`ColorPickerButton` styles/templates, a `Disabled` `VisualState` on
relevant controls for the first time, the hue-strip `Slider` variant), Shared Samples (new
`TabControlDemo`, `ColorPickerDemo`).

## Open items for implementation planning (not blocking spec approval)

- Exact re-entrancy-guard mechanics for `ColorPicker`'s shared-state sync (§4) - a real implementation-
  time risk area, not an architectural fork.
- Whether losing focus needs explicit `Canvas` handling when an element becomes disabled mid-session
  (§1) - small verification, not a design fork.
- Exact default `ItemTemplate` markup for `TabControl`'s header (icon + content layout specifics) and the
  hue-`Slider` theming marker-subtype approach (§3.4, mirrors `ExpanderHeader`'s own precedent) - cosmetic
  theming specifics worked out when `DefaultTheme.xml` is actually written.
- Hex `TextBox` parse/format details (`#RRGGBBAA` vs. also accepting `#RRGGBB` with implied full alpha,
  invalid-input handling) - a small implementation-time UX detail, not architecturally significant.
