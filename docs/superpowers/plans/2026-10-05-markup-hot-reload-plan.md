# Phase 10.2: Applying Markup Text to Live Pages Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `DesignDocument.ApplyText(newText)` stores any new markup text as one undoable step and mirrors it onto the live pages through a structural tree diff, keeping every matched element's id and live instance, and never losing the text when the live pages can't follow.

**Architecture:** A pure `TreeMatcher` diffs the last valid syntax tree against the new one: `x:Name` anchors first, then a name-keyed LCS per matched parent. `DesignDocument` turns the diff into 10.1's mirror actions (plus a new `ElementReplacedAction`) and runs each as an independent unit. A failing unit is reverted on the old tree, reported in `LiveErrors`, and remembered as out of sync, so the next valid apply rebuilds it.

**Tech Stack:** C# / .NET 10, xUnit v2, existing `IcyUI.Design` (Phase 10.1).

**Spec:** `docs/superpowers/specs/2026-10-05-markup-hot-reload-design.md` (builds on `docs/superpowers/specs/2026-10-04-markup-design-document-design.md`)

## Global Constraints

- Everything is in `IcyUI.Design` and `IcyUI.Tests`, plus `Shared Samples/DesignDemo.cs`. **Core `IcyUI` is not touched.**
- `IcyUI.Design` has `stylecop.json` with `documentInternalElements: false`. Every **public** member needs complete XML docs (`<summary>`, `<param>`, `<returns>`, `<exception>`, `<see cref>`).
- Match the surrounding style: copyright header, block-scoped namespaces, StyleCop member order.
- **Warnings:** the baseline is **81**, measured with a full rebuild: `dotnet build "sources/IcyUI.sln" --no-incremental` (incremental builds skip projects and under-report). It must not grow.
- **Tests:** the baseline is **1250**, all passing. `dotnet test` prints "Passed!" even if the host crashes; always read the Total.
- Test strings that span lines use `.ReplaceLineEndings("\n")`.
- Commit after every task on `platform-independent`, ending the message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Live mirroring always uses the mapped **logical** parent, never `UIElement.Parent`.

## Review Focus

