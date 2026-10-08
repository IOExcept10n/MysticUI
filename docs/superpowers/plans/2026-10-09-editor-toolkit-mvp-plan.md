# Editor Toolkit MVP Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the smallest designer toolkit a Stride game can author its screens with: three panels (Outline, Properties, Toolbox), a dev `EditorOverlay`, an `EditorWorkspace` page, and Save from inside the editor.

**Architecture:**
- **Core:** `PropertyGrid` gains a public `PropertyGridValueAdapter` seam and `Refresh()`. Nothing else in core changes.
- **`IcyUI.Design`** gains:
  - a self-verifying `MarkupValueFormatter`;
  - a `MarkupPropertyAdapter` that routes grid edits through `MarkupEditor`;
  - save state on `DesignDocument`/`DesignSession`;
  - three panel controls bound to an `EditorSession`;
  - a shared `EditorCommandBar`;
  - two compositions: `EditorOverlay` (frame + docks over the game) and `EditorWorkspace` (a page control with an isolated preview).

**Tech Stack:** C# / .NET 10, IcyUI core controls (TreeView, PropertyGrid, SplitPane, TabControl, ScrollViewer), xUnit, StyleCop.

**Spec:** `docs/superpowers/specs/2026-10-09-editor-toolkit-mvp-design.md`

## Global Constraints

- **Target and build:** everything targets `net10.0`. Build with `dotnet build "sources/IcyUI.sln"` and test with `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. Check the **Total** line: "Passed!" also prints when the test host crashes.
- **Documentation:** every new public API gets complete XML docs (`<see cref>`, `<see langword>`, `<list>`, `<para>`).
- **Code style:** block-scoped `namespace X { }`, the file copyright header, StyleCop clean.
- **Warning baselines:** IcyUI **84**, IcyUI.Design **1**. Count with `dotnet build <csproj> --no-incremental 2>&1 | grep -E ": warning (SA|CS)" | sort -u | wc -l`.
- **Engine boundaries:**
  - Core (`sources/IcyUI`) must not reference engine types.
  - Everything editor-related lives in `IcyUI.Design`. Release builds of a game don't reference it.
  - This phase needs no engine-project changes: MonoGame, Stride and FNA are untouched.
- **Layout limits:** they use `float.NaN` as "unset". Overflow shrinking is opt-in via `MinWidth`/`MinHeight` (97ca604), so docks and bars are sized and placed explicitly.
- **Save never throws inside a session:** failures surface as `SaveBlockedReason` or a status-line message.
- **No per-frame work in the panels:** they update on document `Changed`, `SelectionChanged` and gesture ends only.
- **Samples:** both hosts show the shared `SampleShell`. Registering a demo in `SampleCatalog.All` registers it in both hosts.
- **Line endings:** test files use CRLF like their neighbours. Markup strings in tests go through `.ReplaceLineEndings("\n")` where the existing hosts do.

## Spec deltas decided while planning (update the spec in Task 1's commit)

1. **`IsModified` compares `Text` with the last saved or loaded text** (ordinal), not the undo position.
   - Why: `UndoStack` coalesces edits to the same attribute within 500 ms, so a save in the middle of a coalescing window would merge into the "saved" entry.
   - Cost: one ordinal comparison per change, a few microseconds for 100 KB.
2. **Attached-property rows are deferred.** The Properties panel lists the instance's own properties (`PropertyGridEntry.EnumerateFor`). Attached attributes such as `Grid.Row` stay editable through drags and the text. The new item is "Attached-property rows in PropertiesPanel".
3. **Editor key bindings stand down while a `TextBox` has focus** (new `EditorBindings` gate). Otherwise typing in a panel would nudge or delete the selected element in Edit mode.
4. **`MarkupValueFormatter` verifies by converting back** through `IcyConfiguration.Types.TypeConverter`, and returns `false` when the text doesn't reproduce an equal value. `CornerRadius` is dropped, since core has none.

## Review Focus

1. **Typing in a Properties row.** Every keystroke must not create an undo step, steal focus or nudge the element. Coalescing gives one undo step, Refresh skips the focused row, and the key gate stops the nudges. Owned by Tasks 1, 4 and 5.
2. **A style-set or default property edited for the first time.** It must gain a local attribute, and Reset must remove it so the style value returns. Owned by Task 4.
3. **Switching the Outline to another document, then back.** The tree must show no rows from the previous document, and the expansion must be restored for the same document. Owned by Task 6.
4. **Save with no resolvable path, or a read-only file.** The button is disabled with a reason, or the status shows the I/O error, and the game keeps running. Owned by Tasks 3 and 8.
5. **The workspace opened while the overlay is shown on the same canvas, and the reverse.** The second one shows the busy notice, attaches once the first detaches, and never throws. Owned by Tasks 9 and 10.

---

## File structure

| File | Responsibility |
|---|---|
| `sources/IcyUI/Data/PropertyGridValueAdapter.cs` (new) | Read/write seam for `PropertyGrid` rows; default = reflection. |
| `sources/IcyUI/UI/Controls/PropertyGrid.cs` (modify) | Uses the adapter; expression rows; Reset; `Refresh()`. |
| `sources/IcyUI.Design/MarkupValueFormatter.cs` (new) | Value → markup text, verified by round trip. |
| `sources/IcyUI.Design/DesignDocument.cs`, `DesignSession.cs` (modify) | `IsModified`, `ModifiedChanged`, `CanSave`, `SaveBlockedReason`, `SaveAll`, `UseSourceRoot`, `FindSourceRoot`. |
| `sources/IcyUI.Design/Editor/EditorBindings.cs` (modify) | Gate key bindings while a `TextBox` is focused. |
| `sources/IcyUI.Design/Editor/Panels/MarkupPropertyAdapter.cs` (new) | `PropertyGridValueAdapter` over one selected element. |
| `sources/IcyUI.Design/Editor/Panels/PropertiesPanel.cs` (new) | Header + `PropertyGrid` for the session's selection. |
| `sources/IcyUI.Design/Editor/Panels/OutlineItem.cs`, `OutlinePanel.cs` (new) | Element-tree outline with two-way selection. |
| `sources/IcyUI.Design/Editor/Panels/ToolboxItem.cs`, `ToolboxPanel.cs` (new) | Insertable element list. |
| `sources/IcyUI.Design/Editor/Panels/EditorCommandBar.cs` (new) | Mode, Undo, Redo, Save, Save all, modified badge, status. |
| `sources/IcyUI.Design/Editor/EditorOverlayOptions.cs`, `EditorOverlay.cs` (new) | Dev overlay composition. |
| `sources/IcyUI.Design/Editor/EditorWorkspace.cs` (new) | Page composition with preview host. |
| `sources/Shared Samples/EditorWorkspaceDemo.cs` (new), `SampleCatalog.cs`, `SampleShell.cs` (modify) | Samples. |
| Tests under `sources/IcyUI.Tests/Controls/` and `sources/IcyUI.Tests/Design/...` | One test file per unit, named after it. |

---

### Task 1: Core `PropertyGridValueAdapter`, expression rows, Reset and `Refresh()`

**Files:**
- Create: `sources/IcyUI/Data/PropertyGridValueAdapter.cs`
- Modify: `sources/IcyUI/UI/Controls/PropertyGrid.cs`
- Modify: `docs/superpowers/specs/2026-10-09-editor-toolkit-mvp-design.md` (apply the four spec deltas above)
- Test: `sources/IcyUI.Tests/Controls/PropertyGridAdapterTests.cs`

**Interfaces:**
- Produces:
  - `public class PropertyGridValueAdapter`, with:
    - `static Default`;
    - virtual members `GetValue`, `TrySetValue`, `GetExpression`, `TrySetExpression`, `CanReset`, `Reset`, all taking `(PropertyGridEntry entry, object target, ...)`.
  - On `PropertyGrid`:
    - `PropertyGridValueAdapter ValueAdapter { get; set; }`
    - `void Refresh()`
- Row shape, which tests rely on: the row `Grid` has children `[0]` label `TextBlock`, `[1]` editor, and `[2]` a Reset `Button` (only when `CanReset`).

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Controls/PropertyGridAdapterTests.cs
using Icy.Data;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class PropertyGridAdapterTests
    {
        private sealed class Target
        {
            public string Nickname { get; set; } = "Alpha";

            public int Score { get; set; } = 10;
        }

        private sealed class RecordingAdapter : PropertyGridValueAdapter
        {
            public List<(string Name, object? Value)> Writes { get; } = [];

            public Dictionary<string, string> Expressions { get; } = [];

            public HashSet<string> Resettable { get; } = [];

            public List<string> Resets { get; } = [];

            public bool AcceptExpressions { get; set; } = true;

            public override bool TrySetValue(PropertyGridEntry entry, object target, object? value)
            {
                Writes.Add((entry.Name, value));
                return base.TrySetValue(entry, target, value);
            }

            public override string? GetExpression(PropertyGridEntry entry, object target) => Expressions.GetValueOrDefault(entry.Name);

            public override bool TrySetExpression(PropertyGridEntry entry, object target, string text)
            {
                if (!AcceptExpressions)
                    return false;
                Expressions[entry.Name] = text;
                return true;
            }

            public override bool CanReset(PropertyGridEntry entry, object target) => Resettable.Contains(entry.Name);

            public override void Reset(PropertyGridEntry entry, object target) => Resets.Add(entry.Name);
        }

        [Fact]
        public void TheDefaultAdapter_WritesThroughReflection()
        {
            var target = new Target();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            ((TextBox)FindEditor(grid, "Nickname")!).Text = "Beta";

            Assert.Same(PropertyGridValueAdapter.Default, grid.ValueAdapter);
            Assert.Equal("Beta", target.Nickname);
        }

        [Fact]
        public void ACustomAdapter_ReceivesEveryWrite()
        {
            var adapter = new RecordingAdapter();
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            ((TextBox)FindEditor(grid, "Score")!).Text = "42";

            Assert.Contains(("Score", (object?)42), adapter.Writes);
        }

        [Fact]
        public void AnExpressionRow_ShowsTheTextAndCommitsOnlyOnFocusLoss()
        {
            var adapter = new RecordingAdapter();
            adapter.Expressions["Nickname"] = "{Binding Name}";
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            var box = (TextBox)FindEditor(grid, "Nickname")!;
            Assert.Equal("{Binding Name}", box.Text);

            box.Text = "{Binding Title}";
            Assert.Equal("{Binding Name}", adapter.Expressions["Nickname"]);
            Assert.Empty(adapter.Writes);

            PropertyGrid.CommitExpressionForTest(box);
            Assert.Equal("{Binding Title}", adapter.Expressions["Nickname"]);
        }

        [Fact]
        public void ARejectedExpression_MarksTheBoxInvalid()
        {
            var adapter = new RecordingAdapter { AcceptExpressions = false };
            adapter.Expressions["Nickname"] = "{Binding Name}";
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            var box = (TextBox)FindEditor(grid, "Nickname")!;
            box.Text = "{Broken";
            PropertyGrid.CommitExpressionForTest(box);

            Assert.True(box.ControlState.HasFlag(Icy.UI.Styles.ControlState.Invalid));
        }

        [Fact]
        public void AResettableRow_HasAResetButtonThatCallsTheAdapter()
        {
            var adapter = new RecordingAdapter();
            adapter.Resettable.Add("Score");
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            Grid row = FindRow(grid, "Score")!;
            var reset = Assert.IsType<Button>(row.Children[2]);
            reset.Command!.Execute(null);

            Assert.Equal(["Score"], adapter.Resets);
            Assert.Equal(2, FindRow(grid, "Nickname")!.Children.Count);
        }

        [Fact]
        public void Refresh_RereadsValuesWithoutReplacingRows()
        {
            var target = new Target();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();
            Grid rowBefore = FindRow(grid, "Score")!;

            target.Score = 99;
            grid.Refresh();

            Assert.Same(rowBefore, FindRow(grid, "Score"));
            Assert.Equal("99", ((TextBox)FindEditor(grid, "Score")!).Text);
        }

        [Fact]
        public void Refresh_PicksUpANewlyResettableRow()
        {
            var adapter = new RecordingAdapter();
            var grid = new PropertyGrid { ValueAdapter = adapter, Target = new Target() };
            grid.Measure();

            adapter.Resettable.Add("Score");
            grid.Refresh();

            Assert.IsType<Button>(FindRow(grid, "Score")!.Children[2]);
        }

        [Fact]
        public void Refresh_SkipsTheRowBeingWritten()
        {
            // The adapter's write raises a document change that calls Refresh synchronously; rebuilding the editor
            // that is mid-write would drop the user's widget (an open color popup, a slider drag).
            var target = new Target();
            var grid = new PropertyGrid();
            var adapter = new RefreshingAdapter(grid);
            grid.ValueAdapter = adapter;
            grid.Target = target;
            grid.Measure();

            var box = (TextBox)FindEditor(grid, "Score")!;
            box.Text = "5";

            Assert.Same(box, FindEditor(grid, "Score"));
            Assert.Equal(5, target.Score);
        }

        [Fact]
        public void ChangingTheAdapter_RebuildsTheRows()
        {
            var adapter = new RecordingAdapter();
            adapter.Expressions["Nickname"] = "{Binding Name}";
            var grid = new PropertyGrid { Target = new Target() };
            grid.Measure();

            grid.ValueAdapter = adapter;
            grid.Measure();

            Assert.Equal("{Binding Name}", ((TextBox)FindEditor(grid, "Nickname")!).Text);
        }

        private sealed class RefreshingAdapter(PropertyGrid grid) : PropertyGridValueAdapter
        {
            public override bool TrySetValue(PropertyGridEntry entry, object target, object? value)
            {
                bool written = base.TrySetValue(entry, target, value);
                grid.Refresh();
                return written;
            }
        }

        private static Grid? FindRow(PropertyGrid grid, string name)
        {
            foreach (ItemContainer container in GetRealizedContainers(grid).Values)
            {
                if (container.Content is Grid row && row.Children.ElementAtOrDefault(0) is TextBlock label && label.Text == name)
                    return row;
            }

            return null;
        }

        private static UIElement? FindEditor(PropertyGrid grid, string name) => FindRow(grid, name)?.Children.ElementAtOrDefault(1);

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(control)!;
    }
}
```

