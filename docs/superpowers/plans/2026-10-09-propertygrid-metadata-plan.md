# PropertyGrid Metadata Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the PropertyGrid know every static property rule before writing, so invalid input is marked Invalid with a readable reason and never reaches a throwing setter. Also honour `[DefaultValue]`, `[EditorBrowsable(Never)]` and `[Description]`.

**Architecture:**
- `PropertyGridEntry` reads the metadata and exposes `Validate`.
- `PropertyGrid` validates every typed numeric value, offers Reset to the default through its default adapter, and reports the focused row's help text through `ActiveEntry`/`ActiveMessage`.
- Core's guarded properties get matching `[Range]` attributes.
- Hosts (`PropertiesPanel`, the PropertyGrid demo) display `ActiveMessage`.

**Tech Stack:** C# / .NET 10, `System.ComponentModel.DataAnnotations.RangeAttribute`, xUnit, StyleCop.

**Spec:** `docs/superpowers/specs/2026-10-09-propertygrid-metadata-design.md`

## Global Constraints

- **Build and test:** everything targets `net10.0`. Build with `dotnet build "sources/IcyUI.sln"` and test with `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. Check the **Total** line: "Passed!" also prints when the test host crashes.
- **Documentation:** every new public API gets complete XML docs (`<see cref>`, `<see langword>`, `<list>`, `<para>`).
- **Code style:** block-scoped `namespace X { }`, the file copyright header, StyleCop clean.
- **Warning baselines:** IcyUI **84**, IcyUI.Design **1**. Count with `dotnet build <csproj> --no-incremental 2>&1 | grep -E ": warning (SA|CS)" | sort -u | wc -l`; for Design, add `| grep "IcyUI.Design.csproj\]"` before `sort`.
- **Rules attribute:** `System.ComponentModel.DataAnnotations.RangeAttribute` only, with `MinimumIsExclusive`/`MaximumIsExclusive`; an infinite bound means open-ended. No new attribute types.
- **Valid by default:** a value equal to the property's `[DefaultValue]` is always valid (`float.NaN.Equals(float.NaN)` is `true`).
- **Reason text:** English and invariant-culture: "Must be greater than 0.", "Must be at least 0.", "Must be less than 10.", "Must be at most 10.", "Must be between 0 and 1.".
- **Engine boundaries:** core (`sources/IcyUI`) and `IcyUI.Design` only. MonoGame, Stride and FNA are untouched.
- **Setter guards stay:** they remain as the safety net for code.
- **Line endings:** C# files in these folders are CRLF in git (autocrlf). Keep whatever a file already uses when editing.

## Spec deltas decided while planning (apply in Task 1's commit)

1. **Infinite values are never inside a range**, even with an infinite inclusive bound: "open-ended" means "no limit on finite values".
   - Why: typing `1e40` parses to `∞` for a `float`, and `[0, +∞]` would otherwise accept it. That was the `DefaultEstimatedItemHeight` report.
   - Typed text that parses to `±∞` is rejected for every numeric row, ranged or not.
2. **Vector rows aren't validated:** `PropertyGridEntry.Range` is read only for numeric types, so a vector entry never has one. Drop the "Vector rows" bullet from the spec.
3. **Invalid text reverts on focus loss:** the box shows the current value again and the Invalid state clears. This replaces today's "settle on the clamped value".
4. **`SelectedIndex` is annotated `[Range(-1d, double.PositiveInfinity)]`,** not `[Range(-1, int.MaxValue)]`. A finite `int.MaxValue` bound would make it a bounded range and build a slider.

## Review Focus

1. **Typing a value one keystroke at a time into a bounded range whose minimum has several digits.** `[Range(1000, 5000)]` is the case: each prefix ("2", "25") is invalid but must stay in the box, untouched, and nothing may be written. Owned by Task 2.
2. **Clearing a NaN-default layout limit.** Deleting `MaxWidth`'s text must write NaN ("unset"), not mark the box invalid. Owned by Task 2.
3. **Reset's button disappearing after it acts.** The default adapter has no change notification, so the grid must refresh the row itself. Owned by Task 3.
4. **Focus moving between two rows.** `ActiveMessage` must end on the row that gained focus, whatever order the two `FocusChanged` events fire in. Owned by Task 4.
5. **First-chance exceptions from other test threads.** The no-exception test must count only its own thread's exceptions, or the parallel suite makes it flaky. Owned by Task 6.

---

## File structure

| File | Responsibility |
|---|---|
| `sources/IcyUI/Data/ValueRange.cs` (new) | Range value: containment and reason text. |
| `sources/IcyUI/Data/PropertyGridEntry.cs` (modify) | Reads `[Range]`, `[DefaultValue]`, `[Description]`, `[EditorBrowsable]`; `Validate`. |
| `sources/IcyUI/Data/PropertyGridValueAdapter.cs` (modify) | Default Reset to `[DefaultValue]`. |
| `sources/IcyUI/UI/Controls/PropertyGrid.cs` (modify) | Validated numeric editors; Reset refresh; `ActiveEntry`/`ActiveMessage`. |
| `sources/IcyUI/UI/UIElement.cs`, `UI/Controls/ItemsControl.cs`, `Selector.cs`, `WrapGrid.cs`, `TreeView.cs`, `SelectingItemsControl.cs` (modify) | `[Range]` annotations. |
| `sources/IcyUI.Design/Editor/Panels/PropertiesPanel.cs` (modify) | Status line shows `ActiveMessage`. |
| `sources/Shared Samples/PropertyGridDemo.cs` (modify) | Help line; two `[Description]`s. |
| Tests | `Data/ValueRangeTests.cs` (new), `Data/PropertyGridEntryTests.cs`, `Controls/PropertyGridTests.cs`, `Controls/PropertyGridAdapterTests.cs`, `Controls/PropertyGridHelpTests.cs` (new), `UI/RangeAnnotationTests.cs` (new), `Design/Editor/PropertiesPanelTests.cs`. |

---

### Task 1: `ValueRange` and `PropertyGridEntry` metadata

**Files:**
- Create: `sources/IcyUI/Data/ValueRange.cs`
- Modify: `sources/IcyUI/Data/PropertyGridEntry.cs`, and `sources/IcyUI/UI/Controls/PropertyGrid.cs` (only to compile against the new `Range` type)
- Modify: `docs/superpowers/specs/2026-10-09-propertygrid-metadata-design.md` (the four spec deltas)
- Test: `sources/IcyUI.Tests/Data/ValueRangeTests.cs` (new), `sources/IcyUI.Tests/Data/PropertyGridEntryTests.cs`

**Interfaces:**
- Produces:
  - `public readonly record struct ValueRange(double Min, double Max, bool MinIsExclusive, bool MaxIsExclusive)` in `Icy.Data`, with:
    - `bool IsBounded`;
    - `bool Contains(double value)`;
    - `string Describe()`.
  - On `PropertyGridEntry`:
    - `ValueRange? Range` (replaces the tuple);
    - `bool HasDefaultValue`;
    - `object? DefaultValue`;
    - `string? Description`;
    - `bool Validate(object? value, [NotNullWhen(false)] out string? reason)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Data/ValueRangeTests.cs
using System.Globalization;
using Icy.Data;
using Xunit;

