# Samples shell: a shared sidebar for both sample hosts

> Design spec, discussed with Ivan on 2026-10-07. It's the second of three sub-projects that came out of the
> "samples navigation sidebar" request: **TreeView** (done, `2026-10-06-treeview-design.md`) → **samples shell** (this
> spec) → **editor scope** (`EditorFrame` restricted to a subtree, with scroll forwarding).

## Context

The sample hosts run 20 shared demos, navigated only by PageUp/PageDown, and each host registers them its own way:

- **MonoGame Sample** has `SamplesRunner`/`SampleBase` and one `DrawableGameComponent` wrapper per demo (about 25
  lines each, re-importing the font in every `LoadContent`). It also has three engine-only samples (`InputLoggingSample`,
  `FontsSample`, `UISample`) and an unregistered `RenderContextTest`, all left over from building the rendering
  subsystem and no longer needed.
- **Stride Sample** has 20 hand-written root fields, a hard-coded `% 20`, and an `IsVisible` switch. Its demo order
  differs from MonoGame's, and PageUp goes *forward* like PageDown.
- Both hosts build all 20 demos at startup and keep them attached to the canvas, toggling `IsVisible`. `DesignDemo` and
  `EditorDemo` are built last so the design session doesn't track the other pages.

**Success looks like this:**
- Both hosts show the same grouped sidebar, built from one demo list in `Shared Samples`.
- Adding a demo means adding one catalog line, without a wrapper class or edits to either `SampleGame.cs`.
- Demos switch by mouse, keyboard and gamepad. Tall demos scroll inside the content area.

## Decisions

| Decision | Choice |
|---|---|
| Engine-only MonoGame samples | **Deleted** with the runner and the wrappers. The shell lists only the shared demos. |
| Grouping | 6 categories, below. Categories are non-selectable and start expanded. |
| Demo lifetime | **Lazy and cached.** Built on first selection, then kept. Only the selected demo is attached to the canvas. |
| Design session | **Attached at startup** by the shell, so every demo page is tracked in the same way, whatever order you click in. |
| Layout | `SplitPane`: a resizable sidebar (about 220 px) on the left and the content host on the right. |
| Scrolling | Each demo gets its own cached `ScrollViewer`, unless its catalog entry says it `ScrollsItself`. |
| Previous/next demo | PageUp/PageDown, registered by the shell. Shift+Tab is no longer bound: it is reverse focus traversal. |
| TreeView Right key | **Standard tree convention** (a core change, below): Right is unhandled on a leaf, so spatial navigation leaves the tree. |

### Categories

| Category | Demos |
|---|---|
| Basics | Controls, Styles, Navigation |
| Markup | Markup, Markup Styles, Control Templates |
| Layout | Split Pane, Expander, Wrap Grid, Scaling |
| Items | Items Control, List Box, Selector, Tab Control, Tree View |
| Dialogs & Pickers | Dialog, Color Picker |
| Design Tools | Property Grid, Design, Editor |

## Design

### Components

Both new types live in `Shared Samples`, so both hosts get the same shell. Both are linked into `IcyUI.Tests`.

**`SampleCatalog`** is a static class holding the single ordered demo list.

```csharp
public sealed record SampleEntry(string Category, string Name, Func<IcyConfiguration, string, UIElement> Build, bool ScrollsItself = false);

public static class SampleCatalog
{
    public static IReadOnlyList<SampleEntry> All { get; }
}
```

The list order is the sidebar order. Categories appear in the order of their first entry.

`ScrollsItself` is `true` for **Controls** and **Styles**. `ControlsDemo` already wraps its page in a stretched
`ScrollViewer` and keeps its floating `Window` as a sibling of it; `StylesDemo` returns a `ScrollViewer`. Wrapping
either in the shell's viewer would give the inner viewer unbounded height, so it would grow instead of scrolling, and
`ControlsDemo`'s `Window` would scroll with the page.

**`SampleShell`** is a static class with `Build(IcyConfiguration configuration, string fontFamily)`, the same shape as
every `*Demo.Build`. An `internal Build(configuration, fontFamily, IReadOnlyList<SampleEntry> entries)` overload lets
tests pass their own entries. `Build`:

1. Calls `DesignDemo.SessionFor(configuration)` before building anything else.
2. Builds the root: a `SplitPane` with a dark background (`#FF19191E`), the sidebar on the left and the content host on
   the right.
3. Fills the sidebar `TreeView` with static `TreeViewNode`s. Each category is a non-selectable, expanded branch; each demo
   is a selectable leaf that carries its `SampleEntry`.
4. Makes the content host a `ContentControl`. On `TreeView.SelectionChanged` it looks the selected entry up in a
   `Dictionary<SampleEntry, UIElement>`. On a miss it calls `entry.Build(configuration, fontFamily)`, wraps the result
   in a new `ScrollViewer` unless `ScrollsItself` is set, and caches it. The cached element becomes the host's `Content`,
   which detaches the previous demo.
5. Registers PageUp/PageDown (see Input).
6. Selects the first entry (Controls).

### Core change: TreeView's Right key

The TreeView spec made Right "the next row in flattened order", unhandled only on the last row. In a sidebar, that blocks
the way to the content: Right on any leaf but the last moves down a row. `TreeView.OnNavigate` switches to the ARIA/WinUI
convention:

| Row | Right |
|---|---|
| Collapsed branch | Expand it (unchanged). |
| Expanded branch | Move to its first child. |
| Leaf | **Not handled**: spatial navigation takes over. |