`PropertyGrid.CommitExpressionForTest` is an `internal static` hook: core already exposes internals to the test assembly. It does exactly what focus loss does, which avoids building a canvas just to move focus.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertyGridAdapterTests"`
Expected: a build failure, because `PropertyGridValueAdapter` doesn't exist.

- [ ] **Step 3: Create `PropertyGridValueAdapter`**

```csharp
// sources/IcyUI/Data/PropertyGridValueAdapter.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

namespace Icy.Data
{
    /// <summary>
    /// Decides how a <see cref="UI.Controls.PropertyGrid"/> reads and writes the properties it shows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default adapter (<see cref="Default"/>) reads and writes the target's properties directly. Derive from this
    /// class to route edits elsewhere, for example into an undoable document, or to show some properties as raw text
    /// expressions instead of typed editors.
    /// </para>
    /// <para>
    /// Every member is virtual and has a working default, so an adapter overrides only what it changes.
    /// </para>
    /// </remarks>
    public class PropertyGridValueAdapter
    {
        /// <summary>
        /// Gets the adapter that reads and writes properties directly through reflection.
        /// </summary>
        public static PropertyGridValueAdapter Default { get; } = new();

        /// <summary>
        /// Reads the value <paramref name="entry"/>'s editor shows.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <returns>The value; the default reads it with <see cref="PropertyGridEntry.GetValue(object)"/>.</returns>
        public virtual object? GetValue(PropertyGridEntry entry, object target) => entry.GetValue(target);

        /// <summary>
        /// Writes a value the user entered in <paramref name="entry"/>'s typed editor.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <param name="value">The new value, already converted to <see cref="PropertyGridEntry.PropertyType"/>.</param>
        /// <returns><see langword="true"/> when the value was accepted.</returns>
        public virtual bool TrySetValue(PropertyGridEntry entry, object target, object? value) => entry.TrySetValue(target, value);

        /// <summary>
        /// Gets the raw text to show instead of a typed editor, such as a <c>{Binding}</c> expression.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <returns>
        /// The text, or <see langword="null"/> (the default) for a typed editor. A non-<see langword="null"/> text turns
        /// the row into a text box that commits through <see cref="TrySetExpression"/> when it loses focus.
        /// </returns>
        public virtual string? GetExpression(PropertyGridEntry entry, object target) => null;

        /// <summary>
        /// Writes the text the user entered in an expression row.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <param name="text">The entered text.</param>
        /// <returns>
        /// <see langword="true"/> when the text was accepted; on <see langword="false"/> (the default) the row is marked
        /// <see cref="UI.Styles.ControlState.Invalid"/>.
        /// </returns>
        public virtual bool TrySetExpression(PropertyGridEntry entry, object target, string text) => false;

        /// <summary>
        /// Determines whether the row shows a Reset button.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        /// <returns><see langword="true"/> to show the button; the default returns <see langword="false"/>.</returns>
        public virtual bool CanReset(PropertyGridEntry entry, object target) => false;

        /// <summary>
        /// Resets the property when the user presses the row's Reset button.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the grid shows.</param>
        public virtual void Reset(PropertyGridEntry entry, object target)
        {
        }
    }
}
```

- [ ] **Step 4: Rework `PropertyGrid`**

Make these changes in `sources/IcyUI/UI/Controls/PropertyGrid.cs`:

1. **Fields:**

```csharp
        private PropertyGridValueAdapter valueAdapter = PropertyGridValueAdapter.Default;
        private PropertyGridEntry? writingEntry;
```

2. **`ValueAdapter`**, after `Target`:

```csharp
        /// <summary>
        /// Gets or sets how rows read and write <see cref="Target"/>'s properties. Defaults to
        /// <see cref="PropertyGridValueAdapter.Default"/>, which reads and writes them directly.
        /// </summary>
        /// <remarks>Setting it rebuilds every row.</remarks>
        /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
        public PropertyGridValueAdapter ValueAdapter
        {
            get => valueAdapter;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (ReferenceEquals(valueAdapter, value))
                    return;
                valueAdapter = value;
                ItemsSource = BuildRows(target);
            }
        }
```

3. **A private row type**, so `Refresh` can find each row's entry without a side table:

```csharp
        private sealed class PropertyRow(PropertyGridEntry entry) : Grid
        {
            public PropertyGridEntry Entry { get; } = entry;
        }
```

4. **`CreateContainer`:** build a `PropertyRow` instead of a `Grid`, with a third `Auto` column, and fill it through a new `FillRow(PropertyRow row, object target)`:

```csharp
            var row = new PropertyRow(entry);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // keep the existing comment above this line

            var label = new TextBlock { Text = entry.DisplayName, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);
            FillRow(row, currentTarget);
            return new ItemContainer { Content = row };
```

```csharp
        /// <summary>
        /// Replaces <paramref name="row"/>'s editor and Reset button with fresh ones reading the current value.
        /// </summary>
        private void FillRow(PropertyRow row, object target)
        {
            while (row.Children.Count > 1)
                row.Children.RemoveAt(row.Children.Count - 1);

            UIElement editor = BuildEditor(row.Entry, target);
            Grid.SetColumn(editor, 1);
            row.Children.Add(editor);

            if (valueAdapter.CanReset(row.Entry, target))
            {
                PropertyGridEntry entry = row.Entry;
                var reset = new Button
                {
                    Content = new TextBlock { Text = "Reset" },
                    Padding = new Thickness(6, 2),
                    Margin = new Thickness(4, 0, 0, 0),
                    Command = new ActionCommand(() => valueAdapter.Reset(entry, target)),
                };
                Grid.SetColumn(reset, 2);
                row.Children.Add(reset);
            }
        }
```

Check `grep -rn "class .*: ICommand" sources/IcyUI` for an existing core action-command type and use it. If none exists, add a private nested `ActionCommand(Action execute) : ICommand` (`CanExecute` always true, a no-op `CanExecuteChanged` with empty add/remove).

5. **`BuildEditor`:**
   - Replace `entry.GetValue(target)` with `valueAdapter.GetValue(entry, target)`.
   - Insert the expression branch first, after the value read:

```csharp
            if (valueAdapter.GetExpression(entry, target) is { } expression)
                return BuildExpressionEditor(entry, target, expression);
```

6. **Route every write through the adapter.** Replace every `entry.TrySetValue(target, X)` in `BuildEditor`, `BuildNumericEditor`, `BuildRangedNumericEditor`, `BuildEnumEditor`, `BuildColorEditor` and `BuildVectorEditor` with `Write(entry, target, X)`:

```csharp
        private bool Write(PropertyGridEntry entry, object target, object? value)
        {
            // A write may refresh this grid synchronously (an adapter whose document raises Changed); the row being
            // written keeps its editor so an open popup or a slider drag survives.
            PropertyGridEntry? previous = writingEntry;
            writingEntry = entry;
            try
            {
                return valueAdapter.TrySetValue(entry, target, value);
            }
            finally
            {
                writingEntry = previous;
            }
        }
```

7. **The expression editor and its commit hook:**

```csharp
        private UIElement BuildExpressionEditor(PropertyGridEntry entry, object target, string expression)
        {
            var box = new TextBox { Text = expression, IsEnabled = !entry.IsReadOnly };
            box.FocusChanged += (_, _) =>
            {
                if (!box.IsFocused)
                    CommitExpression(box, entry, target);
            };
            expressionCommits[box] = () => CommitExpression(box, entry, target);
            return box;
        }

        private void CommitExpression(TextBox box, PropertyGridEntry entry, object target)
        {
            bool accepted = valueAdapter.TrySetExpression(entry, target, box.Text);
            box.ControlState = accepted ? box.ControlState & ~ControlState.Invalid : box.ControlState | ControlState.Invalid;
        }

        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, Action> expressionCommits = new();

        /// <summary>Commits an expression row's text the way losing focus does. For tests.</summary>
        internal static void CommitExpressionForTest(TextBox box)
        {
            for (UIElement? element = box; element != null; element = element.Parent)
            {
                if (element is PropertyGrid grid && grid.expressionCommits.TryGetValue(box, out Action? commit))
                {
                    commit();
                    return;
                }
            }

            throw new InvalidOperationException("The box isn't an expression row of a PropertyGrid.");
        }
```

If `box.Parent` doesn't chain up to the grid before the grid is on a canvas, store the owning grid in a static `ConditionalWeakTable<TextBox, PropertyGrid>` instead, and look it up directly. Check `UIElement.Parent` for items-control containers before choosing. Add `using Icy.UI.Styles;` if `ControlState` isn't already in scope.

8. **`Refresh()`:**

```csharp
        /// <summary>
        /// Re-reads every row's value, expression and Reset state from <see cref="Target"/> through
        /// <see cref="ValueAdapter"/>, keeping the rows themselves.
        /// </summary>
        /// <remarks>
        /// Call it after something other than this grid changed the target, such as an undo. Two rows keep their editor:
        /// the one being written right now, and the one holding the keyboard focus, so typing is never interrupted.
        /// </remarks>
        public void Refresh()
        {
            if (target == null)
                return;

            UIElement? focused = Canvas?.FocusedElement;
            foreach (ItemContainer container in realizedContainers.Values)
            {
                if (container.Content is not PropertyRow row || ReferenceEquals(row.Entry, writingEntry))
                    continue;
                if (focused != null && row.Children.Count > 1 && IsSelfOrAncestor(row.Children[1], focused))
                    continue;
                FillRow(row, target);
            }
        }

        private static bool IsSelfOrAncestor(UIElement ancestor, UIElement element)
        {
            for (UIElement? current = element; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
            }

            return false;
        }
```

Equality for `writingEntry`: entries are rebuilt only by `BuildRows`, so the reference held by the row is the one the editor closure captured.

- [ ] **Step 5: Run the new tests and the existing PropertyGrid tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`
Expected: all pass. The existing `PropertyGridTests` `FindEditor` still finds `Children[1]`.

- [ ] **Step 6: Apply the spec deltas**

Edit `docs/superpowers/specs/2026-10-09-editor-toolkit-mvp-design.md`:
- In "Saving", replace "Undoing back to the saved state also clears it. It's tracked by undo-stack position." with "Undoing back to the saved text also clears it: it compares the text with the last saved or loaded text, because coalesced undo entries make the undo position unreliable."
- In "`MarkupPropertyAdapter`", replace the attached-properties sentence with "Attached properties (`Grid.Row`, …) aren't listed in the MVP; drags and the text edit them."
- Add a "Keyboard" bullet under `EditorOverlay`: "Editor key bindings stand down while a `TextBox` has focus, so typing in a panel never nudges or deletes the selection."
- In "Value formatting", drop `CornerRadius`, and say the formatter verifies by converting back through the configuration's type converter.
- Add "Attached-property rows in PropertiesPanel" to Out of scope.

- [ ] **Step 7: Check warnings, run the full suite, and commit**

Run: `dotnet build sources/IcyUI/IcyUI.csproj --no-incremental 2>&1 | grep -E ": warning (SA|CS)" | sort -u | wc -l`. Expected: `84`.
Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj`. Expected: Failed 0, Total = previous + 9.

```bash
git add sources/IcyUI/Data/PropertyGridValueAdapter.cs sources/IcyUI/UI/Controls/PropertyGrid.cs sources/IcyUI.Tests/Controls/PropertyGridAdapterTests.cs docs/superpowers/specs/2026-10-09-editor-toolkit-mvp-design.md
git commit -m "Let a PropertyGrid route reads and writes through an adapter, and refresh in place"
```

---

### Task 2: `MarkupValueFormatter`

**Files:**
- Create: `sources/IcyUI.Design/MarkupValueFormatter.cs`
- Test: `sources/IcyUI.Tests/Design/MarkupValueFormatterTests.cs`

**Interfaces:**
- Produces: `public sealed class MarkupValueFormatter` in `Icy.Design`:
  - `MarkupValueFormatter(ITypeConverter converter)`
  - `bool TryFormat(object? value, Type type, [NotNullWhen(true)] out string? text)`
- Consumes: `Icy.Data.ITypeConverter.Convert(object?, Type)`, `Icy.Design.Editor.Placement.MarkupValues.Format(Thickness)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Design/MarkupValueFormatterTests.cs
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Icy.Data;
using Icy.Design;
using Icy.Rendering.Brushes;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Design
{
    public class MarkupValueFormatterTests
    {
        [Flags]
        private enum Sides { None = 0, Left = 1, Right = 2 }

        private static readonly MarkupValueFormatter Formatter = new(new TypeConversionManager());

        public static TheoryData<object, Type> RoundTrips => new()
        {
            { "hello", typeof(string) },
            { true, typeof(bool) },
            { 42, typeof(int) },
            { -7L, typeof(long) },
            { 0.1f, typeof(float) },
            { 1e-7f, typeof(float) },
            { 123456.789, typeof(double) },
            { HorizontalAlignment.Right, typeof(HorizontalAlignment) },
            { Sides.Left | Sides.Right, typeof(Sides) },
            { new Thickness(1, 2, 3, 4), typeof(Thickness) },
            { new Thickness(5), typeof(Thickness) },
            { Color.FromArgb(128, 10, 20, 30), typeof(Color) },
        };

        [Theory]
        [MemberData(nameof(RoundTrips))]
        public void AFormattedValue_ConvertsBackToAnEqualValue(object value, Type type)
        {
            Assert.True(Formatter.TryFormat(value, type, out string? text));
            Assert.Equal(value, new TypeConversionManager().Convert(text, type));
        }

        [Fact]
        public void ASolidBrush_FormatsAsItsColor()
        {
            Assert.True(Formatter.TryFormat(new SolidColorBrush(Color.FromArgb(255, 1, 2, 3)), typeof(IBrush), out string? text));
            Assert.Equal("#FF010203", text);
        }

        [Fact]
        public void Numbers_UseTheInvariantCulture_EvenUnderACommaCulture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            try
            {
                Assert.True(Formatter.TryFormat(1.5f, typeof(float), out string? text));
                Assert.Equal("1.5", text);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Fact]
        public void AValueWithNoMarkupForm_IsRefused()
        {
            Assert.False(Formatter.TryFormat(new object(), typeof(object), out _));
            Assert.False(Formatter.TryFormat(null, typeof(int), out _));
        }

        [Fact]
        public void ANullString_FormatsAsEmpty()
        {
            Assert.True(Formatter.TryFormat(null, typeof(string), out string? text));
            Assert.Equal(string.Empty, text);
        }
    }
}
```