namespace Icy.Tests.Data
{
    public class ValueRangeTests
    {
        [Theory]
        [InlineData(0d, 1d, false, false, 0d, true)]
        [InlineData(0d, 1d, false, false, 1d, true)]
        [InlineData(0d, 1d, true, false, 0d, false)]
        [InlineData(0d, 1d, false, true, 1d, false)]
        [InlineData(0d, double.PositiveInfinity, true, false, 1e30, true)]
        [InlineData(0d, double.PositiveInfinity, false, false, double.PositiveInfinity, false)]
        [InlineData(0d, 1d, false, false, double.NaN, false)]
        [InlineData(-1d, double.PositiveInfinity, false, false, -1d, true)]
        public void Contains_HonoursExclusiveEnds_AndRejectsNonFiniteValues(double min, double max, bool minExclusive, bool maxExclusive, double value, bool expected) =>
            Assert.Equal(expected, new ValueRange(min, max, minExclusive, maxExclusive).Contains(value));

        [Theory]
        [InlineData(0d, double.PositiveInfinity, true, false, "Must be greater than 0.")]
        [InlineData(0d, double.PositiveInfinity, false, false, "Must be at least 0.")]
        [InlineData(double.NegativeInfinity, 10d, false, true, "Must be less than 10.")]
        [InlineData(double.NegativeInfinity, 10d, false, false, "Must be at most 10.")]
        [InlineData(0d, 1d, false, false, "Must be between 0 and 1.")]
        [InlineData(0d, 1d, true, false, "Must be greater than 0 and at most 1.")]
        [InlineData(0d, 1d, false, true, "Must be at least 0 and less than 1.")]
        [InlineData(0d, 1d, true, true, "Must be greater than 0 and less than 1.")]
        [InlineData(double.NegativeInfinity, double.PositiveInfinity, false, false, "Must be a finite number.")]
        public void Describe_NamesTheRule(double min, double max, bool minExclusive, bool maxExclusive, string expected) =>
            Assert.Equal(expected, new ValueRange(min, max, minExclusive, maxExclusive).Describe());

        [Fact]
        public void Describe_UsesTheInvariantCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            try
            {
                Assert.Equal("Must be between 0.5 and 1.5.", new ValueRange(0.5, 1.5, false, false).Describe());
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void IsBounded_NeedsBothEndsFinite()
        {
            Assert.True(new ValueRange(0, 1, false, false).IsBounded);
            Assert.False(new ValueRange(0, double.PositiveInfinity, false, false).IsBounded);
        }
    }
}
```

In `sources/IcyUI.Tests/Data/PropertyGridEntryTests.cs`:

1. Replace `Assert.Equal((0d, 100d), armor.Range);` with `Assert.Equal(new ValueRange(0, 100, false, false), armor.Range);`.
2. Add these members to `PlainPoco`:

```csharp
            [Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]
            public float Positive { get; set; } = 1;

            [DefaultValue(float.NaN)]
            [Range(0d, double.PositiveInfinity)]
            public float Limit { get; set; } = float.NaN;

            [DefaultValue(40)]
            public float ConvertedDefault { get; set; } = 40;

            [Description("How far the hero can see.")]
            public int Sight { get; set; }

            [EditorBrowsable(EditorBrowsableState.Never)]
            public int NeverShown { get; set; }

            [EditorBrowsable(EditorBrowsableState.Advanced)]
            public int AdvancedShown { get; set; }

            [Range(1, 1, MinimumIsExclusive = true)]
            public int EmptyRange { get; set; } = 1;
```

3. Add these tests:

```csharp
        [Fact]
        public void EnumerateFor_ReadsExclusiveAndOpenEndedRanges()
        {
            var positive = Assert.Single(PropertyGridEntry.EnumerateFor(new PlainPoco()), e => e.Name == nameof(PlainPoco.Positive));

            Assert.Equal(new ValueRange(0, double.PositiveInfinity, true, false), positive.Range);
        }

        [Fact]
        public void EnumerateFor_AnEmptyRange_IsTreatedAsNoRange()
        {
            var empty = Assert.Single(PropertyGridEntry.EnumerateFor(new PlainPoco()), e => e.Name == nameof(PlainPoco.EmptyRange));

            Assert.Null(empty.Range);
        }

        [Fact]
        public void EnumerateFor_ReadsDefaultValue_ConvertedToThePropertyType()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());
            var converted = Assert.Single(entries, e => e.Name == nameof(PlainPoco.ConvertedDefault));
            var health = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Health));

            Assert.True(converted.HasDefaultValue);
            Assert.Equal(40f, converted.DefaultValue);
            Assert.False(health.HasDefaultValue);
        }

        [Fact]
        public void EnumerateFor_ReadsDescription()
        {
            var sight = Assert.Single(PropertyGridEntry.EnumerateFor(new PlainPoco()), e => e.Name == nameof(PlainPoco.Sight));

            Assert.Equal("How far the hero can see.", sight.Description);
        }

        [Fact]
        public void EnumerateFor_HidesEditorBrowsableNever_ButShowsAdvanced()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());

            Assert.DoesNotContain(entries, e => e.Name == nameof(PlainPoco.NeverShown));
            Assert.Contains(entries, e => e.Name == nameof(PlainPoco.AdvancedShown));
        }

        [Fact]
        public void Validate_AcceptsTheDefault_AndRejectsOutOfRange()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());
            var limit = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Limit));
            var positive = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Positive));

            Assert.True(limit.Validate(float.NaN, out _));
            Assert.True(limit.Validate(0f, out _));
            Assert.False(limit.Validate(-1f, out string? reason));
            Assert.Equal("Must be at least 0.", reason);
            Assert.False(positive.Validate(0f, out reason));
            Assert.Equal("Must be greater than 0.", reason);
            Assert.False(positive.Validate(float.PositiveInfinity, out _));
        }
```

Add `using System.ComponentModel;` to the test file if it isn't there.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~ValueRangeTests|FullyQualifiedName~PropertyGridEntryTests"`
Expected: a build failure, because `ValueRange` doesn't exist.

- [ ] **Step 3: Create `ValueRange`**

```csharp
// sources/IcyUI/Data/ValueRange.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Globalization;

namespace Icy.Data
{
    /// <summary>
    /// The values a numeric property accepts, as declared by
    /// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/>.
    /// </summary>
    /// <param name="Min">The lower bound; <see cref="double.NegativeInfinity"/> for none.</param>
    /// <param name="Max">The upper bound; <see cref="double.PositiveInfinity"/> for none.</param>
    /// <param name="MinIsExclusive">Whether <paramref name="Min"/> itself is outside the range.</param>
    /// <param name="MaxIsExclusive">Whether <paramref name="Max"/> itself is outside the range.</param>
    /// <remarks>
    /// An infinite bound means the range is open on that side. Infinite and NaN values are never inside a range: "open"
    /// means no limit on finite values.
    /// </remarks>
    public readonly record struct ValueRange(double Min, double Max, bool MinIsExclusive, bool MaxIsExclusive)
    {
        /// <summary>
        /// Gets a value indicating whether both bounds are finite, so the range can drive a slider.
        /// </summary>
        public bool IsBounded => double.IsFinite(Min) && double.IsFinite(Max);

        /// <summary>
        /// Determines whether <paramref name="value"/> is inside the range.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns><see langword="true"/> when the value is finite and satisfies both bounds.</returns>
        public bool Contains(double value)
        {
            if (!double.IsFinite(value))
                return false;

            bool aboveMin = MinIsExclusive ? value > Min : value >= Min;
            bool belowMax = MaxIsExclusive ? value < Max : value <= Max;
            return aboveMin && belowMax;
        }

        /// <summary>
        /// Describes the range as a user-facing rule, such as "Must be greater than 0.".
        /// </summary>
        /// <returns>An English sentence with numbers in the invariant culture.</returns>
        public string Describe()
        {
            bool hasMin = double.IsFinite(Min);
            bool hasMax = double.IsFinite(Max);
            string min = Min.ToString(CultureInfo.InvariantCulture);
            string max = Max.ToString(CultureInfo.InvariantCulture);
            string lower = MinIsExclusive ? $"greater than {min}" : $"at least {min}";
            string upper = MaxIsExclusive ? $"less than {max}" : $"at most {max}";

            return (hasMin, hasMax) switch
            {
                (true, true) when !MinIsExclusive && !MaxIsExclusive => $"Must be between {min} and {max}.",
                (true, true) => $"Must be {lower} and {upper}.",
                (true, false) => $"Must be {lower}.",
                (false, true) => $"Must be {upper}.",
                _ => "Must be a finite number.",
            };
        }
    }
}
```