1. **A save that changes several things at once, one of which fails** (a bad value next to a good insertion). Expect every good part to show and only the bad element to stay behind, until a later valid save heals it. Test: Task 4, `ALoaderErrorInOneUnit_StillAppliesTheOthers` and `AFailedUnit_HealsOnTheNextValidApply`.
2. **Named elements moved out of a container that the same save deletes.** Expect the moved instance to survive and its `x:Name` to still resolve. Test: Task 4, `NamesFollowMovesOutOfRemovedContainers`.
3. **Undo across a mix of visual edits and text applies, including a malformed intermediate text.** Expect the exact original text and a page back in sync. Test: Task 3, `UndoAcrossVisualAndTextEdits_RestoresTheExactText`; Task 4, `UndoThroughAMalformedState`.
4. **A game setter that throws a non-markup exception** (MarkupDemo's `Quantity` setter does). Expect a reported failure, never a crash, on both the visual fast path and `ApplyText`. Test: Task 1, `SetterException_OnTheFastPath_RollsBackEveryCopy`; Task 4, `AThrowingSetter_BecomesALiveError`.
5. **Formatting-only saves** (reindenting, re-quoting, entity spelling). Expect no live work at all, so runtime state is untouched. Test: Task 2, `FormattingQuotesAndEntitiesOnly_AreNoChange`; Task 3, `AFormattingOnlySave_KeepsEveryInstance`.

## Deviations from the spec (decided while planning)

1. The demo uses **preset whole-text edits** instead of a text box: `TextBox` has no multi-line mode. The spec allowed this fallback.
2. A unit that fails is reverted **on the old tree**. The document swaps its state back to the last valid tree for that unit's `Revert`, then swaps forward again, so 10.1's actions are reused unchanged.
3. A **failed removal** (practically impossible, since `LiveContent.Remove` doesn't throw) is reported in `LiveErrors` but isn't tracked as out of sync: no old node remains to rebuild it from.
4. After every `ApplyText`, a **name pass** re-registers the `x:Name` of every live element that has one. This keeps names right for elements moved out of removed containers (Review Focus 2).

---

### Task 1: Folded 10.1 minors: setter exceptions (M4), loads during pending values (M1), names leaked by a failed insert (M3)

**Files:**
- Create: `sources/IcyUI.Design/Editing/Failures.cs`
- Modify: `sources/IcyUI.Design/DesignDocument.cs` (`Apply`'s catch, `TryFastSetAttribute`'s catch, `AddScope`)
- Modify: `sources/IcyUI.Design/Editing/ElementInsertedAction.cs` (`Execute`)
- Modify: `sources/IcyUI.Tests/Design/DesignTestTypes.cs`, `sources/IcyUI.Tests/Design/DesignTestHost.cs` (two test types)
- Test: `sources/IcyUI.Tests/Design/FoldedMinorTests.cs`

**Interfaces:**
- Produces: `internal static class Failures` with `bool IsFatal(Exception)` and `string Describe(Exception)`. Task 4 uses both.
- Produces (tests): `ThrowingBox` (plain CLR `int Count`, throws `ArgumentOutOfRangeException` for negatives) and `PickyPanel` (a `StackPanel` that throws `InvalidOperationException` when a `TextBlock` is added). Both are registered in `DesignTestHost`.

- [ ] **Step 1: Write the test types and the failing tests**

Append to `sources/IcyUI.Tests/Design/DesignTestTypes.cs`, inside the namespace:

```csharp
    /// <summary>
    /// An element whose plain CLR setter throws a non-markup exception for bad values, like a game's own validation.
    /// </summary>
    internal sealed class ThrowingBox : UIElement
    {
        private int count;

        public int Count
        {
            get => count;
            set
            {
                ArgumentOutOfRangeException.ThrowIfNegative(value);
                count = value;
            }
        }

        protected override Size MeasureContent() => Size.Empty;

        protected override void ArrangeContent()
        {
        }
    }

    /// <summary>
    /// A panel that refuses text blocks, so adding one fails after the child was already built.
    /// </summary>
    internal sealed class PickyPanel : Icy.UI.Controls.StackPanel
    {
        protected override void OnChildAdding(object? sender, Icy.Data.CancellableEventArgs<UIElement> e)
        {
            if (e.Data is Icy.UI.Controls.TextBlock)
                throw new InvalidOperationException("No text blocks here.");
            base.OnChildAdding(sender, e);
        }
    }
```

In `DesignTestHost`'s constructor, next to the other registrations:

```csharp
            Configuration.Types.Markup.RegisterShortName<ThrowingBox>();
            Configuration.Types.Markup.RegisterShortName<PickyPanel>();
```

`sources/IcyUI.Tests/Design/FoldedMinorTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    /// <summary>
    /// 10.1 minors folded into 10.2 because "the text always wins" depends on them.
    /// </summary>
    public class FoldedMinorTests
    {
        // M4: a game setter throwing a non-markup exception must be a failed edit, not a crash, and the fast path must
        // put back every copy it already changed.
        [Fact]
        public void SetterException_OnTheFastPath_RollsBackEveryCopy()
        {
            using var host = new DesignTestHost();
            string page = "<StackPanel x:Name=\"root\"><ThrowingBox x:Name=\"t\" Count=\"5\"/></StackPanel>";
            (UIElement first, DesignDocument document) = host.Load(page, "same.xml");
            (UIElement second, _) = host.Load(page, "same.xml");

            EditResult result = document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<ThrowingBox>(first, "t")), "Count", "-1");

            Assert.False(result.Succeeded);
            Assert.Equal(5, DesignTestHost.Named<ThrowingBox>(first, "t").Count);
            Assert.Equal(5, DesignTestHost.Named<ThrowingBox>(second, "t").Count);
            Assert.Equal(page, document.Text);
        }

        [Fact]
        public void SetterException_OnTheNormalPath_IsAFailedEdit()
        {
            using var host = new DesignTestHost();
            string page = "<StackPanel x:Name=\"root\"><ThrowingBox x:Name=\"t\" Count=\"5\"/></StackPanel>";
            (UIElement root, DesignDocument document) = host.Load(page);

            // "{}" escapes the value, which keeps the edit off the fast path.
            EditResult result = document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<ThrowingBox>(root, "t")), "Count", "{}-1");

            Assert.False(result.Succeeded);
            Assert.Equal(5, DesignTestHost.Named<ThrowingBox>(root, "t").Count);
            Assert.Equal(page, document.Text);
        }

        // M1: a page loaded while fast-path values are pending must correlate against the current text.
        [Fact]
        public void ALoadWhileValuesArePending_RecordsTheLaterAttributes()
        {
            using var host = new DesignTestHost();
            (UIElement first, DesignDocument document) = host.Load("<StackPanel><Border x:Name=\"b\" Width=\"10\" Height=\"5\"/></StackPanel>", "same.xml");
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(first, "b")), "Width", "12345");

            (UIElement second, DesignDocument again) = host.Load(document.Text, "same.xml");

            Assert.Same(document, again);
            Assert.True(document.Map.TryGetEntry(DesignTestHost.Named<Border>(second, "b"), out var entry));
            Assert.True(entry!.Members.ContainsKey("Height"));
        }

        // M3: when adding a built element to its live parent fails, the names it registered must not stay behind.
        [Fact]
        public void AFailedLiveInsert_LeavesNoNamesBehind()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<PickyPanel x:Name=\"root\"/>");

            EditResult result = document.Editor.InsertElement(host.IdOf(root), 0, "<TextBlock x:Name=\"orphan\"/>");

            Assert.False(result.Succeeded);
            Assert.Null(MarkupNameScope.GetScope(root)!.Find("orphan"));
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~FoldedMinorTests"`
Expected: FAIL, 4 of 4.
- the two setter tests and the insert test throw (`TargetInvocationException` / `InvalidOperationException`) out of the editor;
- the pending-values test fails its `ContainsKey("Height")` assertion.

- [ ] **Step 3: Implement**

`sources/IcyUI.Design/Editing/Failures.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Decides which exceptions from live mirroring are an edit's failure rather than the process's.
    /// </summary>
    internal static class Failures
    {
        /// <summary>
        /// Gets whether <paramref name="exception"/> means the process can't safely continue, so it must propagate.
        /// Anything else thrown while mirroring (a game setter's validation, say) is the edit's failure.
        /// </summary>
        public static bool IsFatal(Exception exception) =>
            exception is OutOfMemoryException or InsufficientExecutionStackException or AccessViolationException;

        /// <summary>
        /// Gets the message to report for a failure, looking through the reflection wrapper a plain CLR setter's
        /// exception arrives in.
        /// </summary>
        public static string Describe(Exception exception) =>
            (exception is TargetInvocationException { InnerException: { } inner } ? inner : exception).Message;
    }
}
```

In `DesignDocument.Apply`, replace

```csharp
                if (ex is MarkupException or DesignEditException)
                    return EditResult.Failure(step.Change.Count > 0 ? new TextSpan(step.Change[0].Span.Start, 0) : default, ex.Message);
                throw;
```

with

```csharp
                if (!Failures.IsFatal(ex))
                    return EditResult.Failure(step.Change.Count > 0 ? new TextSpan(step.Change[0].Span.Start, 0) : default, Failures.Describe(ex));
                throw;
```

In `DesignDocument.TryFastSetAttribute`, replace `catch (MarkupException ex)` with `catch (Exception ex) when (!Failures.IsFatal(ex))`, and `EditResult.Failure(attribute.ValueSpan, ex.Message)` with `EditResult.Failure(attribute.ValueSpan, Failures.Describe(ex))`.

Replace `AddScope` with:

```csharp
        internal void AddScope(MarkupLoadScope scope)
        {
            // A page loaded from the current text must correlate against it, not against the last parsed text.
            FlushPending();
            scopes.Add(scope);
        }
```

In `ElementInsertedAction.Execute`, replace

```csharp
                UIElement built = document.BuildElement(scope, element, (UIElement)liveParent);
                LiveContent.Insert(liveParent, document.ComputeLiveIndex(element, liveParent, scope), built, document.Registry);
                inserted.Add((liveParent, built, scope));
```

with

```csharp
                UIElement built = document.BuildElement(scope, element, (UIElement)liveParent);
                try
                {
                    LiveContent.Insert(liveParent, document.ComputeLiveIndex(element, liveParent, scope), built, document.Registry);
                }
                catch
                {
                    // Built but never added: Revert won't see it, so take its names and map entries back here.
                    LiveTree.UnregisterNames(scope, built);
                    document.Map.RemoveSubtree(built);
                    throw;
                }

                inserted.Add((liveParent, built, scope));
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~FoldedMinorTests"`
Expected: PASS, Total 4.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1254, no failures), then `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep "Warning(s)"` (81).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design
git commit -m "Treat setter exceptions as failed edits, and fix two mirroring leaks

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `TreeMatcher`, the structural diff

**Files:**
- Create: `sources/IcyUI.Design/Editing/TreeDiff.cs`, `sources/IcyUI.Design/Editing/TreeMatcher.cs`
- Test: `sources/IcyUI.Tests/Design/TreeMatcherTests.cs`

**Interfaces:**
- Consumes: `DocumentSyntax`, `ElementSyntax`, `AttributeSyntax` (10.1), `MarkupNamespaces.Directives`, `MarkupDirectives.Name`.
- Produces (`Icy.Design.Editing`, internal):
  - `sealed class TreeDiff` with `string? RootProblem`, `bool OpaqueChanged`, `Dictionary<ElementSyntax, ElementSyntax> Pairs` (new → old; matched, moved or replaced, plus structure inside property elements), `List<ElementSyntax> Removed` (old, topmost), `List<(ElementSyntax Old, ElementSyntax New)> Moved`, `List<(ElementSyntax Old, ElementSyntax New)> Replaced`, `List<ElementSyntax> Inserted` (new, topmost), `List<(ElementSyntax Element, string Name)> ChangedAttributes` (new element), `bool IsEmpty`.
  - `sealed class TreeMatcher` with `static TreeDiff Match(DocumentSyntax oldDocument, DocumentSyntax newDocument)` and `static string? GetDirective(DocumentSyntax document, ElementSyntax element, string localName)`.

The matcher is pure: it reads two trees and knows nothing about ids or live objects. Task 3 turns its output into ids and mirror actions.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/TreeMatcherTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editing;
using Icy.Design.Syntax;
using Xunit;

namespace Icy.Tests.Design
{
    public class TreeMatcherTests
    {
        [Fact]
        public void IdenticalTrees_AreNoChange()
        {
            const string page = "<StackPanel><Border x:Name=\"b\" Width=\"10\"/><TextBlock>Hi</TextBlock></StackPanel>";

            TreeDiff diff = Diff(page, page);

            Assert.True(diff.IsEmpty);
            Assert.Equal(3, diff.Pairs.Count);
        }

        [Fact]
        public void FormattingQuotesAndEntitiesOnly_AreNoChange()
        {
            TreeDiff diff = Diff(
                "<StackPanel><TextBlock Text=\"a &amp; b\" Width=\"10\"/></StackPanel>",
                "<StackPanel>\n  <TextBlock\n    Width='10'\n    Text='a &#38; b' />\n</StackPanel>");

            Assert.True(diff.IsEmpty);
        }

        [Fact]
        public void AttributeChanges_AreListedPerAttribute()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border Width=\"10\" Margin=\"1\"/></StackPanel>",
                "<StackPanel><Border Width=\"20\" Height=\"5\"/></StackPanel>");

            Assert.Equal(["Height", "Margin", "Width"], diff.ChangedAttributes.Select(x => x.Name).Order());
            Assert.All(diff.ChangedAttributes, x => Assert.Equal("Border", x.Element.Name));
        }

        [Fact]
        public void AChangeInTheMiddle_IsOneRemovalAndOneInsertion()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border/><Button/><TextBlock/></StackPanel>",
                "<StackPanel><Border/><CheckBox/><TextBlock/></StackPanel>");

            Assert.Equal("Button", Assert.Single(diff.Removed).Name);
            Assert.Equal("CheckBox", Assert.Single(diff.Inserted).Name);
            Assert.Equal(3, diff.Pairs.Count);
        }

        [Fact]
        public void ANamedElementInAnotherParent_IsAMove()
        {
            TreeDiff diff = Diff(
                "<StackPanel><StackPanel x:Name=\"l\"><Border x:Name=\"box\"/></StackPanel><StackPanel x:Name=\"r\"/></StackPanel>",
                "<StackPanel><StackPanel x:Name=\"l\"/><StackPanel x:Name=\"r\"><Border x:Name=\"box\"/></StackPanel></StackPanel>");

            (ElementSyntax old, ElementSyntax moved) = Assert.Single(diff.Moved);
            Assert.Equal("Border", old.Name);
            Assert.Equal("r", NameOf(moved.Parent!));
            Assert.Empty(diff.Removed);
            Assert.Empty(diff.Inserted);
        }

        [Fact]
        public void ANamedElementThatChangedType_IsReplacedInPlace()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border x:Name=\"b\"/></StackPanel>",
                "<StackPanel><Button x:Name=\"b\"/></StackPanel>");

            Assert.Equal("Button", Assert.Single(diff.Replaced).New.Name);
            Assert.Empty(diff.Removed);
            Assert.Empty(diff.Inserted);
        }

        [Fact]
        public void UnnamedElementsReordered_AreARemovalAndAnInsertion()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border/><Button/></StackPanel>",
                "<StackPanel><Button/><Border/></StackPanel>");

            Assert.Single(diff.Removed);
            Assert.Single(diff.Inserted);
            Assert.Empty(diff.Moved);
        }

        [Fact]
        public void NamedElementsReordered_AreAMoveWithinTheParent()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border x:Name=\"a\"/><Button x:Name=\"b\"/></StackPanel>",
                "<StackPanel><Button x:Name=\"b\"/><Border x:Name=\"a\"/></StackPanel>");

            Assert.Single(diff.Moved);
            Assert.Empty(diff.Removed);
            Assert.Empty(diff.Inserted);
        }

        [Fact]
        public void ChangedElementText_IsAReplacement()
        {
            TreeDiff diff = Diff(
                "<StackPanel><TextBlock>Hi</TextBlock></StackPanel>",
                "<StackPanel><TextBlock>Bye</TextBlock></StackPanel>");

            Assert.Equal("TextBlock", Assert.Single(diff.Replaced).New.Name);
        }

        [Fact]
        public void AChangeInsideAPropertyElement_IsOpaque()
        {
            TreeDiff diff = Diff(
                "<Grid><Grid.RowDefinitions><RowDefinition Height=\"Auto\"/></Grid.RowDefinitions></Grid>",
                "<Grid><Grid.RowDefinitions><RowDefinition Height=\"10\"/></Grid.RowDefinitions></Grid>");

            Assert.True(diff.OpaqueChanged);
            Assert.Empty(diff.ChangedAttributes);
            Assert.Equal(3, diff.Pairs.Count);
        }

        [Fact]
        public void ARootThatChangedType_IsARootProblem()
        {
            TreeDiff diff = Diff("<StackPanel/>", "<Border/>");

            Assert.NotNull(diff.RootProblem);
            Assert.Empty(diff.Pairs);
        }

        [Fact]
        public void AChangedNamespaceDeclaration_IsAReplacement()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border/></StackPanel>",
                "<StackPanel><Border xmlns:z=\"u\"/></StackPanel>");

            Assert.Equal("Border", Assert.Single(diff.Replaced).New.Name);
        }

        [Fact]
        public void ANamedElementMovedIntoANewContainer_IsRemovedAndComesWithTheInsertion()
        {
            TreeDiff diff = Diff(
                "<StackPanel><Border x:Name=\"box\"/></StackPanel>",
                "<StackPanel><StackPanel><Border x:Name=\"box\"/></StackPanel></StackPanel>");

            Assert.Equal("StackPanel", Assert.Single(diff.Inserted).Name);
            Assert.Equal("Border", Assert.Single(diff.Removed).Name);
            Assert.Empty(diff.Moved);
        }

        private static TreeDiff Diff(string before, string after) =>
            TreeMatcher.Match(DocumentSyntax.Parse(before), DocumentSyntax.Parse(after));

        private static string? NameOf(ElementSyntax element) => element.FindAttribute("x:Name")?.Value;
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeMatcherTests"`
Expected: the build FAILS (`TreeMatcher`, `TreeDiff` not found).

