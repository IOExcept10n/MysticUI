# Tier-2 Phase 9 — PropertyGrid

> Design spec for Phase 9 of the Tier-2 controls roadmap (`ah-i-ve-understood-i-piped-axolotl.md`).
> Confirmed with Ivan via a short multiple-choice design discussion rather than open-ended brainstorming,
> given the shape of the open questions (target model, metadata source, editor type set) was already
> well-understood from surveying the existing reflection/binding infrastructure first.

## Context

`PropertyGrid` is a Unity-Inspector-style control: point it at an object, it lists that object's
properties as rows (label + type-appropriate editor), lets the user edit them. It is the last "concrete
control" phase on the roadmap before Phase 10 (`EditorFrame`/`EditorPanel`, the speculative WYSIWYG
designer toolkit) — `PropertyGrid` is explicitly meant to be a building block for that later phase, but
must also stand alone as a runtime inspector, not an editor-tool-only component.

**No cross-engine-integration-project impact** — like every prior Tier-2 phase except the very first
theme/DnD foundation, this is pure core-`IcyUI` composition and reflection, reusing already-shipped
rendering/input/theming plumbing. Nothing in this phase requires new `IcyUI.MonoGame`/`IcyUI.Stride` code,
and `IcyUI.FNA` (still an unimplemented stub) needs no equivalent work as a result of it.

## Decisions