- [ ] **Step 4: Extend `PropertyGridEntry`**

In `sources/IcyUI/Data/PropertyGridEntry.cs`:

1. **Constructor:** after `Range = TryReadRange(property);`, add:

```csharp
            Description = property.GetCustomAttribute<DescriptionAttribute>()?.Description;
            (HasDefaultValue, DefaultValue) = TryReadDefault(property);
```

2. **Replace the `Range` property** with this (keep its position):

```csharp
        /// <summary>
        /// Gets the values this numeric property accepts, from
        /// <see cref="System.ComponentModel.DataAnnotations.RangeAttribute"/>, or <see langword="null"/> when it declares
        /// none, isn't numeric, or declares a range that contains no values.
        /// </summary>
        public ValueRange? Range { get; }

        /// <summary>
        /// Gets a value indicating whether the property declares a default through <see cref="DefaultValueAttribute"/>.
        /// </summary>
        public bool HasDefaultValue { get; }

        /// <summary>
        /// Gets the declared default, converted to <see cref="PropertyType"/> when the attribute stored another numeric
        /// type (<c>[DefaultValue(40)]</c> on a <see cref="float"/>). Meaningful only when <see cref="HasDefaultValue"/>
        /// is <see langword="true"/>.
        /// </summary>
        public object? DefaultValue { get; }

        /// <summary>
        /// Gets the property's description from <see cref="DescriptionAttribute"/>, or <see langword="null"/>.
        /// </summary>
        public string? Description { get; }
```

3. **Add `Validate`** after `TrySetValue`:

```csharp
        /// <summary>
        /// Checks a value against the property's declared rules before it's written.
        /// </summary>
        /// <param name="value">The candidate value, already of <see cref="PropertyType"/>.</param>
        /// <param name="reason">Why the value is rejected, when this method returns <see langword="false"/>.</param>
        /// <returns>
        /// <list type="bullet">
        /// <item><description><see langword="true"/> for a value equal to <see cref="DefaultValue"/>, so a <see cref="float.NaN"/> default still means "unset".</description></item>
        /// <item><description><see langword="true"/> when there's no <see cref="Range"/>.</description></item>
        /// <item><description>Otherwise whether the numeric value is inside <see cref="Range"/>.</description></item>
        /// </list>
        /// </returns>
        public bool Validate(object? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? reason)
        {
            reason = null;
            if (HasDefaultValue && Equals(value, DefaultValue))
                return true;
            if (Range is not { } range)
                return true;

            double number;
            try
            {
                number = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                reason = range.Describe();
                return false;
            }

            if (range.Contains(number))
                return true;

            reason = range.Describe();
            return false;
        }
```

4. **`TryCreate`:** after the `[Browsable(false)]` check, add:

```csharp
            if (info.GetCustomAttribute<EditorBrowsableAttribute>()?.State == EditorBrowsableState.Never)
                return false;
```

   Update its `<returns>` doc to mention `[EditorBrowsable(EditorBrowsableState.Never)]`.

5. **Replace `TryReadRange`** with this, and add `TryReadDefault` after it:

```csharp
        private static ValueRange? TryReadRange(PropertyInfo property)
        {
            var range = property.GetCustomAttribute<RangeAttribute>();
            if (range == null || !IsNumericType(property.PropertyType))
                return null;

            try
            {
                var result = new ValueRange(
                    Convert.ToDouble(range.Minimum, System.Globalization.CultureInfo.InvariantCulture),
                    Convert.ToDouble(range.Maximum, System.Globalization.CultureInfo.InvariantCulture),
                    range.MinimumIsExclusive,
                    range.MaximumIsExclusive);

                // A range that contains no values gives a consumer bounds it can't satisfy (Slider.Minimum/Maximum, for
                // one, throw on min > max) - treat it as no range.
                bool empty = result.Min > result.Max || (result.Min == result.Max && (result.MinIsExclusive || result.MaxIsExclusive));
                return empty ? null : result;
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                return null;
            }
        }

        private static (bool HasDefault, object? Value) TryReadDefault(PropertyInfo property)
        {
            if (property.GetCustomAttribute<DefaultValueAttribute>() is not { } attribute)
                return (false, null);

            object? value = attribute.Value;
            Type type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            if (value == null)
                return (!type.IsValueType || type != property.PropertyType, null);
            if (type.IsInstanceOfType(value))
                return (true, value);
            if (IsNumericType(type) && value is IConvertible)
            {
                try
                {
                    return (true, Convert.ChangeType(value, type, System.Globalization.CultureInfo.InvariantCulture));
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
                {
                    return (false, null);
                }
            }

            return (false, null);
        }
```

   Keep the existing private `IsNumericType`. If the file has no `using System.ComponentModel;`, add it.

6. **`PropertyGrid.cs`:** it reads `range.Min`/`range.Max` as doubles. `BuildRangedNumericEditor`'s parameter type changes from `(double Min, double Max) range` to `ValueRange range`; the body compiles unchanged. In `BuildNumericEditor`, change `if (entry.Range is { } range)` to `if (entry.Range is { IsBounded: true } range)`. That line is the only behavioural change in this task: open-ended ranges get the plain text box.

- [ ] **Step 5: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~ValueRangeTests|FullyQualifiedName~PropertyGrid"`
Expected: all pass.

- [ ] **Step 6: Apply the spec deltas**

In `docs/superpowers/specs/2026-10-09-propertygrid-metadata-design.md`:

- Under `ValueRange`, change "`Contains(double value)`: honours the exclusive flags; `false` for NaN." to "`Contains(double value)`: honours the exclusive flags; `false` for NaN and for infinite values, even with an infinite inclusive bound."
- Delete the "**Vector rows**" bullet.
- In "Numeric rows", add: "Invalid text reverts to the current value when the box loses focus. Text that parses to an infinite value is invalid in every numeric row."
- In the annotation table, change `SelectedIndex`'s rule to `[Range(-1d, double.PositiveInfinity)]`.
- Change "## Out of scope"'s last bullet to "`[Range]` on vectors and matrices (the grid reads ranges for numeric types only)."

- [ ] **Step 7: Warnings, full suite, commit**