`IBrush`/`SolidColorBrush` live in `Icy.Rendering.Brushes`. Confirm with `grep -rn "class SolidColorBrush" sources/IcyUI`, and adjust the using if the namespace differs. If `SolidColorBrush` exposes its color under another name than `Color`, use that name in Step 3.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~MarkupValueFormatterTests"`
Expected: a build failure, because `MarkupValueFormatter` doesn't exist.

- [ ] **Step 3: Implement it**

```csharp
// sources/IcyUI.Design/MarkupValueFormatter.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Globalization;
using System.Numerics;
using Icy.Data;
using Icy.Design.Editor.Placement;
using Icy.Rendering.Brushes;
using Icy.UI;

namespace Icy.Design
{
    /// <summary>
    /// Writes property values as markup attribute text that loads back to the same value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each value gets a candidate text in the form people write by hand: invariant-culture numbers, enum names,
    /// <c>#AARRGGBB</c> colors, the shortest <see cref="Thickness"/> form, and comma-separated vector and size components.
    /// The candidate is then converted back with the configuration's <see cref="ITypeConverter"/>, and it's only accepted
    /// when that reproduces an equal value. A value with no verified form is refused, so an editor can show it read-only
    /// instead of writing markup that wouldn't load.
    /// </para>
    /// </remarks>
    public sealed class MarkupValueFormatter
    {
        private readonly ITypeConverter converter;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupValueFormatter"/> class.
        /// </summary>
        /// <param name="converter">The converter markup loads values with, usually <c>configuration.Types.TypeConverter</c>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="converter"/> is <see langword="null"/>.</exception>
        public MarkupValueFormatter(ITypeConverter converter)
        {
            ArgumentNullException.ThrowIfNull(converter);
            this.converter = converter;
        }

        /// <summary>
        /// Formats <paramref name="value"/> as markup text for a property of type <paramref name="type"/>.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="type">The property's type.</param>
        /// <param name="text">The markup text, when this method returns <see langword="true"/>.</param>
        /// <returns><see langword="true"/> when a text was found that loads back to an equal value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="type"/> is <see langword="null"/>.</exception>
        public bool TryFormat(object? value, Type type, [NotNullWhen(true)] out string? text)
        {
            ArgumentNullException.ThrowIfNull(type);
            text = null;

            if (type == typeof(string))
            {
                text = (string?)value ?? string.Empty;
                return true;
            }

            if (value == null || Candidate(value) is not { } candidate)
                return false;

            if (!RoundTrips(candidate, value, type))
                return false;

            text = candidate;
            return true;
        }

        private static string? Candidate(object value) => value switch
        {
            bool b => b ? "True" : "False",
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            Enum e => e.ToString().Replace(" ", string.Empty, StringComparison.Ordinal),
            Thickness t => MarkupValues.Format(t),
            Color c => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}",
            SolidColorBrush brush => Candidate(brush.Color),
            Vector2 v => Join(v.X, v.Y),
            Vector3 v => Join(v.X, v.Y, v.Z),
            Vector4 v => Join(v.X, v.Y, v.Z, v.W),
            Point p => $"{p.X.ToString(CultureInfo.InvariantCulture)},{p.Y.ToString(CultureInfo.InvariantCulture)}",
            Size s => $"{s.Width.ToString(CultureInfo.InvariantCulture)},{s.Height.ToString(CultureInfo.InvariantCulture)}",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => null,
        };

        private static string Join(params float[] values) =>
            string.Join(",", values.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));

        private bool RoundTrips(string candidate, object value, Type type)
        {
            try
            {
                object? back = converter.Convert(candidate, type);
                return back is SolidColorBrush loaded && value is SolidColorBrush original
                    ? loaded.Color == original.Color
                    : Equals(back, value);
            }
            catch (Exception ex) when (ex is InvalidCastException or FormatException or ArgumentException or OverflowException or NotSupportedException)
            {
                return false;
            }
        }
    }
}
```

`Enum.ToString()` for flags yields `"Left, Right"`. Removing the spaces gives `"Left,Right"`, which `Enum.Parse` accepts. If the round trip still fails for flags, keep the spaces. The round-trip check makes either choice safe.

- [ ] **Step 4: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~MarkupValueFormatterTests"`
Expected: all pass. If a `RoundTrips` row fails because markup has no form for that type, the formatter is behaving correctly by refusing it. Remove the row, and note the type in the commit message as "no markup form". Don't weaken the verification.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI.Design/MarkupValueFormatter.cs sources/IcyUI.Tests/Design/MarkupValueFormatterTests.cs
git commit -m "Add a markup value formatter that verifies each value by loading it back"
```

---

### Task 3: Save state: `IsModified`, `CanSave`, `SaveAll`, `UseSourceRoot`, `FindSourceRoot`

**Files:**
- Modify: `sources/IcyUI.Design/DesignDocument.cs`
- Modify: `sources/IcyUI.Design/DesignSession.cs`
- Test: `sources/IcyUI.Tests/Design/SaveStateTests.cs`

**Interfaces:**
- Produces, on `DesignDocument`:
  - `bool IsModified`
  - `event EventHandler? ModifiedChanged`
  - `bool CanSave`
  - `string? SaveBlockedReason`
- Produces, on `DesignSession`:
  - `IReadOnlyList<(DesignDocument Document, string Reason)> SaveAll()`
  - `void UseSourceRoot(string root)`
  - `static string? FindSourceRoot(string marker, string relative = "")`
  - `static string? FindSourceRoot(string startDirectory, string marker, string relative)`, the testable overload

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Design/SaveStateTests.cs
using Icy.Design;
using Xunit;

namespace Icy.Tests.Design
{
    public sealed class SaveStateTests : IDisposable
    {
        private const string Markup = """<StackPanel><Button x:Name="b" Width="10"/></StackPanel>""";
        private readonly string root = Directory.CreateTempSubdirectory("icy-save-").FullName;
        private readonly DesignTestHost host = new();

        public void Dispose()
        {
            host.Dispose();
            Directory.Delete(root, recursive: true);
        }

        [Fact]
        public void ADocument_IsModifiedAfterAnEdit_AndCleanAfterSave()
        {
            File.WriteAllText(Path.Combine(root, "page.xml"), Markup);
            host.Session.UseSourceRoot(root);
            (var page, DesignDocument document) = host.Load(Markup, "page.xml");
            int raised = 0;
            document.ModifiedChanged += (_, _) => raised++;

            Assert.False(document.IsModified);
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(page, "b")), "Width", "20");
            Assert.True(document.IsModified);

            document.Save();
            Assert.False(document.IsModified);
            Assert.Equal(2, raised);
            Assert.Contains("Width=\"20\"", File.ReadAllText(Path.Combine(root, "page.xml")));
        }

        [Fact]
        public void UndoingBackToTheSavedText_ClearsModified()
        {
            (var page, DesignDocument document) = host.Load(Markup, "page.xml");
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(page, "b")), "Width", "20");

            document.Editor.UndoStack.Undo();

            Assert.False(document.IsModified);
        }

        [Fact]
        public void ADocumentOutsideTheSourceRoot_CannotSave_AndSaysWhy()
        {
            host.Session.UseSourceRoot(root);
            (_, DesignDocument document) = host.Load(Markup, "missing.xml");

            Assert.False(document.CanSave);
            Assert.Contains("missing.xml", document.SaveBlockedReason);
        }

        [Fact]
        public void ADocumentWithNoSourcePath_CannotSave()
        {
            (_, DesignDocument document) = host.Load(Markup, sourcePath: null);

            Assert.False(document.CanSave);
            Assert.Equal("The page has no source path.", document.SaveBlockedReason);
        }

        [Fact]
        public void SaveAll_SavesModifiedDocuments_AndReportsSkippedOnes()
        {
            File.WriteAllText(Path.Combine(root, "a.xml"), Markup);
            host.Session.UseSourceRoot(root);
            (var a, DesignDocument savable) = host.Load(Markup, "a.xml");
            (var b, DesignDocument unsavable) = host.Load(Markup, "b.xml");
            savable.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(a, "b")), "Width", "30");
            unsavable.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(b, "b")), "Width", "30");

            var skipped = host.Session.SaveAll();

            Assert.False(savable.IsModified);
            Assert.Single(skipped);
            Assert.Same(unsavable, skipped[0].Document);
            GC.KeepAlive(a);
            GC.KeepAlive(b);
        }

        [Fact]
        public void SaveAll_ReportsAnIOErrorInsteadOfThrowing()
        {
            string path = Path.Combine(root, "locked.xml");
            File.WriteAllText(path, Markup);
            host.Session.UseSourceRoot(root);
            (var page, DesignDocument document) = host.Load(Markup, "locked.xml");
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Icy.UI.UIElement>(page, "b")), "Width", "30");

            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                var skipped = host.Session.SaveAll();
                Assert.Single(skipped);
            }

            Assert.True(document.IsModified);
        }

        [Fact]
        public void FindSourceRoot_WalksUpToTheMarker()
        {
            string project = Path.Combine(root, "Game");
            string bin = Path.Combine(project, "bin", "Debug");
            Directory.CreateDirectory(bin);
            Directory.CreateDirectory(Path.Combine(project, "Assets", "UI"));
            File.WriteAllText(Path.Combine(project, "Game.csproj"), "<Project/>");

            Assert.Equal(Path.Combine(project, "Assets", "UI"), DesignSession.FindSourceRoot(bin, "Game.csproj", Path.Combine("Assets", "UI")));
            Assert.Null(DesignSession.FindSourceRoot(bin, "Other.csproj", string.Empty));
        }
    }
}
```

`DesignTestHost.Load(markup, sourcePath)` already exists, and the default `sourcePath` is `"test.xml"`.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~SaveStateTests"`
Expected: a build failure, because the members are missing.

- [ ] **Step 3: Implement it in `DesignDocument`**

1. **A field and its initialization:** add `private string savedText;`. In the constructor, after `parsedText = text;`, add `savedText = text;`.
2. **Funnel the three `Changed?.Invoke(...)` sites** (in `Apply`, `ApplyTextCore` and `TryFastSetAttribute`) through:

```csharp
        private void RaiseChanged(DocumentChangedEventArgs args)
        {
            bool wasModified = IsModified;
            lastModified = !string.Equals(text.Text, savedText, StringComparison.Ordinal);
            Changed?.Invoke(this, args);
            if (wasModified != lastModified)
                ModifiedChanged?.Invoke(this, EventArgs.Empty);
        }
```

   with `private bool lastModified;`.

3. **The public members:**

```csharp
        /// <summary>
        /// Occurs when <see cref="IsModified"/> changed.
        /// </summary>
        public event EventHandler? ModifiedChanged;

        /// <summary>
        /// Gets a value indicating whether the text differs from what was last loaded or saved.
        /// </summary>
        /// <remarks>Undoing back to the saved text makes it <see langword="false"/> again.</remarks>
        public bool IsModified => lastModified;

        /// <summary>
        /// Gets a value indicating whether <see cref="Save"/> has a file to write.
        /// </summary>
        public bool CanSave => SaveBlockedReason == null;

        /// <summary>
        /// Gets why <see cref="Save"/> can't write, or <see langword="null"/> when it can.
        /// </summary>
        public string? SaveBlockedReason =>
            SourcePath == null ? "The page has no source path."
            : Session.SourcePathResolver(SourcePath) == null ? $"'{SourcePath}' doesn't resolve to a file. Set the source root."
            : null;
```

4. **`Save()`:** use `SaveBlockedReason` for its exception message. **`SaveAs`:** after a successful write, call `MarkSaved()`:

```csharp
        private void MarkSaved()
        {
            savedText = text.Text;
            if (lastModified)
            {
                lastModified = false;
                ModifiedChanged?.Invoke(this, EventArgs.Empty);
            }
        }
```

5. **`ReloadFromSource()`:** after `ApplyText(...)`, call `MarkSaved()`, since the text now equals the file.

- [ ] **Step 4: Implement it in `DesignSession`**

