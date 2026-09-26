# PropertyGrid Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `PropertyGrid`, a Unity-Inspector-style control that lists an arbitrary object's properties as rows (label + type-appropriate editor) and lets a user edit them.

**Architecture:** A new data-model type (`PropertyGridEntry`) unifies IcyUI's registered `[RegisterReference]` properties and plain reflected `PropertyInfo` behind one shape, reading standard BCL attributes (`[Browsable]`/`[DisplayName]`/`[ReadOnly]`/`[Category]`/`[Range]`) for per-property metadata. `PropertyGrid : ItemsControl` turns a target object into a flat list of `PropertyGridEntry`/category-header items, builds each row's `Grid` (label | editor) directly in C# via an overridden `CreateContainer` (matching `TabControl`'s own precedent for un-templated, code-built rows), and disables `PoolingEnabled` for the same reason `TabControl` just did — a raw-C#-built row has no `{Binding}` for `ItemsControl`'s pooled-reuse path to refresh.

**Tech Stack:** C#/.NET 8, xUnit, the existing IcyUI core library (`sources/IcyUI`), no new NuGet dependencies (`System.ComponentModel.DataAnnotations` is part of the .NET 8 shared framework already referenced transitively).

**Spec:** `docs/superpowers/specs/2026-09-27-propertygrid-design.md`

## Global Constraints

