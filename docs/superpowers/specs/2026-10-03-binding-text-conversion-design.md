# Text → value conversion for two-way bindings (#5)

> Design spec, discussed with Ivan on 2026-10-03. It's the reverse of 3a's follow-up `1fba077`, which made the source →
> target direction format non-`string` values as text.

## Context

A two-way binding from `TextBox.Text` to a non-`string` source property (`int`, `float`, an `enum`…) is broken today:

- `Binding.SetValueToSource` writes the raw `string` into the source through reflection, and that throws for any
  non-`string` property.
- `UpdateSource` wraps the failure in a `BindingException` and rethrows it unless `FallbackValue` is set, so **typing a
  letter throws out of the input handler.** Intermediate edit states such as `-` or an empty box fail the same way.
- **After one failed write the binding is permanently disabled.** `SetValueToSource` sets `IsEnabled = false` before
  writing and resets it to `true` only on success, so later valid input never reaches the source.

## Decisions

| Decision | Choice |
|---|---|
| Conversion | `TypeDescriptor.GetConverter(sourceType).ConvertFromString(null, culture, text)`. It covers every numeric type, `decimal`, `bool`, enums (by name) and `Nullable<T>`. |
| Culture | The converter culture when one is set, otherwise `CultureInfo.CurrentCulture`. This mirrors the source → target direction. |
| Empty text | `null` for nullable and reference source types; an error for non-nullable ones. |
| Invalid input | **Never throw. Keep the source unchanged, and expose an error state.** The text stays as typed. Ivan's choice over reverting on focus loss or failing silently. |
| Error surface | `Binding.HasError`, `Binding.Error` and `Binding.ErrorChanged`, plus a new `ControlState.Invalid` flag on the target element, styled by the default theme. |

## Detailed design

### 1. `Binding`

**Conversion in `SetValueToSource`.**
1. If `ConverterParameters` is set, the converter runs as today; its result is written as is.
2. Otherwise, when the target value is a `string` and the source property type is neither `string` nor `object`, it is
   converted:
   - empty text becomes `null` when the source type accepts `null` (reference types or `Nullable<T>`) and is an error
     otherwise;
   - any other text goes through
     `TypeDescriptor.GetConverter(Nullable.GetUnderlyingType(type) ?? type).ConvertFromString(null, culture, text)`.
3. Any other value is written unchanged.

**No throwing on writes to the source.**
- Any exception raised while converting, or by the source property's own setter (e.g. `ScalingConfiguration.UserScale`
  rejecting `0`), is caught.
- The source keeps its previous value, the binding enters the error state, and nothing propagates to the input handler.
- `FallbackValue` keeps its current meaning: when set, it's still written to the source on failure, and the binding
  still records the error.
- `IsEnabled` is always restored (`try`/`finally`), so a failure no longer disables the binding.
- A missing `Source` is still a programming error and keeps throwing `BindingException`.

**Error state (new public API):**

| Member | Meaning |
|---|---|
| `bool HasError` | `true` after a failed write to the source; `false` after any later successful write in either direction (to the source, or from the source to the target). |
| `Exception? Error` | The failure that caused the error state (the converter's `FormatException`, the setter's `ArgumentException`, …), or `null`. |
| `event EventHandler? ErrorChanged` | Raised when `HasError` changes, or when `Error` changes while `HasError` stays `true`. |

Disposing a binding clears its error state.

### 2. Showing the error

- **`ControlState.Invalid = 1 << 9`.** When a binding's target is a `UIElement`, the binding reports its error state
  to that element through an internal hook. The element keeps the set of bindings currently in error and has `Invalid`
  while that set isn't empty. So a second, valid binding on the same element can't clear another binding's error, and a
  binding disposed while in error removes itself.
- **Default theme:** the `TextBox` style's `CommonStates` group gains two states.
  - `Invalid` (`State="Invalid"`): border `#FFD84C4C`.
  - `FocusedInvalid` (`State="Focused, Invalid"`): border `#FFFF6A6A`. It has two state bits, so it wins over both
    `Focused` and `Invalid` when the box is focused and invalid (the visual state with the most matching bits wins).
- Other controls opt in through their own styles. Only `TextBox` gets a default visual, because typing is where invalid
  input comes from.

## Edge cases

- Intermediate edit states (`-`, `1e`, empty for `int`) put the binding in the error state without throwing. Finishing
  the edit (`-5`) clears it.
- Typing `1.` into a `float` field: `1.` parses as `1`. The binding's own re-entrancy guard keeps the target from being
  rewritten to `1` mid-edit.
- An `enum` parses by member name, case-sensitively (`TypeConverter` behavior).
- A source → target update while the binding is in error (e.g. the model changed) clears the error, because the target
  now shows a valid value again.

## Testing

Unit tests on a real `TextBox` target and a plain model source:
- `"12"` sets an `int` source to 12.
- `"12a"` keeps 12, sets `HasError` and `Error` (a `FormatException`), raises `ErrorChanged`, and marks the `TextBox`
  `Invalid`.
- Correcting the text clears the error and the flag.
- After a failure, later valid input still updates the source (the `IsEnabled` regression).
- An `enum` by name; a `float` parsed with the converter culture; empty text gives `null` for `int?`.
- A source setter that throws gives the error state, not an exception.
- Two bindings on one element: one in error keeps the element `Invalid` while the other succeeds.
- Disposing an erroring binding clears `Invalid`.
- A source → target update clears the error.
- Theme: an invalid, focused `TextBox` gets the `FocusedInvalid` border.
- The existing source → target formatting (`1fba077`) is unchanged.

Build: no new warnings (the baseline is 82); `IcyUI.Stride` stays at 0.

Manual (Ivan runs it): the Controls demo's TextBox, or a TextBox bound to a numeric property. Type letters, then fix
them; the border turns red and back, and nothing crashes.

## Critical files

- `sources/IcyUI/Data/Bindings/Binding.cs`
- `sources/IcyUI/UI/Styles/VisualState.cs` (`ControlState.Invalid`)
- `sources/IcyUI/UI/UIElement.cs` (binding error aggregation)
- `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (`TextBox` states)
- `sources/IcyUI.Tests/Data/Bindings/BindingTextConversionTests.cs` (new)
