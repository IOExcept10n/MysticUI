# Editor toolkit MVP (Phase 10.4-lite / 10.5-lite)

> Design spec, discussed with Ivan on 2026-10-08/09. This closes the current stage of Phase 10
> (`2026-10-04-markup-design-document-design.md`) with the smallest toolkit a real game can be authored with. Builds on
> the editor frame (`2026-10-05-editor-frame-design.md`) and the editor scope (`2026-10-08-editor-scope-design.md`).

## Context

Ivan needs an IcyUI MVP by 2026-10-11: a university project (a Stride game, developed on x64 and Windows-on-ARM64)
will depend on it. The game uses bindings, navigation, animations, styles and markup-driven screens, and it needs the
designer toolkit. The plan after the MVP is to build the game, record every developer-experience issue it surfaces,
then improve the API and only then run an optimization pass over the whole project.

So this phase has two goals:

- Make the toolkit good enough to author real game screens in two ways:
  - a **dev overlay**, toggled while debugging;
  - a **page** with the complete editor, reachable from the game's debug settings.
- Leave open design questions to dogfooding instead of guessing them now.

Today `IcyUI.Design` has the document and edit engine (10.1), hot reload (10.2), the headless `EditorSession`, the
visual `EditorFrame` (10.3) and scoping. Several pieces are still missing:

- **No panels.** The only way to select is clicking in the frame, and the only property edits are drags and nudges.
- **No Save from inside the editor.** `DesignDocument.Save()` exists, but `DesignSession.SourcePathResolver` resolves only
  existing file paths. A Stride game loads screens by asset name, and the deployed copy under `bin/` is overwritten by
  the next build.
- **Core `PropertyGrid` edits its `Target` directly through reflection.** An edit made there changes the live object
  but never reaches the markup text, so it can't be undone or saved. The deferred "live refresh" also leaves it stale
  after a drag or an undo.

## Decisions