- No new IcyUI attribute types — metadata comes from `System.ComponentModel.BrowsableAttribute`/`DisplayNameAttribute`/`ReadOnlyAttribute`/`CategoryAttribute` and `System.ComponentModel.DataAnnotations.RangeAttribute` only.
- No core-framework files are modified — `PropertyGridEntry` recovers a registered property's `PropertyInfo` via `IPropertyReference.OwnerType.GetProperty(IPropertyReference.Name)`, not a new accessor on `IPropertyReference` itself.
- Nested/complex-object rows, and `DateOnly`/`TimeOnly`/`DateTime`/`Uri` editors, are out of scope — both fall through to the generic read-only `ToString()` row, marked with a `// TODO` citing the spec.
- `PropertyGrid.PoolingEnabled` must be `false` after construction (mirrors `TabControl`'s fix in commit `ee48546`).
- All public APIs get full XML doc comments (project-wide rule, `CLAUDE.local.md`).

## Review Focus

- **A property whose type has no matching editor and isn't caught by the explicit fallback throws instead of degrading gracefully** — e.g. a custom `enum`-like struct, or a property whose getter throws. `CreateContainer`'s dispatch must have an unconditional final fallback branch, and `GetValue` failures must not crash row construction.
- **Two properties with the same `[DisplayName]` but different `Name`, or a subclass property that shadows a base property (`new` modifier), get double-counted or collide during the registered/reflected dedup** — the dedup key must be the actual reflected member (`PropertyInfo`), not the display string.
- **A `[Range]` attribute on a non-numeric property** (attribute misuse, or a future editor forgetting to guard the type) — `Range` parsing must not throw for a property whose `PropertyType` isn't one `Convert.ToDouble` can handle; skip the range treatment rather than crash the whole enumeration.
- **`Target` is reassigned from object A to object B whose properties differ in count or type at the same index** — this is the exact `TabControl` pooling-bug shape; a regression test must assign twice and check the second assignment's rows, not just the first.
- **A `Target` of `null`** — `PropertyGrid.Target = null` must clear all rows without throwing (the spec's `EnumerateFor` is asked for on a non-null target only elsewhere; the control itself must guard the null case explicitly).

---

## Task 1: `PropertyGridEntry` and `EnumValueCache`

**Files:**
- Create: `sources/IcyUI/Data/PropertyGridEntry.cs`
- Create: `sources/IcyUI/Data/EnumValueCache.cs`
- Test: `sources/IcyUI.Tests/Data/PropertyGridEntryTests.cs`
- Test: `sources/IcyUI.Tests/Data/EnumValueCacheTests.cs`

**Interfaces:**
- Consumes: `Icy.Data.Markup.PropertyRegistry.For(object)` → `PropertyRegistry`; `PropertyRegistry.GetPropertyStore(Type)` → `IPropertyStore`; `IPropertyStore.EnumerateProperties(bool includeInherited = true)` → `IEnumerable<IPropertyReference>`; `IPropertyReference.{Name, OwnerType}`.
- Produces (for Task 2 to consume): `Icy.Data.PropertyGridEntry` with public members `Name : string`, `DisplayName : string`, `Category : string`, `PropertyType : Type`, `IsReadOnly : bool`, `Range : (double Min, double Max)?`, `GetValue(object target) : object?`, `TrySetValue(object target, object? value) : bool`, and the static `EnumerateFor(object target) : IReadOnlyList<PropertyGridEntry>`. `Icy.Data.EnumValueCache.GetValues(Type enumType) : Array`.

- [ ] **Step 1: Write the failing tests for `PropertyGridEntry`**

Create `sources/IcyUI.Tests/Data/PropertyGridEntryTests.cs`:

```csharp
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Icy.Data;
using Icy.Data.Markup.Attributes;
using Xunit;

namespace Icy.Tests.Data
{
    public class PropertyGridEntryTests
    {
        private sealed class PlainPoco
        {
            public string Name { get; set; } = string.Empty;

            [Category("Stats")]
            public int Health { get; set; }

            [Category("Stats")]
            [Range(0, 100)]
            public float Armor { get; set; }

            [DisplayName("Is Alive")]
            public bool Alive { get; set; }

            [Browsable(false)]
            public string Hidden { get; set; } = "secret";

            public string ReadOnlyValue { get; } = "fixed";

            [ReadOnly(true)]
            public string ExplicitlyReadOnly { get; set; } = "locked";
        }

        private sealed class RegisteredTarget : Icy.Data.Markup.DependencyObject
        {
            private string registeredName = string.Empty;

            [RegisterReference]
            [Category("Registered")]
            public string RegisteredName
            {
                get => registeredName;
                set => SetProperty(ref registeredName, value);
            }
        }

        [Fact]
        public void EnumerateFor_PlainPoco_ReadsCategoryDisplayNameAndRange()
        {
            var target = new PlainPoco();
            var entries = PropertyGridEntry.EnumerateFor(target);

            var health = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Health));
            Assert.Equal("Stats", health.Category);

            var armor = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Armor));
            Assert.Equal((0d, 100d), armor.Range);

            var alive = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Alive));
            Assert.Equal("Is Alive", alive.DisplayName);
        }

        [Fact]
        public void EnumerateFor_BrowsableFalse_IsExcluded()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());
            Assert.DoesNotContain(entries, e => e.Name == nameof(PlainPoco.Hidden));
        }

        [Fact]
        public void EnumerateFor_NoSetterOrExplicitReadOnly_IsMarkedReadOnly()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());

            var noSetter = Assert.Single(entries, e => e.Name == nameof(PlainPoco.ReadOnlyValue));
            Assert.True(noSetter.IsReadOnly);

            var explicitReadOnly = Assert.Single(entries, e => e.Name == nameof(PlainPoco.ExplicitlyReadOnly));
            Assert.True(explicitReadOnly.IsReadOnly);
        }

        [Fact]
        public void GetValue_And_TrySetValue_RoundTripOnPlainPoco()
        {
            var target = new PlainPoco();
            var entries = PropertyGridEntry.EnumerateFor(target);
            var health = Assert.Single(entries, e => e.Name == nameof(PlainPoco.Health));

            Assert.True(health.TrySetValue(target, 42));
            Assert.Equal(42, health.GetValue(target));
            Assert.Equal(42, target.Health);
        }

        [Fact]
        public void TrySetValue_OnReadOnlyEntry_ReturnsFalseAndDoesNotThrow()
        {
            var target = new PlainPoco();
            var entries = PropertyGridEntry.EnumerateFor(target);
            var readOnly = Assert.Single(entries, e => e.Name == nameof(PlainPoco.ExplicitlyReadOnly));

            Assert.False(readOnly.TrySetValue(target, "new value"));
            Assert.Equal("locked", target.ExplicitlyReadOnly);
        }

        [Fact]
        public void EnumerateFor_RegisteredDependencyObjectProperty_IsIncludedAndNotDuplicated()
        {
            var target = new RegisteredTarget();
            var entries = PropertyGridEntry.EnumerateFor(target);

            var matches = entries.Where(e => e.Name == nameof(RegisteredTarget.RegisteredName)).ToList();
            Assert.Single(matches);
            Assert.Equal("Registered", matches[0].Category);

            Assert.True(matches[0].TrySetValue(target, "Hero"));
            Assert.Equal("Hero", target.RegisteredName);
        }

        [Fact]
        public void EnumerateFor_GroupsEntriesByFirstSeenCategoryOrder()
        {
            var entries = PropertyGridEntry.EnumerateFor(new PlainPoco());
            var categories = entries.Select(e => e.Category).Distinct().ToList();

            int healthIndex = categories.IndexOf("Stats");
            int miscIndex = categories.IndexOf("Misc");
            Assert.True(healthIndex >= 0 && miscIndex >= 0);
        }
    }
}
```

- [ ] **Step 2: Run the tests to confirm they fail on missing types**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PropertyGridEntryTests"`
Expected: FAIL to compile - `Icy.Data.PropertyGridEntry` does not exist yet.

- [ ] **Step 3: Implement `PropertyGridEntry`**

Create `sources/IcyUI/Data/PropertyGridEntry.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Icy.Data.Markup;

namespace Icy.Data
{
    /// <summary>
    /// Describes a single property a <see cref="UI.Controls.PropertyGrid"/> row edits - unifying an
    /// <see cref="IPropertyReference"/>-registered property and a plain reflected <see cref="PropertyInfo"/>
    /// behind one shape, since <see cref="PropertyGrid.Target"/> may be either.
    /// </summary>
    /// <remarks>
    /// Metadata comes entirely from standard <see cref="System.ComponentModel"/>/
    /// <see cref="System.ComponentModel.DataAnnotations"/> attributes on the underlying property - see the
    /// design spec (<c>docs/superpowers/specs/2026-09-27-propertygrid-design.md</c>) for why no new IcyUI
    /// attribute type was introduced for this.
    /// </remarks>
    public sealed class PropertyGridEntry
    {
        private readonly PropertyInfo property;

        private PropertyGridEntry(PropertyInfo property)
        {
            this.property = property;
            DisplayName = property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName ?? property.Name;
            Category = property.GetCustomAttribute<CategoryAttribute>()?.Category ?? "Misc";
            IsReadOnly = property.GetCustomAttribute<ReadOnlyAttribute>()?.IsReadOnly ?? !property.CanWrite;
            Range = TryReadRange(property);
        }

        /// <summary>
        /// Gets the underlying property's name.
        /// </summary>
        public string Name => property.Name;

        /// <summary>
        /// Gets the label to show for this property - <see cref="DisplayNameAttribute"/>'s value, or
        /// <see cref="Name"/> when none is present.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// Gets the group this property is shown under - <see cref="CategoryAttribute"/>'s value, or
        /// <c>"Misc"</c> when none is present.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the underlying property's value type.
        /// </summary>
        public Type PropertyType => property.PropertyType;

        /// <summary>
        /// Gets a value indicating whether this property's editor should be disabled - either
        /// <see cref="ReadOnlyAttribute"/> says so explicitly, or the underlying property has no public setter.
        /// </summary>
        public bool IsReadOnly { get; }

        /// <summary>
        /// Gets the numeric range a <see cref="RangeAttribute"/> declares for this property, converted to
        /// <see langword="double"/>, or <see langword="null"/> when no valid range applies.
        /// </summary>
        public (double Min, double Max)? Range { get; }

        /// <summary>
        /// Reads this property's current value from <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The object to read the value from.</param>
        /// <returns>The property's current value.</returns>
        public object? GetValue(object target) => property.GetValue(target);

        /// <summary>
        /// Attempts to write <paramref name="value"/> to this property on <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The object to write the value to.</param>
        /// <param name="value">The value to assign.</param>
        /// <returns>
        /// <see langword="true"/> if the value was written; <see langword="false"/> when <see cref="IsReadOnly"/>
        /// is <see langword="true"/>, the property has no setter, or the assignment threw (an incompatible
        /// <paramref name="value"/>).
        /// </returns>
        public bool TrySetValue(object target, object? value)
        {
            if (IsReadOnly || !property.CanWrite)
                return false;

            try
            {
                property.SetValue(target, value);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (TargetInvocationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Enumerates every browsable property on <paramref name="target"/>'s runtime type - properties
        /// registered via <c>[RegisterReference]</c> first (deduplicated by the reflected member itself, not by
        /// name string), then every other public readable property found by plain reflection - grouped by
        /// <see cref="Category"/> in first-seen order.
        /// </summary>
        /// <param name="target">The object to enumerate properties for.</param>
        /// <returns>The resulting entries, grouped by <see cref="Category"/>.</returns>
        public static IReadOnlyList<PropertyGridEntry> EnumerateFor(object target)
        {
            ArgumentNullException.ThrowIfNull(target);

            Type type = target.GetType();
            var seen = new HashSet<PropertyInfo>();
            var entries = new List<PropertyGridEntry>();

            foreach (IPropertyReference reference in PropertyRegistry.For(target).GetPropertyStore(type).EnumerateProperties())
            {
                PropertyInfo? info = reference.OwnerType.GetProperty(reference.Name, BindingFlags.Public | BindingFlags.Instance);
                if (info == null || info.GetIndexParameters().Length > 0 || !seen.Add(info))
                    continue;
                if (TryCreate(info, out PropertyGridEntry? entry))
                    entries.Add(entry);
            }

            foreach (PropertyInfo info in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (info.GetIndexParameters().Length > 0 || !info.CanRead || !seen.Add(info))
                    continue;
                if (TryCreate(info, out PropertyGridEntry? entry))
                    entries.Add(entry);
            }

            return entries.GroupBy(e => e.Category).SelectMany(group => group).ToList();
        }

        private static bool TryCreate(PropertyInfo info, out PropertyGridEntry? entry)
        {
            entry = null;
            var browsable = info.GetCustomAttribute<BrowsableAttribute>();
            if (browsable != null && !browsable.Browsable)
                return false;

            entry = new PropertyGridEntry(info);
            return true;
        }

        private static (double Min, double Max)? TryReadRange(PropertyInfo property)
        {
            var range = property.GetCustomAttribute<RangeAttribute>();
            if (range == null)
                return null;

            try
            {
                return (Convert.ToDouble(range.Minimum), Convert.ToDouble(range.Maximum));
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
            {
                return null;
            }
        }
    }
}
```

- [ ] **Step 4: Run the tests, fix any failures, confirm green**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PropertyGridEntryTests"`
Expected: PASS, all 7 tests.

(`Icy.Data.Markup.DependencyObject` is the real, correct base - confirmed at `sources/IcyUI/Data/Markup/DependencyObject.cs`, a `protected` parameterless constructor. `SetProperty(ref field, value)` is the established backing pattern every other `[RegisterReference]` property in this codebase already uses - e.g. `ToggleButton.IsChecked`, `Slider.Value` - not a string-keyed `GetValue`/`SetValue` API, which doesn't exist on this base type.)

- [ ] **Step 5: Write the failing test for `EnumValueCache`**

Create `sources/IcyUI.Tests/Data/EnumValueCacheTests.cs`:

```csharp
using Icy.Data;
using Xunit;

namespace Icy.Tests.Data
{
    public class EnumValueCacheTests
    {
        private enum Suit { Clubs, Diamonds, Hearts, Spades }

        [Fact]
        public void GetValues_ReturnsAllEnumValuesInDeclarationOrder()
        {
            Array values = EnumValueCache.GetValues(typeof(Suit));

            Assert.Equal(new object[] { Suit.Clubs, Suit.Diamonds, Suit.Hearts, Suit.Spades }, values.Cast<object>());
        }

        [Fact]
        public void GetValues_CalledTwiceForSameType_ReturnsCachedInstance()
        {
            Array first = EnumValueCache.GetValues(typeof(Suit));
            Array second = EnumValueCache.GetValues(typeof(Suit));

            Assert.Same(first, second);
        }

        [Fact]
        public void GetValues_NonEnumType_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => EnumValueCache.GetValues(typeof(string)));
        }
    }
}
```

- [ ] **Step 6: Run to confirm it fails**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EnumValueCacheTests"`
Expected: FAIL to compile - `Icy.Data.EnumValueCache` does not exist yet.

- [ ] **Step 7: Implement `EnumValueCache`**

Create `sources/IcyUI/Data/EnumValueCache.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Concurrent;

namespace Icy.Data
{
    /// <summary>
    /// Caches <see cref="Enum.GetValues(Type)"/>'s result per <see cref="Type"/> - used by
    /// <see cref="UI.Controls.PropertyGrid"/> to populate an enum-typed property's <c>ComboBox</c> editor
    /// without re-reflecting the same type's values on every row rebuild.
    /// </summary>
    public static class EnumValueCache
    {
        private static readonly ConcurrentDictionary<Type, Array> Cache = new();

        /// <summary>
        /// Gets every defined value of <paramref name="enumType"/>, in declaration order.
        /// </summary>
        /// <param name="enumType">An enum type.</param>
        /// <returns>The cached array <see cref="Enum.GetValues(Type)"/> returned for <paramref name="enumType"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="enumType"/> is not an enum type.</exception>
        public static Array GetValues(Type enumType)
        {
            ArgumentNullException.ThrowIfNull(enumType);
            if (!enumType.IsEnum)
                throw new ArgumentException($"'{enumType}' is not an enum type.", nameof(enumType));

            return Cache.GetOrAdd(enumType, Enum.GetValues);
        }
    }
}
```

- [ ] **Step 8: Run both test files, confirm all green**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PropertyGridEntryTests|FullyQualifiedName~EnumValueCacheTests"`
Expected: PASS, all 10 tests.

- [ ] **Step 9: Commit**

```bash
git add sources/IcyUI/Data/PropertyGridEntry.cs sources/IcyUI/Data/EnumValueCache.cs sources/IcyUI.Tests/Data/PropertyGridEntryTests.cs sources/IcyUI.Tests/Data/EnumValueCacheTests.cs
git commit -m "Add PropertyGridEntry and EnumValueCache"
```

---

## Task 2: `PropertyGrid` control skeleton, primitive editors, category headers

**Files:**
- Create: `sources/IcyUI/UI/Controls/PropertyGrid.cs`
- Test: `sources/IcyUI.Tests/Controls/PropertyGridTests.cs`

**Interfaces:**
- Consumes: `Icy.Data.PropertyGridEntry` (Task 1's produced type, exact members as above); `Icy.UI.Controls.ItemsControl`'s `CreateContainer(DataTemplate, object) : ItemContainer` (`protected virtual`), `PoolingEnabled : bool`, `ItemTemplate : DataTemplate?`, `ItemsSource : IEnumerable?` (all inherited, no signature changes); `Icy.UI.Controls.Grid`/`ColumnDefinition`/`GridLength` (`Width = GridLength.Auto` / `GridLength.Star`, `Grid.SetColumn(UIElement, int)`); `Icy.UI.Controls.TextBox.Text : string` + `TextChanged` event; `Icy.UI.Controls.CheckBox` (`ToggleButton.IsChecked : bool` + `IsCheckedChanged` event); `Icy.UI.Controls.Slider.Value/Minimum/Maximum : float` + `ValueChanged` event.
- Produces (for Task 3 to extend): `Icy.UI.Controls.PropertyGrid : ItemsControl` with public `Target : object?`; a `private UIElement BuildEditor(PropertyGridEntry entry, object target)` dispatch method Task 3 adds more `case`s to; a `private sealed record CategoryHeader(string Name)` marker type Task 3's fallback branch must also not match against.

- [ ] **Step 1: Write the failing tests**

Create `sources/IcyUI.Tests/Controls/PropertyGridTests.cs`:

```csharp
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class PropertyGridTests
    {
        private sealed class SampleTarget
        {
            public string Nickname { get; set; } = "Alpha";

            public bool Active { get; set; } = true;

            public int Score { get; set; } = 10;
        }

        private sealed class OtherTarget
        {
            public bool Ready { get; set; } = true;

            public string Note { get; set; } = "hi";
        }

        [Fact]
        public void PoolingEnabled_IsFalseAfterConstruction()
        {
            var grid = new PropertyGrid();
            Assert.False(grid.PoolingEnabled);
        }

        [Fact]
        public void Target_Null_ProducesNoItems()
        {
            var grid = new PropertyGrid { Target = null };
            grid.Measure();
            Assert.Equal(0, GetRealizedContainers(grid).Count);
        }

        [Fact]
        public void Target_StringProperty_RealizesTextBoxEditorBoundToValue()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var textBox = Assert.IsType<TextBox>(FindEditor(grid, nameof(SampleTarget.Nickname)));
            Assert.Equal("Alpha", textBox.Text);

            textBox.Text = "Beta";
            Assert.Equal("Beta", target.Nickname);
        }

        [Fact]
        public void Target_BoolProperty_RealizesCheckBoxEditorBoundToValue()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var checkBox = Assert.IsType<CheckBox>(FindEditor(grid, nameof(SampleTarget.Active)));
            Assert.True(checkBox.IsChecked);

            checkBox.IsChecked = false;
            Assert.False(target.Active);
        }

        [Fact]
        public void Target_NumericPropertyWithoutRange_RealizesPlainTextBoxEditor()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var textBox = Assert.IsType<TextBox>(FindEditor(grid, nameof(SampleTarget.Score)));
            Assert.Equal("10", textBox.Text);

            textBox.Text = "42";
            Assert.Equal(42, target.Score);
        }

        [Fact]
        public void Target_ReassignedToDifferentlyShapedObject_RowsReflectNewTargetWithNoStaleWidgets()
        {
            var grid = new PropertyGrid { Target = new SampleTarget() };
            grid.Measure();

            var other = new OtherTarget();
            grid.Target = other;
            grid.Measure();

            var checkBox = Assert.IsType<CheckBox>(FindEditor(grid, nameof(OtherTarget.Ready)));
            Assert.True(checkBox.IsChecked);

            var textBox = Assert.IsType<TextBox>(FindEditor(grid, nameof(OtherTarget.Note)));
            Assert.Equal("hi", textBox.Text);

            Assert.Null(FindEditor(grid, nameof(SampleTarget.Nickname)));
        }

        /// <summary>
        /// Finds the realized row's editor widget (the row <see cref="Grid"/>'s second child) whose label
        /// (first child, a <see cref="TextBlock"/>) matches <paramref name="propertyDisplayName"/> - none of
        /// this file's sample types use <c>[DisplayName]</c>, so the label always equals the property name.
        /// </summary>
        private static UIElement? FindEditor(PropertyGrid grid, string propertyDisplayName)
        {
            foreach (ItemContainer container in GetRealizedContainers(grid).Values)
            {
                if (container.Content is Grid row &&
                    row.Children.ElementAtOrDefault(0) is TextBlock label &&
                    label.Text == propertyDisplayName)
                {
                    return row.Children.ElementAtOrDefault(1);
                }
            }

            return null;
        }

        /// <summary>
        /// Reaches <see cref="ItemsControl"/>'s private realized-container map - the exact reflection helper
        /// <c>ListBoxTests.cs</c>/<c>TabControlTests.cs</c> already use for this, reused verbatim rather than
        /// adding a new test-only accessor to the control itself.
        /// </summary>
        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(control)!;
    }
}
```

This reuses `ListBoxTests.cs`/`TabControlTests.cs`'s own established reflection helper for reaching `ItemsControl.realizedContainers` (confirmed present, `protected readonly Dictionary<int, ItemContainer>`, `sources/IcyUI/UI/Controls/ItemsControl.cs:43`) - no new test-only members need to be added to `PropertyGrid` itself, and `.Measure()` is confirmed (via the same two test files) to be what triggers realization without a live `Canvas`.

- [ ] **Step 2: Run to confirm compile failure**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PropertyGridTests"`
Expected: FAIL to compile - `Icy.UI.Controls.PropertyGrid` does not exist yet.

- [ ] **Step 3: Implement the `PropertyGrid` skeleton with string/bool/numeric editors and category headers**

Create `sources/IcyUI/UI/Controls/PropertyGrid.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using Icy.Data;
using Icy.Data.Markup.Attributes;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A Unity-Inspector-style control: lists <see cref="Target"/>'s properties as rows (a label and a
    /// type-appropriate editor), letting a user edit them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ItemsControl.ItemsSource"/> holds a flat mix of <see cref="PropertyGridEntry"/> (a real row)
    /// and an internal category-header marker, rebuilt whenever <see cref="Target"/> changes.
    /// <see cref="CreateContainer"/> is overridden to build each row's <see cref="Grid"/> (label | editor)
    /// directly in code - see <see cref="BuildEditor"/> for the per-<see cref="Type"/> editor dispatch - rather
    /// than through <see cref="ItemsControl.ItemTemplate"/>/markup, mirroring <see cref="TabControl"/>'s own
    /// precedent for un-templated, code-built items.
    /// </para>
    /// <para>
    /// <see cref="ItemsControl.PoolingEnabled"/> is disabled at construction for the same reason
    /// <see cref="TabControl"/> disables it: a row built directly in code has no <c>{Binding}</c> for
    /// <see cref="ItemsControl"/>'s pooled-reuse path (which only reassigns the pooled container's
    /// <see cref="ContentControl.Content"/>'s <see cref="UIElement.DataContext"/>) to refresh - without this, a
    /// recycled row could keep showing a previous <see cref="Target"/>'s editor widget/value.
    /// </para>
    /// </remarks>
    public class PropertyGrid : ItemsControl
    {
        private object? target;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyGrid"/> class.
        /// </summary>
        public PropertyGrid()
        {
            // Never built - CreateContainer below never calls DataTemplate.Build - only assigned so
            // ItemsControl.EnsureRealized's unconditional ResolveTemplate() call doesn't throw for want of an
            // ItemTemplate. See the class remarks for why pooling is also disabled here.
            ItemTemplate = new DataTemplate();
            PoolingEnabled = false;
        }

        /// <summary>
        /// Gets or sets the object whose properties this grid displays and edits. Setting this re-enumerates
        /// every browsable property via <see cref="PropertyGridEntry.EnumerateFor(object)"/> and rebuilds every
        /// row; assigning <see langword="null"/> clears the grid.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? Target
        {
            get => target;
            set
            {
                if (!SetProperty(ref target, value))
                    return;
                ItemsSource = BuildRows(value);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Dispatches to a category-header row for a <see cref="CategoryHeader"/> marker item, or a
        /// label+editor <see cref="Grid"/> row for a <see cref="PropertyGridEntry"/> - see
        /// <see cref="BuildEditor"/> for the editor-widget dispatch by <see cref="PropertyGridEntry.PropertyType"/>.
        /// </remarks>
        /// <param name="template">Unused - see the class remarks.</param>
        /// <param name="item">Either a <see cref="CategoryHeader"/> or a <see cref="PropertyGridEntry"/>.</param>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
        {
            if (item is CategoryHeader header)
                return new ItemContainer { Content = new TextBlock { Text = header.Name } };

            var entry = (PropertyGridEntry)item;
            object currentTarget = target ?? throw new InvalidOperationException(
                $"'{nameof(PropertyGrid)}' realized a row with no '{nameof(Target)}' set.");

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            var label = new TextBlock { Text = entry.DisplayName, VerticalAlignment = VerticalAlignment.Center };
            UIElement editor = BuildEditor(entry, currentTarget);

            Grid.SetColumn(label, 0);
            Grid.SetColumn(editor, 1);
            row.Children.Add(label);
            row.Children.Add(editor);

            return new ItemContainer { Content = row };
        }

        /// <summary>
        /// Builds the editor widget for <paramref name="entry"/>, wired to read <paramref name="target"/>'s
        /// current value and write edits back to it. Falls back to a read-only <see cref="TextBlock"/> showing
        /// <see cref="object.ToString"/> for any type without a dedicated editor.
        /// </summary>
        /// <param name="entry">The property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <returns>The freshly built editor widget.</returns>
        private UIElement BuildEditor(PropertyGridEntry entry, object target)
        {
            object? value = entry.GetValue(target);

            if (entry.PropertyType == typeof(string))
            {
                var textBox = new TextBox { Text = (string?)value ?? string.Empty, IsEnabled = !entry.IsReadOnly };
                textBox.TextChanged += (_, _) => entry.TrySetValue(target, textBox.Text);
                return textBox;
            }

            if (entry.PropertyType == typeof(bool))
            {
                var checkBox = new CheckBox { IsChecked = value is true, IsEnabled = !entry.IsReadOnly };
                checkBox.IsCheckedChanged += (_, _) => entry.TrySetValue(target, checkBox.IsChecked);
                return checkBox;
            }

            if (IsNumericType(entry.PropertyType))
                return BuildNumericEditor(entry, target, value);

            // TODO: nested/complex-object rows, and dedicated DateOnly/TimeOnly/DateTime/Uri editors, are out of
            // scope for v1 - see docs/superpowers/specs/2026-09-27-propertygrid-design.md. Every such type (and
            // any other type without a dedicated editor above) falls through to this read-only display.
            return new TextBlock { Text = value?.ToString() ?? string.Empty, VerticalAlignment = VerticalAlignment.Center };
        }

        /// <summary>
        /// Builds a numeric editor for <paramref name="entry"/> - a plain <see cref="TextBox"/> when no
        /// <see cref="PropertyGridEntry.Range"/> is present.
        /// </summary>
        /// <param name="entry">The numeric property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">The property's current value.</param>
        /// <returns>The freshly built editor widget.</returns>
        private UIElement BuildNumericEditor(PropertyGridEntry entry, object target, object? value)
        {
            var textBox = new TextBox
            {
                Text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0",
                IsEnabled = !entry.IsReadOnly,
            };
            textBox.TextChanged += (_, _) =>
            {
                if (TryConvertNumeric(textBox.Text, entry.PropertyType, out object? converted))
                    entry.TrySetValue(target, converted);
            };
            return textBox;
        }

        /// <summary>
        /// Determines whether <paramref name="type"/> is one of the numeric primitive types this control
        /// recognizes for its numeric editor branch.
        /// </summary>
        /// <param name="type">The type to check.</param>
        private static bool IsNumericType(Type type) =>
            type == typeof(sbyte) || type == typeof(byte) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong) ||
            type == typeof(float) || type == typeof(double) || type == typeof(decimal);

        /// <summary>
        /// Attempts to parse <paramref name="text"/> into <paramref name="targetType"/>, one of the numeric
        /// types <see cref="IsNumericType"/> recognizes.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <param name="targetType">The numeric type to parse into.</param>
        /// <param name="value">The parsed value, when this method returns <see langword="true"/>.</param>
        private static bool TryConvertNumeric(string text, Type targetType, out object? value)
        {
            value = null;
            if (!double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed))
                return false;

            try
            {
                value = Convert.ChangeType(parsed, targetType, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is InvalidCastException or OverflowException)
            {
                return false;
            }
        }

        /// <summary>
        /// Builds the flat, category-grouped row sequence <see cref="ItemsSource"/> is assigned from - a
        /// <see cref="CategoryHeader"/> before the first row of each newly encountered
        /// <see cref="PropertyGridEntry.Category"/>, in the order <see cref="PropertyGridEntry.EnumerateFor(object)"/>
        /// already groups them in.
        /// </summary>
        /// <param name="target">The object to build rows for, or <see langword="null"/> to produce no rows.</param>
        private static IEnumerable<object> BuildRows(object? target)
        {
            if (target == null)
                yield break;

            string? currentCategory = null;
            foreach (PropertyGridEntry entry in PropertyGridEntry.EnumerateFor(target))
            {
                if (entry.Category != currentCategory)
                {
                    currentCategory = entry.Category;
                    yield return new CategoryHeader(currentCategory);
                }

                yield return entry;
            }
        }

        /// <summary>
        /// A non-interactive divider row inserted before each new <see cref="PropertyGridEntry.Category"/> group.
        /// </summary>
        /// <param name="Name">The category name to display.</param>
        private sealed record CategoryHeader(string Name);
    }
}
```

- [ ] **Step 4: Run the tests, fix any failures, confirm green**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PropertyGridTests"`
Expected: PASS, all 6 tests.

- [ ] **Step 5: Run the full suite to confirm no regressions**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS, 863 pre-existing + all new tests from Tasks 1-2.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/PropertyGrid.cs sources/IcyUI.Tests/Controls/PropertyGridTests.cs
git commit -m "Add PropertyGrid control: string/bool/numeric editors, category headers"
```

---

## Task 3: Enum, Color, and `System.Numerics` editors; unrecognized-type fallback test

**Files:**
- Modify: `sources/IcyUI/UI/Controls/PropertyGrid.cs` (extend `BuildEditor`)
- Modify: `sources/IcyUI.Tests/Controls/PropertyGridTests.cs` (add tests)

**Interfaces:**
- Consumes: Task 1's `PropertyGridEntry`/`EnumValueCache`; Task 2's `BuildEditor` dispatch method and `IsNumericType`/`TryConvertNumeric` helpers; `Icy.UI.Controls.ComboBox` (`SelectingItemsControl.SelectedItem : object?` + `SelectionChanged` event, `ComboBox.ItemsSource : IEnumerable?`); `Icy.UI.Controls.ColorPickerButton` (`SelectedColor : System.Drawing.Color` + `ColorChanged` event).
- Produces: no new public surface - extends `BuildEditor`'s existing dispatch, tested the same way Task 2's editors were.

- [ ] **Step 1: Write the failing tests**

Add to `sources/IcyUI.Tests/Controls/PropertyGridTests.cs`, inside the existing `PropertyGridTests` class (extend `SampleTarget` with the new property types used below, and add these `[Fact]`s):

```csharp
// Add to SampleTarget:
//     public DayOfWeek FavoriteDay { get; set; } = DayOfWeek.Monday;
//     public System.Drawing.Color TintColor { get; set; } = System.Drawing.Color.Red;
//     public System.Numerics.Vector3 Position { get; set; } = new(1f, 2f, 3f);
//     public object Unsupported { get; set; } = new();

[Fact]
public void Target_EnumProperty_RealizesComboBoxWithAllEnumValues()
{
    var target = new SampleTarget();
    var grid = new PropertyGrid { Target = target };
    grid.Measure();

    var comboBox = Assert.IsType<ComboBox>(FindEditor(grid, nameof(SampleTarget.FavoriteDay)));
    Assert.Equal(DayOfWeek.Monday, comboBox.SelectedItem);

    comboBox.SelectedItem = DayOfWeek.Friday;
    Assert.Equal(DayOfWeek.Friday, target.FavoriteDay);
}

[Fact]
public void Target_ColorProperty_RealizesColorPickerButtonBoundToValue()
{
    var target = new SampleTarget();
    var grid = new PropertyGrid { Target = target };
    grid.Measure();

    var button = Assert.IsType<ColorPickerButton>(FindEditor(grid, nameof(SampleTarget.TintColor)));
    Assert.Equal(System.Drawing.Color.Red, button.SelectedColor);

    button.SelectedColor = System.Drawing.Color.Blue;
    Assert.Equal(System.Drawing.Color.Blue, target.TintColor);
}

[Fact]
public void Target_Vector3Property_RealizesThreeComponentTextBoxesBoundToValue()
{
    var target = new SampleTarget();
    var grid = new PropertyGrid { Target = target };
    grid.Measure();

    var editor = Assert.IsType<StackPanel>(FindEditor(grid, nameof(SampleTarget.Position)));
    var boxes = editor.Children.OfType<TextBox>().ToList();
    Assert.Equal(3, boxes.Count);
    Assert.Equal("1", boxes[0].Text);
    Assert.Equal("2", boxes[1].Text);
    Assert.Equal("3", boxes[2].Text);

    boxes[0].Text = "9";
    Assert.Equal(9f, target.Position.X);
}

[Fact]
public void Target_UnrecognizedType_RealizesReadOnlyTextBlockAndDoesNotThrow()
{
    var target = new SampleTarget();
    var grid = new PropertyGrid { Target = target };
    grid.Measure();

    var display = Assert.IsType<TextBlock>(FindEditor(grid, nameof(SampleTarget.Unsupported)));
    Assert.False(string.IsNullOrEmpty(display.Text));
}
```

- [ ] **Step 2: Run to confirm failures**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PropertyGridTests"`
Expected: FAIL - `ComboBox`/`ColorPickerButton`/`StackPanel` editors don't exist for these types yet (falls through to the generic `ToString()` `TextBlock` from Task 2, so the enum/Color/Vector3 assertions fail their `Assert.IsType` checks).

- [ ] **Step 3: Extend `BuildEditor`**

In `sources/IcyUI/UI/Controls/PropertyGrid.cs`, add these branches to `BuildEditor` (after the `IsNumericType` branch, before the fallback `return`), and add the two new private builder methods plus a `using System.Numerics;` and `using System.Drawing;`:

```csharp
if (entry.PropertyType.IsEnum)
    return BuildEnumEditor(entry, target, value);

if (entry.PropertyType == typeof(Color))
    return BuildColorEditor(entry, target, value);

if (entry.PropertyType == typeof(Vector2))
    return BuildVectorEditor(entry, target, ["X", "Y"], v => new Vector2((float)v[0], (float)v[1]), v => [((Vector2)v!).X, ((Vector2)v).Y]);

if (entry.PropertyType == typeof(Vector3))
    return BuildVectorEditor(entry, target, ["X", "Y", "Z"], v => new Vector3((float)v[0], (float)v[1], (float)v[2]), v => [((Vector3)v!).X, ((Vector3)v).Y, ((Vector3)v).Z]);

if (entry.PropertyType == typeof(Vector4))
    return BuildVectorEditor(entry, target, ["X", "Y", "Z", "W"], v => new Vector4((float)v[0], (float)v[1], (float)v[2], (float)v[3]), v => [((Vector4)v!).X, ((Vector4)v).Y, ((Vector4)v).Z, ((Vector4)v).W]);

if (entry.PropertyType == typeof(Quaternion))
    return BuildVectorEditor(entry, target, ["X", "Y", "Z", "W"], v => new Quaternion((float)v[0], (float)v[1], (float)v[2], (float)v[3]), v => [((Quaternion)v!).X, ((Quaternion)v).Y, ((Quaternion)v).Z, ((Quaternion)v).W]);

if (entry.PropertyType == typeof(Matrix3x2))
    return BuildVectorEditor(entry, target, ["M11", "M12", "M21", "M22", "M31", "M32"], BuildMatrix3x2, DecomposeMatrix3x2);

if (entry.PropertyType == typeof(Matrix4x4))
    return BuildVectorEditor(entry, target, ["M11", "M12", "M13", "M14", "M21", "M22", "M23", "M24", "M31", "M32", "M33", "M34", "M41", "M42", "M43", "M44"], BuildMatrix4x4, DecomposeMatrix4x4);
```

```csharp
/// <summary>
/// Builds a <see cref="ComboBox"/> editor over every value of <paramref name="entry"/>'s enum type.
/// </summary>
/// <param name="entry">The enum-typed property this row edits.</param>
/// <param name="target">The object <paramref name="entry"/> belongs to.</param>
/// <param name="value">The property's current value.</param>
private UIElement BuildEnumEditor(PropertyGridEntry entry, object target, object? value)
{
    var comboBox = new ComboBox
    {
        ItemsSource = EnumValueCache.GetValues(entry.PropertyType),
        SelectedItem = value,
        IsEnabled = !entry.IsReadOnly,
    };
    comboBox.SelectionChanged += (_, _) => entry.TrySetValue(target, comboBox.SelectedItem);
    return comboBox;
}

/// <summary>
/// Builds a <see cref="ColorPickerButton"/> editor for a <see cref="Color"/>-typed property.
/// </summary>
/// <param name="entry">The color-typed property this row edits.</param>
/// <param name="target">The object <paramref name="entry"/> belongs to.</param>
/// <param name="value">The property's current value.</param>
private UIElement BuildColorEditor(PropertyGridEntry entry, object target, object? value)
{
    var button = new ColorPickerButton
    {
        SelectedColor = value is Color color ? color : Color.White,
        IsEnabled = !entry.IsReadOnly,
    };
    button.ColorChanged += (_, _) => entry.TrySetValue(target, button.SelectedColor);
    return button;
}

/// <summary>
/// Builds a composite row of one labeled numeric <see cref="TextBox"/> per component for a
/// <c>System.Numerics</c> vector/quaternion/matrix-typed property - <paramref name="componentNames"/> in
/// display order, <paramref name="compose"/> rebuilding the struct from every field's parsed value on any
/// edit, <paramref name="decompose"/> reading the current per-component values back out.
/// </summary>
/// <param name="entry">The property this row edits.</param>
/// <param name="target">The object <paramref name="entry"/> belongs to.</param>
/// <param name="componentNames">The component labels, in display order.</param>
/// <param name="compose">Builds the struct value from the parsed component values, in the same order.</param>
/// <param name="decompose">Reads the struct's current component values back out, in the same order.</param>
private UIElement BuildVectorEditor(
    PropertyGridEntry entry,
    object target,
    string[] componentNames,
    Func<double[], object> compose,
    Func<object?, double[]> decompose)
{
    var panel = new StackPanel { Orientation = Orientation.Horizontal };
    double[] current = decompose(entry.GetValue(target));
    var boxes = new TextBox[componentNames.Length];

    for (int i = 0; i < componentNames.Length; i++)
    {
        int index = i;
        var box = new TextBox
        {
            Text = current[i].ToString(System.Globalization.CultureInfo.InvariantCulture),
            IsEnabled = !entry.IsReadOnly,
        };
        boxes[i] = box;
        box.TextChanged += (_, _) =>
        {
            var values = new double[componentNames.Length];
            for (int j = 0; j < componentNames.Length; j++)
            {
                if (!double.TryParse(boxes[j].Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out values[j]))
                    return;
            }

            entry.TrySetValue(target, compose(values));
        };
        panel.Children.Add(box);
    }

    return panel;
}

private static Matrix3x2 BuildMatrix3x2(double[] v) => new((float)v[0], (float)v[1], (float)v[2], (float)v[3], (float)v[4], (float)v[5]);

private static double[] DecomposeMatrix3x2(object? value)
{
    var m = (Matrix3x2)(value ?? Matrix3x2.Identity);
    return [m.M11, m.M12, m.M21, m.M22, m.M31, m.M32];
}

private static Matrix4x4 BuildMatrix4x4(double[] v) => new(
    (float)v[0], (float)v[1], (float)v[2], (float)v[3],
    (float)v[4], (float)v[5], (float)v[6], (float)v[7],
    (float)v[8], (float)v[9], (float)v[10], (float)v[11],
    (float)v[12], (float)v[13], (float)v[14], (float)v[15]);

private static double[] DecomposeMatrix4x4(object? value)
{
    var m = (Matrix4x4)(value ?? Matrix4x4.Identity);
    return [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
}
```

Before wiring this in, check `sources/IcyUI/UI/Controls/ComboBox.cs` for its actual `SelectedItem` setter behavior when the assigned value isn't already present in `ItemsSource` at assignment time (a `Selector`/`SelectingItemsControl` concern - confirm setting `SelectedItem` before or after `ItemsSource` is populated behaves as expected; if `ItemsSource` must be set first, reorder the object-initializer properties above accordingly) - this is exactly the kind of "verify against the real file, don't assume" step this project's own process repeatedly calls for.

- [ ] **Step 4: Run the tests, fix any failures, confirm green**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~PropertyGridTests"`
Expected: PASS, all 10 tests (6 from Task 2 + 4 new).

- [ ] **Step 5: Run the full suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS, no regressions.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/PropertyGrid.cs sources/IcyUI.Tests/Controls/PropertyGridTests.cs
git commit -m "Add PropertyGrid enum/Color/System.Numerics editors"
```

---

## Task 4: Theming, sample, and both-host registration

**Files:**
- Modify: `sources/IcyUI/Resources/Themes/DefaultTheme.xml`
- Create: `sources/Shared Samples/PropertyGridDemo.cs`
- Create: `sources/MonoGame Sample/Samples/PropertyGridSample.cs`
- Modify: `sources/MonoGame Sample/SampleGame.cs`
- Modify: `sources/Stride Sample/SampleGame.cs`

**Interfaces:**
- Consumes: `Icy.UI.Controls.PropertyGrid` (Tasks 2-3's public surface: `Target : object?`); the `TabControlSample`/`ColorPickerSample`/`TabControlDemo`/`ColorPickerDemo` registration pattern from this session's prior commits (`7092f0d`).

- [ ] **Step 1: Read the exact registration precedent before editing**

Read `sources/MonoGame Sample/Samples/TabControlSample.cs`, `sources/Shared Samples/TabControlDemo.cs`, and the `TabControlDemo`/`ColorPickerDemo` registration lines in both `sources/MonoGame Sample/SampleGame.cs` and `sources/Stride Sample/SampleGame.cs` (added in commit `7092f0d`) in full before writing anything - this task's file changes must match that established shape exactly (the array-index-based `SamplesRunner` registration for MonoGame, the field+`Build`/`Add`/`UpdateSelectedDemo`+`% N` divisor bump for Stride), not a newly invented one.

- [ ] **Step 2: Add the `DefaultTheme.xml` entry**

In `sources/IcyUI/Resources/Themes/DefaultTheme.xml`, immediately after the existing `TabControl` style (the untemplated `TextBox`/`ScrollViewer`/`Window`/`TabControl` block found earlier in this session):

```xml
<!-- PropertyGrid: untemplated, like TabControl/TextBox/ScrollViewer/Window above - realizes its own rows
     directly, not via Chrome, so plain Border-level decoration is all it needs. -->
<Style TargetType="PropertyGrid" Background="#FF26262C" BorderBrush="#FF56566A" BorderThickness="1"/>
```

- [ ] **Step 3: Write `PropertyGridDemo`**

Create `sources/Shared Samples/PropertyGridDemo.cs`, following `TabControlDemo.cs`'s established shape (a static `Build(IcyConfiguration configuration, string fontFamily)` factory returning a root `UIElement`). It must define a small demo POCO exercising every v1 editor type (string, bool, numeric with and without `[Range]`, enum, `System.Drawing.Color`, at least one `System.Numerics` type) and a second, differently-shaped POCO, plus a `Button` that toggles `PropertyGrid.Target` between instances of the two - this is the pooling-off regression case from the spec's Testing section, made visually inspectable, not just unit-tested. Ground every constructor/property call against the real `TabControlDemo.cs`/`ColorPickerDemo.cs` files read in Step 1, not this plan's own sketch.

- [ ] **Step 4: Register in both sample hosts**

Add `PropertyGridSample.cs` to `MonoGame Sample` and the matching field/`Build`/`Add`/`UpdateSelectedDemo`/`% N` changes to both `SampleGame.cs` files, exactly mirroring how `TabControlDemo`/`ColorPickerDemo` were wired in commit `7092f0d` (bump the MonoGame `SamplesRunner` array and the Stride `% N` divisor by one each, for the new demo).

- [ ] **Step 5: Build both sample projects**

Run: `dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj"`
Run: `dotnet build "sources/Stride Sample/Stride Sample.csproj"`
Expected: both build with 0 errors.

- [ ] **Step 6: Run the full test suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS, no regressions.

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/Resources/Themes/DefaultTheme.xml "sources/Shared Samples/PropertyGridDemo.cs" "sources/MonoGame Sample/Samples/PropertyGridSample.cs" "sources/MonoGame Sample/SampleGame.cs" "sources/Stride Sample/SampleGame.cs"
git commit -m "Add PropertyGrid theming, demo, and both-host registration"
```

---

## Final step (not a task - handled by the execution skill)

A whole-branch review before merge, per this project's own established practice for every prior Tier-2 phase - specifically re-probe the `Target`-reassignment/pooling-off behavior and the numeric/vector parse-on-edit paths behaviorally (build and actually run the demo), not just via the unit tests above. Then a manual smoke test on both engines, with Ivan notified first per `[[feedback_smoke_test_notification]]`.