Run the IcyUI warning count. Expected: `84`.
Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj`. Expected: Failed 0.

```bash
git add sources/IcyUI/Data/ValueRange.cs sources/IcyUI/Data/PropertyGridEntry.cs sources/IcyUI/UI/Controls/PropertyGrid.cs sources/IcyUI.Tests/Data/ValueRangeTests.cs sources/IcyUI.Tests/Data/PropertyGridEntryTests.cs docs/superpowers/specs/2026-10-09-propertygrid-metadata-design.md
git commit -m "Read ranges, defaults, descriptions and EditorBrowsable into PropertyGridEntry"
```

---

### Task 2: Validated numeric editors

**Files:**
- Modify: `sources/IcyUI/UI/Controls/PropertyGrid.cs`
- Test: `sources/IcyUI.Tests/Controls/PropertyGridTests.cs`

**Interfaces:**
- Consumes: `PropertyGridEntry.Range`, `HasDefaultValue`, `DefaultValue`, `Validate` (Task 1).
- Produces (private, used by Task 4):
  - `PropertyRow.InvalidReason` (`string?`);
  - `private void SetInvalid(TextBox box, string? reason)`.

- [ ] **Step 1: Write the failing tests and update the two clamp tests**

In `PropertyGridTests.cs`, add these targets next to `RangedTarget`:

```csharp
        private sealed class LimitTarget
        {
            [DefaultValue(float.NaN)]
            [Range(0d, double.PositiveInfinity)]
            public float MaxSize { get; set; } = 10;

            [Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]
            public float Estimate { get; set; } = 40;

            public float Free { get; set; } = 1;
        }
```

**Replace** `Target_OutOfRangeTextBoxEdit_ClampsTargetSliderAndTextBoxToRangeConsistently` with:

```csharp
        [Fact]
        public void Target_OutOfRangeTextBoxEdit_IsMarkedInvalid_WritesNothing_AndRevertsOnFocusLoss()
        {
            var target = new RangedTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var panel = Assert.IsType<StackPanel>(FindEditor(grid, nameof(RangedTarget.Volume)));
            var slider = Assert.IsType<Slider>(panel.Children.ElementAtOrDefault(0));
            var textBox = Assert.IsType<TextBox>(panel.Children.ElementAtOrDefault(1));

            textBox.SetFocused(true);
            textBox.Text = "150";

            Assert.Equal(50, target.Volume);
            Assert.Equal(50f, slider.Value);
            Assert.Equal("150", textBox.Text);
            Assert.True(textBox.ControlState.HasFlag(ControlState.Invalid));

            textBox.SetFocused(false);

            Assert.Equal("50", textBox.Text);
            Assert.False(textBox.ControlState.HasFlag(ControlState.Invalid));
        }
```

**Replace the body** of `Target_RangeMinimumWiderThanOneDigit_DoesNotRewriteTheTextBoxWhileItIsFocused` with:

```csharp
            // Typing "2500" into a [Range(1000, 5000)] property passes through "2" and "25", both below the minimum: they
            // must stay in the box untouched (marked Invalid), write nothing, and the valid "2500" must land exactly.
            var target = new HighRangedTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var panel = Assert.IsType<StackPanel>(FindEditor(grid, nameof(HighRangedTarget.Bitrate)));
            var slider = Assert.IsType<Slider>(panel.Children.ElementAtOrDefault(0));
            var textBox = Assert.IsType<TextBox>(panel.Children.ElementAtOrDefault(1));

            textBox.SetFocused(true);
            textBox.Text = "2";
            Assert.Equal("2", textBox.Text);
            Assert.Equal(2000, target.Bitrate);
            Assert.True(textBox.ControlState.HasFlag(ControlState.Invalid));

            textBox.Text = "25";
            Assert.Equal("25", textBox.Text);
            Assert.Equal(2000, target.Bitrate);

            textBox.Text = "2500";
            Assert.Equal(2500, target.Bitrate);
            Assert.Equal(2500f, slider.Value);
            Assert.False(textBox.ControlState.HasFlag(ControlState.Invalid));

            // Focus loss with invalid text shows the current value again.
            textBox.Text = "10";
            textBox.SetFocused(false);

            Assert.Equal("2500", textBox.Text);
            Assert.Equal(2500, target.Bitrate);
