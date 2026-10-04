# Phase 10.1: markup design document and edit engine

> Design spec, discussed with Ivan on 2026-10-04. It's the first sub-project of Phase 10 (the editor toolkit), the last
> Tier-2 roadmap phase.

## Context

Phase 10 was described only as "`EditorFrame`/`EditorPanel`, the speculative WYSIWYG designer toolkit". It needs to turn
live UI back into markup, and IcyUI has no serializer: `MarkupLoader` parses an `XDocument`, builds the object graph, and
discards the document (only `ControlTemplate`/`DataTemplate` content keeps its `XElement` subtree for deferred
instantiation).

### Goal of the whole toolkit

The toolkit is a **development-time tool for IcyUI users**, shipped as part of a larger SDK:

1. **A designer app** for designing game screens and markup.
2. **Hot reload/edit** during development: the designer and the running game stay in sync with the markup files.
3. **A debug-time in-game utility.** The developer calls it from code on a live page inside the real game scene, edits
   the UI in context, and **saves the edits back to markup after the playtest**. This is like Unity's play mode, but
   the changes survive.

It is not meant for players, though some studios may include it in official modding tools.

The full end state contains the `EditorFrame` overlay, an editor window, an **attachable PropertyGrid**, and an
**attachable markup text editor** in the style of an IDE's XAML editor. The text editor is live-synced in every
direction: visual edits update its buffer, typing in it updates the live page, and external edits to the file update
both.

**Success looks like this:** open a live page, change things, save. The file's diff contains only what was changed.
Bindings, `{StaticResource}` references, styles, comments and formatting stay untouched.

### Why a document model, not a serializer

On a live page a property's local value is just its backing field (`PropertyValuePrecedence` tracks only the Style,
VisualState and Animation tiers separately). A bound `Text`, a value the game set from code and a value written in
markup all look the same. Walking the live tree and writing effective values would bake runtime state such as player
names and scroll offsets into the file. Faithful saving needs to know where each value came from.

### Phase 10 decomposition

Each sub-project gets its own spec → plan → implementation cycle.

| # | Sub-project | Delivers | Depends on |
|---|---|---|---|
| **10.1** | **Markup design document and edit engine** (this spec; headless) | Tracking loads, span-preserving syntax tree, node↔object map, typed edit operations mirrored onto the live tree, undo/redo, lossless save. | — |
| 10.2 | Hot reload | File watching → re-parse → tree diff → edit operations against the live tree; full-reload fallback. The same path later serves the text editor. | 10.1 |
| 10.3 | Editor overlay (`EditorFrame`) | Attaches to any live page: hit-test selection, adorners, move/resize, each gesture an edit operation; captures input over the game while editing. | 10.1 |
| 10.4 | Editor panels | Element tree outline, PropertyGrid bound through the edit engine, toolbox, resource browser, markup text editor. | 10.1, 10.3 |
| 10.5 | Hosts | In-game utility API, standalone designer app, SDK packaging. | all |

10.3 and later are where the cross-engine risk is (overlay input capture over a game scene). 10.1 is pure core and
needs nothing in `IcyUI.MonoGame`/`IcyUI.Stride`; `IcyUI.FNA` needs no equivalent work.

## Decisions