- [ ] **Step 3: Implement**

`sources/IcyUI.Design/Editing/TreeDiff.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;

namespace Icy.Design.Editing
{
    /// <summary>
    /// The structural difference between two markup trees, element by element.
    /// </summary>
    internal sealed class TreeDiff
    {
        /// <summary>
        /// Gets or sets why the root can't be followed in place (its type or <c>x:Class</c> changed), or
        /// <see langword="null"/>. When set, nothing else is filled in.
        /// </summary>
        public string? RootProblem { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether anything under a property element changed. Those parts are opaque:
        /// they can only be shown by reloading the page.
        /// </summary>
        public bool OpaqueChanged { get; set; }

        /// <summary>
        /// Gets every new element that corresponds to an old one (matched, moved, replaced, or paired structurally
        /// inside a property element), mapped to that old element. New elements missing here are new.
        /// </summary>
        public Dictionary<ElementSyntax, ElementSyntax> Pairs { get; } = [];

        /// <summary>
        /// Gets the old elements that are gone, topmost only: their descendants go with them.
        /// </summary>
        public List<ElementSyntax> Removed { get; } = [];

        /// <summary>
        /// Gets the elements, matched by <c>x:Name</c>, that sit somewhere else now.
        /// </summary>
        public List<(ElementSyntax Old, ElementSyntax New)> Moved { get; } = [];

        /// <summary>
        /// Gets the elements that correspond but can't be updated in place (type, own text or namespace declarations
        /// changed), so they're rebuilt with their id kept.
        /// </summary>
        public List<(ElementSyntax Old, ElementSyntax New)> Replaced { get; } = [];

        /// <summary>
        /// Gets the new elements with no old counterpart, topmost only: their descendants come with them.
        /// </summary>
        public List<ElementSyntax> Inserted { get; } = [];

        /// <summary>
        /// Gets each attribute added, removed or changed on a corresponding element, named as written in the new tree
        /// (or the old one, for a removal), with the new element it belongs to.
        /// </summary>
        public List<(ElementSyntax Element, string Name)> ChangedAttributes { get; } = [];

        /// <summary>
        /// Gets a value indicating whether nothing live has to change.
        /// </summary>
        public bool IsEmpty =>
            RootProblem == null && !OpaqueChanged && Removed.Count == 0 && Moved.Count == 0
            && Replaced.Count == 0 && Inserted.Count == 0 && ChangedAttributes.Count == 0;
    }
}
```

`sources/IcyUI.Design/Editing/TreeMatcher.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Text;
using Icy.Design.Syntax;
using Icy.Markup;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Diffs two markup trees structurally: <c>x:Name</c> anchors first, then a longest common subsequence of element
    /// names for the children of every pair.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Named elements are paired wherever they are, which turns a named element in another parent into a move and keeps
    /// the live instance game code may hold. Unnamed elements are aligned per parent, so an unnamed element moved or
    /// reordered becomes a removal plus an insertion.
    /// </para>
    /// <para>
    /// Content under property elements (<c>&lt;Grid.RowDefinitions&gt;</c>, styles, templates) is opaque: it's paired
    /// structurally so ids survive, and any textual difference there only sets <see cref="TreeDiff.OpaqueChanged"/>.
    /// </para>
    /// </remarks>
    internal sealed class TreeMatcher
    {
        private readonly DocumentSyntax oldDocument;
        private readonly DocumentSyntax newDocument;
        private readonly TreeDiff diff = new();
        private readonly Dictionary<ElementSyntax, ElementSyntax> namedOldToNew = [];
        private readonly Dictionary<ElementSyntax, ElementSyntax> namedNewToOld = [];
        private readonly HashSet<ElementSyntax> handledOld = [];
        private readonly HashSet<ElementSyntax> alignedOld = [];

        private TreeMatcher(DocumentSyntax oldDocument, DocumentSyntax newDocument)
        {
            this.oldDocument = oldDocument;
            this.newDocument = newDocument;
        }

        /// <summary>
        /// Diffs <paramref name="oldDocument"/> against <paramref name="newDocument"/>. Both must have a root.
        /// </summary>
        public static TreeDiff Match(DocumentSyntax oldDocument, DocumentSyntax newDocument)
        {
            ArgumentNullException.ThrowIfNull(oldDocument);
            ArgumentNullException.ThrowIfNull(newDocument);
            ElementSyntax oldRoot = oldDocument.Root ?? throw new ArgumentException("The old tree has no root.", nameof(oldDocument));
            ElementSyntax newRoot = newDocument.Root ?? throw new ArgumentException("The new tree has no root.", nameof(newDocument));

            var matcher = new TreeMatcher(oldDocument, newDocument);
            if (oldRoot.Name != newRoot.Name)
            {
                matcher.diff.RootProblem = $"The root element changed from '{oldRoot.Name}' to '{newRoot.Name}'. Reload the page to see it.";
                return matcher.diff;
            }

            if (GetDirective(oldDocument, oldRoot, "Class") != GetDirective(newDocument, newRoot, "Class"))
            {
                matcher.diff.RootProblem = "The root element's x:Class changed. Reload the page to see it.";
                return matcher.diff;
            }

            matcher.IndexNames();
            matcher.Pair(oldRoot, newRoot);
            matcher.SweepDepartedNames();
            return matcher.diff;
        }

        /// <summary>
        /// Gets the value of an <c>x:</c> directive written on <paramref name="element"/>, such as <c>x:Name</c>.
        /// </summary>
        public static string? GetDirective(DocumentSyntax document, ElementSyntax element, string localName)
        {
            foreach (AttributeSyntax attribute in element.Attributes)
            {
                if (attribute.LocalName == localName && attribute.Prefix.Length > 0 && document.ResolvePrefix(element, attribute.Prefix) == MarkupNamespaces.Directives)
                    return attribute.Value;
            }

            return null;
        }

        private static bool IsInElementTree(ElementSyntax element)
        {
            for (ElementSyntax? current = element; current != null; current = current.Parent)
            {
                if (current.IsPropertyElement)
                    return false;
            }

            return true;
        }

        private static Dictionary<string, ElementSyntax> IndexNames(DocumentSyntax document)
        {
            var result = new Dictionary<string, ElementSyntax>(StringComparer.Ordinal);
            foreach (ElementSyntax element in document.Elements)
            {
                if (IsInElementTree(element) && GetDirective(document, element, MarkupDirectives.Name) is { } name)
                    result.TryAdd(name, element);
            }

            return result;
        }

        private static string NamespaceDeclarations(ElementSyntax element)
        {
            var builder = new StringBuilder();
            foreach (AttributeSyntax attribute in element.Attributes)
            {
                if (attribute.IsNamespaceDeclaration)
                    builder.Append(attribute.Name).Append('=').Append(attribute.Value).Append(';');
            }

            return builder.ToString();
        }

        private static string OwnText(DocumentSyntax document, ElementSyntax element)
        {
            var builder = new StringBuilder();
            foreach (MarkupSyntaxNode node in element.Content)
            {
                if (node is TextSyntax or CDataSyntax)
                    builder.Append(document.Text, node.Span.Start, node.Span.Length);
            }

            return builder.ToString().Trim();
        }

        private static string SpanText(DocumentSyntax document, ElementSyntax element) =>
            document.Text.Substring(element.Span.Start, element.Span.Length);

        private void IndexNames()
        {
            Dictionary<string, ElementSyntax> oldNames = IndexNames(oldDocument);
            Dictionary<string, ElementSyntax> newNames = IndexNames(newDocument);
            foreach ((string name, ElementSyntax old) in oldNames)
            {
                if (newNames.TryGetValue(name, out ElementSyntax? partner))
                {
                    namedOldToNew[old] = partner;
                    namedNewToOld[partner] = old;
                }
            }
        }

        private void Pair(ElementSyntax old, ElementSyntax current)
        {
            diff.Pairs[current] = old;
            handledOld.Add(old);

            if (old.Name != current.Name
                || NamespaceDeclarations(old) != NamespaceDeclarations(current)
                || OwnText(oldDocument, old) != OwnText(newDocument, current))
            {
                diff.Replaced.Add((old, current));
                return;
            }

            CompareAttributes(old, current);
            Align(old, current);
        }

        private void CompareAttributes(ElementSyntax old, ElementSyntax current)
        {
            foreach (AttributeSyntax attribute in current.Attributes)
            {
                if (attribute.IsNamespaceDeclaration)
                    continue;

                AttributeSyntax? before = old.FindAttribute(attribute.Name);
                if (before == null || before.Value != attribute.Value)
                    diff.ChangedAttributes.Add((current, attribute.Name));
            }

            foreach (AttributeSyntax attribute in old.Attributes)
            {
                if (!attribute.IsNamespaceDeclaration && current.FindAttribute(attribute.Name) == null)
                    diff.ChangedAttributes.Add((current, attribute.Name));
            }
        }

        private void Align(ElementSyntax old, ElementSyntax current)
        {
            alignedOld.Add(old);
            AlignPropertyElements(old, current);

            // Named elements whose partner lives under another parent are handled there, as moves.
            List<ElementSyntax> oldChildren = [.. old.ContentElements.Where(x => !(namedOldToNew.TryGetValue(x, out ElementSyntax? p) && !ReferenceEquals(p.Parent, current)))];
            List<ElementSyntax> newChildren = [.. current.ContentElements.Where(x => !(namedNewToOld.TryGetValue(x, out ElementSyntax? p) && !ReferenceEquals(p.Parent, old)))];

            var matchedOld = new HashSet<ElementSyntax>();
            var matchedNew = new HashSet<ElementSyntax>();
            foreach ((ElementSyntax a, ElementSyntax b) in LongestCommonSubsequence(oldChildren, newChildren))
            {
                matchedOld.Add(a);
                matchedNew.Add(b);
                Pair(a, b);
            }

            foreach (ElementSyntax b in newChildren)
            {
                if (matchedNew.Contains(b))
                    continue;

                if (namedNewToOld.TryGetValue(b, out ElementSyntax? partner) && !matchedOld.Contains(partner))
                {
                    // A named element reordered within this parent.
                    matchedOld.Add(partner);
                    MoveOrReplace(partner, b);
                }
                else
                {
                    diff.Inserted.Add(b);
                }
            }

            foreach (ElementSyntax a in oldChildren)
            {
                if (!matchedOld.Contains(a) && handledOld.Add(a))
                    diff.Removed.Add(a);
            }

            // Named elements that arrived here from another parent.
            foreach (ElementSyntax b in current.ContentElements)
            {
                if (namedNewToOld.TryGetValue(b, out ElementSyntax? partner) && !ReferenceEquals(partner.Parent, old) && !handledOld.Contains(partner))
                    MoveOrReplace(partner, b);
            }
        }

        private void MoveOrReplace(ElementSyntax old, ElementSyntax current)
        {
            if (old.Name == current.Name)
            {
                diff.Moved.Add((old, current));
                Pair(old, current);
                return;
            }

            // A different type somewhere else: there's nothing to keep.
            handledOld.Add(old);
            diff.Removed.Add(old);
            diff.Inserted.Add(current);
        }

        private void AlignPropertyElements(ElementSyntax old, ElementSyntax current)
        {
            List<ElementSyntax> oldProperties = [.. old.Elements.Where(x => x.IsPropertyElement)];
            foreach (ElementSyntax property in current.Elements.Where(x => x.IsPropertyElement))
            {
                int index = oldProperties.FindIndex(x => x.Name == property.Name);
                if (index < 0)
                {
                    diff.OpaqueChanged = true;
                    continue;
                }

                ElementSyntax match = oldProperties[index];
                oldProperties.RemoveAt(index);
                PairStructure(match, property);
                if (SpanText(oldDocument, match) != SpanText(newDocument, property))
                    diff.OpaqueChanged = true;
            }

            if (oldProperties.Count > 0)
                diff.OpaqueChanged = true;
        }

        private void PairStructure(ElementSyntax old, ElementSyntax current)
        {
            if (old.Name != current.Name)
                return;

            diff.Pairs[current] = old;
            handledOld.Add(old);
            using IEnumerator<ElementSyntax> oldChildren = old.Elements.GetEnumerator();
            using IEnumerator<ElementSyntax> newChildren = current.Elements.GetEnumerator();
            while (oldChildren.MoveNext() && newChildren.MoveNext())
                PairStructure(oldChildren.Current, newChildren.Current);
        }

        /// <summary>
        /// Removes named elements that left an aligned parent for one the diff never visits (inside an inserted or
        /// replaced element): they're rebuilt as part of that element, so the old instance has to go.
        /// </summary>
        private void SweepDepartedNames()
        {
            foreach ((ElementSyntax old, _) in namedOldToNew)
            {
                if (!handledOld.Contains(old) && old.Parent != null && alignedOld.Contains(old.Parent))
                {
                    handledOld.Add(old);
                    diff.Removed.Add(old);
                }
            }
        }

        private List<(ElementSyntax Old, ElementSyntax New)> LongestCommonSubsequence(List<ElementSyntax> oldChildren, List<ElementSyntax> newChildren)
        {
            int rows = oldChildren.Count;
            int columns = newChildren.Count;
            int[,] lengths = new int[rows + 1, columns + 1];
            for (int i = rows - 1; i >= 0; i--)
            {
                for (int j = columns - 1; j >= 0; j--)
                {
                    lengths[i, j] = Same(oldChildren[i], newChildren[j])
                        ? lengths[i + 1, j + 1] + 1
                        : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
                }
            }

            var pairs = new List<(ElementSyntax, ElementSyntax)>();
            for (int i = 0, j = 0; i < rows && j < columns;)
            {
                if (Same(oldChildren[i], newChildren[j]))
                {
                    pairs.Add((oldChildren[i], newChildren[j]));
                    i++;
                    j++;
                }
                else if (lengths[i + 1, j] >= lengths[i, j + 1])
                {
                    i++;
                }
                else
                {
                    j++;
                }
            }

            return pairs;
        }

        private bool Same(ElementSyntax old, ElementSyntax current)
        {
            if (namedOldToNew.TryGetValue(old, out ElementSyntax? partner))
                return ReferenceEquals(partner, current);
            if (namedNewToOld.ContainsKey(current))
                return false;
            return old.Name == current.Name;
        }
    }
}
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeMatcherTests"`
Expected: PASS, Total 13.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1267, no failures), then `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep "Warning(s)"` (81).

