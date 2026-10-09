# PropertyGrid metadata: ranges, defaults, descriptions and visibility

> Design spec, discussed with Ivan on 2026-10-09. Follow-up to the editor toolkit MVP
> (`2026-10-09-editor-toolkit-mvp-design.md`) and the original grid design (`2026-09-27-propertygrid-design.md`).
> Scheduled before the 2026-10-11 MVP.

## Context

Dogfooding the editor showed that the grid knows little about the properties it edits:

- **Honoured today:** `[Category]`, `[DisplayName]`, `[Browsable(false)]`, `[ReadOnly]`, and `[Range]` on numeric types.
  `[Range]` always becomes a slider, ignores exclusive bounds, and can't express an open-ended range.
- **Ignored:** `[DefaultValue]` (no Reset in the plain grid), `[EditorBrowsable]` and `[Description]` (core has no
  tooltips).
- **Rules live only in setters.** Core has 16 `Guard.*` call sites. Most of them are static rules on properties with
  no matching attribute, so the grid writes the value and the setter throws. The edit engine catches that, but each rejected
  keystroke is a first-chance exception, the message is a raw, culture-dependent guard text, and the grid can't explain the
  rule up front. Typing `1e40` into `DefaultEstimatedItemHeight` is even accepted as `∞`.

Goal: the grid knows every static rule before writing. Invalid input shows the Invalid state with a readable reason, and
never reaches a throwing setter. Defaults are resettable, hidden members stay hidden, and descriptions are visible.

## Decisions

| Question | Decision |
|---|---|
| How rules are declared | The standard `System.ComponentModel.DataAnnotations.RangeAttribute`, already used for `Opacity`. No new attribute type. |
| Exclusive bounds | `RangeAttribute.MinimumIsExclusive` / `MaximumIsExclusive`. |
| Open-ended ranges | An infinite bound (`double.PositiveInfinity` / `NegativeInfinity`). |
| NaN meaning "unset" | A value equal to the property's `[DefaultValue]` is always valid. NaN-defaulted layout limits accept "unset" with no extra attribute. |
| Slider or text box | Slider + text box only when both bounds are finite; otherwise a validated text box. |
| Where help text appears | The grid exposes `ActiveEntry` / `ActiveMessage` / `ActiveMessageChanged`; hosts show it. No built-in help pane. |
| Reset in the plain grid | Offered when `[DefaultValue]` exists and differs from the current value; writes the default. The editor's markup adapter keeps "remove the attribute". |
| `[EditorBrowsable]` | `Never` hides the row like `[Browsable(false)]`; `Advanced` and `Always` show it. |
| Setter guards | Stay as the safety net for code. A test keeps each guard and its attribute in agreement. |

## Design

### Rules: `PropertyGridEntry`

A new public `readonly record struct ValueRange(double Min, double Max, bool MinIsExclusive, bool MaxIsExclusive)` in
`Icy.Data`:

- `IsBounded`: both ends finite.
- `Contains(double value)`: honours the exclusive flags; `false` for NaN.
- `Describe()`: the English reason text, by case:
  - "Must be greater than 0."
  - "Must be at least 0."
  - "Must be less than 10."
  - "Must be at most 10."
  - "Must be between 0 and 1."
  - "Must be greater than 0 and at most 1." and its mirror;
  - all numbers in the invariant culture.

`PropertyGridEntry` changes:

- `ValueRange? Range` replaces the `(double Min, double Max)? Range` tuple. It's read from `[Range]` on numeric types,
  including the exclusive flags. A range that describes no values (`Min > Max`, or `Min == Max` with an exclusive end)
  is ignored, as an inverted one is today.
- `bool HasDefaultValue` / `object? DefaultValue`: from `[DefaultValue]`. The value is converted to the property type when
  `DefaultValueAttribute` stored another numeric type (`[DefaultValue(40)]` on a `float`).
- `string? Description`: from `[Description]`.
- `bool Validate(object? value, [NotNullWhen(false)] out string? reason)`:
  - `true` when the value equals `DefaultValue` (`float.NaN` equals itself here);
  - otherwise `true` when there's no range;
  - otherwise the numeric value must be inside the range, else `false` with `Range.Describe()`.
- Enumeration also skips `[EditorBrowsable(EditorBrowsableState.Never)]` members.

### Editors: `PropertyGrid`