| Decision | Choice |
|---|---|
| Source of truth while editing | **The markup text.** A span-annotated syntax tree is a view of it. A visual edit becomes a minimal text change, so a future text editor's caret and undo survive, and save writes the buffer as is. |
| Where the code lives | A new **`IcyUI.Design`** assembly. Core gets only a minimal observer seam. A release game doesn't reference `IcyUI.Design`. |
| Core API exposure | Public, documented **interfaces** for whatever `IcyUI.Design` needs. The concrete loader classes and incidental features stay `internal`. No `InternalsVisibleTo`. |
| XML parsing in the design layer | Our own span-preserving, error-tolerant tokenizer and tree in `IcyUI.Design`. `XDocument` only records start positions and throws on the first error. The core loader stays on `XDocument`, unchanged. |
| Editable scope in 10.1 | **The element tree and its attributes.** Styles, resources and template content are preserved byte for byte but are opaque. The map is node → *set* of objects from day one, so editing resources/styles and templates are additive later phases. |
| Edits that can't be mirrored in place | **Rebuild the nearest mapped ancestor subtree** and raise `SubtreeReplaced`. If that would reach the root, mark the document `NeedsReload`. The page object is never replaced automatically. |
| Runtime content | Live elements the game added in code have no node. They are read-only "runtime content": inspectable, never serialized. |
| Tracking opt-in | Must be on **before** a page loads. Pages loaded earlier are untracked. |
| Performance | Zero extra allocations when off; under ~1 MB per tracked 50 KB page when on. See [Performance](#performance). |

## Detailed design

### 1. Architecture

```
┌────────────── IcyUI (core) ──────────────┐     ┌──────────────── IcyUI.Design (new) ─────────────────┐
│ MarkupLoader  ──notifies──► IMarkupLoad- │     │ MarkupSyntax: tokenizer + span tree (error-tolerant)│
│   (XDocument, unchanged)    Observer?    │◄────│ DesignDocument: text buffer + syntax tree +         │
│                                          │     │                 node↔object map                     │
│ IMarkupBuilder (public seam, internal    │◄────│ MarkupEditor: typed edit ops → TextChangeSet        │
│   impl): build subtree / apply member    │     │               + live mirroring                      │
│   value in a captured context            │     │ UndoStack (of inverse operations)                   │
└──────────────────────────────────────────┘     │ DesignSession: tracking install, documents by path, │
                                                 │                events                               │
                                                 └─────────────────────────────────────────────────────┘
```

### 2. Core seam (`Icy.Markup`)

All new types have complete XML documentation. `MarkupLoader`'s internals stay `internal`.

**`IMarkupLoadObserver`**, installed through a new `MarkupConfiguration.LoadObserver` property (default `null`). It
follows the existing replaceable-strategy pattern of `MarkupConfiguration.Activator`.

```csharp
public interface IMarkupLoadObserver
{
    MarkupLoadScopeKind ObservedKinds { get; }

    void DocumentStarted(MarkupLoadScope scope);
    void ObjectCreated(MarkupLoadScope scope, XElement node, object instance);
    void MemberApplied(MarkupLoadScope scope, XObject node, object target, MarkupMember member, object? value);
    void DocumentCompleted(MarkupLoadScope scope, object root);
    void DocumentFailed(MarkupLoadScope scope, MarkupException error);
}
```

- `MemberApplied` fires for attributes and property elements. `value` is the converted literal or the markup
  extension's result; a `{Binding}` reports its `IBinding`, so a later edit can `IBindingTarget.Unbind` it.
- `DocumentStarted`/`DocumentCompleted`/`DocumentFailed` bracket every `Load*`/`LoadObject` call, every merged
  dictionary load, and every template content instantiation.
- `MarkupLoadScopeKind` is a flags enum: `Document`, `MergedDictionary`, `TemplateContent`, `DataTemplateContent`.
  The loader creates no scope and sends no notifications for kinds outside `ObservedKinds`.

**`MarkupLoadScope`**: a public sealed class with an internal constructor, an opaque handle to one load.

- Exposes `SourcePath`, `NameScope`, `Kind` and `SourceText`.
- `SourceText` is populated only when an observer is set. The loader then reads the reader/stream into a string first
  and parses that string, so the design layer edits exactly the text the live tree was built from, with no race against
  an external save.
- Internally it holds whatever the loader needs to work in that document's context later.

**`IMarkupBuilder`**, implemented internally by `MarkupLoader` and obtained through a public accessor:

```csharp
public interface IMarkupBuilder
{
    object BuildFragment(MarkupLoadScope scope, XElement fragment, UIElement? liveParent);
    object? ApplyAttribute(MarkupLoadScope scope, object target, XAttribute attribute);
}
```

- `BuildFragment` builds an element subtree in the document's context. `StaticResourceExtension` resolves keys by
  walking `MarkupExtensionContext.ElementStack`, which exists only during a load, so the builder **rebuilds that stack
  from the live parent chain** (root → `liveParent`). Inserted fragments resolve `{StaticResource}` exactly as at load
  time, with no change to the extension.
- `ApplyAttribute` applies one attribute's text to a live object exactly as the loader would: type conversion, markup
  extensions, attached properties and `x:` directives.
- The observer fires for builder calls too, so inserted elements enter the map through the same path as the initial
  load.

**`MarkupNameScope.Unregister(string name)`**: new public, documented method, needed to rename and remove named
elements.

### 3. Syntax layer, document and map (`Icy.Design`)

**Text model.** `MarkupText` is an immutable string plus a `Version`. It changes through a `TextChangeSet`: sorted,
non-overlapping `TextChange(start, length, newText)` entries, each set able to produce its own inverse.

**Syntax tree.**
- Nodes: `DocumentSyntax`, `ElementSyntax`, `AttributeSyntax`, `TextSyntax`, `CommentSyntax`, `CDataSyntax`,
  `ProcessingInstructionSyntax`.
- Nodes are immutable and carry absolute spans:
  - an element has its full span, name span, the end of its start tag, a self-closing flag and an optional end-tag span;
  - an attribute has its full span, name span, value span (without quotes) and quote char.
- Error recovery produces nodes flagged `IsMissing` and a list of `Diagnostic(span, message)`. A malformed buffer never
  throws.
- Prefixes are not resolved by the tree; resolution uses the same rules as the loader, including the namespaces
  `MarkupLoader.ParseDocument` predeclares.
- Writing a value escapes it for the attribute's existing quote char (`&quot;`/`&apos;`, `&lt;`, `&amp;`).

**Re-sync.** Every buffer change triggers a **full re-parse**, followed by a **tree matcher**:
1. Matched parents are compared child by child.
2. Elements are matched on name, using an LCS over sibling sequences.

Matched nodes keep their stable `NodeId`. For visual edits the matcher's job is trivial; it's the same matcher 10.2
needs for arbitrary text edits. Incremental parsing is deferred until the text editor shows a need, and can be added
behind `DocumentSyntax.Parse(MarkupText, previousTree?)` without changing the map or editor APIs.

**`DesignDocument`.** One per markup file. It owns the text buffer, the current tree, the last valid tree and the map:
- `NodeId` → set of `WeakReference<object>`, so the map never keeps alive elements the game removed;
- `object` → `NodeId` through a `ConditionalWeakTable`;
- `(NodeId, attribute name)` → `AppliedMember { MarkupMember, IBinding? }`.

**Loader ↔ syntax correlation.** A loader `XElement`/`XAttribute` is matched to its syntax node by `IXmlLineInfo`
(line, column). The implementation must pin down where `XmlReader` places the column and how it counts `\r\n`; tests
cover LF, CRLF, tabs and multi-line attributes.

### 4. Edit operations, mirroring and undo

Operations live on `MarkupEditor`. Each returns an `EditResult`: success, or failure with a `Diagnostic`.

| Operation | Text change | Live mirroring |
|---|---|---|
| `SetAttribute(node, name, valueText)`, including attached properties (`Grid.Row`) and `x:Name` | Replace the value span. A new attribute is appended in the sibling attributes' style: on the same line, or on its own line with matching indentation when they are one per line; same quote char. | Unbind the recorded `IBinding`, if any → `IMarkupBuilder.ApplyAttribute`. `x:Name` renames in the `MarkupNameScope` in place; the instance stays the same, so game references stay valid. |
| `ClearAttribute(node, name)` | Remove the attribute and its leading whitespace. | Unbind → a registered property gets `ClearLocalValue` (falls back to style/default); a plain CLR property has no "unset", so → subtree rebuild. |
| `InsertElement(parent, index, fragmentText)` | Insert with the siblings' indentation. A self-closing parent is expanded to start and end tags. | `BuildFragment` → insert into the parent's content. The live index is placed **after the nearest mapped previous sibling**, so runtime content doesn't shift positions. |
| `RemoveElement(node)` | Remove it, plus its whole line(s) when it occupies them. | Detach the live object(s); unregister their names. |
| `MoveElement(node, newParent, index)` | Remove + insert in one change set. | Re-parent the **same instance**, preserving runtime state. Incompatible content kinds → rebuild. |

**Subtree rebuild.**
- Triggers: a constructor-consumed attribute, an element type change, a content-text change, a plain-CLR clear.
- Action: rebuild the nearest mapped `UIElement` ancestor in place and raise `SubtreeReplaced(old, new)`. The old
  subtree's map entries and names are dropped, and the new subtree's are recorded.
- If the rebuild would reach the root (a root type change, the root's constructor arguments), set `NeedsReload`
  instead.

**Opaque regions.** In 10.1, operations targeting nodes inside styles, resources or template content change the text
and set `NeedsReload`. The editor UI of later phases won't offer them until those regions become editable.

**Atomicity.** A visual operation is all-or-nothing. The text changes, then the live tree is mirrored. If mirroring
throws (e.g. `Width="abc"`), both are rolled back and the operation returns a failure. The text is never out of sync
with a successful edit.

**Transactions and coalescing.** `using (editor.BeginTransaction("Resize"))` groups operations into one undo entry.
Successive operations on the same member within a short window coalesce, so a drag is one undo step.

**Undo/redo.** An undo entry stores the **inverse operations**, which mirror directly onto the live tree, plus the
text change set they are expected to produce; tests assert the two match. Undoing a raw text change would need the
general text-diff → live path, which is 10.2's job. When the text editor arrives, plain text edits enter the same
stack through that path, so there is one shared stack for visual and text editing.

**Threading.** Operations must run on the UI thread that owns the page's `Canvas`. Any other thread gets an
`InvalidOperationException`; there is no hidden marshaling.

**Events on `DesignDocument`:** `Changed(TextChangeSet, version)`, `SubtreeReplaced`, `NeedsReloadChanged`,
`DiagnosticsChanged`.

### 5. Session and opt-in API

```csharp
#if DEBUG   // or the developer's own dev-tools symbol
using DesignSession session = DesignSession.Attach(configuration);
#endif
...
DesignDocument? document = session.FindDocument(element, out NodeId node);
```

- `Attach` installs the observer (`ObservedKinds = Document | MergedDictionary` in 10.1). `LoadObserver` is a single
  slot; `Attach` throws if it's already occupied.
- `Dispose` uninstalls the observer and releases every document.
- The session tracks documents by source path, since merged dictionaries and `Frame` navigation load other files.
- Documents whose mapped objects have all been collected are pruned on the next session access.
- `FindDocument` returns `null` for untracked elements: pages loaded before `Attach`, and runtime content.
- `DesignDocument.Save()` writes the buffer to `SourcePath`; `Text` exposes it for other targets.

## Performance

**Off** (a release game; `IcyUI.Design` is not referenced):
- one `LoadObserver is null` branch per element and per attribute, including template instantiation in pooled
  ItemsControl containers;
- no `SourceText` copy, no `MarkupLoadScope`: **zero extra allocations**, asserted by a test.

**On** (estimates, to be measured during implementation, for a ~50 KB page with about 1,000 elements and 4,000
attributes):

| Item | Estimate |
|---|---|
| `SourceText` copy | ~100 KB (UTF-16) |
| Syntax tree | ~150–250 B per element, ~80 B per attribute → ~0.5 MB |
| Map | one weak reference + table entry per element, ~48 B per attribute → ~0.25 MB |
| **Total per tracked page** | **under ~1 MB** |
| Parse + match per edit | budget **≤ 5 ms for 100 KB**, checked on x64 and ARM64 |

**Hot-path rules:**
- `ObservedKinds` keeps pooled-container recycling free of scope allocations and notifications, even with tracking on.
- **Coalesced drags defer the re-parse.** Inside a coalescing transaction, each frame only updates the one attribute's
  value text and mirrors it with `ApplyAttribute`. The re-parse + match runs once, at commit or when a different node
  is targeted.
- No LINQ or closures in the tokenizer. Tokens are offsets into the buffer; strings are created only for names and
  values the tree exposes.

## Edge cases

- **A failed initial load.** If the initial load throws, the observer gets `DocumentFailed` and no document is
  tracked.
- **A malformed buffer.** The live tree keeps the last valid tree's state; diagnostics describe the errors. (In 10.1
  only visual operations change the buffer, and they always produce valid text, so this matters from 10.2 on.)
- **Runtime content between mapped siblings.** Inserts anchor on the nearest mapped previous sibling; if there is none,
  the element goes first among the mapped content, after any leading runtime content.
- **One node, many objects.** Only template content produces this in practice, and it's opaque in 10.1. Operations on a
  node with several live objects mirror onto each one.
- **The same file loaded twice** (two pages from one markup file). Both live trees map to one `DesignDocument`; edits
  mirror onto both.
- **`x:Name` collisions.** Renaming to a name already in the scope fails the operation with a diagnostic.

## Testing

All tests in `IcyUI.Tests`, under `Design/`, except the zero-allocation test, which stays in core `Markup/`.

| Area | Tests |
|---|---|
| Syntax | **Round-trip:** spans rebuild the text exactly for every `Shared Samples` markup document and `DefaultTheme.xml`. A table of malformed inputs never throws; diagnostic spans are correct. |
| Correlation | `XElement`/`XAttribute` ↔ syntax node matching with LF, CRLF, tabs and multi-line attributes. |
| Seam | Notification order; `SourceText` equals the input; `ObservedKinds` filtering; **null observer → zero extra bytes allocated** (`GC.GetAllocatedBytesForCurrentThread`). |
| Builder | `BuildFragment` resolves `{StaticResource}` from live ancestors and `{Binding}` from the live DataContext; `ApplyAttribute` matches a fresh load. |
| Operations | Exact expected text after each operation (indentation, quote style, self-closing expansion); live mirroring; rollback when mirroring fails; runtime-content anchoring; `SubtreeReplaced`; `NeedsReload`; `x:Name` rename and collision. |
| Undo | Inverse operations produce exactly the inverse text change sets; coalescing; transactions. |
| Matcher | `NodeId`s stay stable across edits. |
| Pooling | A tracked page with an ItemsControl: scrolling and recycling produce no notifications and no map growth (behavioral). |
| Lifetime | Elements the game removed are collectable (`WeakReference` + `GC.Collect`); `Dispose` uninstalls the observer. |
| Performance | Parse + match time for a generated 100 KB document is **measured and reported, not asserted**. Allocation assertions are asserted. |

**Manual check.** A `DesignDemo` in `Shared Samples`, registered in both hosts' `SampleGame.cs`: it loads a page with
tracking on and offers *Bigger title*, *Insert button*, *Move*, *Undo* and *Redo* buttons, plus a read-only `TextBox`
showing the live markup text. Ivan runs it on both engines.

## Project setup

- New `sources/IcyUI.Design/IcyUI.Design.csproj`: `RootNamespace` `Icy.Design`, references only `IcyUI`. Wired like
  `IcyUI.csproj`: `GenerateDocumentationFile`, `EnforceCodeStyleInBuild`, StyleCop, packable with `LICENSE`. No new
  packages.
- Added to `IcyUI.sln`. `IcyUI.Tests` and both sample hosts reference it.
- `CLAUDE.md` layout table gets a row for `IcyUI.Design`: dev-time tooling, never required by games at runtime.
- No engine references, so x64 and ARM64 behave the same.

## Out of scope (later phases)

- Editing styles, resources and template content (the map already supports it).
- Hot reload, file watching, and text diff → live operations (10.2).
- `EditorFrame`, panels, the markup text editor, and an attachable PropertyGrid bound through the edit engine
  (10.3, 10.4).
- Host APIs, the designer app, SDK packaging, an optional host page-reload callback (10.5).
- Incremental parsing.
- Chaining several load observers.

## Critical files

- `sources/IcyUI/Markup/MarkupLoader.cs`: observer notifications, `SourceText` capture, `IMarkupBuilder`
  implementation, element-stack reconstruction.
- `sources/IcyUI/Markup/MarkupConfiguration.cs`: `LoadObserver`.
- `sources/IcyUI/Markup/MarkupNameScope.cs`: `Unregister`.
- New in `sources/IcyUI/Markup/`: `IMarkupLoadObserver.cs`, `MarkupLoadScope.cs`, `MarkupLoadScopeKind.cs`,
  `IMarkupBuilder.cs`.
- New project `sources/IcyUI.Design/`: `Syntax/*`, `MarkupText.cs`, `TextChangeSet.cs`, `DesignDocument.cs`,
  `MarkupEditor.cs`, `UndoStack.cs`, `DesignSession.cs`, `TreeMatcher.cs`.
- `sources/Shared Samples/DesignDemo.cs` and both hosts' `SampleGame.cs`.