```bash
git add sources/IcyUI.Design/Editing/TreeDiff.cs sources/IcyUI.Design/Editing/TreeMatcher.cs sources/IcyUI.Tests/Design/TreeMatcherTests.cs
git commit -m "Add a structural tree matcher for markup text changes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `ApplyText`: ids, units, undo and `ReloadFromSource`

**Files:**
- Create: `sources/IcyUI.Design/Editing/TextUnit.cs`, `sources/IcyUI.Design/Editing/ElementReplacedAction.cs`
- Modify: `sources/IcyUI.Design/DesignDocument.cs` (live tree field, `ApplyText`, `ApplyTextCore`, `ReloadFromSource`, `IsInSync`, `LiveErrors`, unit creation)
- Modify: `sources/IcyUI.Design/Editing/UndoItem.cs` (text items), `sources/IcyUI.Design/UndoStack.cs` (`RecordText`, replaying text items)
- Test: `sources/IcyUI.Tests/Design/ApplyTextTests.cs`

**Interfaces:**
- Consumes: `TreeMatcher.Match`, `TreeDiff` (Task 2); 10.1's `ElementRemovedAction(NodeId)`, `ElementMovedAction(NodeId node, NodeId newParent, int newStart)`, `ElementInsertedAction(NodeId parent, int start, IReadOnlyList<NodeId>? restoredIds = null)`, `AttributeChangedAction(NodeId, string)`, `DesignDocument.Rebuild(ElementSyntax)`, `MirrorContext(DesignDocument, DocumentSnapshot)`, `DocumentSnapshot(MarkupText, DocumentSyntax, LineMap, ids, nodes)`.
- Produces:
  - `public EditResult DesignDocument.ApplyText(string newText)`, `public bool DesignDocument.ReloadFromSource()`, `public bool DesignDocument.IsInSync`, `public IReadOnlyList<Diagnostic> DesignDocument.LiveErrors`.
  - Internal `DesignDocument.ApplyTextCore(string newText)`; private `MirrorText()`, `CreateUnits(TreeDiff, DocumentSnapshot)`, `RunUnit(TextUnit, MirrorContext, DocumentSnapshot)`, and the fields `liveSyntax`, `outOfSync` (`HashSet<NodeId>`), `liveErrors` (`List<Diagnostic>`), `rootProblem` (`bool`). Task 4 extends all of these.
  - `internal readonly record struct TextUnit(MirrorAction Action, NodeId? Node, TextSpan Span)`.
  - `internal sealed class ElementReplacedAction(NodeId node) : MirrorAction`.
  - `UndoItem.ForText(string previousText)`, `UndoItem.Text`; `UndoStack.RecordText(string previousText, string description)`.

**How the document tracks the live tree.** A new field `liveSyntax` holds the tree the id map and the live objects belong to. It equals `syntax` except while the text is malformed or the root changed; then `syntax` follows the text and `liveSyntax` keeps the last tree the pages could follow. `Resync` (visual edits) and `Restore` keep the two equal, as before.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/ApplyTextTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Icy.Design;
using Icy.Design.Text;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Icy.Tests.Design
{
    public class ApplyTextTests(ITestOutputHelper output)
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10" Margin="1"/>
              </StackPanel>
              <StackPanel x:Name="right">
                <TextBlock x:Name="label">Hi</TextBlock>
              </StackPanel>
              <Button>Unnamed</Button>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void AttributeChanges_AreMirroredOnTheSameInstance()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");

            EditResult result = document.ApplyText(Page.Replace("Width=\"10\" Margin=\"1\"", "Width=\"20\" Height=\"5\"", StringComparison.Ordinal));

            Assert.True(result.Succeeded, result.ToString());
            Assert.Same(box, DesignTestHost.Named<Border>(root, "box"));
            Assert.Equal(20f, box.Width);
            Assert.Equal(5f, box.Height);
            Assert.NotEqual(new Thickness(1), box.Margin);
            Assert.True(document.IsInSync);
        }

        [Fact]
        public void InsertsRemovesAndMoves_AreMirroredKeepingNamedInstances()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            var left = DesignTestHost.Named<StackPanel>(root, "left");
            var right = DesignTestHost.Named<StackPanel>(root, "right");

            document.ApplyText(
                """
                <StackPanel x:Name="root">
                  <StackPanel x:Name="left">
                    <Border x:Name="added"/>
                  </StackPanel>
                  <StackPanel x:Name="right">
                    <TextBlock x:Name="label">Hi</TextBlock>
                    <Border x:Name="box" Width="10" Margin="1"/>
                  </StackPanel>
                </StackPanel>
                """.ReplaceLineEndings("\n"));

            Assert.Same(box, right.Children[1]);
            Assert.Same(DesignTestHost.Named<Border>(root, "added"), Assert.Single(left.Children));
            Assert.Equal(2, ((StackPanel)root).Children.Count);
            Assert.DoesNotContain(((StackPanel)root).Children, x => x is Button);
        }

        [Fact]
        public void ChangedElementText_ReplacesTheElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var label = DesignTestHost.Named<TextBlock>(root, "label");
            int replaced = 0;
            document.SubtreeReplaced += (_, _) => replaced++;

            document.ApplyText(Page.Replace(">Hi<", ">Bye<", StringComparison.Ordinal));

            var rebuilt = DesignTestHost.Named<TextBlock>(root, "label");
            Assert.NotSame(label, rebuilt);
            Assert.Equal("Bye", rebuilt.Text);
            Assert.Equal(1, replaced);
        }

        [Fact]
        public void IdenticalText_IsANoOp()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);

            document.ApplyText(Page);

            Assert.Equal(0, document.Version);
            Assert.False(document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void Changed_CarriesTheMinimalChange()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            TextChangeSet? changes = null;
            document.Changed += (_, e) => changes = e.Changes;

            document.ApplyText(Page.Replace("Width=\"10\"", "Width=\"20\"", StringComparison.Ordinal));

            TextChange change = Assert.Single(changes!);
            Assert.Equal("2", change.NewText);
            Assert.Equal(1, change.Span.Length);
            Assert.Equal(document.Text, changes!.Apply(Page));
        }

        [Fact]
        public void ApplyText_IsOneUndoStep()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            string edited = Page.Replace("Width=\"10\"", "Width=\"20\"", StringComparison.Ordinal);
            document.ApplyText(edited);

            Assert.True(document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, box.Width);

            Assert.True(document.Editor.UndoStack.Redo().Succeeded);
            Assert.Equal(edited, document.Text);
            Assert.Equal(20f, box.Width);
        }

        [Fact]
        public void AFormattingOnlySave_KeepsEveryInstance()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            UIElement[] before = [.. new[] { "root", "left", "box", "right", "label" }.Select(x => DesignTestHost.Named<UIElement>(root, x))];
            int replaced = 0;
            document.SubtreeReplaced += (_, _) => replaced++;

            document.ApplyText(Page.Replace("  ", "\t", StringComparison.Ordinal).Replace("\"10\"", "'10'", StringComparison.Ordinal));

            Assert.Equal(before, new[] { "root", "left", "box", "right", "label" }.Select(x => DesignTestHost.Named<UIElement>(root, x)));
            Assert.Equal(0, replaced);
            Assert.True(document.IsInSync);
        }

        [Fact]
        public void IdsSurviveForMatchedAndMovedElements()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));

            document.ApplyText(Page
                .Replace("    <Border x:Name=\"box\" Width=\"10\" Margin=\"1\"/>\n", string.Empty, StringComparison.Ordinal)
                .Replace("<TextBlock x:Name=\"label\">Hi</TextBlock>", "<TextBlock x:Name=\"label\">Hi</TextBlock><Border x:Name=\"box\" Width=\"10\" Margin=\"1\"/>", StringComparison.Ordinal));

            Assert.Equal(box, host.IdOf(DesignTestHost.Named<Border>(root, "box")));
            Assert.Equal("Border", document.GetNode(box)!.Name);
        }

        [Fact]
        public void TheSameFileLoadedTwice_IsUpdatedInBothCopies()
        {
            using var host = new DesignTestHost();
            (UIElement first, DesignDocument document) = host.Load(Page, "same.xml");
            (UIElement second, _) = host.Load(Page, "same.xml");

            document.ApplyText(Page.Replace("Width=\"10\"", "Width=\"30\"", StringComparison.Ordinal));

            Assert.Equal(30f, DesignTestHost.Named<Border>(first, "box").Width);
            Assert.Equal(30f, DesignTestHost.Named<Border>(second, "box").Width);
        }

        [Fact]
        public void ReloadFromSource_AppliesTheResolvedFile()
        {
            using var host = new DesignTestHost();
            string path = Path.Combine(Path.GetTempPath(), $"icy-reload-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, Page);
            try
            {
                (UIElement root, DesignDocument document) = host.Load(Page, path);
                File.WriteAllText(path, Page.Replace("Width=\"10\"", "Width=\"40\"", StringComparison.Ordinal));

                Assert.True(document.ReloadFromSource());
                Assert.Equal(40f, DesignTestHost.Named<Border>(root, "box").Width);
            }
            finally
            {
                File.Delete(path);
            }

            (_, DesignDocument unresolved) = host.Load(Page, "Pages/NotAFile.xml");
            Assert.False(unresolved.ReloadFromSource());
        }

        [Fact]
        public void UndoAcrossVisualAndTextEdits_RestoresTheExactText()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            document.Editor.SetAttribute(box, "Height", "5");
            document.ApplyText(document.Text.Replace("Width=\"10\"", "Width=\"30\"", StringComparison.Ordinal));
            document.Editor.SetAttribute(box, "Width", "40");
            document.Editor.InsertElement(host.IdOf(DesignTestHost.Named<StackPanel>(root, "left")), 0, "<TextBlock/>");
            string final = document.Text;

            while (document.Editor.UndoStack.CanUndo)
                Assert.True(document.Editor.UndoStack.Undo().Succeeded);
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, DesignTestHost.Named<Border>(root, "box").Width);

            while (document.Editor.UndoStack.CanRedo)
                Assert.True(document.Editor.UndoStack.Redo().Succeeded);
            Assert.Equal(final, document.Text);
        }

        [Fact]
        public void ARootThatChangedType_StoresTheTextAndNeedsReload()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);

            document.ApplyText("<Border/>");

            Assert.Equal("<Border/>", document.Text);
            Assert.True(document.NeedsReload);
            Assert.False(document.IsInSync);
            Assert.Single(document.LiveErrors);
            Assert.IsType<StackPanel>(root);
        }

        [Fact]
        public void ParseAndDiff_OfA100KbDocument_IsMeasured()
        {
            using var host = new DesignTestHost();
            var builder = new StringBuilder("<StackPanel x:Name=\"root\">\n");
            for (int i = 0; builder.Length < 100_000; i++)
                builder.Append(CultureInfo.InvariantCulture, $"  <Border x:Name=\"b{i}\" Width=\"10\" Height=\"20\" Margin=\"1,2,3,4\" HorizontalAlignment=\"Left\"/>\n");
            builder.Append("</StackPanel>");
            string page = builder.ToString();
            (_, DesignDocument document) = host.Load(page);

            var timings = new List<double>();
            for (int i = 0; i < 7; i++)
            {
                string next = page.Replace("x:Name=\"b0\" Width=\"10\"", $"x:Name=\"b0\" Width=\"{11 + i}\"", StringComparison.Ordinal);
                var watch = Stopwatch.StartNew();
                document.ApplyText(next);
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }

            timings.Sort();
            Assert.True(document.IsInSync);
            output.WriteLine($"{page.Length} chars: ApplyText (parse + diff + one attribute) median {timings[timings.Count / 2]:0.00} ms (budget 5 ms).");
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ApplyTextTests"`
Expected: the build FAILS (`ApplyText`, `IsInSync`, `LiveErrors`, `ReloadFromSource` not found).