| Decision | Choice |
|---|---|
| Target model | **Hybrid.** `PropertyGrid.Target` can be a `[RegisterReference]`-tagged `DependencyObject` or a plain untagged POCO. Enumeration dedups registered-vs-reflected by name (registered wins). Ivan's explicit choice over registered-only (too narrow — a plain game-data POCO would show up empty) or pure-reflection-only (throws away the existing registry for controls that already have one). |
| Get/set mechanism | Cached `PropertyInfo` reflection, uniformly for both source kinds — **not** a live two-way `Binding` per row. A row reads the current value when its container is (re)attached, writes back explicitly on edit-commit. External changes to `Target` while displayed do not auto-refresh; only reassigning `Target` re-reads everything. Deliberate v1 simplification (flagged as a future fast-follow), not an oversight — building live reactivity for an untyped, possibly-non-notifying runtime target is materially harder than the "point at an object, edit it" v1 need actually asked for. |
| Per-property metadata | Existing BCL attributes only, no new IcyUI attribute types: `System.ComponentModel.BrowsableAttribute`, `DisplayNameAttribute`, `ReadOnlyAttribute`, `CategoryAttribute` (already partially consumed elsewhere for registered properties), `System.ComponentModel.DataAnnotations.RangeAttribute` (numeric min/max → drives a `Slider` instead of a bare `TextBox`). Ivan's explicit choice over inventing IcyUI-specific attributes. |
| v1 editor type set | string, bool, numeric primitives (plain or `Slider`+`TextBox` when `[Range]` present), enum (`ComboBox` over `Enum.GetValues`), IcyUI `Color` (via this session's `ColorPickerButton`), and `System.Numerics` vectors/matrices (`Vector2/3/4`, `Quaternion`, `Matrix3x2`, `Matrix4x4`) as a composite per-component row. |
| Nested/complex objects | **Explicitly deferred** — no recursive Expander-into-sub-PropertyGrid default in v1. Ivan wants dedicated custom editors for these later rather than a generic recursive fallback now. |
| Date/time/URI editors | **Explicitly deferred, noted only** — `DateOnly`/`TimeOnly`/`DateTime` (calendar/time pickers) and `Uri` (file picker) have no widget yet anywhere in the codebase. Flagged with `// TODO` at the fallback site so the gap stays discoverable, not silently dropped. |
| Unrecognized-type fallback | Read-only `TextBlock` showing `value?.ToString()` — covers both deferred cases above and any future type nobody's built an editor for yet. Never a hard failure. |
| Category display | Flat list of rows, grouped by `[Category]` with a simple non-interactive header divider before each group's first row — **not** a collapsible `Expander` per category (that machinery is reserved for the deferred nested-object case). This is my own default, not something Ivan was explicitly asked about — flagged for review once built. |
| Row construction | Direct C# composition inside overridden `AttachContainer`/`DetachContainer` (the same pooled-rebind hooks `Selector`/`WrapGrid`/`ListBox` already use), **not** markup `DataTemplate`/`ItemTemplateSelector` — the per-type get/set glue doesn't fit a declarative template well, matching every prior control that needed custom per-item logic. |
| Base class | `PropertyGrid : ItemsControl` — matches the "any control holding multiple materialized items is an `ItemsControl`" principle Ivan established during Phase 6 (`WrapGrid`/`ListBox`). |

## Detailed design

### 1. `PropertyGridEntry`

New file `sources/IcyUI/Data/PropertyGridEntry.cs`. Unifies a registered `IPropertyReference` and a
reflected `PropertyInfo` behind one shape:

```csharp
public sealed class PropertyGridEntry
{
    public string Name { get; }
    public string DisplayName { get; }      // [DisplayName], else Name
    public string Category { get; }          // [Category], else "Misc"
    public Type PropertyType { get; }
    public bool IsReadOnly { get; }          // [ReadOnly(true)], or no public setter
    public (double Min, double Max)? Range { get; }   // [Range], numeric types only
    public object? GetValue(object target);
    public bool TrySetValue(object target, object? value);

    public static IReadOnlyList<PropertyGridEntry> EnumerateFor(object target);
}
```

`EnumerateFor`:
- Resolves `target.GetType()`; if the type has any `[RegisterReference]` members, pulls their names via
  `PropertyRegistry.For(target).GetPropertyStore(type).EnumerateProperties()`.
- Pulls every other public readable `PropertyInfo` via reflection, skipping names already covered by the
  registered set.
- Reads `[Browsable]`/`[DisplayName]`/`[ReadOnly]`/`[Category]`/`[Range]` directly off the underlying
  `PropertyInfo` for **both** source kinds uniformly — confirmed via codebase survey that
  `PropertyReferencesRegistration` only pre-parses `Category`/`DefaultValue` today, not the
  PropertyGrid-relevant attributes, so a registered property's own `IPropertyReference` cannot be trusted
  to already carry them.
- **Implementation must first confirm `IPropertyReference`/`PropertyReference<T,V>` exposes its wrapped
  `PropertyInfo` publicly** (survey step, not yet verified) — add a minimal accessor if missing rather
  than re-resolving the same `PropertyInfo` a second time via `type.GetProperty(name)`.
- Filters `[Browsable(false)]`; groups by `Category`, preserving first-seen category order.

### 2. `EnumValueCache`

New file `sources/IcyUI/Data/EnumValueCache.cs`. A small static, cached `Enum.GetValues(Type)` per type —
confirmed no equivalent helper exists anywhere in the codebase today (`ComboBox`'s own `ItemsSource` has
always been caller-supplied `IEnumerable` so far).

### 3. `PropertyGrid`

New file `sources/IcyUI/UI/Controls/PropertyGrid.cs`, `PropertyGrid : ItemsControl`.

- `Target : object?` — setter calls `PropertyGridEntry.EnumerateFor(value)` and assigns the result to the
  inherited `ItemsSource`.
- Overrides `AttachContainer`/`DetachContainer` to build/rebind a two-column `Grid` (Auto label | Star
  editor) per realized row, dispatching the editor widget by `entry.PropertyType`:

  | Type | Editor |
  |---|---|
  | `string` | `TextBox` |
  | `bool` | `CheckBox` |
  | numeric, no `[Range]` | numeric-validating `TextBox` |
  | numeric, `[Range]` | `Slider` (`Minimum`/`Maximum` from the attribute, `Convert.ToDouble` against the boxed `Minimum`/`Maximum`) + adjacent numeric `TextBox` — mirrors `ColorPicker`'s hue-slider+hex-box pairing |
  | `enum` | `ComboBox`, `ItemsSource = EnumValueCache.GetValues(propertyType)` |
  | IcyUI `Color` | `ColorPickerButton`, `SelectedColor` two-way |
  | `Vector2/3/4`, `Quaternion`, `Matrix3x2`, `Matrix4x4` | composite row: one labeled numeric `TextBox` per component (X/Y/Z/W, or `M11`.. for matrices); commit rebuilds the struct from every field and calls `entry.TrySetValue` |
  | anything else (incl. deferred nested/date/time/URI cases) | read-only `TextBlock` showing `value?.ToString()`, `// TODO` citing this spec |

- **Pooled-container correctness is the sharpest risk area.** The immediately-prior commit on this branch
  ("Fix TabControl header pooling stale-content bug") is the exact failure mode to guard against here: a
  pooled row container reused for a *different* editor-widget shape (e.g. row 3 was a `TextBox` for object
  A's string property, must become a `CheckBox` for object B's bool property at the same index) must
  discard-and-rebuild the editor child, never attempt to rebind a mismatched widget. `AttachContainer`
  must branch on "does the pooled container's existing editor-kind match this entry's needed editor-kind"
  before deciding update-in-place vs. rebuild.
- Category header dividers: a plain non-interactive row (e.g. a bold `TextBlock` in its own full-width
  `Grid` row) inserted immediately before the first row of each new `Category` encountered while walking
  the enumerated entries in order.

### 4. Theming

`sources/IcyUI/Resources/Themes/DefaultTheme.xml` gains a `PropertyGrid` **Style-only** entry
(`Background`/`BorderBrush`/`BorderThickness`, no `ControlTemplate`) — matches `TextBox`/`ScrollViewer`'s
existing precedent for controls that reach into their own internals without named template parts. Every
editor widget used inside a row (`TextBox`/`CheckBox`/`ComboBox`/`Slider`/`ColorPickerButton`) is already
themed from prior phases — nothing new needed there.

## Testing

- `PropertyGridEntry`: registered-vs-reflected dedup (registered wins on name collision); `[Browsable]`
  filtering; `[DisplayName]`/`[Category]`/`[ReadOnly]`/`[Range]` reading for both source kinds; get/set
  round-trip for both source kinds; category grouping preserves first-seen order.
- `PropertyGrid`: one test per editor type in the dispatch table above, including the `[Range]`-present
  vs. absent numeric branch and every `System.Numerics` type; unrecognized-type fallback renders read-only
  `ToString()` and never throws.
- **Pooled-container type-mismatch regression test**, mirroring the TabControl bug's own repro shape:
  assign `Target` to object A, then to object B with a different property-type sequence at the same row
  indices, confirm no stale widget survives and no value is bound to a mismatched editor.
- Manual smoke test (both engines, per `[[feedback_smoke_test_notification]]`): the sample demonstrates
  every v1 editor type rendering and editing correctly, and specifically exercises the `Target`-swap
  pooling edge case visually, not just via unit test.

## Critical files

**New:** `sources/IcyUI/Data/PropertyGridEntry.cs`, `EnumValueCache.cs`,
`sources/IcyUI/UI/Controls/PropertyGrid.cs`, matching test files under `sources/IcyUI.Tests/Data/` and
`sources/IcyUI.Tests/Controls/`, `sources/Shared Samples/PropertyGridDemo.cs`,
`sources/MonoGame Sample/Samples/PropertyGridSample.cs`.

**Modified:** `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (new `PropertyGrid` style),
`sources/MonoGame Sample/SampleGame.cs` and `sources/Stride Sample/SampleGame.cs` (demo registration),
possibly `sources/IcyUI/Data/Markup/IPropertyReference.cs`/`PropertyReference.cs` (minimal `PropertyInfo`
accessor, pending the survey step in §1).

## Open items for implementation planning (not blocking spec approval)

- Whether `IPropertyReference` needs a new public accessor for its wrapped `PropertyInfo`, or already has
  one — first thing to verify, not assumed here.
- Exact category-header-divider visual treatment (§3) — cosmetic detail, not an architectural fork.
- `[Range]`'s `Minimum`/`Maximum` are boxed `object` on the BCL attribute — assumed `Convert.ToDouble`
  converts cleanly for every numeric primitive type in scope; confirm during implementation.