| Question | Decision |
|---|---|
| Shape of the toolkit | One shared core: panels are plain controls bound to an `EditorSession`. Two thin compositions on top: `EditorOverlay` and `EditorWorkspace`. |
| Which session | Both compositions use the game's single `DesignSession`. Documents, undo history, modified state and Save-all are shared. |
| What the workspace opens | Documents the session already tracks, plus an "Open file…" path box (option A). No folder browsing. |
| Workspace preview | Loads `document.Text` again under the document's `SourcePath`. The session joins it to the same `DesignDocument`, so edits mirror to the live game instance. |
| Save target | An explicit source root (`SourceRoot`) plus an optional discovery helper that walks up from `AppContext.BaseDirectory` to a marker file (option A). |
| Save failure mode | Save is disabled with a visible reason when a document doesn't resolve. It never throws during a session. |
| Save commands | Save (the selection's document) and Save all (every modified tracked document), in both compositions. No auto-save. |
| Properties | Core `PropertyGrid` gains a value adapter and `Refresh()`. `IcyUI.Design` supplies a markup adapter (option A). |
| Attributes holding expressions | Shown as text rows (`{Binding …}`, `{StaticResource …}`), so a typed editor never clobbers them. |
| Overlay toggle | A configurable key, default **F4** (the samples shell's key). `null` means the game binds it itself. |
| Release builds | Zero cost: the game references `IcyUI.Design` only in Debug configurations. |

## Design

### Core: `PropertyGrid` value adapter and `Refresh()`

A new public class in `Icy.Data`, next to `PropertyGridEntry`:

```csharp
public class PropertyGridValueAdapter
{
    public static PropertyGridValueAdapter Default { get; }

    public virtual object? GetValue(PropertyGridEntry entry, object target);
    public virtual bool TrySetValue(PropertyGridEntry entry, object target, object? value);

    public virtual string? GetExpression(PropertyGridEntry entry, object target);   // null: a typed row
    public virtual bool TrySetExpression(PropertyGridEntry entry, object target, string text);

    public virtual bool CanReset(PropertyGridEntry entry, object target);
    public virtual void Reset(PropertyGridEntry entry, object target);
}
```

- It's a class with virtual members rather than an interface, so members can be added after dogfooding without
  breaking adapters. The default implementation is today's behavior:
  - values are read and written through reflection;
  - there are no expression rows;
  - `CanReset` is `false`.
- `PropertyGrid.ValueAdapter` defaults to `PropertyGridValueAdapter.Default`. Setting it rebuilds the rows.
- `BuildEditor` reads and writes through the adapter.
- When `GetExpression` returns non-`null`, the row is a `TextBox` committed on Enter or on losing focus. A failed
  `TrySetExpression` marks the row `ControlState.Invalid`, the same visual state as #5's binding errors.
- When `CanReset` is `true`, a small Reset button appears at the end of the row.
- `PropertyGrid.Refresh()` re-reads every realized row's value and expression state in place: no rebuild and no focus
  loss. The exception is the row being edited, which keeps its text until it's committed.
- `PropertyGrid` disables pooling, so `Refresh()` only touches its own rows. No stale-container path is added.

**Engines:** core only. MonoGame, Stride and a future FNA host need no work.

### `IcyUI.Design`: value formatting

`MarkupValues` (today `Format(int)` and `Format(Thickness)` for gestures) becomes a public `MarkupValueFormatter`:

- `bool TryFormat(object? value, Type type, out string text)` covers:
  - `string`
  - `bool`
  - integral and floating-point numbers, invariant culture, with `float` and `double` written round-trippable
  - enums (flags joined the way the loader parses them)
  - `Color`
  - solid brushes
  - `Vector2`/`Vector3`/`Vector4`
  - `Point`, `Size`, `Thickness`, `CornerRadius`
  - any type whose `TypeConverter` converts to and from `string`
- Anything else returns `false`. That row is read-only in the markup adapter, and the panel's status line says why when it is selected (core has no tooltips).
- **The invariant:** every formatted value loads back to an equal value through `MarkupLoader`. Tests check this per
  type.

### `IcyUI.Design`: `MarkupPropertyAdapter`

A `PropertyGridValueAdapter` for one selected element (`DesignDocument` + `NodeId` + live instance):

| Member | Behaviour |
|---|---|
| `GetValue` | The live instance's value, so styles, bindings and defaults show what's actually on screen. |
| `TrySetValue` | Formats the value and calls `MarkupEditor.SetAttribute`. One undo step per commit. Slider drags commit inside one `BeginTransaction`, so a drag is one step. |
| `GetExpression` | The attribute's raw text when the attribute exists and is a markup extension (starts with `{` and isn't escaped `{}`). |
| `TrySetExpression` | `SetAttribute` with the raw text. The edit engine's existing reload and diagnostics handle invalid input, and the row turns Invalid. |
| `CanReset` / `Reset` | The attribute exists / `MarkupEditor.ClearAttribute`. |

A property set only by a style shows the style's value. Editing it writes a *local* attribute, as in XAML designers.
Attached properties written as `Owner.Property` attributes are listed when they're present in the markup. Adding a
new attached property is out of scope.

### Panels

All three are public controls in a new `Icy.Design.Editor.Panels` namespace. Each has a `Session` property
(`EditorSession?`): `null` shows an empty state. Each subscribes only while it's both on a canvas and given a session.

**`OutlinePanel`**

- A `TreeView` over the element tree of the selected document's **syntax**, not the live visuals. That way
  out-of-sync and failed elements still appear, in a warning style.
- Item label: `Type` or `Type "Name"`.
- Its `Document` follows the session's selection. It can also be set explicitly (the workspace sets it from its
  document picker).
- **Selection:**
  - Selecting a row calls `EditorSession.Select(document, node)`.
  - `SelectionChanged` reveals and selects the matching row through `TreeView` Reveal.
- **Updates:** `DesignDocument.Changed` refreshes the affected nodes. A structural edit rebuilds the tree from the new
  syntax, keeping expansion state by `NodeId`.
- **Commands:**
  - Delete → `DeleteSelection`.
  - Duplicate (Ctrl+D) → `InsertElement` of the node's text after itself.
  - Drag a row onto another row → `MoveElement` (before, after or into, from the drop zone of the row).

**`PropertiesPanel`**

- A `PropertyGrid` with a `MarkupPropertyAdapter` for the current selection, plus a header showing the type, name and
  document.
- Calls `Refresh()` on:
  - `SelectionChanged` (it retargets);
  - `DesignDocument.Changed` for the selected document;
  - the end of a frame gesture.
