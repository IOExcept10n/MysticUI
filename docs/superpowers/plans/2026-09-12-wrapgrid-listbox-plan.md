# Tier-2 Phase 6 — WrapGrid, ListBox, and the SelectingItemsControl Extraction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Tasks 4 and 5 (`WrapGrid`, `ListBox`) have no dependency on each other and are meant to be dispatched in parallel** once Tasks 1-3 are committed - see each task's Interfaces block.

**Goal:** Ship `WrapGrid` (auto-flowing virtualizing grid layout) and `ListBox` (plain always-visible virtualizing selectable list), both deriving from a new shared `SelectingItemsControl` base extracted out of `Selector`, on top of `ItemsControl`'s virtualization geometry widened to be pluggable.

**Architecture:** `ItemsControl`'s `RealizeRange`/`LocateViewportStart`/`ExtentHeight`/`EnsureRealized`/`Derealize` and the offset fields they use are widened from `private` to `protected`/`protected virtual` - a mechanical, behavior-preserving change (Task 1). `SelectedIndex`/`SelectedItem`/`SelectionChanged`/`CreateContainer` are hoisted out of `Selector` into a new abstract `SelectingItemsControl : ItemsControl` (Task 2), which `Selector` now derives from instead of `ItemsControl` directly - every other line in `Selector.cs` is untouched. `SelectorItem` gains a `Tapped` event for click-to-select (Task 3). `WrapGrid : SelectingItemsControl` (Task 4) overrides the now-virtual geometry hooks for uniform-cell grid math; `ListBox : SelectingItemsControl` (Task 5) overrides none of them, inheriting `ItemsControl`'s default vertical-list behavior as-is - both wire `SelectorItem.Tapped` for click-to-select. A final task (Task 6) registers both demos in the sample hosts and runs full verification.

**Tech Stack:** C# / .NET, IcyUI's own markup+styling system, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-12-wrapgrid-design.md`

## Global Constraints

- No cross-engine impact - pure core-`IcyUI` work. `IcyUI.MonoGame`/`IcyUI.Stride` only touched in Task 6 (sample registration); `IcyUI.FNA` (still an unimplemented stub) needs no equivalent work.
- All public APIs get complete XML doc comments (summary + remarks where non-obvious), matching every existing control in this codebase.
- Tasks 1-3 must land, in order, before Tasks 4/5 start - both `WrapGrid` and `ListBox` derive from `SelectingItemsControl` (Task 2) and depend on `ItemsControl`'s widened geometry hooks (Task 1) and `SelectorItem.Tapped` (Task 3).
- Tasks 4 and 5 touch entirely disjoint files (`WrapGrid.cs`/`WrapGridTests.cs`/`WrapGridDemo.cs` vs. `ListBox.cs`/`ListBoxTests.cs`/`ListBoxDemo.cs`) and share no interface surface beyond what Tasks 1-3 already provide - safe to implement concurrently.
- Neither Task 4 nor Task 5 touches `MonoGame Sample/SampleGame.cs` or `Stride Sample/SampleGame.cs` - both would need to edit the same two files, which is deferred to Task 6 (sequential, after both land) specifically to avoid a merge conflict between parallel work.

---

## Task 1: `ItemsControl` virtualization-geometry visibility widening

**Files:**
- Modify: `sources/IcyUI/UI/Controls/ItemsControl.cs`
- Test: `sources/IcyUI.Tests/Controls/ItemsControlTests.cs`

**Interfaces:**
- Produces: `protected float horizontalOffset/verticalOffset/viewportWidth/viewportHeight` (were `private`), `protected virtual float ComputeExtentHeight()`, `protected virtual (int Index, float Offset) LocateViewportStart()` (was `private`), `protected virtual void RealizeRange(int firstIndex, float firstOffset)` (was `private`), `protected void EnsureRealized(int index)`/`protected void Derealize(int index)` (were `private`) - all consumed by `WrapGrid` (Task 4).

- [ ] **Step 1: Write the failing test**