- [ ] **Step 3: Add the unit types**

`sources/IcyUI.Design/Editing/TextUnit.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// One independently applied part of a text change: the action, the element it leaves out of sync if it fails
    /// (<see langword="null"/> when there's nothing to rebuild later, as for a removal), and where to report a failure.
    /// </summary>
    internal readonly record struct TextUnit(MirrorAction Action, NodeId? Node, TextSpan Span);
}
```

`sources/IcyUI.Design/Editing/ElementReplacedAction.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Rebuilds every live copy of an element from its current text, keeping its id: for an element whose type, own
    /// text or namespace declarations changed, or that fell out of sync.
    /// </summary>
    internal sealed class ElementReplacedAction(NodeId node) : MirrorAction
    {
        private bool rebuilt;

        public override void Execute(MirrorContext context)
        {
            DesignDocument document = context.Document;
            if (document.GetNode(node) is not { } element)
                return;

            if (!document.IsEditable(element))
            {
                document.MarkNeedsReload();
                return;
            }

            // Set first: a rebuild that fails halfway may already have replaced some copies.
            rebuilt = true;
            document.Rebuild(element);
        }

        public override void Revert(MirrorContext context)
        {
            // The document holds the old tree again, so rebuilding shows the old markup.
            if (rebuilt && context.Document.GetNode(node) is { } element)
                context.Document.Rebuild(element);
            rebuilt = false;
        }

        public override MirrorAction CreateInverse(MirrorContext context) => new ElementReplacedAction(node);
    }
}
```

- [ ] **Step 4: Teach the undo history about text items**

In `UndoItem`:
1. add a field `private readonly string? text;`;
2. add a constructor parameter `string? text = null`, stored with `this.text = text;`;
3. add these members:

```csharp
        /// <summary>
        /// Gets the whole text to apply when replaying this item, or <see langword="null"/> for a step or a coalesced
        /// value.
        /// </summary>
        public string? Text => text;

        public static UndoItem ForText(string previousText) => new(null, default, null, null, previousText);
```

At the top of `CreateStep`, add:

```csharp
            if (text != null)
                throw new InvalidOperationException("A text item is replayed with ApplyText, not as a step.");
```

In `UndoStack`:
1. Add after `Record`:

```csharp
        internal void RecordText(string previousText, string description)
        {
            redo.Clear();
            if (transaction != null)
            {
                transaction.Items.Add(UndoItem.ForText(previousText));
                return;
            }

            var entry = new UndoEntry(description);
            entry.Items.Add(UndoItem.ForText(previousText));
            undo.Add(entry);
            Changed?.Invoke(this, EventArgs.Empty);
        }
```

2. In `Replay`, replace the loop body (from `EditResult result;` to `replayed.Items.Add(UndoItem.ForStep(inverse!));`) with:

```csharp
                EditResult result = ReplayItem(entry.Items[i], out UndoItem? undone);
                if (!result.Succeeded)
                {
                    RollBack(entry, replayed, from);
                    return result;
                }

                replayed.Items.Add(undone!);
```

3. In `RollBack`, replace `restored = document.Apply(replayed.Items[j].CreateStep(document), out _).Succeeded;` with `restored = ReplayItem(replayed.Items[j], out _).Succeeded;`.

4. Add the helper before `RollBack`:

```csharp
        /// <summary>
        /// Replays one item and gives back the item that would undo the replay.
        /// </summary>
        private EditResult ReplayItem(UndoItem item, out UndoItem? undone)
        {
            undone = null;
            if (item.Text is { } previousText)
            {
                // Applying text never fails: the text always wins, and live gaps show in LiveErrors.
                string current = document.Text;
                document.ApplyTextCore(previousText);
                undone = UndoItem.ForText(current);
                return EditResult.Success();
            }

            EditResult result;
            EditStep? inverse = null;
            try
            {
                result = document.Apply(item.CreateStep(document), out inverse);
            }
            catch (InvalidOperationException ex)
            {
                // Building the step can find its target gone; that's a failed replay, not a crash.
                result = EditResult.Failure(default, ex.Message);
            }

            if (result.Succeeded)
                undone = UndoItem.ForStep(inverse!);
            return result;
        }
```

Text items have no `CoalescedKey`, so `RecordCoalesced` already treats them as steps that end a coalesced run. Their whole-text replay changes offsets, which is exactly why a run must end there.

- [ ] **Step 5: Add `ApplyText` to the document**

In `DesignDocument`:

1. Fields (with the other mutable fields; `liveErrors` and `outOfSync` go with the readonly ones at the top):

```csharp
        private readonly HashSet<NodeId> outOfSync = [];
        private readonly List<Diagnostic> liveErrors = [];
```

```csharp
        private DocumentSyntax liveSyntax;
        private bool rootProblem;
```

In the constructor, after `syntax = Parse(text);`, add `liveSyntax = syntax;`. At the end of `Resync`, add `liveSyntax = newSyntax;`. In `Restore`, add `liveSyntax = snapshot.Syntax;`.

2. Public members (properties after `NeedsReload`, methods after `SaveAs`):

```csharp
        /// <summary>
        /// Gets a value indicating whether the live pages reflect the current text: the text has no syntax errors,
        /// every part of the last <see cref="ApplyText"/> was mirrored, and the root didn't change in a way that needs
        /// a reload.
        /// </summary>
        public bool IsInSync => !syntax.HasErrors && outOfSync.Count == 0 && !rootProblem;

        /// <summary>
        /// Gets why the live pages don't reflect the current text: the parts of the last <see cref="ApplyText"/> that
        /// failed to mirror, with the text they concern. Syntax errors are in <see cref="DocumentSyntax.Diagnostics"/>.
        /// </summary>
        public IReadOnlyList<Diagnostic> LiveErrors => liveErrors;
```

```csharp
        /// <summary>
        /// Replaces the whole text and updates the live pages to match, keeping every element that still corresponds,
        /// with its runtime state. This is how saved files and typed text reach a running page.
        /// </summary>
        /// <param name="newText">The new markup text.</param>
        /// <returns>
        /// Always a success: the text is stored and recorded as one undo step even when the live pages can't follow
        /// all of it. Check <see cref="IsInSync"/> and <see cref="LiveErrors"/> for that.
        /// </returns>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Malformed text changes nothing live; the pages wait for the next well-formed text.</description></item>
        /// <item><description>
        /// Well-formed text is diffed against the last tree the pages followed. Each part is mirrored on its own: one
        /// part that fails (a value that doesn't convert, say) is reported in <see cref="LiveErrors"/> and doesn't stop
        /// the others, and the next <see cref="ApplyText"/> rebuilds what failed.
        /// </description></item>
        /// <item><description>Identical text changes nothing and records nothing.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="newText"/> is <see langword="null"/>.</exception>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <exception cref="InvalidOperationException">The calling thread doesn't own the document's pages.</exception>
        public EditResult ApplyText(string newText)
        {
            ArgumentNullException.ThrowIfNull(newText);
            ThrowIfDisposed();
            VerifyAccess();
            FlushPending();

            string previous = text.Text;
            if (string.Equals(previous, newText, StringComparison.Ordinal))
                return EditResult.Success();

            ApplyTextCore(newText);
            Editor.UndoStack.RecordText(previous, "Apply text");
            return EditResult.Success();
        }

        /// <summary>
        /// Reads the file <see cref="SourcePath"/> resolves to through <see cref="DesignSession.SourcePathResolver"/>,
        /// and applies its text with <see cref="ApplyText"/>.
        /// </summary>
        /// <returns><see langword="false"/>, changing nothing, when the source path doesn't resolve to a file.</returns>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        /// <exception cref="IOException">The file couldn't be read.</exception>
        public bool ReloadFromSource()
        {
            ThrowIfDisposed();
            string? path = SourcePath != null ? Session.SourcePathResolver(SourcePath) : null;
            if (path == null || !File.Exists(path))
                return false;

            ApplyText(File.ReadAllText(path));
            return true;
        }
```

3. Internal and private members (internal after `ApplyAttribute`; private after `FindLiveIndex`):

```csharp
        /// <summary>
        /// Stores <paramref name="newText"/> and mirrors it, without recording undo: <see cref="ApplyText"/> and undo
        /// replay both come through here.
        /// </summary>
        internal void ApplyTextCore(string newText)
        {
            FlushPending();
            TextChange change = DiffText(text.Text, newText);
            text = new MarkupText(newText, text.Version + 1);
            parsedText = newText;
            syntax = Parse(newText);
            lineMap = new LineMap(newText);

            // Malformed text: the id map and the live pages stay on the last tree they could follow.
            if (!syntax.HasErrors)
                MirrorText();

            Changed?.Invoke(this, new DocumentChangedEventArgs(new TextChangeSet([change]), Version));
        }
```

```csharp
        private static TextChange DiffText(string before, string after)
        {
            int limit = Math.Min(before.Length, after.Length);
            int prefix = 0;
            while (prefix < limit && before[prefix] == after[prefix])
                prefix++;

            int suffix = 0;
            while (suffix < limit - prefix && before[before.Length - 1 - suffix] == after[after.Length - 1 - suffix])
                suffix++;

            return new TextChange(new TextSpan(prefix, before.Length - prefix - suffix), after.Substring(prefix, after.Length - prefix - suffix));
        }

        private void MirrorText()
        {
            TreeDiff diff = TreeMatcher.Match(liveSyntax, syntax);
            liveErrors.Clear();
            rootProblem = diff.RootProblem != null;
            if (diff.RootProblem is { } problem)
            {
                // The root can't be swapped in place: keep everything on the last tree until the root matches again.
                liveErrors.Add(new Diagnostic(syntax.Root!.NameSpan, problem));
                MarkNeedsReload();
                return;
            }

            if (diff.OpaqueChanged)
                MarkNeedsReload();

            var before = new DocumentSnapshot(new MarkupText(liveSyntax.Text), liveSyntax, new LineMap(liveSyntax.Text), ids, nodes);
            var newIds = new Dictionary<ElementSyntax, NodeId>();
            var newNodes = new Dictionary<NodeId, ElementSyntax>();
            foreach ((ElementSyntax current, ElementSyntax old) in diff.Pairs)
            {
                if (ids.TryGetValue(old, out NodeId id))
                {
                    newIds[current] = id;
                    newNodes[id] = current;
                }
            }

            foreach (ElementSyntax element in syntax.Elements)
            {
                if (!newIds.ContainsKey(element))
                    AssignNewId(element, newIds, newNodes);
            }

            List<NodeId> vanished = [.. ids.Values.Where(x => !newNodes.ContainsKey(x))];
            ids = newIds;
            nodes = newNodes;
            liveSyntax = syntax;

            var context = new MirrorContext(this, before);
            foreach (TextUnit unit in CreateUnits(diff, before))
                RunUnit(unit, context, before);

            foreach (NodeId id in vanished)
                Map.RemoveNode(id);
        }

        /// <summary>
        /// Turns a diff into units, ordered so insertions anchor against the final set of siblings: removals, moves,
        /// replacements, insertions, then attribute changes.
        /// </summary>
        private List<TextUnit> CreateUnits(TreeDiff diff, DocumentSnapshot before)
        {
            var oldToNew = new Dictionary<ElementSyntax, ElementSyntax>();
            foreach ((ElementSyntax current, ElementSyntax old) in diff.Pairs)
                oldToNew[old] = current;

            var units = new List<TextUnit>();
            foreach (ElementSyntax old in diff.Removed)
            {
                TextSpan span = old.Parent != null && oldToNew.TryGetValue(old.Parent, out ElementSyntax? parent) ? parent.NameSpan : default;
                units.Add(new TextUnit(new ElementRemovedAction(before.Ids[old]), null, span));
            }

            foreach ((_, ElementSyntax current) in diff.Moved)
                units.Add(new TextUnit(new ElementMovedAction(ids[current], ids[current.Parent!], current.Span.Start), ids[current], current.NameSpan));

            var rebuilt = new HashSet<ElementSyntax>();
            foreach ((_, ElementSyntax current) in diff.Replaced)
            {
                rebuilt.Add(current);
                units.Add(new TextUnit(new ElementReplacedAction(ids[current]), ids[current], current.NameSpan));
            }

            foreach (ElementSyntax current in diff.Inserted)
                units.Add(new TextUnit(new ElementInsertedAction(ids[current.Parent!], current.Span.Start), ids[current], current.NameSpan));

            foreach ((ElementSyntax element, string name) in diff.ChangedAttributes)
            {
                if (!rebuilt.Contains(element))
                    units.Add(new TextUnit(new AttributeChangedAction(ids[element], name), ids[element], element.FindAttribute(name)?.Span ?? element.NameSpan));
            }

            return units;
        }

        private void RunUnit(TextUnit unit, MirrorContext context, DocumentSnapshot before) => unit.Action.Execute(context);
```