Left is unchanged: it collapses an expanded branch, moves from a child to its parent, and is unhandled on a collapsed
root. Down remains the way to walk every row in order.

This touches `TreeView.OnNavigate` and its XML docs, and replaces `Right_ExpandsACollapsedBranch_ThenWalksTheRows`. The
TreeView spec gets a dated amendment note pointing here. Every TreeView benefits, not only the sidebar: the Phase 10.4
outline panel and games' settings or inventory trees sit next to other content too.

### Demo changes

Only `EditorDemo` changes. Its "stop editing when hidden" hook listens to `root.Detached` instead of
`PropertyChanged(IsVisible)`, because switching demos now detaches the old one.

The design session now tracks every demo page, not just Design and Editor. That's intended: the hosts are dev-only,
and sub-project 3's editor scope can then edit any demo.

### Input

- **Pointer:** clicking a demo leaf shows it. Clicking a category opens or closes it. Both are existing `TreeView`
  behavior.
- **Keyboard and gamepad:** no shell-specific navigation code.
  - Arrows and the D-pad move through the tree; selection follows focus, so arrowing onto a leaf swaps the content.
  - Focus stays in the sidebar after a swap, so you can browse demos quickly.
  - Right on a demo leaf enters the shown demo through spatial navigation (see the core change above). Left from the
    demo's left edge returns to the sidebar.
- **PageDown / PageUp** select the next / previous demo. They skip categories, wrap around at both ends, and expand a
  collapsed category to reveal the leaf they land on. The sidebar scrolls the selection into view.
- **Host-only hotkeys** stay in each `SampleGame`: F1 (bounds overlay), F2 (diagnostics HUD), and Alt+Enter (fullscreen,
  MonoGame only).

### Hosts

**MonoGame Sample `SampleGame`:**
- `Initialize` creates the canvas with `IsInputEnabled = true`, imports the Airfool font once, and adds
  `SampleShell.Build(uiConfiguration, "Airfool")`.
- It keeps F1/F2/Alt+Enter and the window/fullscreen code, and drops its PageUp/PageDown/Shift+Tab registrations.
- `Update` exits on gamepad Back only. Escape used to quit the app, which collided with dialogs and popups closing on
  Escape.

**Stride Sample `SampleGame`:**
- `BeginRun` keeps the compositor setup, imports the font, and adds the shell.
- The 20 root fields, `UpdateSelectedDemo`, the demo switching command and the demo list in the doc comment go away.
- The stale `<remarks>` (written against Stride 4.2 and never run on a GPU) go away too.

**Deleted:**
- `MonoGame Sample/SamplesRunner.cs` and `MonoGame Sample/SampleBase.cs`.
- Everything in `MonoGame Sample/Samples/`: the 20 demo wrappers, `InputLoggingSample`, `FontsSample`, `UISample` and
  `RenderContextTest`.
- Content, resources and package references that only the deleted samples used. Check them at implementation time.

## Known limitations

- While you edit in the Editor demo, `EditorFrame` covers the whole canvas, the sidebar included. Press "Stop editing"
  before switching demos. Sub-project 3 scopes the frame to the content area.
- Every global command registration a demo makes on the configuration stays active after it's detached. No current
  demo relies on this. Check each demo while implementing.

## Testing

The tests run in `IcyUI.Tests` against `FakeRenderContext` and a `Canvas`. Lazy-build tests use the `internal Build`
overload with counting builders.

- **Catalog:**
  - It has 20 entries with unique names, and the 6 categories in the order above.
  - Every entry's `Build` succeeds against a fresh configuration with the session attached.
- **Lifetime:**
  - At startup, only the first entry's builder has run, and only its demo is attached.
  - Selecting another entry builds it once and detaches the previous demo.
  - Selecting the first entry again shows the same instance, with its `ScrollViewer.VerticalOffset` unchanged.
  - A `ScrollsItself` entry is shown without a wrapping `ScrollViewer`.
- **Session:** a demo built after startup has a `DesignDocument` (`FindDocument` returns non-null).
- **Editor:** switching demos while `EditorDemo` is in Edit mode disposes its frame.
- **Hotkeys:**
  - PageDown on the last demo wraps to the first, and PageUp on the first wraps to the last.
  - Both skip categories.
  - Paging into a collapsed category expands it and selects the leaf.
- **Navigation:** Right from a sidebar leaf moves focus into the shown demo, and Left from its leftmost control returns
  to the sidebar. This is the "sample shell sidebar" test that the spatial-navigation spec deferred.
- **TreeView Right (core):** Right expands a collapsed branch, moves from an expanded branch to its first child, and
  returns `false` on a leaf, both mid-tree and on the last row.
- **Pooled rows:** collapse and expand a category, then check every visible row's label against its node.

Ivan smoke-tests both hosts by hand.

## Performance

- Startup builds the shell and one demo instead of 20. Each other demo is built once, on its first visit.
- Memory grows only with visited demos, up to all 20, which is today's steady state.
- A swap is one `Content` assignment and one layout pass. Detached demos cost nothing per frame: no layout, rendering or
  input.
- The only core change is TreeView's Right key, which does the same amount of work as before. The extra session
  tracking exists only in the sample hosts.

## Out of scope

- Editor scope (sub-project 3).
- Gamepad shortcuts for previous/next demo: `RegisterCommand` takes only `KeyGesture`s, and the D-pad already reaches the
  sidebar.
- Search or filtering in the sidebar.
