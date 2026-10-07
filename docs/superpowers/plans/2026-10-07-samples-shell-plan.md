# Samples Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace both sample hosts' hand-rolled demo switching with one shared sidebar shell (a `TreeView` of
categorized demos plus a lazily built, cached content area), and make `TreeView`'s Right key let focus leave the tree.

**Architecture:** `Shared Samples` gains `SampleCatalog` (the ordered demo list) and `SampleShell` (a `SplitPane`: the
sidebar `TreeView` on the left, a `ContentControl` showing the selected demo on the right). Demos are built on first
selection and cached, each in its own `ScrollViewer`. Only the selected one is attached. Both hosts shrink to "create a
canvas, add `SampleShell.Build(...)`". The only core change is `TreeView.OnNavigate`'s Right key.

**Tech Stack:** C# / .NET 10, IcyUI core, xUnit (`IcyUI.Tests`), MonoGame DesktopGL/WindowsDX host, Stride 4.3 host.

**Spec:** `docs/superpowers/specs/2026-10-07-samples-shell-design.md`

## Global Constraints

- Everything targets `net10.0` (sample hosts: `net10.0-windows`). Never put `Version=` on a `PackageReference`.
- Every public API in `IcyUI` gets complete XML documentation (`<see cref>`, `<see langword>`, `<list>`, `<para>`).
- `IcyUI` code follows StyleCop, block-scoped `namespace X { }`, and the file copyright header:
  ```
  // Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
  // Distributed under MIT license. See LICENSE.md file in the project root for more information
  ```
- New `Shared Samples` files start with the copyright header followed by
  `// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.`
  and `#nullable enable`, like `DesignDemo.cs`. Their namespace is `Icy.SharedSamples`.
- Categories and order, verbatim from the spec: Basics (Controls, Styles, Navigation); Markup (Markup, Markup Styles,
  Control Templates); Layout (Split Pane, Expander, Wrap Grid, Scaling); Items (Items Control, List Box, Selector, Tab
  Control, Tree View); Dialogs & Pickers (Dialog, Color Picker); Design Tools (Property Grid, Design, Editor).