`RunUnit` gets its failure handling in Task 4; for now a failing unit throws out of `ApplyText`.

- [ ] **Step 6: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ApplyTextTests" --logger "console;verbosity=detailed"`
Expected: PASS, Total 13. Copy the measurement line into the task report.

- [ ] **Step 7: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1280, no failures), then `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep "Warning(s)"` (81; if `RunUnit`'s unused `before` parameter warns, keep it, since Task 4 uses it).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/ApplyTextTests.cs
git commit -m "Apply whole markup texts to live pages through a structural diff

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Bad text: per-unit failure, out-of-sync healing, sync state, refusing visual edits

**Files:**
- Modify: `sources/IcyUI.Design/DesignDocument.cs` (`RunUnit`, `CreateUnits`, `MirrorText`, `ApplyTextCore`, new `SyncNames`, `UpdateSyncState`, `RevertOnOldTree`, `SyncStateChanged`)
- Modify: `sources/IcyUI.Design/MarkupEditor.cs` (refuse edits while out of sync)
- Test: `sources/IcyUI.Tests/Design/ApplyTextFailureTests.cs`

**Interfaces:**
- Consumes: Task 3's `outOfSync`, `liveErrors`, `rootProblem`, `TextUnit`, `ElementReplacedAction`; Task 1's `Failures`.
- Produces: `public event EventHandler? DesignDocument.SyncStateChanged`; `MarkupEditor` operations fail with "The markup has errors; fix them first." while `IsInSync` is `false`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/ApplyTextFailureTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class ApplyTextFailureTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10"/>
              </StackPanel>
              <StackPanel x:Name="right">
                <TextBlock x:Name="label">Hi</TextBlock>
              </StackPanel>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void MalformedText_KeepsTheLivePagesAndIds_UntilItIsFixed()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            NodeId id = host.IdOf(box);

            document.ApplyText(Page.Replace("Width=\"10\"", "Width=\"20\"", StringComparison.Ordinal)[..^"</StackPanel>".Length]);

            Assert.False(document.IsInSync);
            Assert.Equal(10f, box.Width);
            Assert.Equal("Border", document.GetNode(id)!.Name);

            document.ApplyText(Page.Replace("Width=\"10\"", "Width=\"20\"", StringComparison.Ordinal));

            Assert.True(document.IsInSync);
            Assert.Same(box, DesignTestHost.Named<Border>(root, "box"));
            Assert.Equal(20f, box.Width);
        }

        [Fact]
        public void ALoaderErrorInOneUnit_StillAppliesTheOthers()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);

            ApplyBadWidthAndAnInsertion(document);

            Assert.Equal(10f, DesignTestHost.Named<Border>(root, "box").Width);
            Assert.NotNull(MarkupNameScope.GetScope(root)!.Find("extra"));
            Diagnostic error = Assert.Single(document.LiveErrors);
            Assert.Equal("Width=\"abc\"", document.Text.Substring(error.Span.Start, error.Span.Length));
            Assert.False(document.IsInSync);
        }

        [Fact]
        public void AFailedUnit_HealsOnTheNextValidApply()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            ApplyBadWidthAndAnInsertion(document);

            document.ApplyText(document.Text.Replace("Width=\"abc\"", "Width=\"30\"", StringComparison.Ordinal));

            Assert.True(document.IsInSync);
            Assert.Empty(document.LiveErrors);
            Assert.Equal(30f, DesignTestHost.Named<Border>(root, "box").Width);
        }

        [Fact]
        public void AFailedInsertion_IsInsertedOnceItBuilds()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            string withLate = Page.Replace("<TextBlock x:Name=\"label\">Hi</TextBlock>", "<TextBlock x:Name=\"label\">Hi</TextBlock><Border x:Name=\"late\" Width=\"abc\"/>", StringComparison.Ordinal);

            document.ApplyText(withLate);
            Assert.Null(MarkupNameScope.GetScope(root)!.Find("late"));
            Assert.False(document.IsInSync);

            document.ApplyText(withLate.Replace("Width=\"abc\"", "Width=\"5\"", StringComparison.Ordinal));

            Assert.True(document.IsInSync);
            Assert.Equal(5f, DesignTestHost.Named<Border>(root, "late").Width);
            Assert.Same(DesignTestHost.Named<Border>(root, "late"), DesignTestHost.Named<StackPanel>(root, "right").Children[1]);
        }

        [Fact]
        public void AThrowingSetter_BecomesALiveError()
        {
            using var host = new DesignTestHost();
            const string page = "<StackPanel x:Name=\"root\"><ThrowingBox x:Name=\"t\" Count=\"5\"/></StackPanel>";
            (UIElement root, DesignDocument document) = host.Load(page);

            document.ApplyText(page.Replace("Count=\"5\"", "Count=\"-1\"", StringComparison.Ordinal));

            Assert.Single(document.LiveErrors);
            Assert.Equal(5, DesignTestHost.Named<ThrowingBox>(root, "t").Count);
        }

        [Fact]
        public void VisualEdits_AreRefusedWhileOutOfSync()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            document.ApplyText(Page[..^"</StackPanel>".Length]);

            Assert.False(document.Editor.SetAttribute(box, "Width", "50").Succeeded);
            Assert.False(document.Editor.RemoveElement(box).Succeeded);

            document.ApplyText(Page);
            Assert.True(document.Editor.SetAttribute(box, "Width", "50").Succeeded);
        }

        [Fact]
        public void SyncStateChanged_FiresOnEachTransition()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            int fired = 0;
            document.SyncStateChanged += (_, _) => fired++;

            document.ApplyText(Page[..^"</StackPanel>".Length]);
            document.ApplyText(Page[..^"</StackPanel>".Length] + "  ");
            document.ApplyText(Page);

            Assert.Equal(2, fired);
        }

        [Fact]
        public void UndoThroughAMalformedState()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            document.ApplyText(Page[..^"</StackPanel>".Length]);

            Assert.True(document.Editor.UndoStack.Undo().Succeeded);

            Assert.Equal(Page, document.Text);
            Assert.True(document.IsInSync);
        }

        [Fact]
        public void NamesFollowMovesOutOfRemovedContainers()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");

            document.ApplyText(
                """
                <StackPanel x:Name="root">
                  <Border x:Name="box" Width="10"/>
                  <StackPanel x:Name="right">
                    <TextBlock x:Name="label">Hi</TextBlock>
                  </StackPanel>
                </StackPanel>
                """.ReplaceLineEndings("\n"));

            Assert.Same(box, MarkupNameScope.GetScope(root)!.Find("box"));
            Assert.Same(box, ((StackPanel)root).Children[0]);
            Assert.Null(MarkupNameScope.GetScope(root)!.Find("left"));
        }

        [Fact]
        public void ARootProblem_ClearsWhenTheRootMatchesAgain()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            document.ApplyText("<Border/>");

            document.ApplyText(Page);

            Assert.True(document.IsInSync);
            Assert.Empty(document.LiveErrors);
        }

        private static void ApplyBadWidthAndAnInsertion(DesignDocument document) =>
            document.ApplyText(Page
                .Replace("Width=\"10\"", "Width=\"abc\"", StringComparison.Ordinal)
                .Replace("<TextBlock x:Name=\"label\">Hi</TextBlock>", "<TextBlock x:Name=\"label\">Hi</TextBlock><Border x:Name=\"extra\"/>", StringComparison.Ordinal));
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ApplyTextFailureTests"`
Expected: the build FAILS (`SyncStateChanged` not found). Once that event exists, the unit-failure tests throw out of `ApplyText`, and the refusal and name tests fail their assertions.

- [ ] **Step 3: Implement**

In `DesignDocument`:

1. Add the event after `DiagnosticsChanged`:

```csharp
        /// <summary>
        /// Occurs when <see cref="IsInSync"/> or <see cref="LiveErrors"/> changed.
        /// </summary>
        public event EventHandler? SyncStateChanged;
```

and these fields with the other mutable ones:

```csharp
        private bool reportedInSync = true;
        private Diagnostic[] reportedErrors = [];
```

2. Replace `RunUnit` with:

```csharp
        /// <summary>
        /// Runs one unit. A unit that fails is reverted on the old tree, reported, and remembered as out of sync, and
        /// the other units still run.
        /// </summary>
        private void RunUnit(TextUnit unit, MirrorContext context, DocumentSnapshot before)
        {
            try
            {
                unit.Action.Execute(context);
            }
            catch (Exception ex) when (!Failures.IsFatal(ex))
            {
                RevertOnOldTree(unit.Action, context, before);
                liveErrors.Add(new Diagnostic(unit.Span, Failures.Describe(ex)));
                if (unit.Node is NodeId failed)
                    outOfSync.Add(failed);
            }
        }

        /// <summary>
        /// Reverts a failed unit with the document showing the old tree, which is what every action's Revert expects,
        /// then puts the new tree back.
        /// </summary>
        private void RevertOnOldTree(MirrorAction action, MirrorContext context, DocumentSnapshot before)
        {
            var after = new DocumentSnapshot(text, syntax, lineMap, ids, nodes);
            Restore(before);
            try
            {
                action.Revert(context);
            }
            catch (Exception ex) when (!Failures.IsFatal(ex))
            {
                // A revert that fails too leaves the element behind the text; it's reported and rebuilt next time.
            }
            finally
            {
                Restore(after);
            }
        }
```

Note that `Restore(after)` restores `text` with its version intact, because `after` captures the current `MarkupText`.

3. In `MirrorText`, before computing the new ids, take the previously out-of-sync ids:

```csharp
            HashSet<NodeId> previouslyOutOfSync = [.. outOfSync];
            outOfSync.Clear();
```