- Never refreshes per frame.

**`ToolboxPanel`**

- `Items` is an `ObservableCollection<ToolboxItem>`, where `ToolboxItem` has `DisplayName`, `Category` and a markup
  `Snippet`.
- `ToolboxItem.CreateDefaults(MarkupConfiguration)` lists every public, non-abstract `UIElement` type with a
  parameterless constructor in `BuiltInNamespaces`. Each gets a minimal snippet, e.g. `<Button Content="Button"/>`, or
  `<Type/>` when there's no special case. Games add their own controls to `Items`.
- **Click:** inserts into the selected element if its placement strategy accepts children, otherwise after the
  selected element. Then the new element is selected.
- **Drag:** a drag onto the frame uses the existing placement drop path, the same one a move gesture uses.

### Saving

- **`DesignDocument` additions:**
  - `IsModified` is `true` after any edit since the last load or Save, and `false` again after Save.
  - Undoing back to the saved state also clears it. It's tracked by undo-stack position.
  - `ModifiedChanged` reports the change.
- `DesignDocument.CanSave` / `SaveBlockedReason` report whether `SourcePathResolver` resolves the document. The
  reasons are "no source path" and "doesn't resolve under the source root".
- **`DesignSession` additions:**
  - `DesignSession.SaveAll()` saves every modified document that can be saved. It returns the documents it skipped,
    with their reasons.
  - `DesignSession.UseSourceRoot(string root)` sets a resolver that maps an asset name to `Path.Combine(root, name)` and
    accepts only existing files.
  - `DesignSession.FindSourceRoot(string marker, string relative = "")` walks up from `AppContext.BaseDirectory` to the
    first directory containing `marker` (e.g. `MyGame.csproj`), then appends `relative` (e.g. `Assets/UI`). It returns
    `null` when nothing matches.
- The compositions surface `SaveBlockedReason` in their status line and disable the button. A Save that throws an I/O
  error shows the message there instead of propagating.

### `EditorOverlay`

```csharp
public sealed class EditorOverlay : IDisposable
{
    public static EditorOverlay Attach(Canvas canvas, DesignSession design, EditorOverlayOptions? options = null);
    public bool IsShown { get; }
    public EditorSession? Session { get; }   // null while hidden
    public void Show(); public void Hide(); public void Toggle();
}
```