```csharp
        /// <summary>
        /// Saves every modified document that can be saved.
        /// </summary>
        /// <returns>
        /// The modified documents that weren't saved, with the reason: <see cref="DesignDocument.SaveBlockedReason"/>,
        /// or the message of the I/O error that stopped the write. Never throws for one document's failure.
        /// </returns>
        /// <exception cref="ObjectDisposedException">The session was disposed.</exception>
        public IReadOnlyList<(DesignDocument Document, string Reason)> SaveAll()
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var skipped = new List<(DesignDocument, string)>();
            foreach (DesignDocument document in Documents)
            {
                if (!document.IsModified)
                    continue;
                if (document.SaveBlockedReason is { } reason)
                {
                    skipped.Add((document, reason));
                    continue;
                }

                try
                {
                    document.Save();
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    skipped.Add((document, ex.Message));
                }
            }

            return skipped;
        }

        /// <summary>
        /// Makes <see cref="DesignDocument.Save"/> write each page to <paramref name="root"/> combined with its source
        /// path, for pages loaded by asset name such as <c>UI/MainMenu.xml</c>.
        /// </summary>
        /// <param name="root">The folder holding the source markup files, usually inside the game project.</param>
        /// <remarks>
        /// Only existing files resolve, so a typo in the root disables Save with a reason instead of creating files in the
        /// wrong place. Use <see cref="FindSourceRoot(string, string)"/> to locate the folder from the running game.
        /// </remarks>
        /// <exception cref="ArgumentException"><paramref name="root"/> is <see langword="null"/> or empty.</exception>
        public void UseSourceRoot(string root)
        {
            ArgumentException.ThrowIfNullOrEmpty(root);
            string full = Path.GetFullPath(root);
            SourcePathResolver = path =>
            {
                string candidate = Path.GetFullPath(Path.Combine(full, path));
                return File.Exists(candidate) ? candidate : null;
            };
        }

        /// <summary>
        /// Finds a source folder by walking up from <see cref="AppContext.BaseDirectory"/> to the first directory that
        /// contains <paramref name="marker"/>.
        /// </summary>
        /// <param name="marker">A file name that marks the project folder, such as <c>MyGame.csproj</c>.</param>
        /// <param name="relative">The path from that folder to the markup files, such as <c>Assets/UI</c>.</param>
        /// <returns>The combined path, or <see langword="null"/> when no ancestor contains the marker.</returns>
        /// <example>
        /// <code>
        /// if (DesignSession.FindSourceRoot("MyGame.csproj", "Assets/UI") is { } root)
        ///     session.UseSourceRoot(root);
        /// </code>
        /// </example>
        public static string? FindSourceRoot(string marker, string relative = "") =>
            FindSourceRoot(AppContext.BaseDirectory, marker, relative);

        /// <summary>
        /// Finds a source folder by walking up from <paramref name="startDirectory"/> to the first directory that contains
        /// <paramref name="marker"/>.
        /// </summary>
        /// <param name="startDirectory">The directory to start from.</param>
        /// <param name="marker">A file name that marks the project folder.</param>
        /// <param name="relative">The path from that folder to the markup files.</param>
        /// <returns>The combined path, or <see langword="null"/> when no ancestor contains the marker.</returns>
        public static string? FindSourceRoot(string startDirectory, string marker, string relative)
        {
            ArgumentException.ThrowIfNullOrEmpty(startDirectory);
            ArgumentException.ThrowIfNullOrEmpty(marker);
            for (DirectoryInfo? directory = new(startDirectory); directory != null; directory = directory.Parent)
            {
                if (File.Exists(Path.Combine(directory.FullName, marker)))
                    return Path.Combine(directory.FullName, relative ?? string.Empty).TrimEnd(Path.DirectorySeparatorChar);
            }

            return null;
        }
```

- [ ] **Step 5: Run the tests and the existing Design tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~Icy.Tests.Design"`
Expected: all pass. Check the `Save()` message tests in `DesignLifetimeTests` and `DesignSessionTests`. If one asserts the old message, update its expectation to the new reason text.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI.Design/DesignDocument.cs sources/IcyUI.Design/DesignSession.cs sources/IcyUI.Tests/Design/SaveStateTests.cs
git commit -m "Track unsaved changes and add Save all, a source root and its discovery"
```

---

### Task 4: `MarkupPropertyAdapter` and `PropertiesPanel`

**Files:**
- Create: `sources/IcyUI.Design/Editor/Panels/MarkupPropertyAdapter.cs`
- Create: `sources/IcyUI.Design/Editor/Panels/PropertiesPanel.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/PropertiesPanelTests.cs`

**Interfaces:**
- Consumes: `PropertyGridValueAdapter` and `PropertyGrid.ValueAdapter`/`Refresh()` (Task 1), `MarkupValueFormatter` (Task 2), `EditorSession.Selection`/`SelectionChanged`, `EditorSelection(Document, Node, Instance)`, `MarkupEditor.SetAttribute`/`ClearAttribute`, `ElementSyntax.FindAttribute(name)`.
- Produces:
  - `public sealed class MarkupPropertyAdapter : PropertyGridValueAdapter`, with:
    - constructor `(DesignDocument document, NodeId node, MarkupValueFormatter formatter)`;
    - `string? LastError { get; }`.
  - `public class PropertiesPanel : Control` in `Icy.Design.Editor.Panels`, with:
    - `EditorSession? Session { get; set; }`;
    - `PropertyGrid Grid { get; }`;
    - `string StatusText { get; }`.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Design/Editor/PropertiesPanelTests.cs
using Icy.Design.Editor.Panels;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class PropertiesPanelTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <TextBlock x:Name="bound" Text="{Binding Missing}"/>
              <Button x:Name="b" Width="40" Height="20"/>
            </StackPanel>
            """;

        private static (EditorTestHost Host, PropertiesPanel Panel) Create()
        {
            var host = new EditorTestHost(Page);
            var panel = new PropertiesPanel { Session = host.Session };
            host.Canvas.AddOverlay(panel);
            host.Render();
            return (host, panel);
        }

        [Fact]
        public void SelectingAnElement_ShowsItsProperties()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<Button>("b"));

                Assert.Same(host.Named<Button>("b"), panel.Grid.Target);
                Assert.IsType<MarkupPropertyAdapter>(panel.Grid.ValueAdapter);
            }
        }

        [Fact]
        public void ATypedEdit_WritesTheAttribute_AsOneUndoStep()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<Button>("b"));
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var entry = Icy.Data.PropertyGridEntry.EnumerateFor(host.Named<Button>("b")).Single(x => x.Name == "Width");

                Assert.True(adapter.TrySetValue(entry, host.Named<Button>("b"), 5f));
                Assert.True(adapter.TrySetValue(entry, host.Named<Button>("b"), 55f));

                Assert.Contains("Width=\"55\"", host.Document.Text);
                host.Document.Editor.UndoStack.Undo();
                Assert.Contains("Width=\"40\"", host.Document.Text);
            }
        }

        [Fact]
        public void AnExpressionAttribute_IsShownAsText_AndTypedEditsDontReplaceIt()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var bound = host.Named<TextBlock>("bound");
                host.Session.Select(bound);
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var text = Icy.Data.PropertyGridEntry.EnumerateFor(bound).Single(x => x.Name == "Text");

                Assert.Equal("{Binding Missing}", adapter.GetExpression(text, bound));
                Assert.True(adapter.TrySetExpression(text, bound, "{Binding Other}"));
                Assert.Contains("Text=\"{Binding Other}\"", host.Document.Text);
            }
        }

        [Fact]
        public void Reset_RemovesTheAttribute()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var button = host.Named<Button>("b");
                host.Session.Select(button);
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var height = Icy.Data.PropertyGridEntry.EnumerateFor(button).Single(x => x.Name == "Height");

                Assert.True(adapter.CanReset(height, button));
                adapter.Reset(height, button);

                Assert.DoesNotContain("Height=", host.Document.Text);
                Assert.False(adapter.CanReset(height, button));
            }
        }

        [Fact]
        public void AnUnsetProperty_GainsALocalAttributeOnFirstEdit()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var button = host.Named<Button>("b");
                host.Session.Select(button);
                var adapter = (MarkupPropertyAdapter)panel.Grid.ValueAdapter;
                var opacity = Icy.Data.PropertyGridEntry.EnumerateFor(button).Single(x => x.Name == "Opacity");

                Assert.False(adapter.CanReset(opacity, button));
                Assert.True(adapter.TrySetValue(opacity, button, 0.5f));

                Assert.Contains("Opacity=\"0.5\"", host.Document.Text);
                Assert.True(adapter.CanReset(opacity, button));
            }
        }

        [Fact]
        public void AnUndo_RefreshesTheGrid()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                var button = host.Named<Button>("b");
                host.Session.Select(button);
                host.Document.Editor.SetAttribute(host.IdOf(button), "Width", "70");
                host.Document.Editor.UndoStack.Undo();

                Assert.Equal(40, button.Width);
                Assert.Equal("40", FindEditorText(panel.Grid, "Width"));
            }
        }

        [Fact]
        public void ClearingTheSelection_EmptiesTheGrid()
        {
            (EditorTestHost host, PropertiesPanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<Button>("b"));
                host.Session.Clear();

                Assert.Null(panel.Grid.Target);
            }
        }

        private static string? FindEditorText(PropertyGrid grid, string name)
        {
            var containers = (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(grid)!;
            foreach (ItemContainer container in containers.Values)
            {
                if (container.Content is Grid row && row.Children[0] is TextBlock label && label.Text == name)
                    return (row.Children[1] as TextBox)?.Text;
            }

            return null;
        }
    }
}
```

Note: `Width` is a `float` property. If `PropertyGrid` shows the plain numeric editor (no `Range`), its text is `"40"`.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertiesPanelTests"`
Expected: a build failure.

- [ ] **Step 3: Implement `MarkupPropertyAdapter`**

```csharp
// sources/IcyUI.Design/Editor/Panels/MarkupPropertyAdapter.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data;
using Icy.Design.Syntax;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// Routes a <see cref="UI.Controls.PropertyGrid"/>'s edits of one markup element into its
    /// <see cref="DesignDocument"/>, so they're undoable and saved.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>Values are read from the live object, so styles, bindings and defaults show what's on screen.</description></item>
    /// <item><description>
    /// A typed edit formats the value with <see cref="MarkupValueFormatter"/> and sets the attribute. Quick successive
    /// edits of one property coalesce into one undo step. A value with no markup form is refused, and
    /// <see cref="LastError"/> says why.
    /// </description></item>
    /// <item><description>
    /// An attribute holding a markup extension (<c>{Binding …}</c>, <c>{StaticResource …}</c>) is shown and edited as text,
    /// so a typed editor never replaces it.
    /// </description></item>
    /// <item><description>Reset removes the attribute, letting the style or default value apply again.</description></item>
    /// </list>
    /// </remarks>
    public sealed class MarkupPropertyAdapter : PropertyGridValueAdapter
    {
        private readonly DesignDocument document;
        private readonly NodeId node;
        private readonly MarkupValueFormatter formatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupPropertyAdapter"/> class.
        /// </summary>
        /// <param name="document">The document the element belongs to.</param>
        /// <param name="node">The element.</param>
        /// <param name="formatter">Formats typed values as attribute text.</param>
        /// <exception cref="ArgumentNullException"><paramref name="document"/> or <paramref name="formatter"/> is <see langword="null"/>.</exception>
        public MarkupPropertyAdapter(DesignDocument document, NodeId node, MarkupValueFormatter formatter)
        {
            ArgumentNullException.ThrowIfNull(document);
            ArgumentNullException.ThrowIfNull(formatter);
            this.document = document;
            this.node = node;
            this.formatter = formatter;
        }

        /// <summary>
        /// Gets why the last write was refused, or <see langword="null"/> when it succeeded.
        /// </summary>
        public string? LastError { get; private set; }

        /// <inheritdoc/>
        public override bool TrySetValue(PropertyGridEntry entry, object target, object? value)
        {
            if (entry.IsReadOnly)
                return Refuse($"{entry.Name} is read-only.");
            if (!formatter.TryFormat(value, entry.PropertyType, out string? text))
                return Refuse($"{entry.Name}: this value has no markup form.");
            return Apply(document.Editor.SetAttribute(node, entry.Name, text));
        }

        /// <inheritdoc/>
        public override string? GetExpression(PropertyGridEntry entry, object target) =>
            Attribute(entry) is { } attribute && IsExpression(attribute.Value) ? attribute.Value : null;

        /// <inheritdoc/>
        public override bool TrySetExpression(PropertyGridEntry entry, object target, string text) =>
            Apply(document.Editor.SetAttribute(node, entry.Name, text));

        /// <inheritdoc/>
        public override bool CanReset(PropertyGridEntry entry, object target) => Attribute(entry) != null;

        /// <inheritdoc/>
        public override void Reset(PropertyGridEntry entry, object target) => Apply(document.Editor.ClearAttribute(node, entry.Name));

        private static bool IsExpression(string value) =>
            value.StartsWith('{') && !value.StartsWith("{}", StringComparison.Ordinal);

        private AttributeSyntax? Attribute(PropertyGridEntry entry) => document.GetNode(node)?.FindAttribute(entry.Name);

        private bool Apply(EditResult result)
        {
            LastError = result.Succeeded ? null : result.Error?.Message;
            return result.Succeeded;
        }

        private bool Refuse(string reason)
        {
            LastError = reason;
            return false;
        }
    }
}
```

Check `EditResult`'s members with `grep -n "public" sources/IcyUI.Design/EditResult.cs`. `Succeeded` and `Error?.Message` are what `EditorFrame.EndDrag` uses.

- [ ] **Step 4: Implement `PropertiesPanel`**