Pass them to `CreateUnits(diff, before, previouslyOutOfSync)`, and after the `Map.RemoveNode` loop add `SyncNames();`. The root-problem early return comes before this and leaves `outOfSync` as it is: the pages stay on the old tree, so what was out of sync still is.

4. Replace `CreateUnits` with the version that heals out-of-sync elements:

```csharp
        /// <summary>
        /// Turns a diff into units, ordered so insertions anchor against the final set of siblings: removals, moves,
        /// replacements, insertions, then attribute changes. Elements left out of sync by an earlier apply are rebuilt,
        /// or inserted when they have no live copy yet.
        /// </summary>
        private List<TextUnit> CreateUnits(TreeDiff diff, DocumentSnapshot before, HashSet<NodeId> previouslyOutOfSync)
        {
            var oldToNew = new Dictionary<ElementSyntax, ElementSyntax>();
            foreach ((ElementSyntax current, ElementSyntax old) in diff.Pairs)
                oldToNew[old] = current;

            var rebuilt = new HashSet<ElementSyntax>();
            var replaced = new List<ElementSyntax>();
            var inserted = new List<ElementSyntax>();
            foreach ((_, ElementSyntax current) in diff.Replaced)
            {
                if (rebuilt.Add(current))
                    replaced.Add(current);
            }

            foreach (ElementSyntax current in diff.Inserted)
            {
                if (rebuilt.Add(current))
                    inserted.Add(current);
            }

            foreach (NodeId id in previouslyOutOfSync)
            {
                if (!nodes.TryGetValue(id, out ElementSyntax? element) || !rebuilt.Add(element))
                    continue;

                if (Map.GetObjects(id).Count > 0)
                    replaced.Add(element);
                else if (element.Parent is { } parent && ids.TryGetValue(parent, out NodeId parentId) && Map.GetObjects(parentId).Count > 0)
                    inserted.Add(element);
            }

            var units = new List<TextUnit>();
            foreach (ElementSyntax old in diff.Removed)
            {
                TextSpan span = old.Parent != null && oldToNew.TryGetValue(old.Parent, out ElementSyntax? parent) ? parent.NameSpan : default;
                units.Add(new TextUnit(new ElementRemovedAction(before.Ids[old]), null, span));
            }

            foreach ((_, ElementSyntax current) in diff.Moved)
                units.Add(new TextUnit(new ElementMovedAction(ids[current], ids[current.Parent!], current.Span.Start), ids[current], current.NameSpan));

            foreach (ElementSyntax current in replaced)
                units.Add(new TextUnit(new ElementReplacedAction(ids[current]), ids[current], current.NameSpan));

            foreach (ElementSyntax current in inserted)
                units.Add(new TextUnit(new ElementInsertedAction(ids[current.Parent!], current.Span.Start), ids[current], current.NameSpan));

            foreach ((ElementSyntax element, string name) in diff.ChangedAttributes)
            {
                if (!rebuilt.Contains(element))
                    units.Add(new TextUnit(new AttributeChangedAction(ids[element], name), ids[element], element.FindAttribute(name)?.Span ?? element.NameSpan));
            }

            return units;
        }
```

5. Add the name pass and the sync-state update (private, after `CreateUnits`):

```csharp
        /// <summary>
        /// Makes every named live element resolvable by its <c>x:Name</c> again. An element moved out of a container the
        /// same apply removed had its name unregistered with that container.
        /// </summary>
        private void SyncNames()
        {
            foreach (ElementSyntax element in syntax.Elements)
            {
                if (TreeMatcher.GetDirective(syntax, element, MarkupDirectives.Name) is not { } name || !ids.TryGetValue(element, out NodeId id))
                    continue;

                foreach ((object instance, MarkupLoadScope scope) in Map.GetObjects(id))
                {
                    if (instance is not UIElement live || scope.NameScope is not { } names || ReferenceEquals(names.Find(name), live))
                        continue;

                    names.Unregister(name);
                    names.Register(name, live);
                }
            }
        }

        private void UpdateSyncState()
        {
            bool inSync = IsInSync;
            if (inSync == reportedInSync && liveErrors.SequenceEqual(reportedErrors))
                return;

            reportedInSync = inSync;
            reportedErrors = [.. liveErrors];
            SyncStateChanged?.Invoke(this, EventArgs.Empty);
        }
```

6. At the end of `ApplyTextCore`, after raising `Changed`, call `UpdateSyncState();`.

In `MarkupEditor`, add after `ValidateAttributeName`:

```csharp
        private EditResult? RefuseWhileOutOfSync() =>
            document.IsInSync ? null : EditResult.Failure(default, "The markup has errors; fix them first.");
```

Then in `InsertElement`, `RemoveElement`, `MoveElement`, `SetAttribute` and `ClearAttribute` (not `BeginTransaction`), right after `document.ThrowIfDisposed();`, add:

```csharp
            if (RefuseWhileOutOfSync() is { } refused)
                return refused;
```

Add to each of those five methods' docs: `/// <para>Refused while <see cref="DesignDocument.IsInSync"/> is <see langword="false"/>: fix the markup first.</para>` inside its `<remarks>`. If a method has no `<remarks>` yet, add one.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design"`
Expected: PASS for `ApplyTextFailureTests` (Total 10 there) and every other design test.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (Total 1290, no failures), then `dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep "Warning(s)"` (81).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/ApplyTextFailureTests.cs
git commit -m "Keep the text when live pages can't follow it, and heal them later

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Text edits in `DesignDemo`

**Files:**
- Modify: `sources/Shared Samples/DesignDemo.cs`
- Test: `sources/IcyUI.Tests/Samples/DesignDemoTests.cs`

**Interfaces:**
- Consumes: `DesignDocument.ApplyText`, `IsInSync`, `LiveErrors` (Tasks 3–4).

`TextBox` has no multi-line mode, so the demo offers preset whole-text edits instead of an editable text box (plan deviation 1). Both hosts already show `DesignDemo`; nothing changes in them.

- [ ] **Step 1: Write the failing test**

Add to `DesignDemoTests`:

```csharp
        [Fact]
        public void Build_OffersTheTextEdits()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            var root = (StackPanel)DesignDemo.Build(configuration, "Airfool");

            Assert.Equal(8, ((StackPanel)root.Children[1]).Children.Count);
        }
```

- [ ] **Step 2: Run it to make sure it fails**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DesignDemoTests"`
Expected: FAIL: `Assert.Equal() Failure: Expected: 8, Actual: 5`.

- [ ] **Step 3: Add the text edits**

In `DesignDemo.Build`, after the line that adds the `Redo` button, add:

```csharp
            toolbar.Children.Add(CreateTextButton("Text: retitle", status, document, () =>
                document.Text.Replace("Edited live, saved as markup", "Changed through ApplyText", StringComparison.Ordinal)));
            toolbar.Children.Add(CreateTextButton("Text: break", status, document, () =>
            {
                // Drop the root's end tag: well-formedness breaks, the page keeps showing the last valid markup.
                int end = document.Text.LastIndexOf("</Border>", StringComparison.Ordinal);
                return end < 0 ? document.Text : document.Text[..end];
            }));
            toolbar.Children.Add(CreateTextButton("Text: restore", status, document, () => Markup));
```

Add the helper after `CreateButton`:

```csharp
        private static Button CreateTextButton(string label, TextBlock status, DesignDocument document, Func<string> nextText)
        {
            var button = new Button
            {
                Content = new TextBlock { Text = label },
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 0, 8, 0),
            };
            button.Click += (_, _) =>
            {
                document.ApplyText(nextText());
                status.Text = document.IsInSync
                    ? "In sync"
                    : $"Out of sync: {(document.LiveErrors.Count > 0 ? document.LiveErrors[0].Message : "the markup has errors")}";
            };
            return button;
        }
```

Update the class summary to mention the text edits: "…and applies whole-text edits through <see cref="DesignDocument.ApplyText"/>, showing whether the page is in sync."

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DesignDemoTests"`
Expected: PASS, Total 3.

- [ ] **Step 5: Build everything, run the whole suite, commit**

Run:
```bash
dotnet build "sources/IcyUI.sln" --no-incremental 2>&1 | grep -E "Warning\(s\)|Error\(s\)"
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"
```
Expected: 0 errors and 81 warnings (both sample hosts compile). Total **1291** (1250 + 4 + 13 + 13 + 10 + 1), no failures.

```bash
git add "sources/Shared Samples/DesignDemo.cs" sources/IcyUI.Tests/Samples/DesignDemoTests.cs
git commit -m "Add whole-text edits to DesignDemo

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 6: Hand over for review and Ivan's smoke test**

Run the whole-branch review of this phase's commits. Then give Ivan this checklist for both engines; don't claim any of it yourself:

1. Open **Design Demo**.
2. **Text: retitle**: the title text changes in place, and every other element (boxes, buttons) is the same instance; nothing flickers or resets.
3. **Text: break**: the page keeps showing the last valid state, and the status says "Out of sync: the markup has errors". The visual buttons (Bigger title…) are refused.
4. **Text: restore**: the page returns to the original, and the status says "In sync".
5. **Undo** repeatedly: the text steps back through every visual and text edit, and the page follows.

---

## Self-review notes

- **Spec coverage:**

| Spec section | Where it's implemented |
|---|---|
| API (`ApplyText`, `ReloadFromSource`, `IsInSync`, `LiveErrors`, `SyncStateChanged`) | Tasks 3–4 |
| Matcher | Task 2 |
| Units, ordering and ids | Task 3 |
| Partial failure and out-of-sync healing | Task 4 |
| Malformed text and the last valid tree | Tasks 3–4 |
| Visual edits refused while out of sync | Task 4 |
| Undo text items | Task 3 |
| Folded M4/M1/M3 | Task 1 |
| Root problem | Tasks 3–4 |
| Performance measurement | Task 3 |
| Demo | Task 5 |

- **Deviations** are listed at the top.
- **Known limitation:** a failed removal isn't tracked as out of sync (deviation 3).