- **Numeric rows:**
  - A bounded range keeps today's slider + text box pair. An open-ended range, or none, gets a plain text box.
  - Every typed value is validated before writing. Invalid or unparsable text:
    - sets `ControlState.Invalid` on the box;
    - writes nothing.
  - Valid text clears the state and writes as today. Empty text writes the default when that's NaN, so a layout limit
    can be cleared by deleting its text; otherwise empty text is just invalid.
  - The ranged pair keeps clamping slider drags, but a typed out-of-range value is now marked Invalid instead of being
    clamped silently.
- **Vector rows:** validated per composed value when the entry has a range. Vectors carry no `[Range]` in core today, so
  this only covers games' own properties.
- **Reset:** `PropertyGridValueAdapter.Default` now offers Reset when `HasDefaultValue` and the current value differs
  (`Equals`, with NaN equal to NaN). `Reset` writes `DefaultValue` through `TrySetValue`. Custom adapters keep their own
  rules.
- **Help text:**
  - `PropertyGridEntry? ActiveEntry`: the entry of the row containing the canvas's focused element, or `null`.
  - `string? ActiveMessage`: the active row's validation reason while its input is invalid, else its `Description`.
  - `event EventHandler? ActiveMessageChanged`: raised when either changes.
  - Tracking runs on the row editors' `FocusChanged` and on validation results. No per-frame work.

### Core annotations

`[Range]`, plus a `[DefaultValue]` where one is missing:

| Property | Rule |
|---|---|
| `UIElement.Width`, `Height` | `[Range(0d, double.PositiveInfinity)]`, default NaN |
| `UIElement.MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight` | `[Range(0d, double.PositiveInfinity)]`, default NaN |
| `UIElement.Opacity` | already `[Range(0f, 1f)]` |
| `ItemsControl.DefaultEstimatedItemHeight` | `[Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]` |
| `Selector.MaxDropDownHeight` | `[Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]` |
| `WrapGrid.ItemWidth`, `ItemHeight` | `[Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]` |
| `TreeView.Indent` | `[Range(0d, double.PositiveInfinity)]` |
| `SelectingItemsControl.SelectedIndex` | `[Range(-1, int.MaxValue)]`; the upper bound (`< ItemCount`) stays a setter guard |

Not covered: `ItemsControl.ScrollIntoView` (a method), and `SelectedIndex`'s dynamic upper bound.

### Hosts

- `PropertiesPanel`'s status line shows the editor's own refusal (`MarkupPropertyAdapter.LastError`) first, then the
  grid's `ActiveMessage`.
- The PropertyGrid demo adds a help line under its grid bound to `ActiveMessage`.

**Engines:** core and `IcyUI.Design` only. MonoGame, Stride and a future FNA host need no work.

## Testing

- **`ValueRange`:** `Contains` for inclusive, exclusive, open-ended and NaN; `Describe` for every case, under a
  comma-decimal culture too.
- **`PropertyGridEntry`:**
  - range read with exclusive flags;
  - an empty range is ignored;
  - `DefaultValue` converted to the property type;
  - `Description`;
  - `Validate` accepts the NaN default and rejects out-of-range;
  - `EditorBrowsable(Never)` is hidden and `Advanced` shown.
- **`PropertyGrid`:**
  - a bounded range builds a slider and an open-ended one a text box;
  - invalid typing marks Invalid and writes nothing, and valid typing clears it;
  - empty text resets a NaN-default property;
  - Reset appears for a non-default value and writes `[DefaultValue]`;
  - `ActiveEntry`/`ActiveMessage` follow focus and validation.
- **Annotation agreement:** for every core property with `[Range]`, values just inside the range (and the default) are
  accepted by the setter, and values just outside throw.
- **No first-chance exceptions:** typing invalid values into annotated rows of the Properties panel raises no exception at
  all, counted with `AppDomain.FirstChanceException`.
- The existing property-edit fuzz and all grid/editor tests stay green.

## Performance

- Enumeration reads three more attributes per property, once per `Target` change.
- Validation is a few comparisons per keystroke.
- `ActiveMessage` updates on focus changes and keystrokes only; there's no per-frame work.
- Release games without the editor pay nothing new beyond the attribute metadata already in the assembly.

## Out of scope

- Validation rules that depend on other state (`SelectedIndex < ItemCount`); they stay setter guards.
- Localised reason text.
- A built-in help pane; tooltips.
- `[Range]` on vectors and matrices in core.