```csharp
// sources/IcyUI.Design/Editor/Panels/PropertiesPanel.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// Shows and edits the properties of an <see cref="EditorSession"/>'s selected element. Every edit becomes an undoable
    /// markup change.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The panel follows <see cref="EditorSession.SelectionChanged"/>, and refreshes its values whenever the selected
    /// element's document changes, whether from this panel, a drag in the editor frame, an undo or new text. It does no
    /// per-frame work.
    /// </para>
    /// <para>
    /// Place it anywhere: in an <see cref="EditorOverlay"/> dock, an <see cref="EditorWorkspace"/>, or your own debug page.
    /// </para>
    /// </remarks>
    public class PropertiesPanel : Control
    {
        private readonly TextBlock header = new() { Margin = new Thickness(6, 4) };
        private readonly TextBlock status = new() { Margin = new Thickness(6, 2), Foreground = System.Drawing.Color.Salmon };
        private EditorSession? session;
        private DesignDocument? watched;
        private MarkupValueFormatter? formatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertiesPanel"/> class.
        /// </summary>
        public PropertiesPanel()
        {
            Grid = new PropertyGrid();
            var layout = new UI.Controls.Grid { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var scroller = new ScrollViewer
            {
                Content = Grid,
                HorizontalScrollMode = ScrollMode.Disabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            UI.Controls.Grid.SetRow(scroller, 1);
            UI.Controls.Grid.SetRow(status, 2);
            layout.Children.Add(header);
            layout.Children.Add(scroller);
            layout.Children.Add(status);
            Content = layout;
            ShowEmpty();
        }

        /// <summary>
        /// Gets the grid the panel shows the properties in.
        /// </summary>
        public PropertyGrid Grid { get; }

        /// <summary>
        /// Gets the status line: why the last edit was refused, or empty.
        /// </summary>
        public string StatusText => status.Text;

        /// <summary>
        /// Gets or sets the session whose selection the panel shows, or <see langword="null"/> for an empty panel.
        /// </summary>
        public EditorSession? Session
        {
            get => session;
            set
            {
                if (ReferenceEquals(session, value))
                    return;
                if (session != null)
                    session.SelectionChanged -= OnSelectionChanged;
                session = value;
                formatter = value == null ? null : new MarkupValueFormatter(value.Design.Configuration.Types.TypeConverter);
                if (session != null)
                    session.SelectionChanged += OnSelectionChanged;
                Retarget();
            }
        }

        private void OnSelectionChanged(object? sender, EventArgs e) => Retarget();

        private void Retarget()
        {
            Watch(session?.Selection?.Document);
            if (session?.Selection is not { } selection || formatter == null)
            {
                ShowEmpty();
                return;
            }

            var adapter = new MarkupPropertyAdapter(selection.Document, selection.Node, formatter);
            Grid.Target = null;
            Grid.ValueAdapter = adapter;
            Grid.Target = selection.Instance;
            string name = selection.Instance.Name is { Length: > 0 } n ? $" \"{n}\"" : string.Empty;
            header.Text = $"{selection.Instance.GetType().Name}{name} — {selection.Document.SourcePath ?? "(no source)"}";
            status.Text = string.Empty;
        }

        private void ShowEmpty()
        {
            Grid.Target = null;
            header.Text = "Nothing selected";
            status.Text = string.Empty;
        }

        private void Watch(DesignDocument? document)
        {
            if (ReferenceEquals(watched, document))
                return;
            if (watched != null)
                watched.Changed -= OnDocumentChanged;
            watched = document;
            if (watched != null)
                watched.Changed += OnDocumentChanged;
        }

        private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
        {
            Grid.Refresh();
            status.Text = (Grid.ValueAdapter as MarkupPropertyAdapter)?.LastError ?? string.Empty;
        }
    }
}
```

The status line also needs to update on a refused write that doesn't change the document (a formatter refusal). Have `MarkupPropertyAdapter` raise an internal `event Action<string?>? ErrorChanged` from `Apply`/`Refuse`. The panel subscribes to it when it creates the adapter and sets `status.Text`. Add a test, `ARefusedValue_ShowsTheReasonInTheStatusLine`: set a property of type `object` through the adapter and assert `panel.StatusText` contains "no markup form".

Check `Control.Content` and `UIElement.Name` exist with those names (`grep -n "public .* Content\b\|public string? Name" sources/IcyUI/UI/Controls/Control.cs sources/IcyUI/UI/UIElement.cs`). If `Control` has no `Content`, derive from `ContentControl` instead. The `UI.Controls.Grid` qualification avoids a clash with the `Grid` property.

- [ ] **Step 5: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~PropertiesPanelTests"`
Expected: all pass.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI.Design/Editor/Panels/MarkupPropertyAdapter.cs sources/IcyUI.Design/Editor/Panels/PropertiesPanel.cs sources/IcyUI.Tests/Design/Editor/PropertiesPanelTests.cs
git commit -m "Add a Properties panel whose edits become undoable markup changes"
```

---

### Task 5: Editor key bindings stand down while a text box has focus

**Files:**
- Modify: `sources/IcyUI.Design/Editor/EditorBindings.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/EditorKeyGateTests.cs`

**Interfaces:**
- Consumes: `Canvas.FocusedElement`, `KeyCommandTable` dispatch semantics (a gesture-handling command whose `CanExecute` is false lets the gesture fall through).
- Produces: no new public API. The documented rule goes on the `EditorBindings` class remarks.

- [ ] **Step 1: Write the failing test**

```csharp
// sources/IcyUI.Tests/Design/Editor/EditorKeyGateTests.cs
using Icy.Input.Devices;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorKeyGateTests
    {
        private const string Page =
            """
            <StackPanel HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="b" Width="40" Height="20" Margin="10"/>
            </StackPanel>
            """;

        [Fact]
        public void ArrowKeys_DontNudge_WhileATextBoxHasFocus()
        {
            using var host = new EditorTestHost(Page);
            var box = new TextBox { Width = 100 };
            host.Canvas.AddOverlay(box);
            host.Render();
            host.Session.Select(host.Named<Button>("b"));

            host.Canvas.Focus(box);
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));

            Assert.DoesNotContain("Margin=\"11", host.Document.Text);

            host.Canvas.Focus(null);
            host.Input.Events.RaiseGesture(new KeyGesture(Keys.Right));

            Assert.Contains("Margin=\"11,10,10,10\"", host.Document.Text);
        }
    }
}
```

Keys are raised the way `EditorCommandTests` does: `host.Input.Events.RaiseGesture(new KeyGesture(...))`. Match the nudge assertion to what `EditorCommandTests` expects for a margin nudge.

- [ ] **Step 2: Run the test and confirm it fails**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~EditorKeyGateTests"`
Expected: FAIL, because the margin is nudged while the box has focus.

- [ ] **Step 3: Wrap the registered commands**

In `EditorBindings`, register a gate around each command instead of the command itself. Keep a map so `Unbind`/`Unregister` remove the same wrapper:

```csharp
        private readonly Dictionary<ICommand, ICommand> gates = new(ReferenceEqualityComparer.Instance);
        private Func<bool> textInputFocused = static () => false;

        internal void Register(IInputEventSystem events, Func<bool> isTextInputFocused)
        {
            registered = events;
            textInputFocused = isTextInputFocused;
            foreach ((KeyGesture gesture, ICommand command) in bindings)
                events.RegisterCommand(Gate(command), gesture, null, handlesGesture: true);
        }

        private ICommand Gate(ICommand command)
        {
            if (!gates.TryGetValue(command, out ICommand? gate))
            {
                gate = new KeyGate(command, () => textInputFocused());
                gates[command] = gate;
            }

            return gate;
        }

        private sealed class KeyGate(ICommand inner, Func<bool> blocked) : ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add => inner.CanExecuteChanged += value;
                remove => inner.CanExecuteChanged -= value;
            }

            public bool CanExecute(object? parameter) => !blocked() && inner.CanExecute(parameter);

            public void Execute(object? parameter)
            {
                if (CanExecute(parameter))
                    inner.Execute(parameter);
            }
        }
```

- Use `Gate(command)` in `Bind`, `Unbind` and `Unregister` wherever the raw command is passed to `RegisterCommand`/`UnregisterCommand`.
- Update the single `Register` call site in `EditorSession` (`grep -n "Bindings.Register" sources/IcyUI.Design/Editor/EditorSession.cs`) to pass `() => Canvas.FocusedElement is TextBox`.
- Add to the `EditorBindings` class remarks: "While a <see cref=\"TextBox\"/> on the canvas has focus, the bindings stand down, so typing in a panel never moves or deletes the selection."

- [ ] **Step 4: Run the gate test and every editor test**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~Icy.Tests.Design.Editor"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI.Design/Editor/EditorBindings.cs sources/IcyUI.Design/Editor/EditorSession.cs sources/IcyUI.Tests/Design/Editor/EditorKeyGateTests.cs
git commit -m "Let editor key bindings stand down while a text box has focus"
```

---

### Task 6: `OutlinePanel`

**Files:**
- Create: `sources/IcyUI.Design/Editor/Panels/OutlineItem.cs`
- Create: `sources/IcyUI.Design/Editor/Panels/OutlinePanel.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/OutlinePanelTests.cs`

**Interfaces:**
- Consumes:
  - `TreeView` (`ItemsSource`, `ChildrenSelector`, `SelectedItem` setter reveals, `SelectionChanged`, `IsExpanded`/`Expand`);
  - `DesignDocument.Syntax`/`GetNodeId`/`Changed`/`DiagnosticsChanged`/`SyncStateChanged`/`IsInSync`;
  - `EditorSession.Select(document, node)`/`Selection`/`SelectionChanged`/`DeleteSelection()`;
  - `MarkupEditor.InsertElement(parent, index, markup)`/`MoveElement(node, newParent, index)`.
- Produces:
  - `public sealed class OutlineItem`, with:
    - `NodeId Node`
    - `string Label`
    - `bool IsHealthy`
    - `ObservableCollection<OutlineItem> Children`
    - `INotifyPropertyChanged`
  - `public class OutlinePanel : Control`, with:
    - `EditorSession? Session`
    - `DesignDocument? Document`
    - `TreeView Tree`
    - `IReadOnlyList<OutlineItem> Roots`
    - `EditResult Duplicate()`
    - `EditResult Move(OutlineItem item, OutlineItem target, OutlineDropPosition position)`
  - `public enum OutlineDropPosition { Before, After, Inside }`

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Design/Editor/OutlinePanelTests.cs
using Icy.Design;
using Icy.Design.Editor.Panels;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class OutlinePanelTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="first" Width="40" Height="20"/>
              <StackPanel x:Name="inner">
                <TextBlock x:Name="leaf" Text="x"/>
              </StackPanel>
            </StackPanel>
            """;

        private static (EditorTestHost Host, OutlinePanel Panel) Create()
        {
            var host = new EditorTestHost(Page);
            var panel = new OutlinePanel { Session = host.Session };
            host.Canvas.AddOverlay(panel);
            host.Render();
            return (host, panel);
        }

        [Fact]
        public void TheOutline_MirrorsTheElementTree_WithTypeAndNameLabels()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;

                OutlineItem root = Assert.Single(panel.Roots);
                Assert.Equal("StackPanel \"root\"", root.Label);
                Assert.Equal(["Button \"first\"", "StackPanel \"inner\""], root.Children.Select(x => x.Label));
                Assert.Equal("TextBlock \"leaf\"", root.Children[1].Children[0].Label);
            }
        }

        [Fact]
        public void SelectingInTheFrame_SelectsTheRow_AndSelectingARow_SelectsTheElement()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<TextBlock>("leaf"));
                Assert.Same(host.Document, panel.Document);
                Assert.Equal("TextBlock \"leaf\"", ((OutlineItem)panel.Tree.SelectedItem!).Label);

                panel.Tree.SelectedItem = panel.Roots[0].Children[0];
                Assert.Same(host.Named<Button>("first"), host.Session.Selection!.Instance);
            }
        }

        [Fact]
        public void AnAttributeEdit_KeepsTheSameItems()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                OutlineItem before = panel.Roots[0].Children[0];

                host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("first")), "Width", "60");

                Assert.Same(before, panel.Roots[0].Children[0]);
            }
        }

        [Fact]
        public void AStructuralEdit_UpdatesTheTree_AndKeepsExpansion()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                OutlineItem inner = panel.Roots[0].Children[1];
                panel.Tree.Expand(inner);

                host.Document.Editor.RemoveElement(host.IdOf(host.Named<Button>("first")));

                Assert.Same(inner, panel.Roots[0].Children[0]);
                Assert.True(panel.Tree.IsExpanded(inner));
            }
        }

        [Fact]
        public void RetargetingToAnotherDocument_ShowsOnlyItsElements()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                var other = new Icy.Markup.MarkupLoader(host.Configuration).Load("<Border x:Name=\"solo\"/>", "other.xml");
                DesignDocument otherDocument = host.Design.FindDocument(other, out _)!;

                panel.Document = otherDocument;

                OutlineItem root = Assert.Single(panel.Roots);
                Assert.Equal("Border \"solo\"", root.Label);
                Assert.Empty(root.Children);

                panel.Document = host.Document;
                Assert.Equal("StackPanel \"root\"", Assert.Single(panel.Roots).Label);
                GC.KeepAlive(other);
            }
        }

        [Fact]
        public void Duplicate_InsertsACopyAfterTheSelection()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                host.Session.Select(host.Named<TextBlock>("leaf"));

                Assert.True(panel.Duplicate().Succeeded);

                Assert.Equal(2, panel.Roots[0].Children[1].Children.Count);
            }
        }

        [Fact]
        public void Move_PutsAnElementInsideAnotherContainer()
        {
            (EditorTestHost host, OutlinePanel panel) = Create();
            using (host)
            {
                panel.Document = host.Document;
                OutlineItem first = panel.Roots[0].Children[0];
                OutlineItem inner = panel.Roots[0].Children[1];

                Assert.True(panel.Move(first, inner, OutlineDropPosition.Inside).Succeeded);

                Assert.Equal(["StackPanel \"inner\""], panel.Roots[0].Children.Select(x => x.Label));
                Assert.Equal(2, panel.Roots[0].Children[0].Children.Count);
            }
        }
    }
}
```

Duplicating a named element would duplicate `x:Name`. `Duplicate` strips the `x:Name` attribute from the copied text (see Step 3).

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~OutlinePanelTests"`
Expected: a build failure.

- [ ] **Step 3: Implement `OutlineItem` and `OutlinePanel`**

`OutlineItem`, all public members documented:

```csharp
    public sealed class OutlineItem : INotifyPropertyChanged
    {
        private string label;
        private bool isHealthy = true;

        internal OutlineItem(NodeId node, string label)
        {
            Node = node;
            this.label = label;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public NodeId Node { get; }

        public string Label
        {
            get => label;
            internal set => Set(ref label, value);
        }

        // False while the element's live copy failed to update (the document is out of sync for it).
        public bool IsHealthy
        {
            get => isHealthy;
            internal set => Set(ref isHealthy, value);
        }

        public ObservableCollection<OutlineItem> Children { get; } = [];

        public override string ToString() => Label;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
```

`OutlinePanel` behaviour:

- **Fields:**
  - `Dictionary<NodeId, OutlineItem> items` holds the reuse cache. It's cleared when `Document` changes, which is what prevents stale rows across documents.
  - `ObservableCollection<OutlineItem> roots`.
  - `bool syncing` guards the two-way selection.