- `ScrollsItself = true` for Controls and Styles only.
- Shell background `#FF19191E`. Sidebar about 220 px: `SplitterPosition = 0.18f`, `MinFirstSize = 180`.
- The shell registers PageDown (next) and PageUp (previous). Shift+Tab is not bound to demo switching anywhere.
- MonoGame Sample exits on gamepad Back only (not Escape).
- Build: `dotnet build "sources/IcyUI.sln"`. Test: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`.
  `dotnet test` prints "Passed!" even when the test host crashes: always check the **Total** count.
- Baseline before this plan: 1588 tests, 84 build warnings (full rebuild). Don't add warnings in `IcyUI`.
- Ivan smoke-tests the sample apps himself. Never claim a smoke test that didn't happen.

## Review Focus

1. **Paging with no selection or a non-demo selection** (e.g. a test or a user clearing `SelectedItem`): PageDown should
   go to the first demo and PageUp to the last, never throw or pick index −2. Pinned in Task 5.
2. **A builder that throws** (a demo broken by the always-on design session or the shared theme): the failure should
   surface in tests, not first in a host. Pinned by Task 3's every-entry build theory.
3. **Re-selecting the already selected demo, or selecting a category with the mouse:** the content must not change or
   be rebuilt. Pinned in Task 4 (`ClickingACategory_KeepsTheShownDemo`).
4. **Switching away while the Editor demo is editing:** its overlay must go with it, or the sidebar becomes unclickable.
   Pinned in Tasks 2 and 4.
5. **Recycled sidebar rows after collapse/expand:** each row must still show its own label. Pinned in Task 6.

---

## File structure

| File | Responsibility |
|---|---|
| `sources/IcyUI/UI/Controls/TreeView.cs` (modify) | Right key: standard tree convention. |
| `sources/IcyUI.Tests/Controls/TreeViewNavigationTests.cs` (modify) | Pins the new Right behavior. |
| `docs/superpowers/specs/2026-10-06-treeview-design.md` (modify) | Dated amendment note on Right. |
| `sources/Shared Samples/EditorDemo.cs` (modify) | Stops editing on `Detached` instead of on hide. |
| `sources/IcyUI.Tests/Samples/EditorDemoTests.cs` (modify) | Pins the detach hook. |
| `sources/Shared Samples/SampleCatalog.cs` (create) | `SampleEntry` and the ordered list of 20 demos. |
| `sources/Shared Samples/SampleShell.cs` (create) | Sidebar, content host, lazy cache, paging hotkeys. |
| `sources/IcyUI.Tests/Samples/SampleCatalogTests.cs` (create) | Catalog shape and that every demo builds. |
| `sources/IcyUI.Tests/Samples/SampleShellTests.cs` (create) | Lifetime, session, editor, paging, navigation, rows. |
| `sources/IcyUI.Tests/IcyUI.Tests.csproj` (modify) | Links all demos plus the catalog and shell. |
| `sources/MonoGame Sample/SampleGame.cs` (modify) | Canvas plus shell; host hotkeys; Back exits. |
| `sources/MonoGame Sample/MonoGame Sample.csproj` (modify) | Links the catalog and shell; drops orphaned assets. |
| `sources/MonoGame Sample/SamplesRunner.cs`, `SampleBase.cs`, `Samples/*` (delete) | Replaced by the shell. |
| `sources/Stride Sample/SampleGame.cs` (modify) | Canvas plus shell; host hotkeys. |
| `sources/Stride Sample/Stride Sample.csproj` (modify) | Links the catalog and shell. |

---

### Task 1: TreeView Right key follows the standard tree convention

**Files:**
- Modify: `sources/IcyUI/UI/Controls/TreeView.cs:366-418` (`OnNavigate` and its XML docs)
- Modify: `sources/IcyUI.Tests/Controls/TreeViewNavigationTests.cs:69-86`
- Modify: `docs/superpowers/specs/2026-10-06-treeview-design.md` (Right row of the key table, around line 165)

**Interfaces:**
- Consumes: nothing new.
- Produces: `TreeView.OnNavigate(Vector2)` returns `false` for Right on a leaf. Task 6's navigation test relies on it.

The fixture in `TreeViewNavigationTests` is `A (expanded) → (B (collapsed) → (C, D), E)`, focused on `A`. `Press(dir)`
returns whether the tree handled the press, and `Current` is the current row's label.

- [ ] **Step 1: Replace the Right test with tests for the new convention**

Replace `Right_ExpandsACollapsedBranch_ThenWalksTheRows` (lines 69-86) with:

```csharp
        [Fact]
        public void Right_ExpandsACollapsedBranch_ThenEntersIt()
        {
            Press(Down);

            Assert.True(Press(Right));
            Assert.True(b.IsExpanded);
            Assert.Equal("B", Current);

            Assert.True(Press(Right));
            Assert.Equal("C", Current);
        }

        [Fact]
        public void Right_OnAnExpandedBranch_MovesToItsFirstChild()
        {
            Assert.True(Press(Right));
            Assert.Equal("B", Current);
        }

        [Fact]
        public void Right_OnALeaf_IsNotHandled()
        {
            tree.Expand(b);
            tree.SelectedItem = c;
            ResetFocus();

            // Mid-tree: C has a next row (D), but Right still leaves the tree.
            Assert.False(Press(Right));
            Assert.Equal("C", Current);

            // Last row.
            tree.SelectedItem = e;
            ResetFocus();
            Assert.False(Press(Right));
            Assert.Equal("E", Current);
        }
```

`ResetFocus()` already exists in the fixture (used by `Left_GoesToTheParent_ThenCollapses`).

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewNavigationTests"`
Expected: `Right_OnALeaf_IsNotHandled` FAILS (Right on C moves to D and returns `true`). The other two pass already,
because the flattened next row of an expanded branch is its first child.

- [ ] **Step 3: Change `OnNavigate`**

In `TreeView.cs`, replace the `if (direction.X > 0) { ... }` block (lines 394-406) with:

```csharp
            if (direction.X > 0)
            {
                if (!HasChildren(row.Item))
                    return false;

                if (!IsExpanded(row.Item))
                    Expand(row.Item);
                else
                    MoveTo(index + 1);
                return true;
            }
```

An expanded branch with children always has its first child at `index + 1` in the flattened rows.

Replace the XML summary above `OnNavigate` (lines 366-369) with:

```csharp
        /// <summary>
        /// Moves through the rows, following the usual tree conventions.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Up and Down move to the previous and next row.</description></item>
        /// <item><description>Right expands a collapsed branch, or moves from an expanded branch to its first child.</description></item>
        /// <item><description>Left collapses an expanded branch, or moves from a child to its parent.</description></item>
        /// </list>
        /// <para>
        /// A press the tree can't use isn't claimed, so focus moves on to the next control: Up on the first row, Down on
        /// the last row, Right on a leaf, and Left on a collapsed root.
        /// </para>
        /// </remarks>
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeView"`
Expected: all TreeView tests pass, including `TreeViewDemoTests`.

- [ ] **Step 5: Amend the TreeView spec**

In `docs/superpowers/specs/2026-10-06-treeview-design.md`, replace the Right row of the key table (around line 165)
with:

```markdown
| Right | Collapsed branch: expand it. Expanded branch: move to its first child. **Leaf: not handled**, so spatial navigation leaves the tree. *(Amended 2026-10-07 by the samples shell spec: Right used to walk to the next row in flattened order, which kept focus from leaving a sidebar tree.)* |
```

- [ ] **Step 6: Run the full suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: 0 failures, Total 1590 (1588 − 1 replaced + 3 new).

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/UI/Controls/TreeView.cs sources/IcyUI.Tests/Controls/TreeViewNavigationTests.cs docs/superpowers/specs/2026-10-06-treeview-design.md
git commit -m "Let Right leave a TreeView from a leaf, like a standard tree"
```

---

### Task 2: EditorDemo stops editing when it's detached

**Files:**
- Modify: `sources/Shared Samples/EditorDemo.cs:121-126`
- Modify: `sources/IcyUI.Tests/Samples/EditorDemoTests.cs:30-39`

**Interfaces:**
- Consumes: `UIElement.Detached` (`event EventHandler?`), `Canvas.Remove(UIElement)`.
- Produces: an `EditorDemo` root that disposes its `EditorFrame` when it leaves the canvas. Task 4 relies on it.

- [ ] **Step 1: Replace the hide test with a detach test**

In `EditorDemoTests.cs`, replace `HidingTheDemo_DetachesTheEditor` with:

```csharp
        [Fact]
        public void RemovingTheDemo_DetachesTheEditor()
        {
            (Canvas canvas, UIElement root) = Build();
            FindButton(root, "Edit this page").Command!.Execute(null);

            canvas.Remove(root);

            Assert.Empty(canvas.Overlays);
            Assert.True(canvas.IsKeyboardNavigationEnabled);
        }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorDemoTests"`
Expected: `RemovingTheDemo_DetachesTheEditor` FAILS (`canvas.Overlays` still holds the frame).

- [ ] **Step 3: Switch the hook**

In `EditorDemo.cs`, replace:

```csharp
            // The hosts switch demos on one canvas: an editor left in Edit mode would keep the other demos' input.
            root.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(UIElement.IsVisible) && !root.IsVisible)
                    Detach();
            };
```

with:

```csharp
            // The samples shell detaches a demo when you switch away: an editor left in Edit mode would keep the input.
            root.Detached += (_, _) => Detach();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~EditorDemoTests"`
Expected: PASS. If it still fails, check that `Canvas.Remove` sets the root's `Canvas` to `null` (which raises
`Detached` from `UIElement.OnDetached`); don't add an `IsVisible` fallback.

- [ ] **Step 5: Commit**

```bash
git add "sources/Shared Samples/EditorDemo.cs" sources/IcyUI.Tests/Samples/EditorDemoTests.cs
git commit -m "Stop the Editor demo's editing when it leaves the canvas"
```

---

### Task 3: SampleCatalog

**Files:**
- Create: `sources/Shared Samples/SampleCatalog.cs`
- Create: `sources/IcyUI.Tests/Samples/SampleCatalogTests.cs`
- Modify: `sources/IcyUI.Tests/IcyUI.Tests.csproj` (the linked-demos `ItemGroup`, lines 34-43)

**Interfaces:**
- Consumes: every `Icy.SharedSamples.*Demo.Build(IcyConfiguration, string)`.
- Produces:
  ```csharp
  public sealed record SampleEntry(string Category, string Name, Func<IcyConfiguration, string, UIElement> Build, bool ScrollsItself = false);
  public static class SampleCatalog { public static IReadOnlyList<SampleEntry> All { get; } }
  ```

- [ ] **Step 1: Link every demo and the catalog into the tests**

Replace the `ItemGroup` with the linked demos in `IcyUI.Tests.csproj` with:

```xml
  <ItemGroup>
    <!-- The samples' markup is the only place the loader is exercised against a full, realistic document.
         Linking the demos in means a document that stops parsing fails the test run rather than a sample launch.
         The catalog references every demo, so all of them are linked. -->
    <Compile Include="..\Shared Samples\*.cs" LinkBase="Samples" />
  </ItemGroup>
```

This also links `SampleShell.cs` once Task 4 creates it. The old explicit links for `MarkupDemo` and `ControlTemplateDemo`
used `Markup\` link folders; the link folder doesn't change compilation.

- [ ] **Step 2: Write the failing catalog tests**

Create `sources/IcyUI.Tests/Samples/SampleCatalogTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Assets;
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Samples
{
    public class SampleCatalogTests
    {
        public static TheoryData<string> Names
        {
            get
            {
                var names = new TheoryData<string>();
                foreach (SampleEntry entry in SampleCatalog.All)
                    names.Add(entry.Name);
                return names;
            }
        }

        [Fact]
        public void All_ListsTheTwentyDemosUnderTheAgreedCategories()
        {
            string[][] expected =
            [
                ["Basics", "Controls", "Styles", "Navigation"],
                ["Markup", "Markup", "Markup Styles", "Control Templates"],
                ["Layout", "Split Pane", "Expander", "Wrap Grid", "Scaling"],
                ["Items", "Items Control", "List Box", "Selector", "Tab Control", "Tree View"],
                ["Dialogs & Pickers", "Dialog", "Color Picker"],
                ["Design Tools", "Property Grid", "Design", "Editor"],
            ];

            string[][] actual = SampleCatalog.All
                .GroupBy(e => e.Category)
                .Select(g => g.Select(e => e.Name).Prepend(g.Key).ToArray())
                .ToArray();

            Assert.Equal(expected, actual);
            Assert.Equal(20, SampleCatalog.All.Count);
            Assert.Equal(SampleCatalog.All.Count, SampleCatalog.All.Select(e => e.Name).Distinct().Count());
        }

        [Fact]
        public void OnlyControlsAndStyles_ScrollThemselves() =>
            Assert.Equal(["Controls", "Styles"], SampleCatalog.All.Where(e => e.ScrollsItself).Select(e => e.Name));

        [Theory]
        [MemberData(nameof(Names))]
        public void EveryDemo_BuildsWithTheSessionAttached(string name)
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new System.Drawing.Size(1280, 720) })
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets();
            IcyConfiguration configuration = builder.Build().UseDefaultTheme();
            DesignDemo.SessionFor(configuration);

            UIElement root = SampleCatalog.All.Single(e => e.Name == name).Build(configuration, "Airfool");

            Assert.NotNull(root);
        }
    }
}
```

`GroupBy` keeps first-appearance order, so the first test also pins that each category's entries are contiguous.

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleCatalogTests"`
Expected: build FAILS with `The name 'SampleCatalog' does not exist in the current context`.

- [ ] **Step 4: Create the catalog**

Create `sources/Shared Samples/SampleCatalog.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Collections.Generic;
using Icy.Configuration;
using Icy.UI;

namespace Icy.SharedSamples
{
    /// <summary>
    /// One demo the <see cref="SampleShell"/> lists.
    /// </summary>
    /// <param name="Category">The sidebar category the demo appears under.</param>
    /// <param name="Name">The demo's sidebar label.</param>
    /// <param name="Build">Builds the demo's root element from the host's configuration and font family.</param>
    /// <param name="ScrollsItself">
    /// <see langword="true"/> when the demo brings its own full-size <see cref="UI.Controls.ScrollViewer"/>, so the shell
    /// must not wrap it in another one.
    /// </param>
    public sealed record SampleEntry(string Category, string Name, Func<IcyConfiguration, string, UIElement> Build, bool ScrollsItself = false);

    /// <summary>
    /// The demos both sample hosts show, in sidebar order. Adding a demo takes one line here.
    /// </summary>
    public static class SampleCatalog
    {
        /// <summary>
        /// Gets every demo in sidebar order. Each category's entries are contiguous.
        /// </summary>
        public static IReadOnlyList<SampleEntry> All { get; } =
        [
            new("Basics", "Controls", ControlsDemo.Build, ScrollsItself: true),
            new("Basics", "Styles", StylesDemo.Build, ScrollsItself: true),
            new("Basics", "Navigation", NavigationDemo.Build),
            new("Markup", "Markup", MarkupDemo.Build),
            new("Markup", "Markup Styles", MarkupStylesDemo.Build),
            new("Markup", "Control Templates", ControlTemplateDemo.Build),
            new("Layout", "Split Pane", SplitPaneDemo.Build),
            new("Layout", "Expander", ExpanderDemo.Build),
            new("Layout", "Wrap Grid", WrapGridDemo.Build),
            new("Layout", "Scaling", ScalingDemo.Build),
            new("Items", "Items Control", ItemsControlDemo.Build),
            new("Items", "List Box", ListBoxDemo.Build),
            new("Items", "Selector", SelectorDemo.Build),
            new("Items", "Tab Control", TabControlDemo.Build),
            new("Items", "Tree View", TreeViewDemo.Build),
            new("Dialogs & Pickers", "Dialog", DialogDemo.Build),
            new("Dialogs & Pickers", "Color Picker", ColorPickerDemo.Build),
            new("Design Tools", "Property Grid", PropertyGridDemo.Build),
            new("Design Tools", "Design", DesignDemo.Build),
            new("Design Tools", "Editor", EditorDemo.Build),
        ];
    }
}
```

The `<see cref="SampleShell"/>` reference resolves once Task 4 adds the shell. Until then the build gives one
`CS1574` warning, which is fine for this commit. (The tests and the sample hosts don't treat warnings as errors.)

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleCatalogTests"`
Expected: PASS, 22 tests (2 facts + 20 theory cases).

If a theory case fails, a demo breaks when built with the session attached or the default theme applied. Debug that
demo (superpowers:systematic-debugging); it would fail in the shell too. Don't drop the case.

- [ ] **Step 6: Run the full suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: 0 failures, Total 1612.

- [ ] **Step 7: Commit**

```bash
git add "sources/Shared Samples/SampleCatalog.cs" sources/IcyUI.Tests/Samples/SampleCatalogTests.cs sources/IcyUI.Tests/IcyUI.Tests.csproj
git commit -m "Add the shared sample catalog and test that every demo builds"
```

---

### Task 4: SampleShell: sidebar, lazy cached content, session at startup

**Files:**
- Create: `sources/Shared Samples/SampleShell.cs`
- Create: `sources/IcyUI.Tests/Samples/SampleShellTests.cs`

**Interfaces:**
- Consumes: `SampleEntry`, `SampleCatalog.All` (Task 3); `DesignDemo.SessionFor(IcyConfiguration)` (internal, same
  assembly); `TreeView.Items`, `TreeView.SelectedItem`, `TreeView.SelectionChanged`; `TreeViewNode(object? header)`
  with `IsSelectable`, `IsExpanded`, `Tag`, `Children`; `SplitPane.First`/`Second`/`SplitterPosition`/`MinFirstSize`.
- Produces:
  ```csharp
  public static class SampleShell
  {
      public static UIElement Build(IcyConfiguration configuration, string fontFamily);
      internal static UIElement Build(IcyConfiguration configuration, string fontFamily, IReadOnlyList<SampleEntry> entries);
  }
  ```
  The returned root is a `SplitPane` whose `First` is the sidebar `TreeView` and whose `Second` is the content
  `ContentControl`. Tasks 5 and 6 find them that way.

- [ ] **Step 1: Write the failing shell tests**

Create `sources/IcyUI.Tests/Samples/SampleShellTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Markup;
using Icy.SharedSamples;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    public class SampleShellTests
    {
        private readonly Dictionary<string, int> builds = [];
        private readonly Dictionary<string, UIElement> built = [];

        [Fact]
        public void Startup_BuildsAndAttachesOnlyTheFirstDemo()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two"), Fake("B", "Three")]);

            Assert.Equal(new Dictionary<string, int> { ["One"] = 1 }, builds);
            Assert.NotNull(built["One"].Canvas);
            Assert.Equal("One", shell.Tree.SelectedItem!.ToString());
        }

        [Fact]
        public void Sidebar_GroupsDemosUnderNonSelectableExpandedCategories()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two"), Fake("B", "Three")]);

            Assert.Equal(["A", "One", "Two", "B", "Three"], shell.Tree.Rows.Select(r => r.Item.ToString()));
            var category = (TreeViewNode)shell.Tree.Items[0];
            Assert.False(category.IsSelectable);
            Assert.True(category.IsExpanded);
        }

        [Fact]
        public void SelectingADemo_BuildsItOnceAndDetachesThePreviousOne()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two")]);

            shell.Select("Two");

            Assert.Equal(1, builds["Two"]);
            Assert.NotNull(built["Two"].Canvas);
            Assert.Null(built["One"].Canvas);
        }

        [Fact]
        public void SelectingADemoAgain_ShowsTheSameInstanceAtItsScrollOffset()
        {
            var shell = Host([Fake("A", "One", height: 3000), Fake("A", "Two")]);
            var viewer = Assert.IsType<ScrollViewer>(shell.Content.Content);
            viewer.VerticalOffset = 500;

            shell.Select("Two");
            shell.Select("One");

            Assert.Equal(1, builds["One"]);
            Assert.Same(viewer, shell.Content.Content);
            Assert.Same(built["One"], viewer.Content);
            Assert.Equal(500, viewer.VerticalOffset);
        }

        [Fact]
        public void ADemoThatScrollsItself_IsShownUnwrapped()
        {
            var shell = Host([Fake("A", "One", scrollsItself: true)]);

            Assert.Same(built["One"], shell.Content.Content);
        }

        [Fact]
        public void ClickingACategory_KeepsTheShownDemo()
        {
            var shell = Host([Fake("A", "One"), Fake("B", "Two")]);
            object shown = shell.Content.Content!;

            shell.Tree.SelectedItem = shell.Tree.Items[1];
            shell.Canvas.Render();

            Assert.Same(shown, shell.Content.Content);
            Assert.Equal(1, builds["One"]);
        }

        [Fact]
        public void TheDesignSession_TracksDemosBuiltAfterStartup()
        {
            UIElement? late = null;
            var lateEntry = new SampleEntry("A", "Late", (configuration, _) => late = new MarkupLoader(configuration).Load("<Border Width=\"10\" Height=\"10\"/>", "Late"));
            var shell = Host([Fake("A", "One"), lateEntry]);

            shell.Select("Late");

            Assert.NotNull(DesignDemo.SessionFor(shell.Configuration).FindDocument(late!, out _));
        }

        [Fact]
        public void SwitchingAwayWhileEditing_RemovesTheEditorOverlay()
        {
            SampleEntry editor = SampleCatalog.All.Single(e => e.Name == "Editor");
            var shell = Host([editor, Fake("A", "Other")]);
            Button edit = shell.Root.EnumerateVisualSubtree().OfType<Button>()
                .First(b => b.Content is TextBlock { Text: "Edit this page" });
            edit.Command!.Execute(null);
            Assert.NotEmpty(shell.Canvas.Overlays);

            shell.Select("Other");

            Assert.Empty(shell.Canvas.Overlays);
        }

        [Fact]
        public void TheSidebar_HasFocusOnceAttached()
        {
            var shell = Host([Fake("A", "One")]);

            Assert.Same(shell.Tree, shell.Canvas.FocusedElement);
        }

        internal SampleEntry Fake(string category, string name, float height = 100, bool scrollsItself = false) =>
            new(category, name, (_, _) =>
            {
                builds[name] = builds.GetValueOrDefault(name) + 1;
                var element = new Border { Width = 200, Height = height };
                built[name] = element;
                return element;
            }, scrollsItself);

        internal static ShellHost Host(IReadOnlyList<SampleEntry> entries, FakeInputSystem? input = null)
        {
            input ??= new FakeInputSystem();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new System.Drawing.Size(1280, 720) })
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets();
            IcyConfiguration configuration = builder.Build().UseDefaultTheme();
            UIElement root = SampleShell.Build(configuration, "Airfool", entries);
            var canvas = new Canvas(configuration) { IsVisible = true, IsInputEnabled = true };
            canvas.Add(root);
            canvas.Render();
            return new ShellHost(configuration, canvas, input, root);
        }

        internal sealed record ShellHost(IcyConfiguration Configuration, Canvas Canvas, FakeInputSystem Input, UIElement Root)
        {
            public TreeView Tree => (TreeView)((SplitPane)Root).First!;

            public ContentControl Content => (ContentControl)((SplitPane)Root).Second!;

            public void Select(string name)
            {
                Tree.SelectedItem = Tree.Items.Cast<TreeViewNode>().SelectMany(c => c.Children).Single(n => n.ToString() == name);
                Canvas.Render();
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleShellTests"`
Expected: build FAILS with `The name 'SampleShell' does not exist in the current context`.

- [ ] **Step 3: Create the shell**

Create `sources/Shared Samples/SampleShell.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using Icy.Configuration;
using Icy.Rendering.Brushes;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// The sample hosts' whole UI: a sidebar <see cref="TreeView"/> of the <see cref="SampleCatalog"/> demos, grouped by
    /// category, next to the selected demo.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item><description>A demo is built the first time it's selected, then kept, so it keeps its state and scroll offset.</description></item>
    /// <item><description>Only the selected demo is attached to the canvas; the others get no layout, rendering or input.</description></item>
    /// <item><description>
    /// The shared design session attaches before any demo is built, so every demo page is tracked, whatever order you
    /// open them in.
    /// </description></item>
    /// </list>
    /// </remarks>
    public static class SampleShell
    {
        /// <summary>
        /// Builds the shell over <see cref="SampleCatalog.All"/> and selects the first demo.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The shell's root element, to add to a <see cref="Canvas"/>.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily) => Build(configuration, fontFamily, SampleCatalog.All);

        /// <summary>
        /// Builds the shell over <paramref name="entries"/>; tests pass their own.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <param name="entries">The demos to list, in sidebar order.</param>
        /// <returns>The shell's root element.</returns>
        internal static UIElement Build(IcyConfiguration configuration, string fontFamily, IReadOnlyList<SampleEntry> entries)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            ArgumentNullException.ThrowIfNull(entries);
            configuration.Fonts.DefaultFontFamily = fontFamily;

            // First, so every demo built from here on is tracked.
            DesignDemo.SessionFor(configuration);

            var tree = new TreeView();
            var categories = new Dictionary<string, TreeViewNode>();
            var leaves = new List<TreeViewNode>();
            foreach (SampleEntry entry in entries)
            {
                if (!categories.TryGetValue(entry.Category, out TreeViewNode? category))
                {
                    category = new TreeViewNode(entry.Category) { IsSelectable = false, IsExpanded = true };
                    categories.Add(entry.Category, category);
                    tree.Items.Add(category);
                }

                var leaf = new TreeViewNode(entry.Name) { Tag = entry };
                category.Children.Add(leaf);
                leaves.Add(leaf);
            }

            var content = new ContentControl
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            var cache = new Dictionary<SampleEntry, UIElement>();
            tree.SelectionChanged += (_, _) =>
            {
                if (tree.SelectedItem is TreeViewNode { Tag: SampleEntry entry })
                    content.Content = GetOrBuild(entry);
            };

            UIElement GetOrBuild(SampleEntry entry)
            {
                if (!cache.TryGetValue(entry, out UIElement? shown))
                {
                    UIElement demo = entry.Build(configuration, fontFamily);
                    shown = entry.ScrollsItself
                        ? demo
                        : new ScrollViewer
                        {
                            Content = demo,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            VerticalAlignment = VerticalAlignment.Stretch,
                        };
                    cache.Add(entry, shown);
                }

                return shown;
            }

            var root = new SplitPane
            {
                First = tree,
                Second = content,
                SplitterPosition = 0.18f,
                MinFirstSize = 180,
                Background = new SolidColorBrush(Color.FromArgb(255, 25, 25, 30)),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };

            // Arrows and the D-pad start in the sidebar.
            root.Attached += (_, _) => root.Canvas?.Focus(tree);

            if (leaves.Count > 0)
                tree.SelectedItem = leaves[0];
            return root;
        }
    }
}
```

Notes for the implementer:
- `#FF19191E` is `Color.FromArgb(255, 25, 25, 30)`.
- If `SplitPane` has no `Background` (check `Control`), put the brush on a wrapping `Border` instead, and update
  `ShellHost.Tree`/`Content` in the tests to unwrap it.
- If `Canvas.Focus` isn't public, use the call the existing tests use (`canvas.Focus(live)` in
  `TreeViewDemoTests`); it is.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleShellTests"`
Expected: 9 PASS.

Likely failure: `SelectingADemoAgain_ShowsTheSameInstanceAtItsScrollOffset` reads 0 if `VerticalOffset` is clamped
against an extent that isn't measured yet. Call `shell.Canvas.Render()` before setting the offset; the shell itself
is fine.

- [ ] **Step 5: Run the full suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: 0 failures, Total 1621.

- [ ] **Step 6: Commit**

```bash
git add "sources/Shared Samples/SampleShell.cs" sources/IcyUI.Tests/Samples/SampleShellTests.cs
git commit -m "Add the samples shell: a categorized sidebar over lazily built, cached demos"
```

---

### Task 5: PageUp/PageDown switch demos

**Files:**
- Modify: `sources/Shared Samples/SampleShell.cs`
- Modify: `sources/IcyUI.Tests/Samples/SampleShellTests.cs`

**Interfaces:**
- Consumes: `IcyConfiguration.Input.Events.RegisterCommand(ICommand, KeyGesture, ...)`;
  `Icy.Input.Devices.KeyGesture(Keys)`, `Keys.PageDown`, `Keys.PageUp`; in tests,
  `FakeInputEventSystem.RaiseGesture(KeyGesture)`.
- Produces: shell-registered PageDown (next demo) and PageUp (previous demo).

- [ ] **Step 1: Write the failing paging tests**

Add to `SampleShellTests` (with `using Icy.Input.Devices;` at the top):

```csharp
        [Fact]
        public void PageDown_SkipsCategoriesAndWraps()
        {
            var shell = Host([Fake("A", "One"), Fake("B", "Two")]);

            Press(shell, Keys.PageDown);
            Assert.Equal("Two", shell.Tree.SelectedItem!.ToString());

            Press(shell, Keys.PageDown);
            Assert.Equal("One", shell.Tree.SelectedItem!.ToString());
        }

        [Fact]
        public void PageUp_GoesBackAndWrapsToTheLastDemo()
        {
            var shell = Host([Fake("A", "One"), Fake("B", "Two"), Fake("B", "Three")]);

            Press(shell, Keys.PageUp);
            Assert.Equal("Three", shell.Tree.SelectedItem!.ToString());

            Press(shell, Keys.PageUp);
            Assert.Equal("Two", shell.Tree.SelectedItem!.ToString());
        }

        [Fact]
        public void Paging_IntoACollapsedCategory_ExpandsIt()
        {
            var shell = Host([Fake("A", "One"), Fake("B", "Two")]);
            var b = (TreeViewNode)shell.Tree.Items[1];
            shell.Tree.Collapse(b);

            Press(shell, Keys.PageDown);

            Assert.True(shell.Tree.IsExpanded(b));
            Assert.Equal("Two", shell.Tree.SelectedItem!.ToString());
            Assert.Same(built["Two"], ((ScrollViewer)shell.Content.Content!).Content);
        }

        [Fact]
        public void Paging_WithNoDemoSelected_StartsFromTheEnds()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two"), Fake("A", "Three")]);

            shell.Tree.SelectedItem = null;
            Press(shell, Keys.PageDown);
            Assert.Equal("One", shell.Tree.SelectedItem!.ToString());

            shell.Tree.SelectedItem = null;
            Press(shell, Keys.PageUp);
            Assert.Equal("Three", shell.Tree.SelectedItem!.ToString());
        }

        private static void Press(ShellHost shell, Keys key)
        {
            Assert.True(shell.Input.Events.RaiseGesture(new KeyGesture(key)));
            shell.Canvas.Render();
        }
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleShellTests"`
Expected: the 4 paging tests FAIL (`RaiseGesture` returns `false`: nothing is registered).

- [ ] **Step 3: Register the commands**

In `SampleShell.cs`, add `using Icy.Input.Devices;` and `using System.Windows.Input;`. Before `return root;`, add:

```csharp
            void Step(int delta)
            {
                if (leaves.Count == 0)
                    return;

                int index = tree.SelectedItem is TreeViewNode node ? leaves.IndexOf(node) : -1;
                int next = index < 0
                    ? (delta > 0 ? 0 : leaves.Count - 1)
                    : (index + delta + leaves.Count) % leaves.Count;

                // Selecting reveals the leaf: its category expands and the sidebar scrolls to it.
                tree.SelectedItem = leaves[next];
            }

            configuration.Input.Events.RegisterCommand(new ShellCommand(() => Step(1)), new KeyGesture(Keys.PageDown));
            configuration.Input.Events.RegisterCommand(new ShellCommand(() => Step(-1)), new KeyGesture(Keys.PageUp));
```

And add this nested type at the end of `SampleShell`:

```csharp
        private sealed class ShellCommand(Action execute) : ICommand
        {
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            public bool CanExecute(object? parameter) => true;

            public void Execute(object? parameter) => execute();
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleShellTests"`
Expected: 13 PASS.

If `Paging_IntoACollapsedCategory_ExpandsIt` fails on `IsExpanded`, `SelectedItem`'s setter isn't revealing the leaf.
Call `tree.Reveal(leaves[next])` before selecting it; `TreeView.Reveal(object)` is public.

- [ ] **Step 5: Commit**

```bash
git add "sources/Shared Samples/SampleShell.cs" sources/IcyUI.Tests/Samples/SampleShellTests.cs
git commit -m "Switch demos with PageUp/PageDown from the shell"
```

---

### Task 6: Pin sidebar navigation and recycled rows

**Files:**
- Modify: `sources/IcyUI.Tests/Samples/SampleShellTests.cs`

**Interfaces:**
- Consumes: Task 1's Right behavior; `FakeInputSystem.Events.Navigation.RaiseFocusChanging(Vector2)` returning args with `Handled` (as in `TreeViewDemoTests`);
  `Canvas.FocusedElement`; `TreeView.List.Realized`, `TreeView.Rows`.
- Produces: tests only. Shell code changes only if a test exposes a bug.

- [ ] **Step 1: Write the tests**

Add to `SampleShellTests`:

```csharp
        [Fact]
        public void RightFromASidebarLeaf_EntersTheDemo_AndLeftComesBack()
        {
            Button? first = null;
            var demo = new SampleEntry("A", "Buttons", (_, _) =>
            {
                var panel = new StackPanel { Orientation = Orientation.Vertical, HorizontalAlignment = HorizontalAlignment.Left };
                first = new Button { Content = new TextBlock { Text = "First" } };
                panel.Children.Add(first);
                panel.Children.Add(new Button { Content = new TextBlock { Text = "Second" } });
                return panel;
            });
            // "Buttons" is not the last row, so the old Right (walk to the next row) would have stayed in the tree.
            var shell = Host([demo, Fake("A", "Other")]);
            Assert.Same(shell.Tree, shell.Canvas.FocusedElement);

            Assert.True(shell.Input.Events.Navigation.RaiseFocusChanging(System.Numerics.Vector2.UnitX).Handled);
            Assert.Same(first, shell.Canvas.FocusedElement);

            Assert.True(shell.Input.Events.Navigation.RaiseFocusChanging(-System.Numerics.Vector2.UnitX).Handled);
            Assert.Same(shell.Tree, shell.Canvas.FocusedElement);
        }

        [Fact]
        public void SidebarRows_ShowTheirOwnLabels_AfterCollapseAndExpand()
        {
            var shell = Host([Fake("A", "One"), Fake("A", "Two"), Fake("B", "Three"), Fake("B", "Four")]);
            var a = (TreeViewNode)shell.Tree.Items[0];

            shell.Tree.Collapse(a);
            shell.Canvas.Render();
            AssertRowsShowTheirItems(shell.Tree);

            shell.Tree.Expand(a);
            shell.Canvas.Render();
            AssertRowsShowTheirItems(shell.Tree);
        }

        private static void AssertRowsShowTheirItems(TreeView tree)
        {
            Assert.NotEmpty(tree.List.Realized);
            foreach (int index in tree.List.Realized.Keys)
            {
                string text = tree.List.Realized[index].EnumerateVisualSubtree().OfType<TextBlock>().Single().Text;
                Assert.Equal(tree.Rows[index].Item.ToString(), text);
            }
        }
```

- [ ] **Step 2: Run them**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~SampleShellTests"`
Expected: 15 PASS. These pin behavior the earlier tasks already built.

If one fails, don't loosen it. Debug it (superpowers:systematic-debugging):
- For navigation, check the tree's current row is "Buttons" (selected at startup), so Right isn't handled by the tree.
- For rows, this would be a pooled-container stale-content bug in `TreeView`.

Fix the root cause in a separate commit with its own regression test.

- [ ] **Step 3: Run the full suite**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: 0 failures, Total 1627.

- [ ] **Step 4: Commit**

```bash
git add sources/IcyUI.Tests/Samples/SampleShellTests.cs
git commit -m "Pin the shell's sidebar-to-demo navigation and recycled sidebar rows"
```

---

### Task 7: MonoGame host runs the shell

**Files:**
- Modify: `sources/MonoGame Sample/SampleGame.cs`
- Modify: `sources/MonoGame Sample/MonoGame Sample.csproj`
- Modify: `sources/MonoGame Sample/Content/Content.mgcb`
- Delete: `sources/MonoGame Sample/SamplesRunner.cs`, `sources/MonoGame Sample/SampleBase.cs`,
  `sources/MonoGame Sample/Samples/` (all 24 files)
- Delete: orphaned assets (Step 4)

**Interfaces:**
- Consumes: `SampleShell.Build(IcyConfiguration, string)`.
- Produces: nothing for later tasks.

- [ ] **Step 1: Delete the runner and the wrappers**

```bash
git rm "sources/MonoGame Sample/SamplesRunner.cs" "sources/MonoGame Sample/SampleBase.cs"
git rm -r "sources/MonoGame Sample/Samples"
```

- [ ] **Step 2: Rewrite `SampleGame.cs`**

Replace the file with:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Mvvm.Input;
using Icy.Configuration;
using Icy.MonoGame.Configuration;
using Icy.SharedSamples;
using Icy.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Icy.MonoGameSample
{
    public class SampleGame : Game
    {
        private const string FontFamily = "Airfool";

        private readonly GraphicsDeviceManager graphics;

        private IcyConfiguration uiConfiguration;
        private Canvas canvas;

        public SampleGame()
        {
            graphics = new(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void Initialize()
        {
            this.UseIcyUI();

            uiConfiguration = this.GetIcyConfiguration();
            uiConfiguration.UseDefaultTheme();
            canvas = new(uiConfiguration) { IsInputEnabled = true };

            var switchFullScreen = new RelayCommand(() =>
            {
                if (!graphics.IsFullScreen) ToFullScreen();
                else ToWindow();
                graphics.ApplyChanges();
            });
            uiConfiguration.Input.Events.RegisterCommand(switchFullScreen, new(Input.Devices.Keys.Enter, Input.Devices.ModifierKeys.Alt));

            // Icy.Diagnostics (Phase 9 M3.5) - F1 toggles the box-model overlay, F2 the diagnostics HUD (frame
            // time/memory/focused element), both engine-agnostic.
            var toggleBoundsOverlay = new RelayCommand(() => ToggleDebugTool("Bounds"));
            var toggleDiagnosticsHud = new RelayCommand(() =>
            {
                ToggleDebugTool("Focus");
                ToggleDebugTool("DiagnosticsHud");
            });
            uiConfiguration.Input.Events.RegisterCommand(toggleBoundsOverlay, new(Input.Devices.Keys.F1));
            uiConfiguration.Input.Events.RegisterCommand(toggleDiagnosticsHud, new(Input.Devices.Keys.F2));

            ToWindow();
            graphics.ApplyChanges();
            base.Initialize();
        }

        protected override void LoadContent()
        {
            uiConfiguration.Fonts.ImportFont(uiConfiguration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            // The shell lists every shared demo and registers PageUp/PageDown to switch between them.
            canvas.Add(SampleShell.Build(uiConfiguration, FontFamily));
        }

        protected override void Update(GameTime gameTime)
        {
            // Not Escape: dialogs and popups close on it.
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                Exit();

            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.White);
            canvas.Render();
            base.Draw(gameTime);
        }

        private void ToggleDebugTool(string name)
        {
            if (!canvas.ActiveDebugTools.Remove(name))
                canvas.ActiveDebugTools.Add(name);
        }

        private void ToWindow()
        {
            graphics.PreferredBackBufferHeight = 720;
            graphics.PreferredBackBufferWidth = 1280;
            graphics.IsFullScreen = false;
        }

        private void ToFullScreen()
        {
            graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
            graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            graphics.IsFullScreen = true;
        }
    }
}
```

The project has nullable and implicit usings disabled (see CLAUDE.md), so the fields stay non-annotated, as before.

- [ ] **Step 3: Link the catalog and shell**

In `MonoGame Sample.csproj`, after the `TreeViewDemo.cs` link (line 48), add:

```xml
    <Compile Include="..\Shared Samples\SampleCatalog.cs" Link="Samples\SampleCatalog.cs" />
    <Compile Include="..\Shared Samples\SampleShell.cs" Link="Samples\SampleShell.cs" />
```

- [ ] **Step 4: Remove orphaned assets**

Only the deleted samples referenced the content pipeline items. Confirm before deleting:

```bash
cd sources
grep -rn "bobr\|Consolas\|Segoe UI\|Inter-Italic\|My own test Arial" --include=*.cs --include=*.xml . | grep -v "/obj/\|/bin/"
```

Expected: no output. Then:
- Delete `MonoGame Sample/Content/bobr.png`, `Consolas.spritefont` and `Segoe UI.spritefont`. Remove their
  `#begin … /build:…` blocks from `Content/Content.mgcb`, and keep the header and references. The content pipeline
  stays: whether to drop it (`MonoGame.Content.Builder.Task`, the `dotnet-mgcb` tool manifest) belongs to the MonoGame
  backend-consistency pass.
- Delete `Resources/Fonts/Inter-Italic-VariableFont_opsz,wght.ttf`, `My own test Arial font.fnt` and
  `My own test Arial font_0.png`, and remove their `<None Update=…>` items from `MonoGame Sample.csproj`.
- Keep `Airfool.otf` and `Resources/Themes/AccentTheme.xml` (`MarkupStylesDemo` loads it).

- [ ] **Step 5: Build the host**

Run: `dotnet build "sources/MonoGame Sample/MonoGame Sample.csproj"`
Expected: 0 errors. On first build it restores `dotnet-mgcb` through the local tool manifest.

- [ ] **Step 6: Commit**

```bash
git add -A "sources/MonoGame Sample"
git commit -m "Run the samples shell in the MonoGame host and retire the per-demo wrappers"
```

Don't claim the app runs. Ivan smoke-tests it.

---

### Task 8: Stride host runs the shell

**Files:**
- Modify: `sources/Stride Sample/SampleGame.cs`
- Modify: `sources/Stride Sample/Stride Sample.csproj`

**Interfaces:**
- Consumes: `SampleShell.Build(IcyConfiguration, string)`.
- Produces: nothing for later tasks.

- [ ] **Step 1: Rewrite `SampleGame.cs`**

Replace the file with:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Mvvm.Input;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.SharedSamples;
using Icy.Stride.Configuration;
using Icy.UI;
using Stride.Engine;
using Stride.Rendering.Compositing;
using Color4 = Stride.Core.Mathematics.Color4;

namespace Icy.StrideSample
{
    /// <summary>
    /// A minimal Stride game hosting IcyUI, wired up in code (no Game Studio project or asset pipeline). It shows
    /// <see cref="SampleShell"/>, the same shared demos and sidebar as <c>MonoGame Sample</c>.
    /// </summary>
    internal sealed class SampleGame : Game
    {
        private Canvas? canvas;

        protected override void BeginRun()
        {
            base.BeginRun();

            // Minimal in-code GraphicsCompositor: clear the back buffer, then hand the "Game" render slot to
            // UseIcyUI(), which wraps it so the UI overlay draws on top of whatever was there before (nothing,
            // here - this sample has no 3D content).
            var compositor = new GraphicsCompositor
            {
                Game = new SceneRendererCollection
                {
                    new ClearRenderer { Color = new Color4(0.1f, 0.1f, 0.12f, 1f) },
                },
            };
            SceneSystem.GraphicsCompositor = compositor;
            SceneSystem.SceneInstance = new SceneInstance(Services, new Scene());

            IcyUISceneRenderer overlay = this.UseIcyUI();
            var configuration = this.GetIcyConfiguration();
            configuration.UseDefaultTheme();
            canvas = new Canvas(configuration) { IsInputEnabled = true };
            overlay.Canvases.Add(canvas);

            configuration.Fonts.ImportFont(configuration.Assets.DefaultAssetContext, @"Resources\Fonts\Airfool.otf");

            // The shell lists every shared demo and registers PageUp/PageDown to switch between them.
            canvas.Add(SampleShell.Build(configuration, "Airfool"));

            // Icy.Diagnostics (Phase 9 M3.5) - F1 toggles the box-model overlay, F2 the diagnostics HUD (frame
            // time/memory/focused element), both engine-agnostic.
            var toggleBoundsOverlay = new RelayCommand(() => ToggleDebugTool("Bounds"));
            var toggleDiagnosticsHud = new RelayCommand(() =>
            {
                ToggleDebugTool("Focus");
                ToggleDebugTool("DiagnosticsHud");
            });
            configuration.Input.Events.RegisterCommand(toggleBoundsOverlay, new KeyGesture(Keys.F1));
            configuration.Input.Events.RegisterCommand(toggleDiagnosticsHud, new KeyGesture(Keys.F2));
        }

        private void ToggleDebugTool(string name)
        {
            if (!canvas!.ActiveDebugTools.Remove(name))
                canvas.ActiveDebugTools.Add(name);
        }
    }
}
```

The canvas no longer sets its own `Background`: the shell paints the same `#FF19191E`.

- [ ] **Step 2: Link the catalog and shell**

In `Stride Sample.csproj`, after the `TreeViewDemo.cs` link (line 38), add:

```xml
    <Compile Include="..\Shared Samples\SampleCatalog.cs" Link="SampleCatalog.cs" />
    <Compile Include="..\Shared Samples\SampleShell.cs" Link="SampleShell.cs" />
```

- [ ] **Step 3: Build the host**

Run: `dotnet build "sources/Stride Sample/Stride Sample.csproj"`
Expected: 0 errors. Remove any `using` the compiler now reports as unused (e.g. `System.Drawing`, `Icy.Rendering.Brushes`).

- [ ] **Step 4: Commit**

```bash
git add "sources/Stride Sample"
git commit -m "Run the samples shell in the Stride host"
```

Don't claim the app runs. Ivan smoke-tests it.

---

### Task 9: Whole-solution verification

**Files:** none (fixes only if something fails).

- [ ] **Step 1: Full rebuild and warning count**

Run: `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"`
Expected: `0 Error(s)` and no more than 84 warnings. If the count rose, list the new ones with
`dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep "warning" | sort -u` and fix those in `IcyUI`. Nullable
warnings from demos newly linked into `IcyUI.Tests` already appear in the Stride Sample build. Report them; don't
silence them.

- [ ] **Step 2: Full test run**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: 0 failures, **Total 1627**.

- [ ] **Step 3: No stale references**

```bash
cd sources
grep -rn "SamplesRunner\|SampleBase\|UpdateSelectedDemo\|Keys.Tab, ModifierKeys.Shift\|Keys.Tab, Input.Devices.ModifierKeys.Shift" --include=*.cs . | grep -v "/obj/\|/bin/"
```

Expected: no output.

- [ ] **Step 4: Whole-branch review**

Use superpowers:requesting-code-review over the commits from Task 1 to here, against the spec. Then hand over to Ivan
for the smoke test on both hosts:
- the sidebar shows the six categories;
- clicking, arrows/D-pad, Right into a demo and Left back, and PageUp/PageDown all switch demos;
- the Editor demo's overlay goes away when you switch;
- Escape no longer quits MonoGame.