Add to `ItemsControlTests.cs` (reuses the file's existing `LoadDataTemplate`/`GetRealizedContainers` helpers):

```csharp
private sealed class GeometryOverrideItemsControl : ItemsControl
{
    public bool LocateViewportStartCalled;
    public bool RealizeRangeCalled;
    public bool ComputeExtentHeightCalled;
    public float ObservedVerticalOffset;
    public float ObservedViewportHeight;

    public void CallEnsureRealized(int index) => EnsureRealized(index);

    public void CallDerealize(int index) => Derealize(index);

    protected override (int Index, float Offset) LocateViewportStart()
    {
        LocateViewportStartCalled = true;
        return base.LocateViewportStart();
    }

    protected override void RealizeRange(int firstIndex, float firstOffset)
    {
        RealizeRangeCalled = true;
        ObservedVerticalOffset = verticalOffset;
        ObservedViewportHeight = viewportHeight;
        base.RealizeRange(firstIndex, firstOffset);
    }

    protected override float ComputeExtentHeight()
    {
        ComputeExtentHeightCalled = true;
        return base.ComputeExtentHeight();
    }
}

[Fact]
public void VirtualizationGeometryHooks_CanBeOverridden_AndBaseStillWorksThroughThem()
{
    var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
    var control = new GeometryOverrideItemsControl
    {
        ItemsSource = Enumerable.Range(0, 10).Cast<object>().ToList(),
        ItemTemplate = template,
    };

    ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 0, 300, 400);
    float extent = control.ExtentHeight;

    Assert.True(control.LocateViewportStartCalled);
    Assert.True(control.RealizeRangeCalled);
    Assert.True(control.ComputeExtentHeightCalled);
    Assert.Equal(0f, control.ObservedVerticalOffset);
    Assert.Equal(400f, control.ObservedViewportHeight);
    Assert.Equal(10 * 40f, extent);
    Assert.NotEmpty(GetRealizedContainers(control));
}

[Fact]
public void EnsureRealized_Derealize_AreCallableDirectlyFromASubclass()
{
    var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
    var control = new GeometryOverrideItemsControl
    {
        ItemsSource = new List<object> { "a", "b" },
        ItemTemplate = template,
    };

    control.CallEnsureRealized(0);
    Assert.Contains(0, GetRealizedContainers(control).Keys);

    control.CallDerealize(0);
    Assert.DoesNotContain(0, GetRealizedContainers(control).Keys);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run (see the `build-test` skill for the exact command): `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: FAIL to compile - `LocateViewportStart`/`RealizeRange`/`ComputeExtentHeight`/`EnsureRealized`/`Derealize` aren't accessible/don't exist as overridable members yet, `verticalOffset`/`viewportHeight` aren't accessible from a subclass.

- [ ] **Step 3: Widen the offset fields**

In `ItemsControl.cs`, change:

```csharp
private float horizontalOffset;
private float verticalOffset;
private float viewportWidth;
private float viewportHeight;
```
→
```csharp
protected float horizontalOffset;
protected float verticalOffset;
protected float viewportWidth;
protected float viewportHeight;
```

- [ ] **Step 4: Split `ExtentHeight` into a virtual `ComputeExtentHeight()`**

Change:

```csharp
/// <inheritdoc/>
public float ExtentHeight => sumOfKnownHeights + ((items.Count - knownCount) * AverageHeight);
```
→
```csharp
/// <inheritdoc/>
public float ExtentHeight => ComputeExtentHeight();

/// <summary>
/// Computes <see cref="ExtentHeight"/> - the running-average single-column estimate by default. A subclass
/// with different virtualization geometry (e.g. a uniform grid) overrides this with its own formula instead.
/// </summary>
protected virtual float ComputeExtentHeight() => sumOfKnownHeights + ((items.Count - knownCount) * AverageHeight);
```

- [ ] **Step 5: Widen `LocateViewportStart` and `RealizeRange` to `protected virtual`**

Change:

```csharp
private (int Index, float Offset) LocateViewportStart()
```
→
```csharp
protected virtual (int Index, float Offset) LocateViewportStart()
```

And change:

```csharp
private void RealizeRange(int firstIndex, float firstOffset)
```
→
```csharp
protected virtual void RealizeRange(int firstIndex, float firstOffset)
```

Update both methods' XML doc `<summary>` to note they're overridable (add one sentence each: "A subclass with different virtualization geometry overrides this to replace the algorithm entirely.").

- [ ] **Step 6: Widen `EnsureRealized` and `Derealize` to `protected`**

Change:

```csharp
private void EnsureRealized(int index)
```
→
```csharp
protected void EnsureRealized(int index)
```

And change:

```csharp
private void Derealize(int index)
```
→
```csharp
protected void Derealize(int index)
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: PASS, including every pre-existing test in the file (this step is the confirmation that widening alone didn't change any behavior).

- [ ] **Step 8: Run the full test suite to check for wider regressions**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS, all 744+ tests (this change touches a class `Selector`/`Dropdown`/`ComboBox`/every `ItemsControl` consumer depends on).

- [ ] **Step 9: Commit**

```bash
git add sources/IcyUI/UI/Controls/ItemsControl.cs sources/IcyUI.Tests/Controls/ItemsControlTests.cs
git commit -m "$(cat <<'EOF'
Widen ItemsControl's virtualization geometry to protected virtual

Mechanical visibility widening only - RealizeRange/LocateViewportStart/
ExtentHeight's body/EnsureRealized/Derealize and the offset fields they
use move from private to protected(/virtual), with zero change to the
base class's own runtime behavior. Lets a subclass with different
virtualization geometry (WrapGrid's uniform grid, next) replace just the
geometry computation while reusing pooling/CreateContainer/collection-
change handling as-is.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

---

## Task 2: `SelectingItemsControl` extraction

**Files:**
- Create: `sources/IcyUI/UI/Controls/SelectingItemsControl.cs`
- Test: `sources/IcyUI.Tests/Controls/SelectingItemsControlTests.cs`
- Modify: `sources/IcyUI/UI/Controls/Selector.cs`
- Test: `sources/IcyUI.Tests/Controls/SelectorTests.cs`

**Interfaces:**
- Consumes: `ItemsControl.realizedContainers`/`ItemCount`/`GetItemAt`/`IndexOfItem`/`CreateContainer` (already `protected`(`virtual`) since Phase 5).
- Produces: `public abstract class SelectingItemsControl : ItemsControl` with `int SelectedIndex`, `object? SelectedItem`, `event EventHandler? SelectionChanged`, `protected virtual void OnSelectionChanged()` - consumed by `Selector` (this task), `WrapGrid`/`ListBox` (Tasks 4-5).

- [ ] **Step 1: Write the failing tests**

Create `sources/IcyUI.Tests/Controls/SelectingItemsControlTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class SelectingItemsControlTests
    {
        private sealed class TestSelectingItemsControl : SelectingItemsControl
        {
        }

        private sealed class TrackingSelectingItemsControl : SelectingItemsControl
        {
            private int nextOrder;

            public int? HookOrder { get; private set; }

            public int? EventOrder { get; private set; }

            public TrackingSelectingItemsControl()
            {
                SelectionChanged += (_, _) => EventOrder = nextOrder++;
            }

            protected override void OnSelectionChanged() => HookOrder = nextOrder++;
        }

        [Fact]
        public void Defaults_MatchSpec()
        {
            var control = new TestSelectingItemsControl();

            Assert.Equal(-1, control.SelectedIndex);
            Assert.Null(control.SelectedItem);
        }

        [Fact]
        public void SelectedIndex_OutOfRange_Throws()
        {
            var control = new TestSelectingItemsControl { ItemsSource = new List<object> { "a", "b" } };

            Assert.Throws<ArgumentOutOfRangeException>(() => control.SelectedIndex = 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => control.SelectedIndex = -2);
        }

        [Fact]
        public void SelectedIndex_And_SelectedItem_StaySynchronized()
        {
            var control = new TestSelectingItemsControl { ItemsSource = new List<object> { "a", "b", "c" } };

            control.SelectedIndex = 1;
            Assert.Equal("b", control.SelectedItem);

            control.SelectedItem = "c";
            Assert.Equal(2, control.SelectedIndex);
        }

        [Fact]
        public void SelectedItem_NotInTheList_ResolvesToNoSelection()
        {
            var control = new TestSelectingItemsControl { ItemsSource = new List<object> { "a", "b" } };

            control.SelectedItem = "not in the list";

            Assert.Equal(-1, control.SelectedIndex);
            Assert.Null(control.SelectedItem);
        }

        [Fact]
        public void SelectionChanged_FiresOncePerRealChange()
        {
            var control = new TestSelectingItemsControl { ItemsSource = new List<object> { "a", "b" } };
            int raised = 0;
            control.SelectionChanged += (_, _) => raised++;

            control.SelectedIndex = 0;
            control.SelectedIndex = 0;

            Assert.Equal(1, raised);
        }

        [Fact]
        public void CreateContainer_RealizesSelectorItemsNotBareItemContainers()
        {
            var control = new TestSelectingItemsControl
            {
                ItemsSource = new List<object> { "a" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };

            InvokeEnsureRealized(control, 0);

            Assert.IsType<SelectorItem>(GetRealizedContainers(control)[0]);
        }

        [Fact]
        public void SelectionState_SurvivesPoolAndReuseCycle()
        {
            var control = new TestSelectingItemsControl
            {
                ItemsSource = new List<object> { "a", "b" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            control.SelectedIndex = 0;
            InvokeEnsureRealized(control, 0);
            Assert.True(((SelectorItem)GetRealizedContainers(control)[0]).IsSelected);

            InvokeDerealize(control, 0);
            InvokeEnsureRealized(control, 0);

            Assert.True(((SelectorItem)GetRealizedContainers(control)[0]).IsSelected);
        }

        [Fact]
        public void OnSelectionChanged_FiresBeforeThePublicSelectionChangedEvent()
        {
            var control = new TrackingSelectingItemsControl { ItemsSource = new List<object> { "a", "b" } };

            control.SelectedIndex = 0;

            Assert.Equal(0, control.HookOrder);
            Assert.Equal(1, control.EventOrder);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        private static void InvokeEnsureRealized(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("EnsureRealized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static void InvokeDerealize(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("Derealize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SelectingItemsControlTests"`
Expected: FAIL to compile - `SelectingItemsControl` doesn't exist yet.

- [ ] **Step 3: Create `SelectingItemsControl.cs`**

Create `sources/IcyUI/UI/Controls/SelectingItemsControl.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup.Attributes;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Adds single-item selection on top of <see cref="ItemsControl"/> - the shared base for
    /// <see cref="Selector"/> (adds a popup on top of this) and <see cref="WrapGrid"/>/<see cref="ListBox"/>
    /// (always-visible, no popup).
    /// </summary>
    /// <remarks>
    /// Realizes <see cref="SelectorItem"/>s (see <see cref="CreateContainer(DataTemplate, object)"/>) instead
    /// of bare <see cref="ItemContainer"/>s, and keeps each realized item's <see cref="SelectorItem.IsSelected"/>
    /// in sync with <see cref="SelectedIndex"/> - including across a pool-and-reuse cycle, since a selected
    /// item's container can be de-realized and rebuilt later by virtualization.
    /// </remarks>
    public abstract class SelectingItemsControl : ItemsControl
    {
        private int selectedIndex = -1;
        private object? selectedItem;

        /// <summary>
        /// Occurs when <see cref="SelectedIndex"/>/<see cref="SelectedItem"/> changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Gets or sets the currently selected item's index, or <c>-1</c> for no selection.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than <c>-1</c> or greater than or equal to the item count.</exception>
        [Category("Behavior")]
        [DefaultValue(-1)]
        [RegisterReference]
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                Guard.IsGreaterThanOrEqualTo(value, -1);
                if (value != -1)
                    Guard.IsLessThan(value, ItemCount);
                if (selectedIndex == value)
                    return;

                if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? oldContainer))
                    ((SelectorItem)oldContainer).IsSelected = false;

                selectedIndex = value;
                selectedItem = selectedIndex == -1 ? null : GetItemAt(selectedIndex);

                OnSelectionChanged();

                if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? newContainer))
                    ((SelectorItem)newContainer).IsSelected = true;

                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the currently selected item, or <see langword="null"/> for no selection. Setting an item
        /// not present in <see cref="ItemsControl.ItemsSource"/> clears selection instead of throwing.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? SelectedItem
        {
            get => selectedItem;
            set => SelectedIndex = IndexOfItem(value);
        }

        /// <inheritdoc/>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
            => new SelectorItem { Content = template.Build(item) };

        /// <summary>
        /// Called after <see cref="SelectedIndex"/>/<see cref="SelectedItem"/> have been updated, before the
        /// realized-container stamp and the public <see cref="SelectionChanged"/> event - empty by default. A
        /// subclass with derived state of its own that depends on the new selection (e.g. <see cref="Selector"/>'s
        /// <see cref="Selector.SelectedValue"/>) overrides this to refresh it, rather than reacting to its own
        /// <see cref="SelectionChanged"/> subscription (which would depend on subscriber-ordering relative to any
        /// external subscriber this control's own consumer adds).
        /// </summary>
        protected virtual void OnSelectionChanged()
        {
        }
    }
}
```

- [ ] **Step 4: Run the new tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SelectingItemsControlTests"`
Expected: PASS.

- [ ] **Step 5: Commit `SelectingItemsControl`**

```bash
git add sources/IcyUI/UI/Controls/SelectingItemsControl.cs sources/IcyUI.Tests/Controls/SelectingItemsControlTests.cs
git commit -m "$(cat <<'EOF'
Add SelectingItemsControl

New abstract base between ItemsControl and Selector - SelectedIndex/
SelectedItem/SelectionChanged/CreateContainer, factored out ahead of
extracting them from Selector in the next commit. Lets WrapGrid/ListBox
(next) get the same selection state without Selector's popup lifecycle.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

- [ ] **Step 6: Remove the now-duplicated members from `Selector.cs`**

In `sources/IcyUI/UI/Controls/Selector.cs`, delete these two fields (they're `private int selectedIndex = -1;` and `private object? selectedItem;` in the field block near the top of the class):

```csharp
        private int selectedIndex = -1;
        private object? selectedItem;
```

Delete the `SelectionChanged` event:

```csharp
        /// <summary>
        /// Occurs when <see cref="SelectedIndex"/>/<see cref="SelectedItem"/> changes.
        /// </summary>
        public event EventHandler? SelectionChanged;
```

Delete the `SelectedIndex` property in full:

```csharp
        /// <summary>
        /// Gets or sets the currently selected item's index, or <c>-1</c> for no selection.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is less than <c>-1</c> or greater than or equal to the item count.</exception>
        [Category("Behavior")]
        [DefaultValue(-1)]
        [RegisterReference]
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                Guard.IsGreaterThanOrEqualTo(value, -1);
                if (value != -1)
                    Guard.IsLessThan(value, ItemCount);
                if (selectedIndex == value)
                    return;

                if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? oldContainer))
                    ((SelectorItem)oldContainer).IsSelected = false;

                selectedIndex = value;
                selectedItem = selectedIndex == -1 ? null : GetItemAt(selectedIndex);
                UpdateSelectedValue();

                if (realizedContainers.TryGetValue(selectedIndex, out ItemContainer? newContainer))
                    ((SelectorItem)newContainer).IsSelected = true;

                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
        }