- **Constructor:**
  - `Tree = new TreeView { ItemsSource = roots, ChildrenSelector = x => ((OutlineItem)x).Children }`.
  - `Tree.SelectionChanged += OnTreeSelection`.
  - `Content = new ScrollViewer { Content = Tree, ... }` if `TreeView` doesn't scroll itself. Check `TreeViewDemo` for how it hosts one.
- **`Document` setter:**
  - Unsubscribe `Changed` from the old document and subscribe to the new one.
  - `items.Clear(); roots.Clear(); Rebuild();`.
- **`Session` setter:** subscribe to `SelectionChanged`. On a change, if `Selection` is not null, set `Document = selection.Document`. Then, under `syncing = true`, set `Tree.SelectedItem = items.GetValueOrDefault(selection.Node)`. That setter reveals the item: it expands the ancestors and scrolls.
- **`OnTreeSelection`:** if not `syncing` and `Tree.SelectedItem is OutlineItem item`, wrap `Session?.Select(Document!, item.Node)` in `try { } catch (ArgumentException) { }`. An element with no live copy in scope can't be selected, which is fine for the outline.
- **`Rebuild()`** runs on `Changed` and on `Document` set:

```csharp
        private void Rebuild()
        {
            if (document?.Syntax.Root is not { } root)
            {
                roots.Clear();
                return;
            }

            var alive = new HashSet<NodeId>();
            OutlineItem? top = Build(root, alive);
            Sync(roots, top == null ? [] : [top]);
            foreach (NodeId gone in items.Keys.Where(x => !alive.Contains(x)).ToList())
                items.Remove(gone);
        }

        private OutlineItem? Build(ElementSyntax element, HashSet<NodeId> alive)
        {
            if (document!.GetNodeId(element) is not { } id)
                return null;
            alive.Add(id);
            if (!items.TryGetValue(id, out OutlineItem? item))
                items[id] = item = new OutlineItem(id, LabelOf(element));
            item.Label = LabelOf(element);

            var children = new List<OutlineItem>();
            foreach (ElementSyntax child in element.ContentElements)
            {
                if (Build(child, alive) is { } built)
                    children.Add(built);
            }

            Sync(item.Children, children);
            return item;
        }

        private static string LabelOf(ElementSyntax element)
        {
            string? name = element.FindAttribute("x:Name")?.Value ?? element.FindAttribute("Name")?.Value;
            return name is { Length: > 0 } ? $"{element.LocalName} \"{name}\"" : element.LocalName;
        }

        // Makes target equal to desired with the fewest collection changes, so the TreeView keeps realized rows.
        private static void Sync(ObservableCollection<OutlineItem> target, IReadOnlyList<OutlineItem> desired)
        {
            for (int i = 0; i < desired.Count; i++)
            {
                if (i < target.Count && ReferenceEquals(target[i], desired[i]))
                    continue;
                int existing = target.IndexOf(desired[i]);
                if (existing > i)
                    target.Move(existing, i);
                else
                    target.Insert(i, desired[i]);
            }

            while (target.Count > desired.Count)
                target.RemoveAt(target.Count - 1);
        }
```

  `Rebuild` is O(elements) per change, but it only mutates what changed. This meets the spec's "full rebuild only on structural edits" intent at the TreeView level. The perf test in Task 11 measures it.

- **`IsHealthy`:** on `SyncStateChanged`, set `IsHealthy = document.IsInSync` for every item. MVP granularity is the whole document; per-node health is a later refinement. The item template shows unhealthy items in `Color.Salmon`. Set a simple `Tree.ItemTemplateSelector`, or rely on `ToString` for the label and skip the colour if templating costs more than 10 minutes. Note which you chose in the commit.
- **`Duplicate()`:**

```csharp
        public EditResult Duplicate()
        {
            if (Session?.Selection is not { } selection || selection.Document.GetNode(selection.Node) is not { } element || element.Parent is not { } parent)
                return EditResult.Failure(default, "Select an element below the root to duplicate it.");
            DesignDocument doc = selection.Document;
            string copy = doc.Text.Substring(element.Span.Start, element.Span.Length);
            if (element.FindAttribute("x:Name") is { } name)
                copy = copy.Remove(name.Span.Start - element.Span.Start, name.Span.Length).Replace("  ", " ", StringComparison.Ordinal);
            int index = parent.ContentElements.ToList().IndexOf(element) + 1;
            return doc.Editor.InsertElement(doc.GetNodeId(parent)!.Value, index, copy);
        }
```

  `AttributeSyntax` derives from `MarkupSyntaxNode`, so it has a `Span`. Check `Span` covers the whole `name="value"`, including a leading space if the parser includes it. Adjust the removal so the copy stays well-formed, and assert the copy's text in the test if the trimming is fiddly.

- **`Move(item, target, position)`:**
  - `Inside`: `MoveElement(item.Node, target.Node, index: target.Children.Count)`.
  - `Before`/`After`: the target's parent from `document.GetNode(target.Node)!.Parent`, at the target's content index (`+1` for `After`).
  - Refuse moving an element into itself or a descendant (`ElementSyntax.IsAncestorOf`) with a failure result.
  - Pointer drag-reordering in the tree: if `TreeView` exposes a drag-drop hook (`grep -n "Drag\|Drop" sources/IcyUI/UI/Controls/TreeView*.cs`), wire it to `Move`. If not, the MVP has only the API plus Alt+Up/Down keyboard commands on the panel, and pointer drag-reorder is a listed follow-up. Say which in the commit.
