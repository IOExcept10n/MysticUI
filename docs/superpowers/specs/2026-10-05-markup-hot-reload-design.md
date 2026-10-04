# Phase 10.2: applying arbitrary markup text to live pages (hot reload)

> Design spec, discussed with Ivan on 2026-10-04/05. It's the second sub-project of Phase 10, building on 10.1
> (`docs/superpowers/specs/2026-10-04-markup-design-document-design.md`).

## Context

10.1 can edit a tracked page through typed operations, each of which knows exactly what it changes. 10.2 handles the
other direction: **new text arrives** (a file saved in an IDE, or, in 10.4, the developer typing in the in-tool text
editor), and the live pages must follow while keeping their runtime state wherever the edit allows.

So the core of 10.2 is one operation: **apply an arbitrary new text to a tracked document**. How the text arrives is not
part of it.

## Decisions

| Decision | Choice |
|---|---|
| Triggers | **No file watcher in 10.2.** Some hosts use virtual file systems (Stride's VFS, packed assets) that can't be watched. 10.2 ships `ApplyText(newText)` plus a convenience `ReloadFromSource()`. Watchers and other triggers belong to the 10.5 hosts, possibly behind a small `IMarkupSource` seam then. |
| Undo | `ApplyText` is **one undoable step**. Undo and redo apply the previous and next text through the same diff, mixing freely with visual edits. |
| How text becomes live changes | **A structural tree diff** of the last valid tree against the new tree, turned into 10.1's mirror actions. Every matched element keeps its id and live instance. |
| Bad text | **The text always wins; the live pages follow when they can.** Malformed text is stored but not mirrored. Loader errors fail per unit, are reported, and heal on the next valid apply. Visual edits are refused while the document is out of sync. |
| `NeedsReload` | Stays sticky in 10.2 (deferred minor M2). |
| Folded 10.1 minors | M4 (non-markup setter exceptions), M1 (load while fast-path values are pending), M3 (names leaked by a failed live insert). |

## Detailed design

### 1. Public API

On `DesignDocument`:

```csharp
public EditResult ApplyText(string newText);          // one undo step; never rejects text
public bool ReloadFromSource();                         // reads through SourcePathResolver; false when unresolvable
public bool IsInSync { get; }                           // the live pages reflect the current text
public IReadOnlyList<Diagnostic> LiveErrors { get; }    // why they don't: loader/mirroring failures, with spans
public event EventHandler? SyncStateChanged;
```

**Behavior:**
- **The text is always stored.** The version bumps, `Changed` fires with one `TextChange` (the span between the common
  prefix and the common suffix of the old and new text), and one undo entry is recorded. Identical text is a no-op:
  no version bump, no event, no undo entry.
- **Malformed text** (syntax diagnostics): no live changes. The text and `Syntax` update. The id map, the live objects
  and the **last valid tree** stay as they were. `IsInSync` becomes `false`.
- **Well-formed text:** the last valid tree is diffed against the new tree (section 2), and the resulting units are
  mirrored (section 3). Units that fail are reported in `LiveErrors`; the others still apply.
- **`IsInSync`** is `true` exactly when the text has no syntax diagnostics, there are no out-of-sync elements, and no
  root-level problem is pending. `SyncStateChanged` fires whenever it, or `LiveErrors`, changes.
- **While out of sync**, `GetNode`/`GetNodeId` answer for the last valid tree, which is the one the live objects belong
  to, and every `MarkupEditor` operation fails with "The markup has errors; fix them first." Undo and redo keep working,
  since they go through text.
- **`ReloadFromSource()`** resolves `SourcePath` through `DesignSession.SourcePathResolver`, reads the file, and calls
  `ApplyText`. It returns `false` without changing anything when the path doesn't resolve. Hosts with a virtual file
  system read their own text and call `ApplyText` directly.
- Pending fast-path values are flushed first, as before every normal edit. `ApplyText` inside an open transaction joins
  it like any other edit.

### 2. The matcher

It runs on the **last valid tree** against the new tree:

1. **Root.** It must keep its element name and its root-only directives (`x:Class`). If either changed, nothing is
   mirrored: `NeedsReload` is set and a `LiveErrors` entry explains why. The text is still stored.
2. **Named anchors.** Old and new elements with the same `x:Name` are paired wherever they are (names are unique per
   document):
   - same element name, same matched parent → a **match**;
   - same element name, different parent → a **move** (same live instance, runtime state kept);
   - different element name → a **replacement** (rebuilt in place, `SubtreeReplaced` fires).
3. **Children of every matched pair, recursively:**
   - Content children (not property elements) are aligned with an **LCS keyed on element name**. An element paired in
     step 2 can only align with its own partner.
   - Old children left unaligned are **removed**; new ones left unaligned are **inserted**. Unnamed elements reordered
     within a parent therefore become remove plus insert.
   - Property elements are matched by name (each appears at most once per parent). Any difference inside them, compared
     as exact text, sets `NeedsReload`, the same opaque rule as 10.1.
4. **Within each matched pair:**
   - Attributes are compared by name and **decoded value**, so a change in formatting, quoting or entity spelling alone
     is no change.
   - Each added, removed or changed attribute becomes one attribute unit.
   - A change of the element's own text content (`<TextBlock>Hi</TextBlock>` → `Bye`) becomes a replacement.
   - Changes to whitespace and comments cause no live work.
   - A changed `xmlns`/prefix declaration on an element turns that element into a replacement, because names are
     compared as written.
5. **Ids.** Every matched or moved new element takes its old element's id, every inserted one gets a fresh id, and
   removed ids vanish. This replaces 10.1's offset mapping for `ApplyText` only; visual edits keep the offset mapping,
   which is exact for them.

The LCS is O(n·m) in each parent's child count; realistic UI fan-out keeps a whole-document diff in the cost range of one
parse.

### 3. Mirroring units, partial failure, undo

**Units**, run in this order so insertions anchor against the final sibling set:

1. removals: the topmost unmatched old elements, `ElementRemovedAction`;
2. moves: `ElementMovedAction`;
3. replacements: a new `ElementReplacedAction`, which rebuilds the element from the new text and keeps its id;
4. insertions: the topmost unmatched new elements, `ElementInsertedAction`;
5. attribute changes: `AttributeChangedAction`.

Before any unit runs, the document switches to the new tree with the matcher's id assignment, so every action sees
`GetOldNode` = the last valid tree and `GetNode` = the new tree, exactly as in 10.1.

**Partial failure.** Each unit runs on its own. A unit that throws is reverted through its own `Revert`, its error
goes into `LiveErrors` with the span of its element or attribute, and the remaining units still run. Units are
independent by construction: an attribute unit only touches its element's copies, and an insertion only its new
subtree.

**Out-of-sync elements.** A failed unit leaves its element's live state behind the text, so the document keeps a set
of **out-of-sync node ids** (from failed attribute changes, replacements and insertions). Each later `ApplyText`
first **reconciles** them:
- if the element still exists in the text, it's rebuilt from its text (or inserted, if it has no live copy);
- if it doesn't, any leftover live copies are removed.

The next diff then starts from a tree the live page actually matches.

**Undo.** `ApplyText` records `UndoItem.ForText(previousText)`. Replaying it calls `ApplyText(previousText)` and
records the opposite text item. Replay is strictly last-in-first-out and restores the exact text before older steps
run, so text items and 10.1 steps mix freely, including inside transactions. A replayed `ApplyText` that applies only
partly is not a replay failure: the text is right, and the live gaps show in `LiveErrors`.

### 4. Edge cases and folded minors

- **The same file loaded twice:** units mirror onto every live copy; a unit fails only for the copies that threw.
- **A load while the document is out of sync:** a fresh load shares the document only if its text matches the
  document's text (unchanged from 10.1).
- **Ids held by tools** survive for every matched element and are lost only for removed elements.
- **Threading:** the owner-dispatcher check, as for every edit.
- **Empty or whitespace-only text:** no root element, so it's malformed and nothing is mirrored.
- **M4, setter exceptions.** Mirroring treats **any non-fatal exception** from a live member (a game setter throwing
  `ArgumentException`, say) as a failure:
  - `ApplyText` reports it in `LiveErrors`;
  - visual edits return a failed `EditResult`, with full rollback, including the fast path's already-applied copies.

  Fatal exceptions (`OutOfMemoryException` and the like) still propagate.
- **M1, load while values are pending.** `AddScope` flushes pending fast-path values first, so a page loaded during a
  drag correlates against the right text.
- **M3, name leak.** When `LiveContent.Insert` throws after a successful build, the names that build registered are
  unregistered.
- **Still deferred:**
  - M2: `NeedsReload` stays sticky;
  - M5: a replaced binding isn't reported (that's in core);
  - M6: `DiagnosticsChanged` compares only diagnostic counts;
  - M7: saving drops a BOM;
  - M8: a drag that starts with a new attribute takes two undo steps.