```

**Add** these tests:

```csharp
        [Fact]
        public void AnOpenEndedRange_BuildsAPlainTextBox()
        {
            var grid = new PropertyGrid { Target = new LimitTarget() };
            grid.Measure();

            Assert.IsType<TextBox>(FindEditor(grid, nameof(LimitTarget.Estimate)));
        }

        [Fact]
        public void InvalidTyping_IsMarked_AndNotWritten_ThenValidTypingClearsIt()
        {
            var target = new LimitTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();
            var box = Assert.IsType<TextBox>(FindEditor(grid, nameof(LimitTarget.Estimate)));

            box.Text = "0";
            Assert.Equal(40f, target.Estimate);
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));

            box.Text = "abc";
            Assert.Equal(40f, target.Estimate);
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));

            box.Text = "12";
            Assert.Equal(12f, target.Estimate);
            Assert.False(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void TextParsingToInfinity_IsInvalid_EvenWithoutARange()
        {
            var target = new LimitTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();
            var box = Assert.IsType<TextBox>(FindEditor(grid, nameof(LimitTarget.Free)));

            box.Text = "1e40";

            Assert.Equal(1f, target.Free);
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void ClearingANaNDefaultLimit_WritesUnset()
        {
            var target = new LimitTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();
            var box = Assert.IsType<TextBox>(FindEditor(grid, nameof(LimitTarget.MaxSize)));

            box.Text = string.Empty;

            Assert.True(float.IsNaN(target.MaxSize));
            Assert.False(box.ControlState.HasFlag(ControlState.Invalid));
        }

        [Fact]
        public void ClearingANumberWithoutANaNDefault_IsInvalid()
        {
            var target = new LimitTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();
            var box = Assert.IsType<TextBox>(FindEditor(grid, nameof(LimitTarget.Estimate)));

            box.Text = string.Empty;

            Assert.Equal(40f, target.Estimate);
            Assert.True(box.ControlState.HasFlag(ControlState.Invalid));
        }
```

Add `using Icy.UI.Styles;` to the test file if it isn't there.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertyGridTests"`
Expected: the new and replaced tests FAIL. Today the box isn't marked Invalid, out-of-range input is clamped, and `1e40` is written.

- [ ] **Step 3: Add the row reason and the shared helpers**

In `PropertyGrid.cs`:

1. **On `PropertyRow`**, add:

```csharp
            /// <summary>
            /// Gets or sets why the text in the row's editor is rejected, or <see langword="null"/> while it's valid.
            /// </summary>
            public string? InvalidReason { get; set; }
```

2. **In `FillRow`**, set `row.InvalidReason = null;` before building the editor, since a rebuilt editor starts valid.

3. **Add these private methods** next to `TryConvertNumeric`:

```csharp
        /// <summary>
        /// Parses and validates text typed into a numeric row.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="text">The typed text.</param>
        /// <param name="value">The value to write, when this method returns <see langword="true"/>.</param>
        /// <param name="reason">Why the text is rejected, when this method returns <see langword="false"/>.</param>
        /// <returns>
        /// <see langword="true"/> for a finite number that passes <see cref="PropertyGridEntry.Validate"/>, or for empty
        /// text when the property's default is <see cref="float.NaN"/>/<see cref="double.NaN"/> ("unset").
        /// </returns>
        private static bool TryParseInput(PropertyGridEntry entry, string text, out object? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                if (entry.HasDefaultValue && entry.DefaultValue is float.NaN or double.NaN)
                {
                    value = entry.DefaultValue;
                    return true;
                }

                value = null;
                reason = "Enter a number.";
                return false;
            }

            if (!TryConvertNumeric(text, entry.PropertyType, out value))
            {
                reason = "Enter a number.";
                return false;
            }

            if (value is float f && float.IsInfinity(f) || value is double d && double.IsInfinity(d))
            {
                reason = "Must be a finite number.";
                return false;
            }

            return entry.Validate(value, out reason);
        }

        /// <summary>
        /// Marks <paramref name="box"/> <see cref="ControlState.Invalid"/> with <paramref name="reason"/>, or clears the mark
        /// when <paramref name="reason"/> is <see langword="null"/>, and records the reason on the box's row.
        /// </summary>
        /// <param name="box">A row editor's text box.</param>
        /// <param name="reason">Why its text is rejected, or <see langword="null"/>.</param>
        private void SetInvalid(TextBox box, string? reason)
        {
            box.ControlState = reason != null ? box.ControlState | ControlState.Invalid : box.ControlState & ~ControlState.Invalid;
            for (UIElement? element = box; element != null; element = element.Parent)
            {
                if (element is PropertyRow row)
                {
                    row.InvalidReason = reason;
                    break;
                }
            }
        }

        private static string FormatNumber(object? value) =>
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
```

   `float.NaN or double.NaN` as constant patterns matches NaN in C# (`is float.NaN` compiles to `float.IsNaN`).

- [ ] **Step 4: Rewrite `BuildNumericEditor`'s plain text box**

Replace the plain-box part of `BuildNumericEditor` (after the `IsBounded` check from Task 1) with:

```csharp
            var textBox = new TextBox { Text = FormatNumber(value), IsEnabled = !entry.IsReadOnly };
            bool reverting = false;
            textBox.TextChanged += (_, _) =>
            {
                if (reverting)
                    return;
                if (TryParseInput(entry, textBox.Text, out object? parsed, out string? reason))
                {
                    SetInvalid(textBox, null);
                    Write(entry, target, parsed);
                }
                else
                {
                    SetInvalid(textBox, reason);
                }
            };
            textBox.FocusChanged += (_, _) =>
            {
                // Leaving the box with rejected text shows the current value again (nothing was written).
                if (textBox.IsFocused || !textBox.ControlState.HasFlag(ControlState.Invalid))
                    return;
                reverting = true;
                try
                {
                    textBox.Text = FormatNumber(SafeGetValue(entry, target));
                }
                finally
                {
                    reverting = false;
                }

                SetInvalid(textBox, null);
            };
            return textBox;
```

Add:

```csharp
        private object? SafeGetValue(PropertyGridEntry entry, object target)
        {
            try
            {
                return valueAdapter.GetValue(entry, target);
            }
            catch (Exception)
            {
                // A getter that throws is shown by BuildEditor already; reverting to empty text is the best left to do.
                return null;
            }
        }
```

Update `BuildNumericEditor`'s summary: "a plain validated `TextBox` when `Range` is absent or open-ended, or a slider + text box pair when it's bounded".

- [ ] **Step 5: Rewrite the ranged editor's typing and focus-loss paths**

In `BuildRangedNumericEditor`:

1. **Replace the `textBox.TextChanged` handler** with:

```csharp
            textBox.TextChanged += (_, _) =>
            {
                if (isSyncing)
                    return;
                if (!TryParseInput(entry, textBox.Text, out object? typed, out string? reason))
                {
                    // Mid-edit text ("2" on the way to "2500" in [1000, 5000]) stays as typed and writes nothing.
                    SetInvalid(textBox, reason);
                    return;
                }

                SetInvalid(textBox, null);
                isSyncing = true;
                try
                {
                    slider.Value = (float)Convert.ToDouble(typed, System.Globalization.CultureInfo.InvariantCulture);
                    Write(entry, target, typed);
                }
                finally
                {
                    isSyncing = false;
                }
            };
```

2. **Replace the `textBox.FocusChanged` handler** with:

```csharp
            textBox.FocusChanged += (_, _) =>
            {
                if (textBox.IsFocused || isSyncing || !textBox.ControlState.HasFlag(ControlState.Invalid))
                    return;
                isSyncing = true;
                try
                {
                    if (TryConvertNumeric(slider.Value, entry.PropertyType, out object? current))
                        textBox.Text = FormatNumber(current);
                }
                finally
                {
                    isSyncing = false;
                }

                SetInvalid(textBox, null);
            };
```

3. **In the `slider.ValueChanged` handler**, write only values that pass `entry.Validate`. A slider can reach an exclusive bound:

```csharp
                    if (TryConvertNumeric(slider.Value, entry.PropertyType, out object? converted) && entry.Validate(converted, out _))
                    {
                        textBox.Text = FormatNumber(converted);
                        SetInvalid(textBox, null);
                        Write(entry, target, converted);
                    }
```

4. **Rewrite the method's `<remarks>`:** the typed value is validated against the range and marked Invalid instead of clamped. The text is never rewritten while focused. Invalid text reverts on focus loss.

- [ ] **Step 6: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`
Expected: all pass.

- [ ] **Step 7: Warnings, full suite, commit**

Warnings: IcyUI `84`. Full suite: Failed 0.

```bash
git add sources/IcyUI/UI/Controls/PropertyGrid.cs sources/IcyUI.Tests/Controls/PropertyGridTests.cs
git commit -m "Validate typed PropertyGrid numbers and mark invalid input instead of writing it"
```

---

### Task 3: Reset to `[DefaultValue]` in the plain grid

**Files:**
- Modify: `sources/IcyUI/Data/PropertyGridValueAdapter.cs`, `sources/IcyUI/UI/Controls/PropertyGrid.cs`
- Test: `sources/IcyUI.Tests/Controls/PropertyGridAdapterTests.cs`

**Interfaces:**
- Consumes: `PropertyGridEntry.HasDefaultValue`/`DefaultValue` (Task 1).
- Produces: `PropertyGridValueAdapter.Default.CanReset`/`Reset` honour `[DefaultValue]`.

- [ ] **Step 1: Write the failing tests**

Add to `PropertyGridAdapterTests`:

```csharp
        private sealed class DefaultedTarget
        {
            [System.ComponentModel.DefaultValue(3)]
            public int Lives { get; set; } = 3;

            public int Plain { get; set; } = 1;
        }

        [Fact]
        public void TheDefaultAdapter_OffersReset_OnlyForAChangedDefaultedValue()
        {
            var target = new DefaultedTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            Assert.Equal(2, FindRow(grid, "Lives")!.Children.Count);
            Assert.Equal(2, FindRow(grid, "Plain")!.Children.Count);

            ((TextBox)FindEditor(grid, "Lives")!).Text = "7";
            grid.Refresh();

            Assert.IsType<Button>(FindRow(grid, "Lives")!.Children[2]);
            Assert.Equal(2, FindRow(grid, "Plain")!.Children.Count);
        }

        [Fact]
        public void Reset_WritesTheDefault_AndRemovesItsButton()
        {
            var target = new DefaultedTarget { Lives = 9 };
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var reset = Assert.IsType<Button>(FindRow(grid, "Lives")!.Children[2]);
            reset.Command!.Execute(null);

            Assert.Equal(3, target.Lives);
            Assert.Equal("3", ((TextBox)FindEditor(grid, "Lives")!).Text);
            Assert.Equal(2, FindRow(grid, "Lives")!.Children.Count);
        }
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertyGridAdapterTests"`
Expected: both FAIL, because there's no Reset button.

- [ ] **Step 3: Implement the default Reset**

In `PropertyGridValueAdapter.cs`, replace `CanReset` and `Reset`:

```csharp
        /// <summary>
        /// Determines whether the row shows a Reset button.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <returns>
        /// The default: <see langword="true"/> when the property declares a
        /// <see cref="System.ComponentModel.DefaultValueAttribute"/>, is writable, and currently holds another value.
        /// </returns>
        public virtual bool CanReset(PropertyGridEntry entry, object target)
        {
            if (!entry.HasDefaultValue || entry.IsReadOnly)
                return false;

            try
            {
                return !Equals(GetValue(entry, target), entry.DefaultValue);
            }
            catch (Exception)
            {
                // A getter that throws can't be compared; offering Reset would act on a value nobody can see.
                return false;
            }
        }

        /// <summary>
        /// Resets the property when the user presses the row's Reset button.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <remarks>The default writes <see cref="PropertyGridEntry.DefaultValue"/> through <see cref="TrySetValue"/>.</remarks>
        public virtual void Reset(PropertyGridEntry entry, object target)
        {
            if (entry.HasDefaultValue)
                TrySetValue(entry, target, entry.DefaultValue);
        }
```

Update the class remarks: the default adapter offers Reset for `[DefaultValue]` properties.

- [ ] **Step 4: Refresh after Reset**

In `PropertyGrid.FillRow`, change the Reset command to refresh the grid after resetting, so the row shows the new value and drops its button:

```csharp
                    Command = new ActionCommand(() =>
                    {
                        adapter.Reset(entry, target);
                        Refresh();
                    }),
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid|FullyQualifiedName~PropertiesPanel"`
Expected: all pass.

- [ ] **Step 6: Warnings, full suite, commit**

Warnings: IcyUI `84`. Full suite: Failed 0.

```bash
git add sources/IcyUI/Data/PropertyGridValueAdapter.cs sources/IcyUI/UI/Controls/PropertyGrid.cs sources/IcyUI.Tests/Controls/PropertyGridAdapterTests.cs
git commit -m "Offer Reset to a property's DefaultValue in the plain PropertyGrid"
```

---

### Task 4: `ActiveEntry` and `ActiveMessage`

**Files:**
- Modify: `sources/IcyUI/UI/Controls/PropertyGrid.cs`
- Test: `sources/IcyUI.Tests/Controls/PropertyGridHelpTests.cs` (new)

**Interfaces:**
- Consumes: `PropertyRow.InvalidReason`, `SetInvalid` (Task 2); `PropertyGridEntry.Description` (Task 1).
- Produces, on `PropertyGrid`:
  - `PropertyGridEntry? ActiveEntry`;
  - `string? ActiveMessage`;
  - `event EventHandler? ActiveMessageChanged`.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Controls/PropertyGridHelpTests.cs
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class PropertyGridHelpTests
    {
        private sealed class Target
        {
            [Description("Seconds before the bomb goes off.")]
            [Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]
            public float Fuse { get; set; } = 3;

            public float Speed { get; set; } = 1;
        }

        private static (Canvas Canvas, PropertyGrid Grid) Create()
        {
            var canvas = new Canvas(new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration()))
            {
                IsInputEnabled = true,
                IsVisible = true,
            };
            var grid = new PropertyGrid { Target = new Target(), Width = 400, Height = 300 };
            canvas.Add(grid);
            canvas.Render();
            return (canvas, grid);
        }

        [Fact]
        public void FocusingARow_MakesItActive_AndShowsItsDescription()
        {
            (Canvas canvas, PropertyGrid grid) = Create();
            int raised = 0;
            grid.ActiveMessageChanged += (_, _) => raised++;

            canvas.Focus(FindBox(grid, "Fuse"));

            Assert.Equal("Fuse", grid.ActiveEntry?.Name);
            Assert.Equal("Seconds before the bomb goes off.", grid.ActiveMessage);
            Assert.Equal(1, raised);
        }

        [Fact]
        public void InvalidInput_ReplacesTheDescription_WithTheReason_UntilItIsFixed()
        {
            (Canvas canvas, PropertyGrid grid) = Create();
            TextBox box = FindBox(grid, "Fuse");
            canvas.Focus(box);

            box.Text = "0";
            Assert.Equal("Must be greater than 0.", grid.ActiveMessage);

            box.Text = "5";
            Assert.Equal("Seconds before the bomb goes off.", grid.ActiveMessage);
        }

        [Fact]
        public void MovingFocusToAnotherRow_FollowsIt_AndLeavingTheGridClearsIt()
        {
            (Canvas canvas, PropertyGrid grid) = Create();
            canvas.Focus(FindBox(grid, "Fuse"));

            canvas.Focus(FindBox(grid, "Speed"));
            Assert.Equal("Speed", grid.ActiveEntry?.Name);
            Assert.Null(grid.ActiveMessage);

            canvas.Focus(null);
            Assert.Null(grid.ActiveEntry);
        }

        private static TextBox FindBox(PropertyGrid grid, string name)
        {
            var containers = (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(grid)!;
            foreach (ItemContainer container in containers.Values)
            {
                if (container.Content is Grid row && row.Children[0] is TextBlock label && label.Text == name)
                    return row.Children[1].EnumerateVisualSubtree().OfType<TextBox>().First();
            }

            throw new InvalidOperationException($"No row '{name}'.");
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertyGridHelpTests"`
Expected: a build failure, because `ActiveEntry` doesn't exist.

- [ ] **Step 3: Implement**

In `PropertyGrid.cs`:

1. **Fields:**

```csharp
        private PropertyRow? activeRow;
        private string? reportedMessage;
        private PropertyGridEntry? reportedEntry;
```

2. **Public members**, after `ValueAdapter`:

```csharp
        /// <summary>
        /// Occurs when <see cref="ActiveEntry"/> or <see cref="ActiveMessage"/> changed.
        /// </summary>
        public event EventHandler? ActiveMessageChanged;

        /// <summary>
        /// Gets the property of the row holding the keyboard focus, or <see langword="null"/>.
        /// </summary>
        public PropertyGridEntry? ActiveEntry => activeRow?.Entry;

        /// <summary>
        /// Gets the help text for <see cref="ActiveEntry"/>: why its typed value is rejected while it is, otherwise its
        /// <see cref="PropertyGridEntry.Description"/>.
        /// </summary>
        /// <remarks>
        /// Core has no tooltips, so hosts show this themselves, for example in a status line under the grid. It updates on
        /// focus changes and keystrokes only.
        /// </remarks>
        public string? ActiveMessage => activeRow == null ? null : activeRow.InvalidReason ?? activeRow.Entry.Description;
```

   Place the event with the other events, if `ItemsControl`'s ordering puts events first; SA1201 decides.

3. **`UpdateActive()`:**

```csharp
        /// <summary>
        /// Re-reads which row holds the focus and raises <see cref="ActiveMessageChanged"/> when the active entry or its
        /// message changed.
        /// </summary>
        private void UpdateActive()
        {
            activeRow = null;
            for (UIElement? element = Canvas?.FocusedElement; element != null; element = element.Parent)
            {
                if (element is PropertyRow row && row.Parent is ItemContainer { Parent: var owner } && ReferenceEquals(owner, this))
                {
                    activeRow = row;
                    break;
                }
            }

            if (ReferenceEquals(reportedEntry, ActiveEntry) && reportedMessage == ActiveMessage)
                return;

            reportedEntry = ActiveEntry;
            reportedMessage = ActiveMessage;
            ActiveMessageChanged?.Invoke(this, EventArgs.Empty);
        }
```

   Check how a realized `ItemContainer` relates to its `ItemsControl`: `grep -n "Parent = this\|\.Parent = " sources/IcyUI/UI/Controls/ItemsControl.cs`. If the container's `Parent` isn't the grid, replace the ownership check with `realizedContainers.ContainsValue(container)` on the `ItemContainer` found while walking up.

4. **In `FillRow`**, after adding the editor, subscribe its focusable elements:

```csharp
            foreach (UIElement focusable in editor.EnumerateVisualSubtree().Where(x => x.IsFocusable))
                focusable.FocusChanged += (_, _) => UpdateActive();
```

   A focus move raises `FocusChanged` on the old and the new element. Each call re-reads `Canvas.FocusedElement`, so the last call settles on the right row whatever the order.

5. **At the end of `SetInvalid`**, call `UpdateActive();`.

6. **In the `Target` setter**, call `UpdateActive()` after `ItemsSource = BuildRows(value);`, since the active row is gone.

- [ ] **Step 4: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`
Expected: all pass.

- [ ] **Step 5: Warnings, full suite, commit**

Warnings: IcyUI `84`. Full suite: Failed 0.

```bash
git add sources/IcyUI/UI/Controls/PropertyGrid.cs sources/IcyUI.Tests/Controls/PropertyGridHelpTests.cs
git commit -m "Report the focused PropertyGrid row's description or validation reason"
```

---

### Task 5: Annotate core's guarded properties, and keep guards and attributes in agreement

**Files:**
- Modify: `sources/IcyUI/UI/UIElement.cs`, `sources/IcyUI/UI/Controls/ItemsControl.cs`, `Selector.cs`, `WrapGrid.cs`, `TreeView.cs`, `SelectingItemsControl.cs`
- Test: `sources/IcyUI.Tests/UI/RangeAnnotationTests.cs` (new)

**Interfaces:**
- Consumes: `PropertyGridEntry.EnumerateFor`, `Range`, `HasDefaultValue`, `DefaultValue` (Task 1).

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/UI/RangeAnnotationTests.cs
using System.Reflection;
using Icy.Data;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.UI
{
    public class RangeAnnotationTests
    {
        public static TheoryData<Type, string> Guarded => new()
        {
            { typeof(UIElement), nameof(UIElement.Width) },
            { typeof(UIElement), nameof(UIElement.Height) },
            { typeof(UIElement), nameof(UIElement.MinWidth) },
            { typeof(UIElement), nameof(UIElement.MinHeight) },
            { typeof(UIElement), nameof(UIElement.MaxWidth) },
            { typeof(UIElement), nameof(UIElement.MaxHeight) },
            { typeof(UIElement), nameof(UIElement.Opacity) },
            { typeof(ItemsControl), nameof(ItemsControl.DefaultEstimatedItemHeight) },
            { typeof(ComboBox), nameof(Selector.MaxDropDownHeight) },
            { typeof(WrapGrid), nameof(WrapGrid.ItemWidth) },
            { typeof(WrapGrid), nameof(WrapGrid.ItemHeight) },
            { typeof(TreeView), nameof(TreeView.Indent) },
            { typeof(ListBox), nameof(SelectingItemsControl.SelectedIndex) },
        };

        [Theory]
        [MemberData(nameof(Guarded))]
        public void EveryGuardedProperty_DeclaresItsRule(Type type, string name)
        {
            PropertyGridEntry entry = Entry(Activator.CreateInstance(type)!, name);

            Assert.NotNull(entry.Range);
        }

        [Fact]
        public void EveryRangeInCore_AgreesWithItsSetter()
        {
            // For each public, constructible UIElement type and each of its properties with a [Range]:
            // - the default and values just inside each finite bound are accepted;
            // - a value just outside either throws or is stored back inside the range (a rotation normalises).
            var failures = new List<string>();
            foreach (Type type in typeof(UIElement).Assembly.GetExportedTypes()
                .Where(t => typeof(UIElement).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null))
            {
                foreach (PropertyGridEntry entry in PropertyGridEntry.EnumerateFor(Activator.CreateInstance(type)!))
                {
                    if (entry.Range is not { } range || entry.IsReadOnly)
                        continue;

                    foreach (double inside in InsideValues(range, entry.PropertyType))
                    {
                        if (!TrySet(type, entry, inside, out _))
                            failures.Add($"{type.Name}.{entry.Name} rejected {inside}, inside {range.Describe()}");
                    }

                    if (entry.HasDefaultValue && !TrySetObject(type, entry, entry.DefaultValue))
                        failures.Add($"{type.Name}.{entry.Name} rejected its default {entry.DefaultValue}");

                    foreach (double outside in OutsideValues(range, entry.PropertyType))
                    {
                        if (TrySet(type, entry, outside, out object? stored) && !range.Contains(Convert.ToDouble(stored)))
                            failures.Add($"{type.Name}.{entry.Name} stored {stored}, outside {range.Describe()}");
                    }
                }
            }

            Assert.Empty(failures);
        }

        private static PropertyGridEntry Entry(object target, string name) =>
            PropertyGridEntry.EnumerateFor(target).Single(e => e.Name == name);

        private static double Step(Type type) => type == typeof(float) || type == typeof(double) ? 0.5 : 1;

        private static IEnumerable<double> InsideValues(ValueRange range, Type type)
        {
            if (double.IsFinite(range.Min))
                yield return range.MinIsExclusive ? range.Min + Step(type) : range.Min;
            if (double.IsFinite(range.Max))
                yield return range.MaxIsExclusive ? range.Max - Step(type) : range.Max;
        }

        private static IEnumerable<double> OutsideValues(ValueRange range, Type type)
        {
            if (double.IsFinite(range.Min))
                yield return range.MinIsExclusive ? range.Min : range.Min - Step(type);
            if (double.IsFinite(range.Max))
                yield return range.MaxIsExclusive ? range.Max : range.Max + Step(type);
        }

        private static bool TrySet(Type type, PropertyGridEntry entry, double value, out object? stored)
        {
            stored = null;
            object target = Activator.CreateInstance(type)!;
            PropertyInfo property = type.GetProperty(entry.Name)!;
            try
            {
                property.SetValue(target, Convert.ChangeType(value, entry.PropertyType, System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (TargetInvocationException)
            {
                return false;
            }

            stored = property.GetValue(target);
            return true;
        }

        private static bool TrySetObject(Type type, PropertyGridEntry entry, object? value)
        {
            object target = Activator.CreateInstance(type)!;
            try
            {
                type.GetProperty(entry.Name)!.SetValue(target, value);
                return true;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~RangeAnnotationTests"`
Expected: `EveryGuardedProperty_DeclaresItsRule` FAILS for every row except `Opacity`. The agreement test passes, since today only `Opacity` and the rotations have ranges. That's fine: it's a guard against future drift, and Step 4 proves it on the new annotations.

- [ ] **Step 3: Annotate**

Each attribute goes with the property's other attributes. Add `using System.ComponentModel.DataAnnotations;` where a file lacks it. `UIElement.cs` already has it.

| File | Property | Add |
|---|---|---|
| `UIElement.cs` | `Width`, `Height`, `MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight` | `[Range(0d, double.PositiveInfinity)]`; each already has `[DefaultValue(float.NaN)]`, so check and add it where missing |
| `ItemsControl.cs` | `DefaultEstimatedItemHeight` | `[Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]` |
| `Selector.cs` | `MaxDropDownHeight` | `[Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]` |
| `WrapGrid.cs` | `ItemWidth`, `ItemHeight` | `[Range(0d, double.PositiveInfinity, MinimumIsExclusive = true)]` |
| `TreeView.cs` | `Indent` | `[Range(0d, double.PositiveInfinity)]` |
| `SelectingItemsControl.cs` | `SelectedIndex` | `[Range(-1d, double.PositiveInfinity)]` |

Check that each of these properties has a `[DefaultValue]` that satisfies its own rule (`WrapGrid.ItemWidth`'s default might be NaN, meaning "measure the first item"). Run `grep -n -B6 "public float ItemWidth\|public float ItemHeight" sources/IcyUI/UI/Controls/WrapGrid.cs`. If the default is NaN and the setter skips its guard for NaN, the "default is valid" rule already covers it. If the default violates the rule, stop and report it rather than changing a default.

Also mention the rule in each property's `<remarks>` (for example "Must be greater than 0; <see cref="float.NaN"/> is the default."), so the generated docs say what the attribute says.

- [ ] **Step 4: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~RangeAnnotationTests"`
Expected: all pass. If the agreement test lists a failure, the attribute and the guard disagree. Fix the attribute to match the guard: guards are the shipped behaviour.

- [ ] **Step 5: Warnings, full suite, commit**

Warnings: IcyUI `84`. Full suite: Failed 0. The grid now builds plain validated text boxes for `Width`-like rows, so check `PropertiesPanelTests` in particular.

```bash
git add sources/IcyUI/UI sources/IcyUI.Tests/UI/RangeAnnotationTests.cs
git commit -m "Declare the guarded core properties' rules with Range attributes"
```

---

### Task 6: Hosts, the no-exception check, and the whole-branch check

**Files:**
- Modify: `sources/IcyUI.Design/Editor/Panels/PropertiesPanel.cs`, `sources/Shared Samples/PropertyGridDemo.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/PropertiesPanelTests.cs`

**Interfaces:**
- Consumes: `PropertyGrid.ActiveMessage`/`ActiveMessageChanged` (Task 4); `MarkupPropertyAdapter.ErrorChanged` (existing, internal).

- [ ] **Step 1: Write the failing tests**

Add to `PropertiesPanelTests`:

```csharp
        [Fact]
        public void TypingAnInvalidWidth_ShowsTheRule_AndThrowsNothing()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<Button>("b"));
                host.Render();
                var box = (TextBox)FindEditor(panel.Grid, "Width")!;
                host.Canvas.Focus(box);

                // Count only this thread's exceptions: the suite runs other test classes in parallel.
                int thread = Environment.CurrentManagedThreadId;
                int thrown = 0;
                void Count(object? sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
                {
                    if (Environment.CurrentManagedThreadId == thread)
                        thrown++;
                }

                AppDomain.CurrentDomain.FirstChanceException += Count;
                try
                {
                    foreach (string text in new[] { "-", "-5", "1e40", "abc" })
                        box.Text = text;
                }
                finally
                {
                    AppDomain.CurrentDomain.FirstChanceException -= Count;
                }

                Assert.Equal(0, thrown);
                Assert.Equal("Enter a number.", panel.StatusText);
                Assert.Contains("Width=\"40\"", host.Document.Text);

                box.Text = "-5";
                Assert.Equal("Must be at least 0.", panel.StatusText);
            }
        }
```

`FindEditor` already exists in this test class (added with the Enter-commit fix).

- [ ] **Step 2: Run the test and confirm it fails**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~TypingAnInvalidWidth"`
Expected: FAIL on the status assertion, because the panel doesn't show `ActiveMessage` yet. The first-chance count should already be 0 after Tasks 2 and 5. If it isn't, the failure message names the escaping path: find it before going on.

- [ ] **Step 3: Show `ActiveMessage` in `PropertiesPanel`**

In `PropertiesPanel.cs`:

1. Add the field `private string? lastError;`.
2. In the constructor, after `Grid = new PropertyGrid();`, add `Grid.ActiveMessageChanged += (_, _) => ShowStatus();`.
3. Replace `OnErrorChanged` with:

```csharp
        private void OnErrorChanged(string? error)
        {
            lastError = error;
            ShowStatus();
        }

        // The editor's own refusal (a value with no markup form, a failed edit) wins over the grid's help text.
        private void ShowStatus() => status.Text = lastError ?? Grid.ActiveMessage ?? string.Empty;
```

4. Wherever the class sets `status.Text = string.Empty;` (on retarget and empty), set `lastError = null;` and call `ShowStatus()` instead.
5. Update `StatusText`'s doc: "the editor's last refusal, or the focused row's validation reason or description, or empty".

- [ ] **Step 4: Run the panel tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertiesPanelTests"`
Expected: all pass, including the existing `ARefusedValue_ShowsTheReasonInTheStatusLine`.

- [ ] **Step 5: The demo's help line**

In `sources/Shared Samples/PropertyGridDemo.cs`:

1. In the markup, after the `</ScrollViewer>` that holds the grid, add `<TextBlock x:Name="Help" FontSize="13" Foreground="Silver" Margin="0,8,0,0" Width="420" HorizontalAlignment="Left"/>`.
2. In `Build`, after `grid.Target = character;`, add:

```csharp
            TextBlock help = root.FindRequiredControl<TextBlock>("Help");
            grid.ActiveMessageChanged += (_, _) => help.Text = grid.ActiveMessage ?? string.Empty;
```

3. On `Character`, add `[Description("The name shown above the character.")]` to `Name`, and `[Description("Hit points, from 0 (down) to 100.")]` to `Health`.
4. Update the demo's class summary: "Focus a row to see its description, or why a typed value is rejected, under the grid."

- [ ] **Step 6: Warnings, full suite, solution build, commit**

Run: IcyUI warnings `84`, Design warnings `1`.
Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj` three times. Expected: Failed 0 each time.
Run: `dotnet build "sources/IcyUI.sln"`. Expected: success, including both sample hosts.

```bash
git add sources/IcyUI.Design/Editor/Panels/PropertiesPanel.cs "sources/Shared Samples/PropertyGridDemo.cs" sources/IcyUI.Tests/Design/Editor/PropertiesPanelTests.cs
git commit -m "Show PropertyGrid help text in the Properties panel and the PropertyGrid demo"
```

- [ ] **Step 7: Whole-branch review and the smoke checklist**

Review `git diff c6ff208..HEAD` against the spec and its deltas. Use one fresh reviewer, or a self-review if Ivan asks for no sub-agents. Fix Critical and Important findings with tests, and list the minors.

Hand Ivan this checklist. **Don't claim a smoke test.**

1. **PropertyGrid demo:** focus Health. Its description shows. Type `150`: the red border appears with "Must be between 0 and 100.", and the value isn't written. Leave the box: the old value is back.
2. **Shell editor or Editor Workspace:** select an element, type `-5` into Width, then `0`. The status shows "Must be at least 0.", then the value is written. Delete the text of MaxWidth: the attribute becomes NaN ("unset").
3. **Shell editor or Editor Workspace:** select a ListBox and type `0` into DefaultEstimatedItemHeight. The status shows "Must be greater than 0." and the debugger doesn't stop on an exception.