```

Delete the `SelectedItem` property in full:

```csharp
        /// <summary>
        /// Gets or sets the currently selected item, or <see langword="null"/> for no selection. Setting an item
        /// not present in <see cref="ItemsControl.ItemsSource"/> clears selection instead of throwing.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? SelectedItem
        {
            get => selectedItem;
            set => SelectedIndex = IndexOfItem(value);
        }
```

Delete the `CreateContainer` override:

```csharp
        /// <inheritdoc/>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
            => new SelectorItem { Content = template.Build(item) };
```

Change the class declaration and its `<remarks>` doc to reference the new base:

```diff
-    public abstract class Selector : ItemsControl
+    public abstract class Selector : SelectingItemsControl
```

(Adjust the class's own `<summary>`/`<remarks>` XML doc that currently says "on top of `ItemsControl`" to say "on top of `SelectingItemsControl`" instead - one sentence, no other wording change needed.)

Add the restored `UpdateSelectedValue()` call as an `OnSelectionChanged` override, placed near `UpdateSelectedValue`'s own private method (which stays exactly as it is, unchanged):

```csharp
        /// <inheritdoc/>
        protected override void OnSelectionChanged() => UpdateSelectedValue();
```

- [ ] **Step 7: Run the regression suites to verify nothing broke**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SelectorTests|FullyQualifiedName~DropdownTests|FullyQualifiedName~ComboBoxTests"`
Expected: PASS, every pre-existing test unchanged - this is the confirmation that the hoist didn't alter `Selector`/`Dropdown`/`ComboBox` behavior. In particular, confirm `DisplayMemberPath_And_SelectedValuePath_ResolveAgainstHeterogeneousItems` in `SelectorTests.cs` still passes - it's the existing test that exercises `UpdateSelectedValue()` running at the right point via the new `OnSelectionChanged` hook.

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS, all tests.

- [ ] **Step 9: Commit**

```bash
git add sources/IcyUI/UI/Controls/Selector.cs
git commit -m "$(cat <<'EOF'
Extract SelectedIndex/SelectedItem/SelectionChanged out of Selector

Selector now derives from SelectingItemsControl instead of ItemsControl
directly - a pure hoist, every other line in Selector.cs (AttachContainer,
OnPopupItemTap, the nav handlers, PopupItemsHost) is unchanged, since they
already only reference SelectedIndex/HighlightedIndex/etc. by name, which
still resolves via inheritance. UpdateSelectedValue()'s call moves from
inline in the old setter to a new OnSelectionChanged() override, at the
same point in the sequence.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

---

## Task 3: `SelectorItem.Tapped`

**Files:**
- Modify: `sources/IcyUI/UI/Controls/SelectorItem.cs`
- Test: `sources/IcyUI.Tests/Controls/SelectorItemTests.cs`

**Interfaces:**
- Produces: `public event EventHandler? Tapped` on `SelectorItem` - consumed by `WrapGrid`/`ListBox` (Tasks 4-5).

- [ ] **Step 1: Write the failing test**

Add to `sources/IcyUI.Tests/Controls/SelectorItemTests.cs` (the file already exists from Phase 5 - add alongside its existing tests, reusing its existing usings):

```csharp
[Fact]
public void OnTap_RaisesTapped()
{
    var item = new SelectorItem();
    bool raised = false;
    item.Tapped += (_, _) => raised = true;

    InvokeOnTap(item);

    Assert.True(raised);
}

[Fact]
public void OnTap_NoSubscribers_DoesNotThrow()
{
    var item = new SelectorItem();

    var exception = Record.Exception(() => InvokeOnTap(item));

    Assert.Null(exception);
}