## Performance

- **No session attached:** zero cost. 10.2 is entirely in `IcyUI.Design`; core is untouched.
- **Per `ApplyText`:**
  - one parse, one diff, and mirroring proportional to what actually changed;
  - identical text short-circuits before parsing;
  - a whitespace-only change produces no units.
- **Budget:** parse + diff **≤ 5 ms for a 100 KB document**. A test measures it and reports the timing (it isn't
  asserted).
- **Memory:**
  - the last valid tree is a separate object only while the text has errors;
  - undo text items hold the previous full text (2 bytes per character per step), which is acceptable for a dev tool.
    Capping history depth is a later concern.

## Testing

All in `IcyUI.Tests/Design/`:

| Area | Tests |
|---|---|
| Matcher | Named moves keep the instance and id; same `x:Name` with a different type is replaced; unnamed reorder becomes remove plus insert; LCS alignment with insertions and removals in the middle; property elements matched by name; formatting/entity/quote-only changes produce no units. |
| Apply | Attribute add/change/remove mirrored; insert/remove/move live; text content change replaces; root type change sets `NeedsReload`; the same file loaded twice updates both copies; identical text is a no-op. |
| Bad text | Malformed text keeps the live pages and ids; `IsInSync` turns false, then true again once fixed. A loader error in one unit still applies the others, reports `LiveErrors` with spans, and heals on the next valid apply. A throwing game setter becomes a `LiveErrors` entry. Visual edits are refused while out of sync. |
| Undo | `ApplyText` is one step; undo/redo across mixed visual and text edits restores the exact text; undo through a malformed state. |
| Folded minors | M4 setter exception during a fast-path drag rolls back every copy; M1 load during pending values; M3 failed live insert leaves no names. |
| Events | Replaying `Changed` reconstructs the text exactly; `SyncStateChanged` fires on each transition. |
| Performance | 100 KB parse + diff measured and reported. |

**Manual check:** `DesignDemo` gets an **"Apply text"** panel: an editable text box pre-filled with the markup, an
Apply button that calls `ApplyText`, and a status line showing `IsInSync` and the first `LiveErrors` entry. If
`TextBox` can't take multi-line input, the panel offers a few preset whole-text edits instead: a rename, a move, a
malformed edit and its fix. Ivan runs it on both engines.

## Cross-engine impact

None. Everything is in `IcyUI.Design`; the hosts only show the updated demo, and `IcyUI.FNA` needs nothing.

## Out of scope

- File watching and other triggers (10.5).
- The in-tool text editor (10.4).
- Incremental parsing.
- Resetting `NeedsReload` (deferred minor M2).
- A cap on undo history depth.
