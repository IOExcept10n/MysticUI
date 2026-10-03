# Binding Text → Value Conversion (#5) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Two-way bindings convert typed text to the source's type, never throw on a bad value, and expose an error
state that themes can show.

**Architecture:**
- `Binding.SetValueToSource` converts `string` targets to the source type through `TypeDescriptor`. Write failures are
  caught into `HasError`/`Error`/`ErrorChanged`, and `IsEnabled` is always restored.
- An erroring binding reports to its `UIElement` target, which sets `ControlState.Invalid` while any of its bindings is
  in error.
- The default theme styles `TextBox` with `Invalid`/`FocusedInvalid` states.

**Tech Stack:** C# / .NET 10, xUnit v2, `System.ComponentModel.TypeDescriptor`.

**Spec:** `docs/superpowers/specs/2026-10-03-binding-text-conversion-design.md`

## Global Constraints

- Every public API gets complete XML documentation. Files start with the repo copyright header and use block-scoped
  namespaces. StyleCop applies to `IcyUI` (member order, one type per file).
- `ControlState.Invalid = 1 << 9`. Theme colors: `Invalid` border `#FFD84C4C`, `FocusedInvalid` border `#FFFF6A6A`.
- A missing `Source` keeps throwing `BindingException`. Every other failure while writing to the source is caught.
- Build: `dotnet build "sources/IcyUI.sln" --no-incremental`, no new warnings against the baseline of **82**;
  `IcyUI.Stride` stays at 0.
- Tests: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. **Check the `Total:` line.** The baseline is **1058**.
- Every commit message ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Plan-level decisions

1. **Culture.** The text path runs only when no `ConverterParameters` is set (a converter does its own conversion), so it
   always uses `CultureInfo.CurrentCulture`. The spec's "converter culture when one is set" therefore never applies on
   this path. The test pins `CurrentCulture` explicitly.
2. **`Error` unwraps `TargetInvocationException`.** Reflection-based path setters wrap the setter's exception; `Error`
   reports the inner one, as the spec describes ("the setter's `ArgumentException`"). `TypeConverter`s report bad text
   as `ArgumentException` (with an inner `FormatException`), so tests assert `Error` is non-null and don't pin a type.
3. **A `FallbackValue` that fails to write is ignored.** The error is already recorded, and throwing would defeat the
   "never throw" rule.

## Review Focus

1. **Typing fast through intermediate states** (`-`, `-1`, `-1x`, `-12`): no exception at any keystroke, and the final
   state is valid with no error. Test: Task 1 (`IntermediateStates_NeverThrow` plus `CorrectingTheText_ClearsTheError`).
2. **A failure followed by valid input:** the source updates, which proves `IsEnabled` is restored. Test: Task 1
   (`AfterAFailure_LaterValidInputStillReachesTheSource`).
3. **Two bindings on one element, one failing:** the element stays `Invalid` while the other succeeds. Test: Task 2.
4. **The model changes while the box shows bad text:** the error clears and the box shows the model's value. Test:
   Task 1 (`SourceToTargetUpdate_ClearsTheError`).
5. **A custom-converter binding:** its behavior is unchanged (the text path is skipped). It's covered by the existing
   converter usage; no new test.

---

### Task 1: Conversion and error state in `Binding`

**Files:**
- Modify: `sources/IcyUI/Data/Bindings/Binding.cs`
- Test: `sources/IcyUI.Tests/Data/Bindings/BindingTextConversionTests.cs` (new)