- **`EditorOverlayOptions`:**
  - `Scope` (default: the canvas root's content);
  - `ToggleKey` (default `KeyGesture(Keys.F4)`, `null` to disable);
  - `DockWidth`.
- **`Show()`:**
  - attaches an `EditorFrame` with the scope;
  - adds two canvas overlays: a left dock (a `TabControl` holding Outline and Toolbox) and a right dock (Properties);
  - adds a top bar with Mode, Undo, Redo, Save, Save all, the modified badge and a status line.
- **`Hide()`:** detaches all of it. The `DesignSession` keeps tracking, and undo history stays with the documents.
- **Collapsing:** the docks collapse to a thin strip, so the game stays visible.
- **Input:** the docks are overlays above the frame's capture layer, so clicks on them never select game elements.

### `EditorWorkspace`

A public `Control` for a game's own debug page.

- **Layout:** a `SplitPane`.
  - Left: a document picker (tracked documents with a modified badge, plus an "Open file…" path box), then
    Outline/Toolbox.
  - Center: the preview host, a `ScrollViewer` whose content is the editor scope.
  - Right: Properties.
  - Top: the same bar as the overlay.
- `DesignSession` property: required to do anything. `null` shows an empty state.
- **Picking a document** loads `document.Text` under `document.SourcePath` into the preview host through the session's
  configuration. The session joins it to the same document, so every edit also reaches the game's live instance. The
  previous preview instance is removed and its load scope released.
- **"Open file…"** loads a file path through the loader; the session tracks it as a new document.
- **Attach and detach:**
  - On attach to a canvas, it attaches an `EditorFrame` scoped to the preview host.
  - On detach, it disposes the frame.
- **Busy canvas:** if another editor session already owns the canvas (e.g. a shown overlay), it shows a "the editor
  overlay is active" notice and retries when that session ends (`EditorSession.FindAttached`).

### Samples

- **New `EditorWorkspaceDemo`** in Shared Samples, registered in both hosts' catalogs. It hosts an `EditorWorkspace` on
  the shell's existing `DesignSession`. Its source root is a temp folder that holds copies of the demo markup, so Save
  never touches the repo.
- **The shell's F4 toggle** switches from a bare `EditorFrame` to `EditorOverlay`, scoped to the content area as
  today.
- **`EditorDemo`** stays as the minimal frame-only example.

### Resolved separately: `CalculateOverflow`

It isn't a bug. Shrinking on overflow is opt-in through `MinWidth`/`MinHeight` (documented, commit 97ca604). Applying
"unset = 0" collapsed detached arranges and squeezed popups wider than the surface. So the docks and the top bar are
placed against the region explicitly, as the toolbar already is, and set a minimum only where shrinking is wanted.

## Testing

- **Core `PropertyGrid`:**
  - the default adapter keeps today's behavior (existing tests stay green);
  - a custom adapter receives reads and writes;
  - expression rows show text and commit on Enter or losing focus;
  - an Invalid state follows a failed expression;
  - Reset shows and acts;
  - `Refresh()` updates values without rebuilding rows or stealing focus, and keeps an in-progress edit.
- **`MarkupValueFormatter`:** a round trip through `MarkupLoader` for every supported type, including culture (format
  under a comma-decimal culture), `float` edge values and flags enums.
- **`MarkupPropertyAdapter`:**
  - a set becomes one undo step, a slider drag one transaction;
  - expression attributes aren't overwritten by typed edits;
  - Reset removes the attribute;
  - a style-set value edits locally.
- **`OutlinePanel`:**
  - selection sync both ways;
  - incremental update on attribute edits;
  - a rebuild preserves expansion;
  - **re-targeting to another document shows no stale rows** (the pooled-container rule);
  - duplicate, delete and move.
- **`ToolboxPanel`:** default discovery; click-insert into a container and after a leaf; a game-added item.
- **Saving:**
  - `IsModified` across edit, save and undo-to-saved;
  - `UseSourceRoot` resolution;
  - `FindSourceRoot` with and without a marker;
  - `SaveAll` skips unresolvable documents;
  - I/O errors don't propagate.
- **`EditorOverlay`:**
  - show, hide and toggle attach and detach everything;
  - the toggle key;
  - dock clicks don't select;
  - hiding keeps the documents' undo history.
- **`EditorWorkspace`:**
  - the preview mirrors edits to a second live instance of the same document;
  - switching documents releases the previous preview;
  - the busy-canvas notice appears and clears.
- Existing editor, scope and shell tests stay green. Library warnings stay at the baseline (IcyUI 84, Design 1).

## Performance

- **Release:** zero. `IcyUI.Design` isn't referenced, and the core `IMarkupLoadObserver` seam stays `null`. The core
  `PropertyGrid` adapter adds one virtual call per row read or write.
- **Overlay hidden:** no per-frame work and no subscriptions beyond the `DesignSession`'s existing tracking.
- **Overlay or workspace shown:** the frame's measured overhead (≈0.09 ms per frame on the 500-element page), plus
  the panels. The panels do no per-frame work: they refresh on edits and selection only.
  - **Per edit:** the outline updates incrementally (a full rebuild happens only on structural edits), and `Refresh()`
    is O(rows).
- **Perf test:** an attribute edit on the 500-element page with all three panels attached, measured in Release:
  outline update plus properties refresh.

## Documentation

- Complete XML docs for every new public API, as in the other phases.
- The `EditorOverlay` and `EditorWorkspace` class docs include a short "using it in your game" recipe:
  - attach the `DesignSession` before loading screens;
  - set the source root;
  - reference `IcyUI.Design` in Debug only.

## Out of scope (after the MVP, or driven by dogfooding)

- Markup text editor (needs a multiline `TextBox`, which core doesn't have), resource browser, multi-select,
  clipboard.
- Standalone designer app, SDK/NuGet packaging.
- Adding new attached properties from the panel; nested-object editors.
- Open placement questions: I5, I6, I7 and I2-Grid. The I-1 Esc/Dialog question too, unless it bites in the game.
- MonoGame backend consistency, MonoGame touch, the WoA DesktopGL deadlock, FNA.