- **Key handling:** override `OnKeyDown` (or the canvas's key hook used by other controls; `grep -n "OnKeyDown\|KeyDown" sources/IcyUI/UI/Controls/ListBox.cs`) for Ctrl+D → `Duplicate()` and Delete → `Session.DeleteSelection()`. Only while the tree has focus.

- [ ] **Step 4: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~OutlinePanelTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI.Design/Editor/Panels/OutlineItem.cs sources/IcyUI.Design/Editor/Panels/OutlinePanel.cs sources/IcyUI.Tests/Design/Editor/OutlinePanelTests.cs
git commit -m "Add an Outline panel with two-way selection, duplicate and move"
```

---

### Task 7: `ToolboxPanel`

**Files:**
- Create: `sources/IcyUI.Design/Editor/Panels/ToolboxItem.cs`
- Create: `sources/IcyUI.Design/Editor/Panels/ToolboxPanel.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/ToolboxPanelTests.cs`

**Interfaces:**
- Consumes: `MarkupConfiguration.BuiltInNamespaces`, `EditorSession.Selection`/`Select(document, node)`, `MarkupEditor.InsertElement(parent, index, markup)` (returns `EditResult` whose `Node` is the inserted element; check the property name on `EditResult`).
- Produces:
  - `public sealed record ToolboxItem(string DisplayName, string Category, string Snippet)`, with `static IReadOnlyList<ToolboxItem> CreateDefaults(IcyConfiguration configuration)`.
  - `public class ToolboxPanel : Control`, with:
    - `EditorSession? Session`
    - `ObservableCollection<ToolboxItem> Items`
    - `EditResult Insert(ToolboxItem item)`

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Design/Editor/ToolboxPanelTests.cs
using Icy.Design.Editor.Panels;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class ToolboxPanelTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="b" Width="40" Height="20"/>
            </StackPanel>
            """;

        [Fact]
        public void TheDefaults_ListBuiltInControls_WithLoadableSnippets()
        {
            using var host = new EditorTestHost(Page);
            var defaults = ToolboxItem.CreateDefaults(host.Configuration);

            Assert.Contains(defaults, x => x.DisplayName == "Button" && x.Snippet == "<Button Content=\"Button\"/>");
            Assert.Contains(defaults, x => x.DisplayName == "StackPanel");
            Assert.DoesNotContain(defaults, x => x.DisplayName == "UIElement");
            foreach (ToolboxItem item in defaults)
                new Icy.Markup.MarkupLoader(host.Configuration).Load(item.Snippet);
        }

        [Fact]
        public void Insert_AddsIntoASelectedContainer_AndSelectsTheNewElement()
        {
            using var host = new EditorTestHost(Page);
            var panel = new ToolboxPanel { Session = host.Session };
            host.Session.Select(host.Named<StackPanel>("root"));

            Assert.True(panel.Insert(new ToolboxItem("Slider", "Controls", "<Slider/>")).Succeeded);

            Assert.Contains("<Slider/>", host.Document.Text);
            Assert.IsType<Slider>(host.Session.Selection!.Instance);
        }

        [Fact]
        public void Insert_AddsAfterASelectedLeaf()
        {
            using var host = new EditorTestHost(Page);
            var panel = new ToolboxPanel { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));

            Assert.True(panel.Insert(new ToolboxItem("CheckBox", "Controls", "<CheckBox/>")).Succeeded);

            Assert.True(host.Document.Text.IndexOf("<CheckBox/>", StringComparison.Ordinal) > host.Document.Text.IndexOf("x:Name=\"b\"", StringComparison.Ordinal));
        }

        [Fact]
        public void Insert_WithNothingSelected_SaysWhy()
        {
            using var host = new EditorTestHost(Page);
            var panel = new ToolboxPanel { Session = host.Session };

            var result = panel.Insert(new ToolboxItem("Button", "Controls", "<Button/>"));

            Assert.False(result.Succeeded);
            Assert.Equal("Select where to insert first.", result.Error!.Message);
        }

        [Fact]
        public void AGameCanAddItsOwnControls()
        {
            var panel = new ToolboxPanel();
            panel.Items.Add(new ToolboxItem("HealthBar", "Game", "<HealthBar/>"));
            Assert.Contains(panel.Items, x => x.DisplayName == "HealthBar");
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~ToolboxPanelTests"`
Expected: a build failure.

- [ ] **Step 3: Implement it**

`ToolboxItem.CreateDefaults`:

```csharp
        public static IReadOnlyList<ToolboxItem> CreateDefaults(IcyConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            var namespaces = new HashSet<string>(configuration.Types.Markup.BuiltInNamespaces, StringComparer.Ordinal);
            return [.. typeof(UIElement).Assembly.GetExportedTypes()
                .Where(t => t.Namespace != null && namespaces.Contains(t.Namespace)
                    && typeof(UIElement).IsAssignableFrom(t) && !t.IsAbstract && !t.IsGenericTypeDefinition
                    && t.GetConstructor(Type.EmptyTypes) != null)
                .Select(t => new ToolboxItem(t.Name, CategoryOf(t), SnippetFor(t)))
                .OrderBy(x => x.Category, StringComparer.Ordinal).ThenBy(x => x.DisplayName, StringComparer.Ordinal)];
        }

        private static string CategoryOf(Type type) =>
            typeof(Icy.UI.Controls.ItemsControl).IsAssignableFrom(type) || type.Name.EndsWith("Panel", StringComparison.Ordinal) || type.Name is "Grid" or "WrapGrid" or "SplitPane"
                ? "Layout & Items"
                : "Controls";

        private static string SnippetFor(Type type) => type.Name switch
        {
            "Button" or "ToggleButton" or "CheckBox" => $"<{type.Name} Content=\"{type.Name}\"/>",
            "TextBlock" => "<TextBlock Text=\"Text\"/>",
            "TextBox" => "<TextBox Width=\"120\"/>",
            "Border" => "<Border Width=\"100\" Height=\"60\"/>",
            _ => $"<{type.Name}/>",
        };
```

Some built-in types may not load from a bare snippet: dialogs, `Window`, `Page`, `Canvas`-only types, `ItemContainer`, or `TreeViewItem`-style internals that are public. The first test loads every snippet. Exclude each failure explicitly with a short reason, using a `static readonly HashSet<string> NotInsertable = ["Window", "Page", "Dialog", "MessageBox", "ItemContainer", "TreeViewItem", "TreeViewList", "TreeViewExpander", "ExpanderHeader", "SelectorItem", "TabItem"]` adjusted to what the test reports. Keep the test green by exclusion, never by catching.

`ToolboxPanel`:
- `Items` is initialized empty. When `Session` is set and `Items` is still empty, it's filled from `CreateDefaults(session.Design.Configuration)`.
- `Content` is a `ListBox` (or `ItemsControl` of `Button`s, whichever `ListBoxDemo` shows as the simple path) over `Items`, with `DisplayName` shown. Clicking an item calls `Insert(item)` and shows a failure in a status `TextBlock`.

`Insert`:

```csharp
        public EditResult Insert(ToolboxItem item)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (Session?.Selection is not { } selection || selection.Document.GetNode(selection.Node) is not { } element)
                return EditResult.Failure(default, "Select where to insert first.");

            DesignDocument document = selection.Document;
            bool container = selection.Instance is Icy.UI.Controls.Panel or Icy.UI.Controls.ContentControl { Content: null };
            NodeId parent;
            int index;
            if (container || element.Parent == null)
            {
                parent = selection.Node;
                index = element.ContentElements.Count();
            }
            else
            {
                parent = document.GetNodeId(element.Parent)!.Value;
                index = element.Parent.ContentElements.ToList().IndexOf(element) + 1;
            }

            EditResult result = document.Editor.InsertElement(parent, index, item.Snippet);
            if (result.Succeeded && result.Node is { } inserted)
            {
                try
                {
                    Session.Select(document, inserted);
                }
                catch (ArgumentException)
                {
                    // Inserted outside the editor's scope; the edit stands, the selection stays.
                }
            }

            return result;
        }
```

Check the real container base type name (`grep -n "class StackPanel\|class Panel\b" sources/IcyUI/UI/Controls/*.cs sources/IcyUI/UI/*.cs`) and the `EditResult` property that carries the inserted node (`EditResult.Success(context.ResultNode)` in `DesignDocument.Apply`). Adjust both names. Drag from the toolbox onto the frame is a listed follow-up: the existing move gesture works on live elements only, and a toolbox drag needs a ghost without an instance. Note it in the commit.

- [ ] **Step 4: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~ToolboxPanelTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI.Design/Editor/Panels/ToolboxItem.cs sources/IcyUI.Design/Editor/Panels/ToolboxPanel.cs sources/IcyUI.Tests/Design/Editor/ToolboxPanelTests.cs
git commit -m "Add a Toolbox panel that inserts built-in or game controls"
```

---

### Task 8: `EditorCommandBar`

**Files:**
- Create: `sources/IcyUI.Design/Editor/Panels/EditorCommandBar.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/EditorCommandBarTests.cs`

**Interfaces:**
- Consumes: `EditorSession.Commands.ToggleMode/Undo/Redo`, `EditorSession.Selection`/`LastEdited`/`ModeChanged`/`SelectionChanged`, `DesignDocument.IsModified`/`ModifiedChanged`/`CanSave`/`SaveBlockedReason`/`Save()`, `DesignSession.SaveAll()`/`Documents`.
- Produces: `public class EditorCommandBar : Control`, with:
  - `EditorSession? Session`
  - `string StatusText`
  - `bool CanSave`
  - `bool HasUnsavedChanges`
  - `void Save()`
  - `void SaveAll()`

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Design/Editor/EditorCommandBarTests.cs
using Icy.Design.Editor.Panels;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public sealed class EditorCommandBarTests : IDisposable
    {
        private const string Page = """<StackPanel HorizontalAlignment="Left" VerticalAlignment="Top"><Button x:Name="b" Width="40" Height="20"/></StackPanel>""";
        private readonly string root = Directory.CreateTempSubdirectory("icy-bar-").FullName;

        public void Dispose() => Directory.Delete(root, recursive: true);

        [Fact]
        public void WithNoSourceRoot_SaveIsDisabled_AndTheStatusSaysWhy()
        {
            using var host = new EditorTestHost(Page);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));

            Assert.False(bar.CanSave);
            Assert.Contains("page.xml", bar.StatusText);
        }

        [Fact]
        public void Save_WritesTheSelectedDocument_AndClearsTheBadge()
        {
            using var host = new EditorTestHost(Page);
            File.WriteAllText(Path.Combine(root, "page.xml"), Page);
            host.Design.UseSourceRoot(root);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));
            host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "60");
            Assert.True(bar.HasUnsavedChanges);

            bar.Save();

            Assert.False(bar.HasUnsavedChanges);
            Assert.Contains("Width=\"60\"", File.ReadAllText(Path.Combine(root, "page.xml")));
        }

        [Fact]
        public void AnIOError_IsShownInTheStatus_NotThrown()
        {
            using var host = new EditorTestHost(Page);
            string path = Path.Combine(root, "page.xml");
            File.WriteAllText(path, Page);
            host.Design.UseSourceRoot(root);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Session.Select(host.Named<Button>("b"));
            host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "60");

            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
                bar.Save();

            Assert.NotEmpty(bar.StatusText);
            Assert.True(bar.HasUnsavedChanges);
        }

        [Fact]
        public void SaveAll_ReportsHowManyWereSkipped()
        {
            using var host = new EditorTestHost(Page);
            var bar = new EditorCommandBar { Session = host.Session };
            host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "60");

            bar.SaveAll();

            Assert.Contains("1 page", bar.StatusText);
        }
    }
}
```

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~EditorCommandBarTests"`
Expected: a build failure.

- [ ] **Step 3: Implement it**

- **Layout:** a horizontal `StackPanel` with these buttons:
  - `Mode` (`Command = session.Commands.ToggleMode`)
  - `Undo` (`Commands.Undo`)
  - `Redo` (`Commands.Redo`)
  - `Save` (a local command calling `Save()`, `CanExecute => CanSave`)
  - `Save all` (calls `SaveAll()`)

  It also has a `badge` `TextBlock` ("● unsaved" when `HasUnsavedChanges`) and a `status` `TextBlock`.
- **`Target`:** `Session?.Selection?.Document ?? Session?.LastEdited`.
- **`CanSave`:** `Target is { CanSave: true }`.
- **`HasUnsavedChanges`:** `Session?.Design.Documents.Any(d => d.IsModified) == true`.
- **`Save()`:**
  - With no target, set the status to "Nothing to save." and return.
  - If `Target.SaveBlockedReason` is set, show it and return.
  - Otherwise `try { Target.Save(); status = "Saved " + Target.SourcePath; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status = ex.Message; }`, then `UpdateState()`.
- **`SaveAll()`:** `var skipped = Session.Design.SaveAll()`. The status is "Saved all." or `$"{skipped.Count} page(s) not saved: {skipped[0].Reason}"`. The test checks for "1 page", so use `"page"`/`"pages"` pluralisation. Then `UpdateState()`.
- **`UpdateState()`:**
  - Refresh the badge.
  - Show `Target?.SaveBlockedReason` in the status when the status is empty or was the previous blocked reason.
  - Raise `CanExecuteChanged` on the Save command.
- **Subscriptions:**
  - `Session.SelectionChanged` and `ModeChanged` → `UpdateState`. The mode label shows "Edit"/"Interact".
  - For every document in `Session.Design.Documents` at subscribe time, plus the selection's document as it changes, `ModifiedChanged` → `UpdateState`.
  - Keep a `HashSet<DesignDocument>` of subscribed documents, and unsubscribe all when `Session` changes.

- [ ] **Step 4: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~EditorCommandBarTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI.Design/Editor/Panels/EditorCommandBar.cs sources/IcyUI.Tests/Design/Editor/EditorCommandBarTests.cs
git commit -m "Add an editor command bar with Save, Save all and an unsaved badge"
```

---

### Task 9: `EditorOverlay`

**Files:**
- Create: `sources/IcyUI.Design/Editor/EditorOverlayOptions.cs`
- Create: `sources/IcyUI.Design/Editor/EditorOverlay.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/EditorOverlayTests.cs`

**Interfaces:**
- Consumes: `EditorFrame.Attach(canvas, design, scope)`/`Dispose()`/`Session`, `EditorSession.FindAttached(canvas)`, `Canvas.AddOverlay`/`RemoveOverlay`/`Overlays`, the panels (Tasks 4, 6, 7, 8), `TabControl` + `TabItem`, `IInputEventSystem.RegisterCommand`/`UnregisterCommand`, `KeyGesture`.
- Produces:
  - `public sealed class EditorOverlayOptions`, with `UIElement? Scope`, `KeyGesture? ToggleKey = new(Keys.F4)`, `float DockWidth = 280`, `string? SourceRoot`.
  - `public sealed class EditorOverlay : IDisposable`, with:
    - `static EditorOverlay Attach(Canvas, DesignSession, EditorOverlayOptions? = null)`
    - `bool IsShown`
    - `EditorSession? Session`
    - `string? LastShowError`
    - `Show()`, `Hide()`, `Toggle()`
    - `event EventHandler? IsShownChanged`

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Design/Editor/EditorOverlayTests.cs
using Icy.Design.Editor;
using Icy.Design.Editor.Panels;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorOverlayTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="b" Width="40" Height="20"/>
            </StackPanel>
            """;

        [Fact]
        public void ShowAndHide_AttachAndDetachEverything()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            int before = host.Canvas.Overlays.Count;
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);

            Assert.False(overlay.IsShown);
            Assert.Equal(before, host.Canvas.Overlays.Count);

            overlay.Show();
            Assert.True(overlay.IsShown);
            Assert.NotNull(EditorSession.FindAttached(host.Canvas));
            Assert.Contains(host.Canvas.Overlays, x => x is OutlinePanel || ContainsPanel<OutlinePanel>(x));

            overlay.Hide();
            Assert.Null(EditorSession.FindAttached(host.Canvas));
            Assert.Equal(before, host.Canvas.Overlays.Count);
        }

        [Fact]
        public void TheToggleKey_ShowsAndHides()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);

            host.Input.Events.RaiseGesture(new Icy.Input.Devices.KeyGesture(Icy.Input.Devices.Keys.F4));
            Assert.True(overlay.IsShown);
            host.Input.Events.RaiseGesture(new Icy.Input.Devices.KeyGesture(Icy.Input.Devices.Keys.F4));
            Assert.False(overlay.IsShown);
        }

        [Fact]
        public void ANullToggleKey_BindsNothing()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design, new EditorOverlayOptions { ToggleKey = null });

            host.Input.Events.RaiseGesture(new Icy.Input.Devices.KeyGesture(Icy.Input.Devices.Keys.F4));
            Assert.False(overlay.IsShown);
        }

        [Fact]
        public void HidingKeepsTheUndoHistory()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);
            overlay.Show();
            host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "60");

            overlay.Hide();

            Assert.True(host.Document.Editor.UndoStack.CanUndo);
            Assert.True(host.Document.IsModified);
        }

        [Fact]
        public void ShowingWhileAnotherEditorOwnsTheCanvas_StaysHidden_AndSaysWhy()
        {
            using var host = new EditorTestHost(Page);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);

            overlay.Show();

            Assert.False(overlay.IsShown);
            Assert.Equal("Another editor is attached to this canvas.", overlay.LastShowError);
        }

        [Fact]
        public void AClickOnADock_DoesNotSelectThePageBeneath()
        {
            using var host = new EditorTestHost(Page, attachSession: false);
            using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);
            overlay.Show();
            host.Render();
            host.Render();

            // The left dock covers x = 0..DockWidth below the bar; the page's button sits at its top-left.
            var dockPoint = new System.Drawing.Point(5, 60);
            Assert.False(host.Canvas.HitTest(dockPoint) is EditorCaptureLayer);
        }

        private static bool ContainsPanel<T>(Icy.UI.UIElement element)
            where T : Icy.UI.UIElement =>
            element is T || element.GetVisualChildrenForTest().Any(ContainsPanel<T>);
    }
}
```

`GetVisualChildrenForTest` stands for whatever visual-tree walker the tests already have. Search for it with `grep -rn "Descendants\|VisualChildren" sources/IcyUI.Tests/*.cs sources/IcyUI.Tests/**/*Host*.cs`. If none exists, keep the overlay's dock elements in an `internal IReadOnlyList<UIElement> Docks` and assert on that.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~EditorOverlayTests"`
Expected: a build failure.

- [ ] **Step 3: Implement it**

- **`Attach`:**
  - Validate the arguments.
  - If `options.SourceRoot` is set, call `design.UseSourceRoot(options.SourceRoot)`.
  - If `ToggleKey` is not null, register a toggle command with `configuration.Input.Events.RegisterCommand(toggle, key)`. Use `handlesGesture: false`, so a game's own F4 command still runs. Document that.
- **`Show()`:**
  - If already shown, return.
  - If `EditorSession.FindAttached(canvas) != null`, set `LastShowError = "Another editor is attached to this canvas."` and return.
  - Otherwise:
    - `frame = EditorFrame.Attach(canvas, design, options.Scope)`; `frame.ToolbarPlacement = EditorToolbarPlacement.BottomLeft`, so the frame toolbar doesn't sit under the command bar.
    - Build `bar = new EditorCommandBar { Session = frame.Session, HorizontalAlignment = Stretch, VerticalAlignment = Top }`, wrapped in a `Border` with the dark background used by the shell (`Color.FromArgb(235, 25, 25, 30)`).
    - Build the left dock: a `Border { Width = DockWidth, HorizontalAlignment = Left, VerticalAlignment = Stretch, Margin = new Thickness(0, BarHeight, 0, 0) }` holding a `TabControl` with `TabItem { Header = "Outline", Content = new OutlinePanel { Session } }` and `TabItem { Header = "Toolbox", Content = new ToolboxPanel { Session } }`, with `SelectedIndex = 0`.
    - Build the right dock: the same `Border`, with `HorizontalAlignment = Right`, holding `new PropertiesPanel { Session }`.
    - Add the overlays in this order: bar, left dock, right dock. They're added after the frame's capture layer, so they sit above it.
    - Set `IsShown = true`, clear `LastShowError`, and raise `IsShownChanged`.
  - `BarHeight` is a `const int BarHeight = 32`. The bar has an explicit `Height = BarHeight`, so no overflow shrinking is involved.
- **Collapsing:** each dock has a small "⟨"/"⟩" button in its header row. It toggles the dock's `Width` between `DockWidth` and `24`, and hides the content while collapsed.
- **`Hide()`:**
  - Remove the three overlays and dispose the frame.
  - Set the panels' `Session = null`, so they unsubscribe from the documents.
  - Set `IsShown = false` and raise `IsShownChanged`.
- **`Dispose()`:** `Hide()` and unregister the toggle command.
- **Class docs:** include the "using it in your game" recipe:

```csharp
    /// <code>
    /// #if DEBUG
    /// DesignSession design = DesignSession.Attach(configuration);           // before loading any screen
    /// if (DesignSession.FindSourceRoot("MyGame.csproj", "Assets/UI") is { } root)
    ///     design.UseSourceRoot(root);
    /// EditorOverlay overlay = EditorOverlay.Attach(canvas, design);          // F4 toggles it
    /// #endif
    /// </code>
```

  Note in the remarks that the game project references `IcyUI.Design` only in Debug, e.g. `<ProjectReference Include="..." Condition="'$(Configuration)' == 'Debug'" />`.

- [ ] **Step 4: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~EditorOverlayTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI.Design/Editor/EditorOverlayOptions.cs sources/IcyUI.Design/Editor/EditorOverlay.cs sources/IcyUI.Tests/Design/Editor/EditorOverlayTests.cs
git commit -m "Add the editor overlay: frame, docked panels and command bar behind one toggle"
```

---

### Task 10: `EditorWorkspace`

**Files:**
- Create: `sources/IcyUI.Design/Editor/EditorWorkspace.cs`
- Test: `sources/IcyUI.Tests/Design/Editor/EditorWorkspaceTests.cs`

**Interfaces:**
- Consumes: `MarkupLoader(configuration).Load(text, sourcePath)`; `DesignSession.Documents`/`FindDocument`; `EditorFrame.Attach(canvas, design, scope)`; `EditorSession.FindAttached`; the panels and `EditorCommandBar`; `SplitPane` (`First`, `Second`, `SplitterPosition`); `UIElement.Attached`/`Detached` events (`root.Attached += ...` is used in `SampleShell`, and `Detached` should exist alongside it; check).
- Produces: `public class EditorWorkspace : Control`, with:
  - `DesignSession? Design`
  - `DesignDocument? CurrentDocument`
  - `UIElement? Preview`
  - `bool IsEditing`
  - `string StatusText`
  - `void Open(DesignDocument document)`
  - `bool OpenFile(string path)`
  - `void RefreshDocuments()`

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Design/Editor/EditorWorkspaceTests.cs
using Icy.Design.Editor;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design.Editor
{
    public class EditorWorkspaceTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root" HorizontalAlignment="Left" VerticalAlignment="Top">
              <Button x:Name="b" Width="40" Height="20"/>
            </StackPanel>
            """;

        // The game's page stays in the canvas root; the workspace is an overlay standing in for a settings page.
        private static (EditorTestHost Host, EditorWorkspace Workspace) Create()
        {
            var host = new EditorTestHost(Page, attachSession: false);
            var workspace = new EditorWorkspace { Design = host.Design, Width = 800, Height = 600 };
            host.Canvas.AddOverlay(workspace);
            host.Render();
            return (host, workspace);
        }

        [Fact]
        public void OpeningADocument_LoadsAPreview_JoinedToTheSameDocument()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                workspace.Open(host.Document);

                Assert.NotNull(workspace.Preview);
                Assert.Same(host.Document, host.Design.FindDocument(workspace.Preview!, out _));
                Assert.True(workspace.IsEditing);
            }
        }

        [Fact]
        public void AnEditInThePreview_ReachesTheLivePage()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                workspace.Open(host.Document);

                host.Document.Editor.SetAttribute(host.IdOf(host.Named<Button>("b")), "Width", "75");

                Assert.Equal(75, host.Named<Button>("b").Width);
                var previewButton = (Button)Icy.Markup.MarkupNameScope.GetScope(workspace.Preview!)!.Find("b")!;
                Assert.Equal(75, previewButton.Width);
            }
        }

        [Fact]
        public void SwitchingDocuments_ReleasesThePreviousPreview()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                var other = new Icy.Markup.MarkupLoader(host.Configuration).Load("<Border x:Name=\"solo\" Width=\"10\" Height=\"10\"/>", "other.xml");
                var otherDocument = host.Design.FindDocument(other, out _)!;

                workspace.Open(host.Document);
                var firstPreview = workspace.Preview!;
                workspace.Open(otherDocument);

                Assert.Null(firstPreview.Canvas);
                Assert.Null(Icy.Markup.MarkupNameScope.GetScope(workspace.Preview!)?.Find("b"));
                Assert.Same(otherDocument, workspace.CurrentDocument);
                GC.KeepAlive(other);
            }
        }

        [Fact]
        public void WhileTheOverlayOwnsTheCanvas_TheWorkspaceWaits_ThenAttaches()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                using EditorOverlay overlay = EditorOverlay.Attach(host.Canvas, host.Design);
                overlay.Show();

                workspace.Open(host.Document);
                Assert.False(workspace.IsEditing);
                Assert.Equal("The editor overlay is active. Close it to edit here.", workspace.StatusText);

                overlay.Hide();
                host.Render();
                Assert.True(workspace.IsEditing);
            }
        }

        [Fact]
        public void OpenFile_TracksANewDocument()
        {
            (EditorTestHost host, EditorWorkspace workspace) = Create();
            using (host)
            {
                string path = Path.Combine(Path.GetTempPath(), $"icy-ws-{Guid.NewGuid():N}.xml");
                File.WriteAllText(path, "<Border x:Name=\"fromFile\" Width=\"10\" Height=\"10\"/>");
                try
                {
                    Assert.True(workspace.OpenFile(path));
                    Assert.Equal(path, workspace.CurrentDocument!.SourcePath);
                }
                finally
                {
                    File.Delete(path);
                }
            }
        }
    }
}
```

The second assertion checks that the preview now shows the other document, which has no `b`. The old preview is detached; the document's object map drops it once it's collected.

- [ ] **Step 2: Run the tests and confirm they fail**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~EditorWorkspaceTests"`
Expected: a build failure.

- [ ] **Step 3: Implement it**

- **Layout:** a root `Grid` with a top `Auto` row holding the `EditorCommandBar` and a `Star` row holding a `SplitPane`.
  - `SplitPane.First` is a left `Grid`: a document picker on top (a `ListBox` of `DocumentEntry(DesignDocument Document)` records, whose `ToString()` is `SourcePath` plus `" ●"` when modified), an "Open file…" row (`TextBox` + `Button`), and a `TabControl` with Outline/Toolbox.
  - `SplitPane.Second` is a nested `SplitPane` (`SplitterPosition = 0.7f`). Its `First` is a `ScrollViewer previewHost`, and its `Second` is the `PropertiesPanel`.
- **`Open(document)`:**
  1. `ReleasePreview()`: dispose `frame`, and set `previewHost.Content = null`.
  2. `Preview = new MarkupLoader(Design.Configuration).Load(document.Text, document.SourcePath)`. Then check `Design.FindDocument(Preview, out _)` is `document`. If it isn't, because the text differs from the document's at load time, set the status to "The preview couldn't join the document." Only `SourcePath` and text equality join, and `document.Text` is the current text, so this holds.
  3. `previewHost.Content = Preview`, `CurrentDocument = document`, and set each panel's `Document`/`Session` to the frame's session as it attaches.
  4. `TryAttach()`:
     - If `Canvas == null`, return; `Attached` retries.
     - If `EditorSession.FindAttached(Canvas)` is not null and isn't ours, set `StatusText = "The editor overlay is active. Close it to edit here."`, set `IsEditing = false`, and set `pendingAttach = true`.
     - Otherwise `frame = EditorFrame.Attach(Canvas, Design, previewHost)`. Then set `outline.Session = properties.Session = toolbox.Session = bar.Session = frame.Session`, `outline.Document = document`, `IsEditing = true`, and clear the status.
- **Retry while waiting:** while `pendingAttach`, check on each render. Override `OnRender` or use the per-frame hook other controls use (`grep -rn "QueueAfterRootLayout\|IFrameTicker" sources/IcyUI/UI/Canvas.cs`). Call `TryAttach()` once `FindAttached` returns null. That's one dictionary lookup per frame, only while waiting.
- **`OpenFile(path)`:**
  - If the file doesn't exist, set the status to `$"'{path}' doesn't exist."` and return false.
  - Otherwise `Preview = loader.Load(File.ReadAllText(path), path)`, then `Open(Design.FindDocument(Preview, out _)!)`. Because the document is already loaded with that text, `Open` reloads into the same document, so don't load twice. Factor the "load into host" step so `OpenFile` uses the freshly loaded root directly.
- **`RefreshDocuments()`:** re-reads `Design.Documents` into the picker. It's called on attach, after `OpenFile`, and on a "↻" button.
- **Detach:** on `Detached`, call `ReleasePreview()`. Keep `CurrentDocument`, so re-attaching re-opens it.
- **Errors:** loading uses the same loader as the game. A `MarkupException` from bad text shows in `StatusText`, and the preview stays empty.

- [ ] **Step 4: Run the tests**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj --filter "FullyQualifiedName~EditorWorkspaceTests"`
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI.Design/Editor/EditorWorkspace.cs sources/IcyUI.Tests/Design/Editor/EditorWorkspaceTests.cs
git commit -m "Add the editor workspace: a page with a preview, panels and a document picker"
```

---

### Task 11: Samples, performance test, docs and the whole-branch check

**Files:**
- Create: `sources/Shared Samples/EditorWorkspaceDemo.cs`
- Modify: `sources/Shared Samples/SampleCatalog.cs`, `sources/Shared Samples/SampleShell.cs`
- Modify: `sources/IcyUI.Tests/Design/Editor/EditorFramePerformanceTests.cs` (add a panels case)
- Modify: the `CLAUDE.md` layout table row for `IcyUI.Design`: "Markup design document, edit engine, editor frame, panels, overlay and workspace (Phase 10)."
- Test: `sources/IcyUI.Tests/Samples/SampleShellTests.cs` (adjust the editor-toggle tests to the overlay). Locate it with `grep -rln "class SampleShellTests" sources/IcyUI.Tests`.

**Interfaces:**
- Consumes: `EditorOverlay`, `EditorWorkspace`, `DesignDemo.SessionFor(configuration)`, `SampleEntry`.
- Produces: `EditorWorkspaceDemo.Build(IcyConfiguration, string)`.

- [ ] **Step 1: Switch the shell's F4 to `EditorOverlay`**

In `SampleShell.Build`:
- Replace the `EditorFrame? editor` toggle with an `EditorOverlay`, created once when the root attaches to a canvas: `EditorOverlay.Attach(canvas, DesignDemo.SessionFor(configuration), new EditorOverlayOptions { Scope = content, ToggleKey = null })`. The shell keeps its own F4 registration and footer button.
- `ToggleEditor` calls `overlay.Toggle()`.
- The label follows `overlay.IsShownChanged`.
- `editStatus` shows `overlay.LastShowError` when `Show` refused.
- On demo switch, keep the existing `Session.Clear()`, via `overlay.Session?.Clear()`.

Update `SampleShellTests` for the new shape. Assert that F4 shows `EditorOverlay` docks, and that the sidebar still selects demos while it's shown.

- [ ] **Step 2: Add `EditorWorkspaceDemo`**

```csharp
// sources/Shared Samples/EditorWorkspaceDemo.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.IO;
using Icy.Configuration;
using Icy.Design;
using Icy.Design.Editor;
using Icy.UI;

namespace Icy.SharedSamples
{
    /// <summary>
    /// The full editor as a page, the way a game's debug settings would host it: pick any page the samples have loaded,
    /// edit it in an isolated preview, and save to a scratch copy.
    /// </summary>
    /// <remarks>
    /// Save writes into a temporary folder holding copies of the demos' markup, never into the repository. Edits also
    /// reach the live demo pages, because the preview joins their documents.
    /// </remarks>
    public static class EditorWorkspaceDemo
    {
        /// <summary>
        /// Builds the demo.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The demo's root element.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;
            DesignSession design = DesignDemo.SessionFor(configuration);

            string scratch = Directory.CreateTempSubdirectory("icy-samples-").FullName;
            foreach (DesignDocument document in design.Documents)
            {
                if (document.SourcePath is { } source && !Path.IsPathRooted(source))
                    File.WriteAllText(Path.Combine(scratch, source), document.Text);
            }

            design.UseSourceRoot(scratch);
            return new EditorWorkspace
            {
                Design = design,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = 0,
            };
        }
    }
}
```

The demo source paths are names like `"DesignDemo"`, so `Path.Combine(scratch, source)` is a flat file. Documents loaded after this demo is built have no scratch copy, so their Save is disabled with a reason. That's the intended behaviour, and the demo's remarks say so. Register it with `new("Design Tools", "Editor Workspace", EditorWorkspaceDemo.Build, ScrollsItself: true)` after "Editor" in `SampleCatalog.All`. Both hosts pick it up through the shell. Confirm neither `SampleGame.cs` keeps a separate list (`grep -n "Demo" "sources/MonoGame Sample/SampleGame.cs" "sources/Stride Sample/SampleGame.cs"`). If one does, add the entry there too.

- [ ] **Step 3: Add the panels performance test**

In `EditorFramePerformanceTests.cs`, add a test modelled on the existing 500-element test:
- Build the same page.
- Attach `EditorFrame`, `OutlinePanel`, `PropertiesPanel` and the command bar to the session.
- Select an element, warm up with 5 edits, then time 50 `SetAttribute` edits of `Width` with a `Stopwatch`.
- Assert the mean is under **2 ms per edit**, and print the mean with the test output helper, as the existing test does.

The 2 ms bound is generous for CI noise. Record the measured value in the commit message.

- [ ] **Step 4: Run the full suite and the warning counts, and build the sample hosts**

Run: `dotnet test sources/IcyUI.Tests/IcyUI.Tests.csproj`. Expected: Failed 0, and **Total** = baseline 1679 + the tests added in Tasks 1–11.
Run: `dotnet build sources/IcyUI/IcyUI.csproj --no-incremental 2>&1 | grep -E ": warning (SA|CS)" | sort -u | wc -l`. Expected: `84`.
Run: `dotnet build sources/IcyUI.Design/IcyUI.Design.csproj --no-incremental 2>&1 | grep -E ": warning (SA|CS)" | sort -u | wc -l`. Expected: `1`.
Run: `dotnet build "sources/IcyUI.sln"`. Expected: success. This includes both sample hosts.

- [ ] **Step 5: Commit**

```bash
git add "sources/Shared Samples/EditorWorkspaceDemo.cs" "sources/Shared Samples/SampleCatalog.cs" "sources/Shared Samples/SampleShell.cs" sources/IcyUI.Tests CLAUDE.md
git commit -m "Run the editor overlay from the samples shell and add the Editor Workspace demo"
```

- [ ] **Step 6: Whole-branch review**

Review `git diff 97ca604..HEAD` against the spec. Use one fresh reviewer, or a self-review if Ivan asks for no sub-agents. Fix Critical and Important findings with tests. List the deferred minors in the final message, plus the follow-ups this plan names:
- toolbox drag onto the frame;
- outline pointer drag-reorder, if not wired;
- attached-property rows;
- per-node outline health.

Then hand the smoke-test checklist to Ivan. **Don't claim a smoke test.**

1. Shell: F4 shows the docks. Select in the page and the outline follows. Edit Width in Properties, then Undo. The sidebar still switches demos. F4 hides it all.
2. In Properties, type in a text row with an element selected. The arrows don't nudge, Delete doesn't delete, and Ctrl+Z after leaving the box undoes the typed edit as one step.
3. Editor Workspace demo: pick the Markup demo's page. Edit it in the preview, switch to the Markup demo, and the change is there. Save writes the scratch copy, and the status shows the path.
4. Both hosts, the ARM64 laptop and the x64 desktop: Stride is the primary target. MonoGame follows the known shutdown caveat on ARM64.