**Interfaces:**
- Produces: `public bool Binding.HasError`, `public Exception? Binding.Error`, `public event EventHandler? Binding.ErrorChanged`.
- Produces (for Task 2): Binding calls `element.SetBindingError(this, bool)` when `Target is UIElement element`. Task 1
  adds the calls; Task 2 adds the method. Until then, Task 1 uses a stub, see Step 3.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Data/Bindings/BindingTextConversionTests.cs`:

```csharp
using System.ComponentModel;
using System.Globalization;
using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Data.Bindings
{
    public class BindingTextConversionTests
    {
        private sealed class Model : INotifyPropertyChanged
        {
            private int positive = 1;

            public event PropertyChangedEventHandler? PropertyChanged;

            public int Count { get; set; }

            public int? Maybe { get; set; } = 3;

            public float Ratio { get; set; }

            public DayOfWeek Day { get; set; }

            public int Positive
            {
                get => positive;
                set
                {
                    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
                    positive = value;
                }
            }

            public void Raise(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private static Binding Bind(TextBox box, Model model, string property)
        {
            IPropertyReference text = PropertyRegistry.Default.GetPropertyStore(typeof(TextBox)).GetProperty(nameof(TextBox.Text));
            return new Binding(box, text, new PropertyPath(property, typeof(Model)))
            {
                Source = model,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                IsEnabled = true,
            };
        }

        [Fact]
        public void Text_ParsesIntoAnIntSource()
        {
            var model = new Model();
            var box = new TextBox();
            Bind(box, model, nameof(Model.Count));

            box.Text = "12";

            Assert.Equal(12, model.Count);
        }

        [Fact]
        public void InvalidText_KeepsTheSource_AndSetsTheErrorState()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "12";
            int changes = 0;
            binding.ErrorChanged += (_, _) => changes++;

            box.Text = "12a";

            Assert.Equal(12, model.Count);
            Assert.True(binding.HasError);
            Assert.NotNull(binding.Error);
            Assert.Equal(1, changes);
        }

        [Fact]
        public void CorrectingTheText_ClearsTheError()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "12a";

            box.Text = "13";

            Assert.Equal(13, model.Count);
            Assert.False(binding.HasError);
            Assert.Null(binding.Error);
        }

        [Fact]
        public void AfterAFailure_LaterValidInputStillReachesTheSource()
        {
            // Regression: a failed write left IsEnabled = false, so the binding ignored every later change.
            var model = new Model();
            var box = new TextBox();
            Bind(box, model, nameof(Model.Count));

            box.Text = "x";
            box.Text = "7";

            Assert.Equal(7, model.Count);
        }

        [Theory]
        [InlineData("-")]
        [InlineData("")]
        [InlineData("1e")]
        public void IntermediateStates_NeverThrow(string text)
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));

            Exception? thrown = Record.Exception(() => box.Text = text);

            Assert.Null(thrown);
            Assert.True(binding.HasError);
        }

        [Fact]
        public void Enum_ParsesByName()
        {
            var model = new Model();
            var box = new TextBox();
            Bind(box, model, nameof(Model.Day));

            box.Text = "Friday";

            Assert.Equal(DayOfWeek.Friday, model.Day);
        }

        [Fact]
        public void Float_IsParsedWithTheCurrentCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                var model = new Model();
                var box = new TextBox();
                Bind(box, model, nameof(Model.Ratio));

                box.Text = "1,5";

                Assert.Equal(1.5f, model.Ratio);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void EmptyText_SetsANullableSourceToNull()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Maybe));

            box.Text = string.Empty;

            Assert.Null(model.Maybe);
            Assert.False(binding.HasError);
        }

        [Fact]
        public void ThrowingSourceSetter_EntersTheErrorState_WithoutThrowing()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Positive));

            Exception? thrown = Record.Exception(() => box.Text = "0");

            Assert.Null(thrown);
            Assert.Equal(1, model.Positive);
            Assert.IsType<ArgumentOutOfRangeException>(binding.Error);
        }

        [Fact]
        public void SourceToTargetUpdate_ClearsTheError()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "oops";

            model.Count = 5;
            model.Raise(nameof(Model.Count));

            Assert.Equal("5", box.Text);
            Assert.False(binding.HasError);
        }

        [Fact]
        public void NumbersStillFormatAsText_SourceToTarget()
        {
            var model = new Model { Count = 42 };
            var box = new TextBox();
            Bind(box, model, nameof(Model.Count));

            Assert.Equal("42", box.Text);
        }
    }
}
```

(Check `TextBox.Text`'s getter and setter, and that `TextBox` constructs without a canvas; existing `TextBoxTests` show
how. If `PropertyPath`'s constructor differs from `(string, Type)`, mirror `FrameDrivenBindingTests`.)

- [ ] **Step 2: Run them to confirm they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~BindingTextConversionTests"`
Expected: build FAILS (`HasError`, `Error` and `ErrorChanged` don't exist).

- [ ] **Step 3: Implement**

In `Binding.cs`:
- Add `using System.Reflection;` (`System.ComponentModel` and `System.Globalization` are already imported).
- Add the fields `private Exception? error;` and `private bool hasError;` (keep alphabetical order with the existing fields).
- Add the event after the existing events, or first among members after the constructor if there are none:

```csharp
        /// <summary>
        /// Occurs when <see cref="HasError"/> changes, or when <see cref="Error"/> changes while <see cref="HasError"/>
        /// stays <see langword="true"/>.
        /// </summary>
        public event EventHandler? ErrorChanged;
```

- Add the properties (alphabetical among the public properties):

```csharp
        /// <summary>
        /// Gets the failure that put this binding into its error state, or <see langword="null"/> when there is none.
        /// </summary>
        /// <remarks>
        /// For example the type converter's exception for text that doesn't parse, or the exception the source property's own
        /// setter threw. Exceptions wrapped in <see cref="TargetInvocationException"/> are unwrapped.
        /// </remarks>
        public Exception? Error => error;

        /// <summary>
        /// Gets a value indicating whether the last write to the source failed.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Writing to the source never throws for a bad value: the source keeps its previous value and this flag is set
        /// instead (see <see cref="Error"/>). It clears on the next successful write in either direction.
        /// </para>
        /// <para>
        /// When the target is a <see cref="UIElement"/>, the element carries <see cref="UI.Styles.ControlState.Invalid"/> while
        /// any of its bindings has an error, so styles can show it.
        /// </para>
        /// </remarks>
        public bool HasError => hasError;
```

Replace `UpdateSource()`'s body:

```csharp
        /// <inheritdoc/>
        public void UpdateSource()
        {
            if (disposedValue || !IsEnabled)
                return;
            if (Source == null)
                throw new BindingException("Source is null.");

            try
            {
                SetValueToSource();
                ClearError();
            }
            catch (Exception ex)
            {
                if (FallbackValue != null)
                {
                    try
                    {
                        Path.SetValue(Source, FallbackValue);
                    }
                    catch (Exception)
                    {
                        // The error is recorded below; a failing fallback must not break the never-throw rule.
                    }
                }

                SetError(ex);
            }

            if (Mode == BindingMode.OneTime)
                DestroyBinding();
        }
```

In `UpdateTarget()`, call `ClearError();` right after `SetValueToTarget();` (before `IsEnabled = true;`).

Replace `SetValueToSource()`:

```csharp
        private void SetValueToSource()
        {
            IsEnabled = false;
            try
            {
                object? value = TargetProperty.GetRawValue(Target);
                Path.SetValue(Source!, ConvertValueToSource(value));
            }
            finally
            {
                IsEnabled = true;
            }
        }
```

Replace `ConvertValueToSource`:

```csharp
        private object? ConvertValueToSource(object? value)
        {
            if (ConverterParameters != null)
                return ConverterParameters.Converter.ConvertTo(null, ConverterParameters.Culture, value, Path.PropertyType);
            return value is string text ? ConvertTextToSourceType(text) : value;
        }

        /// <summary>
        /// Converts typed text to the source property's type with its <see cref="TypeConverter"/> and
        /// <see cref="CultureInfo.CurrentCulture"/>. Empty text becomes <see langword="null"/> for types that accept it.
        /// </summary>
        private object? ConvertTextToSourceType(string text)
        {
            Type type = Path.PropertyType;
            if (type == typeof(string) || type == typeof(object))
                return text;

            Type? underlying = Nullable.GetUnderlyingType(type);
            if (text.Length == 0)
            {
                if (underlying != null || !type.IsValueType)
                    return null;
                throw new FormatException($"An empty value can't be converted to '{type.Name}'.");
            }

            return TypeDescriptor.GetConverter(underlying ?? type).ConvertFromString(null, CultureInfo.CurrentCulture, text);
        }
```

Add the error helpers with the private methods:

```csharp
        private void SetError(Exception exception)
        {
            Exception cause = exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;
            bool changed = !hasError || !ReferenceEquals(error, cause);
            hasError = true;
            error = cause;
            ReportErrorToTarget(true);
            if (changed)
                ErrorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ClearError()
        {
            if (!hasError)
                return;
            hasError = false;
            error = null;
            ReportErrorToTarget(false);
            ErrorChanged?.Invoke(this, EventArgs.Empty);
        }

        private void ReportErrorToTarget(bool inError)
        {
            // Task 2 replaces this body with: if (Target is UIElement element) element.SetBindingError(this, inError);
        }
```

In `Dispose(bool disposing)`, call `ClearError();` before `Source = null!;`.

(Check that `Path.PropertyType` exists on `IPropertyPath`; the old `ConvertValueToSource` already used it.)

- [ ] **Step 4: Run the tests**

Run the Step 2 filter. Expected: all **13** PASS. Then the full suite. Expected: `Total:` **1071**, all passing.

If `Float_IsParsedWithTheCurrentCulture` fails with 15, the converter ignored the culture argument; check that the
`ConvertFromString(ITypeDescriptorContext?, CultureInfo?, string)` overload is the one being called.

- [ ] **Step 5: Build and commit**

Build with no new warnings (≤ 82).

```bash
git add sources/IcyUI/Data/Bindings/Binding.cs sources/IcyUI.Tests/Data/Bindings/BindingTextConversionTests.cs
git commit -m "Convert typed text to the source type and never throw on writes to the source" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `ControlState.Invalid` and the `TextBox` theme states

**Files:**
- Modify: `sources/IcyUI/UI/Styles/VisualState.cs` (`ControlState`), `sources/IcyUI/UI/UIElement.cs`,
  `sources/IcyUI/Data/Bindings/Binding.cs` (`ReportErrorToTarget`), `sources/IcyUI/Resources/Themes/DefaultTheme.xml`
- Test: `sources/IcyUI.Tests/Data/Bindings/BindingTextConversionTests.cs` (append)

**Interfaces:**
- Consumes: the Task 1 error state.
- Produces: `ControlState.Invalid = 1 << 9`; `internal void UIElement.SetBindingError(object binding, bool hasError)`.

- [ ] **Step 1: Write the failing tests**

Append to `BindingTextConversionTests` (add `using Icy.UI.Styles;`, `using System.Drawing;`,
`using Icy.Configuration;`, `using Icy.Assets;`, `using Icy.Tests.Input;`, `using Icy.Tests.Rendering;`,
`using Icy.UI;`, `using Icy.Rendering.Brushes;`):

```csharp
        [Fact]
        public void ErroringBinding_MarksItsTargetInvalid_UntilCorrected()
        {
            var model = new Model();
            var box = new TextBox();
            Bind(box, model, nameof(Model.Count));

            box.Text = "12a";
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));

            box.Text = "12";
            Assert.False(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void TwoBindings_OneInError_KeepsTheElementInvalid()
        {
            var model = new Model();
            var box = new TextBox();
            Binding count = Bind(box, model, nameof(Model.Count));
            Binding day = Bind(box, model, nameof(Model.Day));

            box.Text = "Monday"; // not an int, but a valid DayOfWeek

            Assert.True(count.HasError);
            Assert.False(day.HasError);
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void DisposingAnErroringBinding_ClearsInvalid()
        {
            var model = new Model();
            var box = new TextBox();
            Binding binding = Bind(box, model, nameof(Model.Count));
            box.Text = "nope";

            binding.Dispose();

            Assert.False(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void DefaultTheme_ShowsFocusedInvalidOverFocusedAndInvalid()
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration()).UseDefaultTheme();
            var canvas = new Canvas(configuration);
            var box = new TextBox();
            canvas.Add(box);
            canvas.Render();

            box.ControlState |= ControlState.Focused | ControlState.Invalid;

            var brush = Assert.IsType<SolidColorBrush>(box.BorderBrush);
            Assert.Equal(Color.FromArgb(255, 255, 106, 106), brush.Color);
        }
```

(Check `UseDefaultTheme`'s signature in `BuildingExtensions` (it extends `IcyConfiguration` and returns it), and
`SolidColorBrush`'s color property name. Mirror `ThemedComboBox_InternalTextBox_DoesNotGetItsOwnBorderOrPadding` in
`ComboBoxTests`.)

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~BindingTextConversionTests"`
Expected: build FAILS (`ControlState.Invalid` doesn't exist).

- [ ] **Step 2: Add the flag**

In `VisualState.cs`, after `Highlighted`:

```csharp
        /// <summary>
        /// One of the element's bindings failed to write a value to its source (see
        /// <see cref="Data.Bindings.Binding.HasError"/>), e.g. text that isn't a number in a numeric field.
        /// </summary>
        Invalid = 1 << 9,
```

- [ ] **Step 3: Aggregate per element**

In `UIElement.cs`:
- Add the field `private HashSet<object>? erroringBindings;` in alphabetical order with the other fields.
- Add this method right after `SetFocused` (another internal method):

```csharp
        /// <summary>
        /// Records whether one of this element's bindings is in its error state, keeping
        /// <see cref="Styles.ControlState.Invalid"/> set while any of them is.
        /// </summary>
        /// <param name="binding">The reporting binding.</param>
        /// <param name="hasError">Whether that binding is in its error state.</param>
        internal void SetBindingError(object binding, bool hasError)
        {
            if (hasError)
                (erroringBindings ??= []).Add(binding);
            else
                erroringBindings?.Remove(binding);

            bool invalid = erroringBindings is { Count: > 0 };
            ControlState = invalid ? ControlState | ControlState.Invalid : ControlState & ~ControlState.Invalid;
        }
```

In `Binding.cs`, replace `ReportErrorToTarget`'s body:

```csharp
        private void ReportErrorToTarget(bool inError)
        {
            if (Target is UIElement element)
                element.SetBindingError(this, inError);
        }
```

- [ ] **Step 4: Theme states**

In `DefaultTheme.xml`, replace the `TextBox` style's `CommonStates` group content:

```xml
      <VisualStateGroup Name="CommonStates">
        <VisualState Name="Focused" State="Focused" BorderBrush="#FF3C78D8"/>
        <VisualState Name="Invalid" State="Invalid" BorderBrush="#FFD84C4C"/>
        <VisualState Name="FocusedInvalid" State="Focused, Invalid" BorderBrush="#FFFF6A6A"/>
      </VisualStateGroup>
```

(If the markup loader doesn't parse `"Focused, Invalid"` for a `[Flags]` enum, check how `State="..."` is converted. An
enum `TypeConverter` accepts comma-separated names. If IcyUI's converter doesn't, record a ruling and use the form it
accepts.)

- [ ] **Step 5: Run the tests**

Run the Step 1 filter. Expected: all **17** PASS. Then the full suite. Expected: `Total:` **1075**, all passing. That
includes the theme tests for the other controls, which shows the new `TextBox` states didn't disturb them.

- [ ] **Step 6: Build and commit**

Build with no new warnings (≤ 82).

```bash
git add sources/IcyUI/UI/Styles/VisualState.cs sources/IcyUI/UI/UIElement.cs sources/IcyUI/Data/Bindings/Binding.cs sources/IcyUI/Resources/Themes/DefaultTheme.xml sources/IcyUI.Tests/Data/Bindings/BindingTextConversionTests.cs
git commit -m "Mark elements with failing bindings Invalid and style it for TextBox" -m "Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Verification

- [ ] **Step 1:** `dotnet build "sources/IcyUI.sln" --no-incremental`. Expected: 0 errors, ≤ 82 warnings,
  `IcyUI.Stride` 0.
- [ ] **Step 2:** `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. Expected: `Total:` **1075**, all pass.
- [ ] **Step 3:** Whole-branch review against the spec and the Review Focus list.
- [ ] **Step 4: Manual smoke test (Ivan runs it; never claim it):** a `TextBox` two-way bound to a numeric property, for
  example in a quick sample or the PropertyGrid's numeric editors if they use bindings. Type letters: the border turns
  red, nothing crashes, and the model keeps its last valid value. Fix the text: the border returns to normal and the
  model updates. Focused and invalid shows the brighter red.