private static void InvokeOnTap(SelectorItem item) =>
    typeof(SelectorItem).GetMethod("OnTap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(item, null);
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SelectorItemTests"`
Expected: FAIL to compile - `SelectorItem.Tapped` doesn't exist yet.

- [ ] **Step 3: Add `Tapped` and the `OnTap` override**

In `sources/IcyUI/UI/Controls/SelectorItem.cs`, add after the `IsHighlighted` property (before the closing brace of the class):

```csharp
        /// <summary>
        /// Occurs when this item is tapped - raised from <see cref="OnTap"/>. Used by <see cref="WrapGrid"/>/
        /// <see cref="ListBox"/> for click-to-select, since their realized items live in the normal visual tree
        /// (unlike <see cref="Selector"/>'s popup items, which use their own separate mechanism - see
        /// <see cref="Selector"/>'s own remarks).
        /// </summary>
        public event EventHandler? Tapped;

        /// <inheritdoc/>
        protected internal override void OnTap()
        {
            base.OnTap();
            Tapped?.Invoke(this, EventArgs.Empty);
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SelectorItemTests"`
Expected: PASS.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS, all tests (this is purely additive - confirms `Selector`/`Dropdown`/`ComboBox`, which don't subscribe to `Tapped`, are unaffected).

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/SelectorItem.cs sources/IcyUI.Tests/Controls/SelectorItemTests.cs
git commit -m "$(cat <<'EOF'
Add SelectorItem.Tapped

Purely additive - mirrors Button's own OnTap-to-Click shape. Foundation
for WrapGrid/ListBox's click-to-select (next): since their realized items
live in the normal visual tree, Canvas.OnTap's existing per-element
dispatch reaches this directly, no manual re-HitTest workaround needed.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

---

## Task 4: `WrapGrid`

> **Parallelizable with Task 5** - starts only after Tasks 1-3 are committed; touches no files Task 5 touches.

**Files:**
- Create: `sources/IcyUI/UI/Controls/WrapGrid.cs`
- Test: `sources/IcyUI.Tests/Controls/WrapGridTests.cs`
- Create: `sources/Shared Samples/WrapGridDemo.cs`

**Interfaces:**
- Consumes: `SelectingItemsControl.SelectedIndex`/`SelectedItem`/`SelectionChanged` (Task 2); `ItemsControl.ComputeExtentHeight`/`LocateViewportStart`/`RealizeRange`/`EnsureRealized`/`Derealize`/`verticalOffset`/`viewportHeight`/`realizedContainers`/`ItemCount` (Task 1, plus the pre-existing `protected` surface from Phase 5); `SelectorItem.Tapped` (Task 3).
- Produces: `public class WrapGrid : SelectingItemsControl` with `float ItemWidth`, `float ItemHeight` - not consumed by any other task in this plan, but a public API surface.

- [ ] **Step 1: Write the failing tests for properties and defaults**

Create `sources/IcyUI.Tests/Controls/WrapGridTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class WrapGridTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var grid = new WrapGrid();

            Assert.Equal(-1, grid.SelectedIndex);
            Assert.Null(grid.SelectedItem);
            Assert.Equal(64f, grid.ItemWidth);
            Assert.Equal(64f, grid.ItemHeight);
        }

        [Fact]
        public void ItemWidth_ItemHeight_RejectNonPositiveValues()
        {
            var grid = new WrapGrid();

            Assert.Throws<ArgumentException>(() => grid.ItemWidth = 0f);
            Assert.Throws<ArgumentException>(() => grid.ItemWidth = -1f);
            Assert.Throws<ArgumentException>(() => grid.ItemHeight = 0f);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: FAIL to compile - `WrapGrid` doesn't exist yet.

- [ ] **Step 3: Create `WrapGrid.cs` with `ItemWidth`/`ItemHeight`**

Create `sources/IcyUI/UI/Controls/WrapGrid.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using CommunityToolkit.Diagnostics;
using Icy.Data.Markup.Attributes;

namespace Icy.UI.Controls
{
    /// <summary>
    /// An auto-flowing, virtualizing grid of uniformly-sized cells - realized items fill left-to-right and wrap
    /// to the next row, e.g. an icon/inventory grid.
    /// </summary>
    /// <remarks>
    /// Overrides <see cref="ItemsControl"/>'s virtualization geometry (<see cref="LocateViewportStart"/>/
    /// <see cref="RealizeRange(int, float)"/>/<see cref="ComputeExtentHeight"/>) with exact row/column
    /// arithmetic instead of the base's single-column variable-height estimation - every cell is a fixed,
    /// known-upfront <see cref="ItemWidth"/>×<see cref="ItemHeight"/> size, so no height cache or anchor-walk is
    /// needed. Pooling, <see cref="ItemsControl.CreateContainer(Styles.DataTemplate, object)"/> (realizes
    /// <see cref="SelectorItem"/>s, inherited via <see cref="SelectingItemsControl"/>), and
    /// <see cref="ItemsControl.ItemsSource"/>'s live <see cref="System.Collections.Specialized.INotifyCollectionChanged"/>
    /// reactivity are all inherited unchanged.
    /// </remarks>
    public class WrapGrid : SelectingItemsControl
    {
        private const float DefaultItemSize = 64f;

        private float itemHeight = DefaultItemSize;
        private float itemWidth = DefaultItemSize;

        /// <summary>
        /// Gets or sets the width, in pixels, of every realized cell.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not greater than zero.</exception>
        [Category("Layout")]
        [DefaultValue(DefaultItemSize)]
        [RegisterReference]
        public float ItemWidth
        {
            get => itemWidth;
            set
            {
                Guard.IsGreaterThan(value, 0f);
                if (SetProperty(ref itemWidth, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets or sets the height, in pixels, of every realized cell.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not greater than zero.</exception>
        [Category("Layout")]
        [DefaultValue(DefaultItemSize)]
        [RegisterReference]
        public float ItemHeight
        {
            get => itemHeight;
            set
            {
                Guard.IsGreaterThan(value, 0f);
                if (SetProperty(ref itemHeight, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <summary>
        /// Gets how many cells fit per row at the control's current arranged width - at least <c>1</c>, even if
        /// <see cref="ItemWidth"/> exceeds the available width.
        /// </summary>
        private int ColumnsPerRow => Math.Max(1, (int)(ContentBounds.Width / ItemWidth));
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Controls/WrapGrid.cs sources/IcyUI.Tests/Controls/WrapGridTests.cs
git commit -m "$(cat <<'EOF'
Add WrapGrid: ItemWidth/ItemHeight properties

Tier-2 Phase 6. First slice - properties only, geometry overrides follow.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

- [ ] **Step 6: Write the failing tests for `ComputeExtentHeight`/`ColumnsPerRow` arithmetic**

Add to `WrapGridTests.cs` (needs a way to arrange the control to a known width first, and a way to read `ComputeExtentHeight`/`ColumnsPerRow` - both `private`, invoked via reflection matching this codebase's established pattern):

```csharp
[Fact]
public void ComputeExtentHeight_MatchesTheWorkedExampleInTheSpec()
{
    // ItemWidth = ItemHeight = 64 (default), ContentBounds.Width = 400 -> ColumnsPerRow = 6 (400/64 = 6.25 floors
    // to 6). ItemCount = 100 -> ExtentHeight = ceil(100/6)*64 = 17*64 = 1088.
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 100).Cast<object>().ToList(),
        Width = 400,
    };
    ArrangeAtWidth(grid, 400);

    Assert.Equal(6, InvokeColumnsPerRow(grid));
    Assert.Equal(1088f, grid.ExtentHeight);
}

[Fact]
public void ComputeExtentHeight_ExactMultipleOfColumnsPerRow_NoPartialRow()
{
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 12).Cast<object>().ToList(),
        Width = 400, // ColumnsPerRow = 6, 12 items = exactly 2 full rows
    };
    ArrangeAtWidth(grid, 400);

    Assert.Equal(2 * 64f, grid.ExtentHeight);
}

[Fact]
public void ColumnsPerRow_NarrowerThanOneItem_ClampsToOne()
{
    var grid = new WrapGrid { ItemsSource = new List<object> { "a" }, Width = 30 };
    ArrangeAtWidth(grid, 30);

    Assert.Equal(1, InvokeColumnsPerRow(grid));
}

private static void ArrangeAtWidth(UIElement element, int width)
{
    element.Measure();
    element.Arrange(new Rectangle(0, 0, width, 1000));
}

private static int InvokeColumnsPerRow(WrapGrid grid) =>
    (int)typeof(WrapGrid).GetProperty("ColumnsPerRow", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(grid)!;
```

- [ ] **Step 7: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: FAIL - `ComputeExtentHeight` isn't overridden yet, so `ExtentHeight` still uses the base's variable-height-estimation formula, not the grid formula.

- [ ] **Step 8: Override `ComputeExtentHeight`**

In `WrapGrid.cs`, add after `ColumnsPerRow`:

```csharp
        /// <inheritdoc/>
        protected override float ComputeExtentHeight() =>
            (float)Math.Ceiling(ItemCount / (float)ColumnsPerRow) * ItemHeight;
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: PASS.

- [ ] **Step 10: Commit**

```bash
git add sources/IcyUI/UI/Controls/WrapGrid.cs sources/IcyUI.Tests/Controls/WrapGridTests.cs
git commit -m "$(cat <<'EOF'
Add WrapGrid.ComputeExtentHeight override

Exact row-count arithmetic (ceil(ItemCount/ColumnsPerRow)*ItemHeight)
instead of ItemsControl's variable-height running-average estimate -
matches the worked example in the design spec.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

- [ ] **Step 11: Write the failing tests for `LocateViewportStart`/`RealizeRange`**

Add to `WrapGridTests.cs`:

```csharp
[Fact]
public void OnViewportChanged_RealizesExactlyTheExpectedRange_MatchingTheWorkedExample()
{
    // Same setup as the ComputeExtentHeight worked example: ColumnsPerRow=6, 100 items.
    // verticalOffset=300, viewportHeight=500 -> firstRow=4 (300/64 floors), firstIndex=24;
    // lastRow=(300+500+100)/64=14 (floors), lastIndex=min(99,15*6-1)=89.
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 100).Cast<object>().ToList(),
        ItemTemplate = template,
        Width = 400,
    };
    ArrangeAtWidth(grid, 400);

    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 300, 400, 500);

    var realized = GetRealizedContainers(grid).Keys;
    Assert.Equal(66, realized.Count); // 11 rows * 6 columns
    Assert.Contains(24, realized);
    Assert.Contains(89, realized);
    Assert.DoesNotContain(23, realized);
    Assert.DoesNotContain(90, realized);
}

[Fact]
public void OnViewportChanged_PositionsEachRealizedItemAtItsRowAndColumn()
{
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
        ItemTemplate = template,
        ItemWidth = 50,
        ItemHeight = 30,
        Width = 200, // ColumnsPerRow = 4
    };
    ArrangeAtWidth(grid, 200);

    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 200, 100);

    // Index 5 -> row 1, column 1 -> (50, 30).
    ItemContainer item5 = GetRealizedContainers(grid)[5];
    Assert.Equal(50, item5.ActualBounds.X);
    Assert.Equal(30, item5.ActualBounds.Y);
}

[Fact]
public void OnViewportChanged_ScrollingDown_DerealizesRowsThatScrolledOut()
{
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 100).Cast<object>().ToList(),
        ItemTemplate = template,
        Width = 400,
    };
    ArrangeAtWidth(grid, 400);
    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 500);
    Assert.Contains(0, GetRealizedContainers(grid).Keys);

    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 5000, 400, 500);

    Assert.DoesNotContain(0, GetRealizedContainers(grid).Keys);
}

[Fact]
public void ResizingWidth_ReflowsAlreadyRealizedItemsToTheirNewColumns()
{
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
        ItemTemplate = template,
        ItemWidth = 50,
        ItemHeight = 30,
        Width = 200, // ColumnsPerRow = 4
    };
    ArrangeAtWidth(grid, 200);
    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 200, 100);
    Assert.Equal(50, GetRealizedContainers(grid)[5].ActualBounds.X); // row 1, col 1 at 4 columns

    ArrangeAtWidth(grid, 300); // ColumnsPerRow = 6 now
    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 300, 100);

    // Index 5 -> row 0, column 5 -> (250, 0) at 6 columns.
    Assert.Equal(250, GetRealizedContainers(grid)[5].ActualBounds.X);
    Assert.Equal(0, GetRealizedContainers(grid)[5].ActualBounds.Y);
}

private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
    (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;
```

- [ ] **Step 12: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: FAIL - `LocateViewportStart`/`RealizeRange` aren't overridden yet, so the base's single-column estimation runs instead (wrong index ranges/positions).

- [ ] **Step 13: Override `LocateViewportStart` and `RealizeRange`**

In `WrapGrid.cs`, add:

```csharp
        /// <inheritdoc/>
        protected override (int Index, float Offset) LocateViewportStart()
        {
            if (ItemCount == 0)
                return (0, 0f);

            int row = (int)(verticalOffset / ItemHeight);
            int index = Math.Clamp(row * ColumnsPerRow, 0, ItemCount - 1);
            return (index, row * ItemHeight);
        }

        /// <inheritdoc/>
        protected override void RealizeRange(int firstIndex, float firstOffset)
        {
            const float ScrollAheadBuffer = 100f;
            int columnsPerRow = ColumnsPerRow;
            float rangeEnd = verticalOffset + viewportHeight + ScrollAheadBuffer;
            int lastRow = (int)(rangeEnd / ItemHeight);
            int lastIndex = Math.Min(ItemCount - 1, ((lastRow + 1) * columnsPerRow) - 1);

            var stillRealized = new HashSet<int>();
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                EnsureRealized(index);
                stillRealized.Add(index);

                int row = index / columnsPerRow;
                int column = index % columnsPerRow;
                var targetRect = new Rectangle(
                    ContentBounds.X + (int)(column * ItemWidth),
                    ContentBounds.Y + (int)((row * ItemHeight) - verticalOffset),
                    (int)ItemWidth,
                    (int)ItemHeight);

                ItemContainer container = realizedContainers[index];
                container.InvalidateArrange();
                container.Arrange(targetRect);
            }

            foreach (int realizedIndex in realizedContainers.Keys.ToList())
            {
                if (!stillRealized.Contains(realizedIndex))
                    Derealize(realizedIndex);
            }
        }
```

Add `using System.Linq;` to the top of the file (for `.ToList()`).

- [ ] **Step 14: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: PASS.

- [ ] **Step 15: Commit**

```bash
git add sources/IcyUI/UI/Controls/WrapGrid.cs sources/IcyUI.Tests/Controls/WrapGridTests.cs
git commit -m "$(cat <<'EOF'
Add WrapGrid.LocateViewportStart/RealizeRange overrides

Exact row/column arithmetic virtualization - no anchor-walk or height
cache needed, since every cell is a fixed known-upfront size. Reuses the
inherited EnsureRealized/Derealize (pooling, CreateContainer, Attach/
DetachContainer) unchanged, only the index-range/positioning math differs
from ItemsControl's own single-column variable-height algorithm.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

- [ ] **Step 16: Write the failing tests for selection and click-to-select**

Add to `WrapGridTests.cs`:

```csharp
[Fact]
public void SelectionState_SurvivesPoolAndReuseCycle()
{
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
        ItemTemplate = template,
        Width = 400,
    };
    ArrangeAtWidth(grid, 400);
    grid.SelectedIndex = 5;
    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100);
    Assert.True(((SelectorItem)GetRealizedContainers(grid)[5]).IsSelected);

    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 5000, 400, 100); // scroll item 5 out
    Assert.DoesNotContain(5, GetRealizedContainers(grid).Keys);

    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100); // scroll back
    Assert.True(((SelectorItem)GetRealizedContainers(grid)[5]).IsSelected);
}

[Fact]
public void TappingARealizedItem_SelectsIt()
{
    var (canvas, _) = CreateCanvas();
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
        ItemTemplate = template,
        ItemWidth = 50,
        ItemHeight = 30,
        Width = 200,
        Height = 100,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };
    canvas.Add(grid);
    canvas.Render();

    // ColumnsPerRow = 4 at width 200; index 5 -> row 1, col 1 -> tap around (75, 45).
    canvas.Configuration.Input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(new Point(75, 45), 1));

    Assert.Equal(5, grid.SelectedIndex);
}

private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas(int viewportWidth = 800, int viewportHeight = 600)
{
    var input = new FakeInputSystem();
    var renderContext = new FakeRenderContext { ViewportSize = new Size(viewportWidth, viewportHeight) };
    var assets = new AssetConfiguration(AssetContext.ApplicationContext);
    var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
    return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
}
```

(Raises the tap through the `Canvas`'s own configured input - `canvas.Configuration.Input.Events.Touch.RaiseTap(...)` - matching `SelectorTests.cs`'s established pattern, rather than a disconnected second `FakeInputSystem` instance that the `Canvas` was never wired to.)

- [ ] **Step 17: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: FAIL - `AttachContainer`/`DetachContainer` aren't overridden yet, so `SelectorItem.Tapped` is never wired and `SelectedIndex` never changes on tap.

- [ ] **Step 18: Override `AttachContainer`/`DetachContainer` for selection stamping and tap-to-select**

In `WrapGrid.cs`, add the `containerIndices` field near `itemWidth`/`itemHeight`, and the two overrides:

```csharp
        private readonly Dictionary<SelectorItem, int> containerIndices = [];
```

```csharp
        /// <inheritdoc/>
        protected override void AttachContainer(ItemContainer container, int index)
        {
            base.AttachContainer(container, index);
            var item = (SelectorItem)container;
            item.IsSelected = index == SelectedIndex;
            item.Tapped += Container_Tapped;
            containerIndices[item] = index;
        }

        /// <inheritdoc/>
        protected override void DetachContainer(ItemContainer container)
        {
            var item = (SelectorItem)container;
            item.Tapped -= Container_Tapped;
            containerIndices.Remove(item);
            base.DetachContainer(container);
        }

        private void Container_Tapped(object? sender, EventArgs e)
        {
            if (sender is SelectorItem item && containerIndices.TryGetValue(item, out int index))
                SelectedIndex = index;
        }
```

- [ ] **Step 19: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: PASS.

- [ ] **Step 20: Write and pass the pooling/collection-reactivity/integration tests**

Add to `WrapGridTests.cs`:

```csharp
[Fact]
public void Derealize_ThenEnsureRealizedAgain_ReusesThePooledContainer()
{
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
        ItemTemplate = template,
        Width = 400,
    };
    ArrangeAtWidth(grid, 400);
    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100);
    ItemContainer original = GetRealizedContainers(grid)[0];

    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 5000, 400, 100); // scroll item 0 out
    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100); // scroll back

    Assert.Same(original, GetRealizedContainers(grid)[0]);
}

[Fact]
public void ItemsSource_ObservableCollection_Insert_UpdatesItemsAndDerealizesShiftedIndexes()
{
    var source = new ObservableCollection<object> { "a", "b", "c" };
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid { ItemsSource = source, ItemTemplate = template, Width = 400 };
    ArrangeAtWidth(grid, 400);
    ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100);
    Assert.Contains(2, GetRealizedContainers(grid).Keys); // "c" realized at index 2

    source.Insert(0, "new");

    // "c"'s old index-2 realization must be gone - it would otherwise silently represent "b" now.
    Assert.DoesNotContain(2, GetRealizedContainers(grid).Keys);
}

[Fact]
public void HostedInsideScrollViewer_OnlyRealizesTheVisibleRange()
{
    var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
    var grid = new WrapGrid
    {
        ItemsSource = Enumerable.Range(0, 10000).Cast<object>().ToList(),
        ItemTemplate = template,
    };
    var scrollViewer = new ScrollViewer { Content = grid, Width = 400, Height = 300 };
    var (canvas, _) = CreateCanvas();
    canvas.Add(scrollViewer);

    canvas.Render();

    var realized = GetRealizedContainers(grid);
    Assert.True(realized.Count < 200, $"expected far fewer than 10000 realized, got {realized.Count}");
}
```

The three tests above reuse the file's own existing helpers - `LoadDataTemplate` (Step 1), `ArrangeAtWidth` (Step 6), `GetRealizedContainers` (Step 11), `CreateCanvas` (Step 16) - no new helpers needed. Add `using System.Collections.ObjectModel;` to the test file's usings for `ObservableCollection<object>`.

- [ ] **Step 21: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~WrapGridTests"`
Expected: PASS.

- [ ] **Step 22: Commit**

```bash
git add sources/IcyUI/UI/Controls/WrapGrid.cs sources/IcyUI.Tests/Controls/WrapGridTests.cs
git commit -m "$(cat <<'EOF'
Add WrapGrid click-to-select and confirm inherited pooling/reactivity

AttachContainer/DetachContainer wire SelectorItem.Tapped for click-to-
select (own containerIndices reverse-lookup, mirrors Selector's). Also
verifies WrapGrid gets pooling, ItemsSource reactivity, and real
ScrollViewer-driven virtualization "for free" from ItemsControl/
SelectingItemsControl, with no override needed for any of them.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

- [ ] **Step 23: Create the `WrapGridDemo` sample**

Create `sources/Shared Samples/WrapGridDemo.cs` (mirrors `sources/Shared Samples/ItemsControlDemo.cs`'s exact structure - `ScrollViewer`-wrapped, code finds the named control post-load and assigns `ItemsSource`):

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Collections.ObjectModel;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="WrapGrid"/> (Tier-2 Phase 6): a <see cref="ScrollViewer"/>-hosted,
    /// virtualizing grid of uniformly-sized tiles bound to a few dozen in-memory items.
    /// </summary>
    /// <remarks>
    /// Same <see cref="ScrollViewer"/>-wrapping/post-load-<see cref="ItemsControl.ItemsSource"/>-assignment
    /// convention as <see cref="ItemsControlDemo"/> - markup has no way to inline a runtime
    /// <see cref="ObservableCollection{T}"/>.
    /// </remarks>
    public static class WrapGridDemo
    {
        /// <summary>
        /// The number of <c>"Tile NN"</c> entries the demo's <see cref="ObservableCollection{T}"/> is seeded with.
        /// </summary>
        public const int ItemCount = 48;

        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="ItemsControlDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">WrapGrid (Phase 6)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">48 tiles bound to an ObservableCollection&lt;string&gt;, auto-flowing into rows in a 300px ScrollViewer. Scroll with the mouse wheel or drag the scrollbar; click a tile to select it.</TextBlock>

                <ScrollViewer Width="360" Height="300" HorizontalAlignment="Left">
                  <WrapGrid x:Name="TileGrid" ItemWidth="72" ItemHeight="72">
                    <WrapGrid.ItemTemplate>
                      <DataTemplate>
                        <Border Margin="2" BorderBrush="#FF3A3A44" BorderThickness="1" Padding="4">
                          <TextBlock FontSize="12" Foreground="WhiteSmoke" Text="{Binding}"/>
                        </Border>
                      </DataTemplate>
                    </WrapGrid.ItemTemplate>
                  </WrapGrid>
                </ScrollViewer>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>, then wires a freshly-populated
        /// <see cref="ObservableCollection{T}"/> of <see cref="ItemCount"/> strings into the loaded
        /// <see cref="WrapGrid"/>'s <see cref="ItemsControl.ItemsSource"/>.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration the loader resolves types, converters, and properties through. Its
        /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> is set to <paramref name="fontFamily"/>
        /// here, which is what gives the document's text a font.
        /// </param>
        /// <param name="fontFamily">
        /// The font family every text element resolves. Import it beforehand (see <c>FontSystem.ImportFont</c>) for
        /// text to actually render.
        /// </param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the markup language.</exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(Markup, nameof(WrapGridDemo));

            var items = new ObservableCollection<string>();
            for (int i = 0; i < ItemCount; i++)
                items.Add($"Tile {i:D2}");

            WrapGrid tileGrid = root.FindRequiredControl<WrapGrid>("TileGrid");
            tileGrid.ItemsSource = items;

            return root;
        }
    }
}
```

Do not register it in `MonoGame Sample/SampleGame.cs` or `Stride Sample/SampleGame.cs` yet - that's Task 6.

- [ ] **Step 24: Build to verify the demo compiles**

Run: `dotnet build "sources/IcyUI.sln"`
Expected: 0 errors (the demo file isn't referenced from any sample host yet, but must still compile standalone as part of the `Shared Samples` project).

- [ ] **Step 25: Commit**

```bash
git add "sources/Shared Samples/WrapGridDemo.cs"
git commit -m "$(cat <<'EOF'
Add WrapGridDemo sample

Not yet registered in either sample host - Task 6 wires both this and
ListBoxDemo into MonoGame Sample/SampleGame.cs and Stride Sample/
SampleGame.cs together, to avoid a merge conflict with the parallel
ListBox work touching the same two files.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

---

## Task 5: `ListBox`

> **Parallelizable with Task 4** - starts only after Tasks 1-3 are committed; touches no files Task 4 touches.

**Files:**
- Create: `sources/IcyUI/UI/Controls/ListBox.cs`
- Test: `sources/IcyUI.Tests/Controls/ListBoxTests.cs`
- Create: `sources/Shared Samples/ListBoxDemo.cs`

**Interfaces:**
- Consumes: `SelectingItemsControl.SelectedIndex`/`SelectedItem`/`SelectionChanged` (Task 2); `ItemsControl.AttachContainer`/`DetachContainer` (pre-existing `protected virtual` surface from Phase 5 - no Task 1 change needed, since `ListBox` overrides neither the geometry hooks nor anything Task 1 touched); `SelectorItem.Tapped` (Task 3).
- Produces: `public class ListBox : SelectingItemsControl` - no further public members beyond what's inherited.

- [ ] **Step 1: Write the failing tests**

Create `sources/IcyUI.Tests/Controls/ListBoxTests.cs`:

```csharp
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ListBoxTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var listBox = new ListBox();

            Assert.Equal(-1, listBox.SelectedIndex);
            Assert.Null(listBox.SelectedItem);
        }

        [Fact]
        public void CreateContainer_RealizesSelectorItems()
        {
            var listBox = new ListBox
            {
                ItemsSource = new List<object> { "a" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
            };

            InvokeEnsureRealized(listBox, 0);

            Assert.IsType<SelectorItem>(GetRealizedContainers(listBox)[0]);
        }

        [Fact]
        public void SelectionState_SurvivesPoolAndReuseCycle()
        {
            var listBox = new ListBox
            {
                ItemsSource = new List<object> { "a", "b" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
            };
            listBox.SelectedIndex = 0;
            InvokeEnsureRealized(listBox, 0);
            Assert.True(((SelectorItem)GetRealizedContainers(listBox)[0]).IsSelected);

            InvokeDerealize(listBox, 0);
            InvokeEnsureRealized(listBox, 0);

            Assert.True(((SelectorItem)GetRealizedContainers(listBox)[0]).IsSelected);
        }

        [Fact]
        public void TappingARealizedItem_SelectsIt()
        {
            var (canvas, _) = CreateCanvas();
            var listBox = new ListBox
            {
                ItemsSource = new List<object> { "a", "b", "c" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(listBox);
            canvas.Render();

            canvas.Configuration.Input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(new Point(5, 25), 1)); // second row (20px each)

            Assert.Equal(1, listBox.SelectedIndex);
        }

        [Fact]
        public void HostedInsideScrollViewer_OnlyRealizesTheVisibleRange()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var listBox = new ListBox
            {
                ItemsSource = Enumerable.Range(0, 10000).Cast<object>().ToList(),
                ItemTemplate = template,
            };
            var scrollViewer = new ScrollViewer { Content = listBox, Width = 300, Height = 400 };
            var (canvas, _) = CreateCanvas();
            canvas.Add(scrollViewer);

            canvas.Render();

            var realized = GetRealizedContainers(listBox);
            Assert.True(realized.Count < 50, $"expected far fewer than 10000 realized, got {realized.Count}");
        }

        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas(int viewportWidth = 800, int viewportHeight = 600)
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext { ViewportSize = new Size(viewportWidth, viewportHeight) };
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        private static void InvokeEnsureRealized(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("EnsureRealized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static void InvokeDerealize(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("Derealize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ListBoxTests"`
Expected: FAIL to compile - `ListBox` doesn't exist yet.

- [ ] **Step 3: Create `ListBox.cs`**

Create `sources/IcyUI/UI/Controls/ListBox.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// A plain, always-visible, virtualizing, single-selectable list - the most direct realization of
    /// "an <see cref="ItemsControl"/> with click-to-select."
    /// </summary>
    /// <remarks>
    /// Overrides no virtualization geometry - a plain vertical list of selectable items is exactly
    /// <see cref="ItemsControl"/>'s own default single-column variable-height virtualization, inherited via
    /// <see cref="SelectingItemsControl"/> completely unchanged. Only adds click-to-select, via
    /// <see cref="SelectorItem.Tapped"/> (see <see cref="AttachContainer(ItemContainer, int)"/>/
    /// <see cref="DetachContainer(ItemContainer)"/>) - unlike <see cref="Selector"/>'s popup items, this
    /// control's realized items live in the normal visual tree, so <see cref="Canvas.OnTap"/>'s existing
    /// per-element dispatch reaches them directly.
    /// </remarks>
    public class ListBox : SelectingItemsControl
    {
        private readonly Dictionary<SelectorItem, int> containerIndices = [];

        /// <inheritdoc/>
        protected override void AttachContainer(ItemContainer container, int index)
        {
            base.AttachContainer(container, index);
            var item = (SelectorItem)container;
            item.IsSelected = index == SelectedIndex;
            item.Tapped += Container_Tapped;
            containerIndices[item] = index;
        }

        /// <inheritdoc/>
        protected override void DetachContainer(ItemContainer container)
        {
            var item = (SelectorItem)container;
            item.Tapped -= Container_Tapped;
            containerIndices.Remove(item);
            base.DetachContainer(container);
        }

        private void Container_Tapped(object? sender, EventArgs e)
        {
            if (sender is SelectorItem item && containerIndices.TryGetValue(item, out int index))
                SelectedIndex = index;
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ListBoxTests"`
Expected: PASS.

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS, all tests.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/ListBox.cs sources/IcyUI.Tests/Controls/ListBoxTests.cs
git commit -m "$(cat <<'EOF'
Add ListBox

Tier-2 Phase 6. No geometry override - a plain vertical selectable list
is exactly ItemsControl's own default virtualization behavior, inherited
via SelectingItemsControl unchanged. Only adds SelectorItem.Tapped-driven
click-to-select on top, mirroring WrapGrid's own AttachContainer/
DetachContainer shape.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

- [ ] **Step 7: Create the `ListBoxDemo` sample**

Create `sources/Shared Samples/ListBoxDemo.cs` (mirrors `sources/Shared Samples/SelectorDemo.cs`'s fruit-list pattern, `ScrollViewer`-wrapped like `ItemsControlDemo.cs`):

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System;
using System.Collections.ObjectModel;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// A single screen demonstrating <see cref="ListBox"/> (Tier-2 Phase 6): a <see cref="ScrollViewer"/>-hosted,
    /// virtualizing, single-selectable list bound to a small in-memory collection.
    /// </summary>
    /// <remarks>
    /// Same <see cref="ScrollViewer"/>-wrapping/post-load-<see cref="ItemsControl.ItemsSource"/>-assignment
    /// convention as <see cref="ItemsControlDemo"/>, reusing <see cref="SelectorDemo"/>'s own fruit-name list.
    /// </remarks>
    public static class ListBoxDemo
    {
        /// <summary>
        /// The markup this demo loads, kept inline so the sample stays self-contained - same convention as
        /// <see cref="ItemsControlDemo.Markup"/>.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Margin="0,0,0,16"
                    Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel Orientation="Vertical">
                <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">ListBox (Phase 6)</TextBlock>
                <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">A plain always-visible selectable list, in a 240px ScrollViewer. Click a row to select it.</TextBlock>

                <ScrollViewer Width="240" Height="240" HorizontalAlignment="Left">
                  <ListBox x:Name="FruitListBox">
                    <ListBox.ItemTemplate>
                      <DataTemplate>
                        <Border BorderBrush="#FF3A3A44" BorderThickness="0,0,0,1" Padding="10,8">
                          <TextBlock FontSize="14" Foreground="WhiteSmoke" Text="{Binding}"/>
                        </Border>
                      </DataTemplate>
                    </ListBox.ItemTemplate>
                  </ListBox>
                </ScrollViewer>
              </StackPanel>
            </Border>
            """;

        /// <summary>
        /// Builds the demo's root element by loading <see cref="Markup"/>, then wires a small
        /// <see cref="ObservableCollection{T}"/> of fruit names into the loaded <see cref="ListBox"/>'s
        /// <see cref="ItemsControl.ItemsSource"/>.
        /// </summary>
        /// <param name="configuration">
        /// The library configuration the loader resolves types, converters, and properties through. Its
        /// <see cref="Icy.Rendering.Fonts.FontSystem.DefaultFontFamily"/> is set to <paramref name="fontFamily"/>
        /// here, which is what gives the document's text a font.
        /// </param>
        /// <param name="fontFamily">
        /// The font family every text element resolves. Import it beforehand (see <c>FontSystem.ImportFont</c>) for
        /// text to actually render.
        /// </param>
        /// <returns>The root element to add to a <see cref="Canvas"/>.</returns>
        /// <exception cref="MarkupException">The document is malformed, or breaks a rule of the markup language.</exception>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            configuration.Fonts.DefaultFontFamily = fontFamily;

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(Markup, nameof(ListBoxDemo));

            var fruits = new ObservableCollection<string> { "Apple", "Apricot", "Banana", "Cherry", "Grape", "Mango", "Orange", "Peach", "Pear", "Plum" };

            ListBox listBox = root.FindRequiredControl<ListBox>("FruitListBox");
            listBox.ItemsSource = fruits;

            return root;
        }
    }
}
```

Do not register it in either sample host yet - that's Task 6.

- [ ] **Step 8: Build to verify the demo compiles**

Run: `dotnet build "sources/IcyUI.sln"`
Expected: 0 errors.

- [ ] **Step 9: Commit**

```bash
git add "sources/Shared Samples/ListBoxDemo.cs"
git commit -m "$(cat <<'EOF'
Add ListBoxDemo sample

Not yet registered in either sample host - Task 6 wires both this and
WrapGridDemo into MonoGame Sample/SampleGame.cs and Stride Sample/
SampleGame.cs together, to avoid a merge conflict with the parallel
WrapGrid work touching the same two files.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```

---

## Task 6: Sample registration and final verification

> **Sequential - starts only after both Task 4 and Task 5 are committed.**

**Files:**
- Create: `sources/MonoGame Sample/Samples/WrapGridSample.cs`, `sources/MonoGame Sample/Samples/ListBoxSample.cs`
- Modify: `sources/MonoGame Sample/SampleGame.cs`
- Modify: `sources/MonoGame Sample/MonoGame Sample.csproj`
- Modify: `sources/Stride Sample/SampleGame.cs`
- Modify: `sources/Stride Sample/Stride Sample.csproj`

**Interfaces:**
- Consumes: `WrapGridDemo`/`ListBoxDemo` (Tasks 4-5).

- [ ] **Step 1: Create the MonoGame per-demo wrapper classes**

Create `sources/MonoGame Sample/Samples/WrapGridSample.cs` (mirrors `sources/MonoGame Sample/Samples/SelectorSample.cs` exactly):

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="WrapGridDemo"/> - a virtualizing auto-flow tile grid - as its own selectable sample
    /// (PgUp/PgDown to switch to it, like the other samples).
    /// </summary>
    internal class WrapGridSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public WrapGridSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "WrapGrid Demo")
        {
            VisibleChanged += (_, _) =>
            {
                if (demoRoot != null)
                    demoRoot.IsVisible = Visible;
            };
        }

        protected override void LoadContent()
        {
            UIConfiguration.Fonts.ImportFont(UIConfiguration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            Canvas.IsInputEnabled = true;

            demoRoot = WrapGridDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
```

Create `sources/MonoGame Sample/Samples/ListBoxSample.cs` (identical shape, swap every `WrapGrid`→`ListBox`):

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;

namespace Icy.MonoGameSample.Samples
{
    /// <summary>
    /// Runs <see cref="ListBoxDemo"/> - a plain virtualizing selectable list - as its own selectable sample
    /// (PgUp/PgDown to switch to it, like the other samples).
    /// </summary>
    internal class ListBoxSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public ListBoxSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "ListBox Demo")
        {
            VisibleChanged += (_, _) =>
            {
                if (demoRoot != null)
                    demoRoot.IsVisible = Visible;
            };
        }

        protected override void LoadContent()
        {
            UIConfiguration.Fonts.ImportFont(UIConfiguration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            Canvas.IsInputEnabled = true;

            demoRoot = ListBoxDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
```

- [ ] **Step 2: Register both in `MonoGame Sample/SampleGame.cs`**

Change (`SampleGame.cs`'s `Initialize`, the `samplesRunner.Prepare([...])` array):

```diff
                 new ExpanderSample(this, uiConfiguration, canvas),
-                new SelectorSample(this, uiConfiguration, canvas)
+                new SelectorSample(this, uiConfiguration, canvas),
+                new WrapGridSample(this, uiConfiguration, canvas),
+                new ListBoxSample(this, uiConfiguration, canvas)
                 ]);
```

- [ ] **Step 3: Add both new files to `MonoGame Sample/MonoGame Sample.csproj`**

Change (next to the existing `SelectorDemo.cs` entry):

```diff
     <Compile Include="..\Shared Samples\SelectorDemo.cs" Link="Samples\SelectorDemo.cs" />
+    <Compile Include="..\Shared Samples\WrapGridDemo.cs" Link="Samples\WrapGridDemo.cs" />
+    <Compile Include="..\Shared Samples\ListBoxDemo.cs" Link="Samples\ListBoxDemo.cs" />
```

- [ ] **Step 4: Register both in `Stride Sample/SampleGame.cs`**

Add two new fields, next to `selectorRoot`:

```diff
         private UIElement? selectorRoot;
+        private UIElement? wrapGridRoot;
+        private UIElement? listBoxRoot;
         private int selectedDemo;
```

Add two `Build` calls and two `canvas.Add` calls, next to the `selectorRoot`/`SelectorDemo` ones:

```diff
             selectorRoot = SelectorDemo.Build(configuration, "Airfool");
+            wrapGridRoot = WrapGridDemo.Build(configuration, "Airfool");
+            listBoxRoot = ListBoxDemo.Build(configuration, "Airfool");
             canvas.Add(controlsRoot);
             ...
             canvas.Add(selectorRoot);
+            canvas.Add(wrapGridRoot);
+            canvas.Add(listBoxRoot);
             UpdateSelectedDemo();
```

Change the cycle divisor in `switchDemo` (currently 10 demos, becomes 12):

```diff
-                selectedDemo = (selectedDemo + 1) % 10;
+                selectedDemo = (selectedDemo + 1) % 12;
```

Add two lines to `UpdateSelectedDemo()`, next to `selectorRoot`'s:

```diff
             selectorRoot!.IsVisible = selectedDemo == 9;
+            wrapGridRoot!.IsVisible = selectedDemo == 10;
+            listBoxRoot!.IsVisible = selectedDemo == 11;
```

Update the class's own `<summary>` doc comment (lists every demo it hosts) to add `<see cref="WrapGridDemo"/>` and `<see cref="ListBoxDemo"/>` after `<see cref="SelectorDemo"/>`.

- [ ] **Step 5: Add both new files to `Stride Sample/Stride Sample.csproj`**

Change (next to the existing `SelectorDemo.cs` entry - note Stride's `Link` path has no `Samples\` prefix, unlike MonoGame's):

```diff
     <Compile Include="..\Shared Samples\SelectorDemo.cs" Link="SelectorDemo.cs" />
+    <Compile Include="..\Shared Samples\WrapGridDemo.cs" Link="WrapGridDemo.cs" />
+    <Compile Include="..\Shared Samples\ListBoxDemo.cs" Link="ListBoxDemo.cs" />
```

- [ ] **Step 6: Build both samples to verify they compile**

Run: `dotnet build "sources/IcyUI.sln"`
Expected: 0 errors.

- [ ] **Step 7: Notify Ivan before launching for a visual check**

Per `[[feedback_smoke_test_notification]]` - message Ivan that the samples are ready for a manual visual check on both engines (`WrapGrid` scrolling/reflow/click-to-select, `ListBox` scrolling/click-to-select, and a quick re-check that `Selector`/`Dropdown`/`ComboBox` still behave identically post-refactor) before launching either sample, since he steps away from the PC while it runs.

- [ ] **Step 8: Run the full test suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS, all tests (this is the final full-suite confirmation after every task in this plan).

- [ ] **Step 9: Commit**

```bash
git add "sources/MonoGame Sample" "sources/Stride Sample"
git commit -m "$(cat <<'EOF'
Register WrapGridDemo/ListBoxDemo in both sample hosts

Tier-2 Phase 6, final commit. Both controls bound to demo item lists in
MonoGame and Stride sample hosts, exercising scrolling, virtualization,
and click-to-select.

Co-Authored-By: Claude Sonnet 5 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01Fe14bkcLApPBUwZq7N8qXb
EOF
)"
```
