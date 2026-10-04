# Phase 10.1: Markup Design Document and Edit Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a tracked markup page be edited live (set/clear attributes, insert/remove/move elements, undo/redo) with every edit written back into the original markup text as a minimal change, so saving produces a clean diff.

**Architecture:** Core `IcyUI` gets a null-by-default `IMarkupLoadObserver` seam plus a public `IMarkupBuilder` interface over the loader's internals. A new `IcyUI.Design` assembly keeps the markup text as the source of truth, parses it into a span-preserving syntax tree, correlates syntax nodes with live objects through the loader's line info, and turns typed edit operations into text change sets that are mirrored onto the live tree through the builder.

**Tech Stack:** C# / .NET 10 (`net10.0`), `System.Xml.Linq` (core loader, unchanged), xUnit v2 (`IcyUI.Tests`), StyleCop.Analyzers.

**Spec:** `docs/superpowers/specs/2026-10-04-markup-design-document-design.md`

## Global Constraints

- Target framework comes from `sources/Directory.Build.props` (`net10.0`); never put `Version=` on a `PackageReference`; **no new packages** in this phase.
- `IcyUI` must not reference engine types; `IcyUI.Design` references **only** `IcyUI`.
- Every public API gets complete XML documentation (`<summary>`, `<param>`, `<returns>`, `<exception>`, `<see cref>`, `<see langword>`, `<para>`, `<list>` where useful).
- Match the surrounding style: two-line copyright header, block-scoped `namespace X { }`, StyleCop member order (fields → constructors → events → properties → methods; public → internal → private; static before instance; nested types last).
- The solution build has a **baseline of 82 warnings**. Record it in Task 1 Step 1 and never let it grow.
- Test strings that span lines use `.ReplaceLineEndings("\n")` (or `"\r\n"` on purpose), because the `.cs` files' own line endings vary by machine.
- `dotnet test` prints "Passed!" even when the test host crashes: **always check the Total count** in its summary.
- Commit after every task on `platform-independent`, ending the message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Live mirroring always uses the **mapped logical parent** from the markup tree, never `UIElement.Parent`: a templated `ContentControl`'s content is parented by its chrome/`ContentPresenter`, not by the control.

## Deviations from the spec (decided while planning)

1. `IMarkupLoadObserver.MemberApplied` gets an extra `IBinding? binding` parameter. `BindingExtension.ProvideValue` returns `MarkupValue.Unset`, so the binding has to be found through `IBindingTarget.Bindings` by the loader.
2. `IMarkupBuilder` gains `ParseFragment(text, namespaces)`, so the XML namespace rules (the predeclared `x` prefix) stay in one place in core. `ApplyAttribute` returns `void`; the observer records the result as for any load. `MarkupLoader` implements the interface explicitly.
3. The tree matcher maps nodes by **offset through the known change set** (plus explicit move hints) instead of name-LCS. Every 10.1 change set is known exactly, which makes this exact; the name-LCS matcher for arbitrary text edits moves to 10.2.
4. Moving an element into a parent that can't hold it fails atomically instead of rebuilding; a rebuild could only fail the same way.
5. `DesignDocument.Save()` resolves `SourcePath` through `DesignSession.SourcePathResolver`, because a `SourcePath` is often an asset name. `SaveAs(filePath)` writes anywhere.
6. Loading the same `SourcePath` twice shares one document only when the texts are identical; otherwise each load gets its own document.
7. Elements under a property element (`<ContentControl.Content>`, `<Grid.RowDefinitions>`, …) are opaque in 10.1, like styles, resources and templates.

## Review Focus

1. **Attribute values with entities, both quote characters, `<`, `&` or line breaks.** Expect the written text to be valid, escaped for the attribute's own quote char, and the live value to equal what was set. Test: Task 9, `Set_EscapesForTheExistingQuoteChar` and `Set_LineBreakSurvivesAsACharacterReference`.
2. **Prefixes declared on an ancestor** (`<ui:StackPanel xmlns:ui="…">` then inserting `<ui:Border/>`). Expect fragments to resolve them. Test: Task 8, `Insert_ResolvesAPrefixDeclaredOnAnAncestor`.
3. **CRLF documents** (every Windows-authored file). Expect inserted lines to use `\r\n` too. Test: Task 8, `Insert_KeepsCrlfLineEndings`.
4. **Pages the game changed at runtime**: a mapped element the game already detached, or runtime children between mapped ones. Expect the text edit to succeed and live mirroring to skip or anchor correctly, never throw. Tests: Task 8, `Remove_AnElementTheGameAlreadyDetached_StillEditsTheText` and `Insert_AnchorsAfterTheNearestMappedSibling`.
5. **Using a document after its session is disposed.** Expect `ObjectDisposedException`, not a `NullReferenceException` or a silent no-op. Test: Task 12, `Editor_AfterSessionDispose_Throws`.

---

### Task 1: `IcyUI.Design` project and the text model

**Files:**
- Create: `sources/IcyUI.Design/IcyUI.Design.csproj`
- Create: `sources/IcyUI.Design/Properties/AssemblyInfo.cs`
- Create: `sources/IcyUI.Design/Text/TextSpan.cs`, `TextChange.cs`, `TextChangeSet.cs`, `MarkupText.cs`, `LineMap.cs`
- Modify: `sources/IcyUI.sln` (add project), `sources/IcyUI.Tests/IcyUI.Tests.csproj` (reference it), `CLAUDE.md` (layout table row)
- Test: `sources/IcyUI.Tests/Design/Text/TextChangeSetTests.cs`, `sources/IcyUI.Tests/Design/Text/LineMapTests.cs`

**Interfaces:**
- Produces (namespace `Icy.Design.Text`):
  - `public readonly record struct TextSpan` with constructor `(int start, int length)`, `Start`, `Length`, `End`, `static FromBounds(int start, int end)`.
  - `public readonly record struct TextChange(TextSpan Span, string NewText)`.
  - `public sealed class TextChangeSet : IReadOnlyList<TextChange>` with `TextChangeSet(IEnumerable<TextChange>)`, `static Empty`, `string Apply(string text)`, `TextChangeSet Invert(string originalText)`, `int? MapPosition(int position)`.
  - `public sealed class MarkupText` with `MarkupText(string text, int version = 0)`, `Text`, `Version`, `MarkupText Apply(TextChangeSet)`.
  - `internal sealed class LineMap` with `LineMap(string text)`, `int LineCount`, `int ToOffset(int line, int column)`, `bool TryToOffset(int line, int column, out int offset)` (1-based line and column, as `IXmlLineInfo` reports them).

- [ ] **Step 1: Record the warning baseline**

Run: `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3`
Expected: `Build succeeded.` with `82 Warning(s)` (`NU1900` feed warnings are harmless noise). Note the exact count; every later build must not exceed it.

- [ ] **Step 2: Create the project**

`sources/IcyUI.Design/IcyUI.Design.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <GenerateDocumentationFile>True</GenerateDocumentationFile>
    <PackageLicenseFile>LICENSE</PackageLicenseFile>
    <PackageRequireLicenseAcceptance>True</PackageRequireLicenseAcceptance>
    <EnforceCodeStyleInBuild>True</EnforceCodeStyleInBuild>
    <RootNamespace>Icy.Design</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="StyleCop.Analyzers">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>
  <ItemGroup>
    <None Include="..\..\LICENSE">
      <Pack>True</Pack>
      <PackagePath>\</PackagePath>
    </None>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\IcyUI\IcyUI.csproj" />
  </ItemGroup>
</Project>
```

`sources/IcyUI.Design/Properties/AssemblyInfo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("IcyUI.Tests")]
```

Run:
```bash
dotnet sln "sources/IcyUI.sln" add "sources/IcyUI.Design/IcyUI.Design.csproj"
dotnet add "sources/IcyUI.Tests/IcyUI.Tests.csproj" reference "sources/IcyUI.Design/IcyUI.Design.csproj"
```

In `CLAUDE.md`, add this row to the layout table right after the `IcyUI` row:

```markdown
| `IcyUI.Design` | Dev-time tooling | Markup design document and edit engine (Phase 10). References only `IcyUI`; games never need it at runtime. |
```

- [ ] **Step 3: Write the failing tests**

`sources/IcyUI.Tests/Design/Text/TextChangeSetTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;
using Xunit;

namespace Icy.Tests.Design.Text
{
    public class TextChangeSetTests
    {
        private const string Original = "Hello world!";

        [Fact]
        public void Apply_ReplacesInsertsAndDeletesInOnePass()
        {
            Assert.Equal(">> Hello there", CreateSet().Apply(Original));
        }

        [Fact]
        public void Changes_AreSortedByStart()
        {
            Assert.Equal([0, 6, 11], CreateSet().Select(x => x.Span.Start));
        }

        [Fact]
        public void Invert_RestoresTheOriginalText()
        {
            TextChangeSet set = CreateSet();
            string changed = set.Apply(Original);

            Assert.Equal(Original, set.Invert(Original).Apply(changed));
        }

        [Fact]
        public void Constructor_RejectsOverlappingChanges()
        {
            Assert.Throws<ArgumentException>(() => new TextChangeSet(
            [
                new TextChange(new TextSpan(0, 5), "a"),
                new TextChange(new TextSpan(3, 4), "b"),
            ]));
        }

        [Fact]
        public void Constructor_RejectsTwoInsertionsAtTheSameOffset()
        {
            Assert.Throws<ArgumentException>(() => new TextChangeSet(
            [
                new TextChange(new TextSpan(2, 0), "a"),
                new TextChange(new TextSpan(2, 0), "b"),
            ]));
        }

        [Fact]
        public void Apply_RejectsAChangeOutsideTheText()
        {
            var set = new TextChangeSet([new TextChange(new TextSpan(10, 5), "x")]);

            Assert.Throws<ArgumentException>(() => set.Apply("short"));
        }

        // Insert "abc" at 2, replace [5,8) with "X", delete [10,12).
        [Theory]
        [InlineData(0, 0)]
        [InlineData(2, 5)]
        [InlineData(4, 7)]
        [InlineData(5, null)]
        [InlineData(6, null)]
        [InlineData(8, 9)]
        [InlineData(10, null)]
        [InlineData(12, 11)]
        public void MapPosition_ShiftsSurvivorsAndDropsReplacedPositions(int position, int? expected)
        {
            var set = new TextChangeSet(
            [
                new TextChange(new TextSpan(2, 0), "abc"),
                new TextChange(new TextSpan(5, 3), "X"),
                new TextChange(new TextSpan(10, 2), string.Empty),
            ]);

            Assert.Equal(expected, set.MapPosition(position));
        }

        private static TextChangeSet CreateSet() => new(
        [
            new TextChange(new TextSpan(6, 5), "there"),
            new TextChange(new TextSpan(0, 0), ">> "),
            new TextChange(new TextSpan(11, 1), string.Empty),
        ]);
    }
}
```

`sources/IcyUI.Tests/Design/Text/LineMapTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;
using Xunit;

namespace Icy.Tests.Design.Text
{
    public class LineMapTests
    {
        [Theory]
        [InlineData("a\nbc\nd", 3, 1, 5)]
        [InlineData("a\r\nbc\r\nd", 3, 1, 7)]
        [InlineData("a\rbc\rd", 3, 1, 5)]
        [InlineData("a\nbc", 2, 2, 3)]
        [InlineData("\tx", 1, 2, 1)]
        public void ToOffset_CountsEveryLineBreakStyleOnce(string text, int line, int column, int expected)
        {
            Assert.Equal(expected, new LineMap(text).ToOffset(line, column));
        }

        [Fact]
        public void TryToOffset_RejectsALineOutsideTheText()
        {
            Assert.False(new LineMap("a\nb").TryToOffset(3, 1, out _));
        }
    }
}
```

- [ ] **Step 4: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.Text"`
Expected: the build FAILS with `CS0246` (`TextChangeSet`, `TextSpan`, `LineMap` not found).

- [ ] **Step 5: Implement the text model**

`sources/IcyUI.Design/Text/TextSpan.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Text
{
    /// <summary>
    /// Represents a contiguous range of characters in a markup text.
    /// </summary>
    public readonly record struct TextSpan
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextSpan"/> struct.
        /// </summary>
        /// <param name="start">The offset of the first character in the span.</param>
        /// <param name="length">The number of characters in the span.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="start"/> or <paramref name="length"/> is negative.</exception>
        public TextSpan(int start, int length)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(start);
            ArgumentOutOfRangeException.ThrowIfNegative(length);
            Start = start;
            Length = length;
        }

        /// <summary>
        /// Gets the offset of the first character in the span.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// Gets the number of characters in the span.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the offset just past the last character in the span.
        /// </summary>
        public int End => Start + Length;

        /// <summary>
        /// Creates a span from its start and end offsets.
        /// </summary>
        /// <param name="start">The offset of the first character in the span.</param>
        /// <param name="end">The offset just past the last character in the span.</param>
        /// <returns>The span covering <c>[start, end)</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="end"/> is less than <paramref name="start"/>.</exception>
        public static TextSpan FromBounds(int start, int end) => new(start, end - start);

        /// <inheritdoc/>
        public override string ToString() => $"[{Start}..{End})";
    }
}
```

`sources/IcyUI.Design/Text/TextChange.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Text
{
    /// <summary>
    /// Describes one replacement in a markup text: the characters in <see cref="Span"/> are replaced by
    /// <see cref="NewText"/>.
    /// </summary>
    /// <param name="Span">The range of the original text being replaced. A zero-length span is a pure insertion.</param>
    /// <param name="NewText">The replacement text. An empty string is a pure deletion.</param>
    public readonly record struct TextChange(TextSpan Span, string NewText);
}
```

`sources/IcyUI.Design/Text/TextChangeSet.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Text;

namespace Icy.Design.Text
{
    /// <summary>
    /// An ordered set of non-overlapping <see cref="TextChange"/>s, all expressed against the same original text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The changes are kept sorted by start offset. Two changes may touch, but they can't overlap, and at most one
    /// pure insertion may sit at any offset, so applying the set is unambiguous.
    /// </para>
    /// <para>
    /// A change set is the unit every edit of a design document produces, and the unit a future text editor
    /// receives to update its buffer without losing its caret or its own undo history.
    /// </para>
    /// </remarks>
    public sealed class TextChangeSet : IReadOnlyList<TextChange>
    {
        private readonly TextChange[] changes;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextChangeSet"/> class.
        /// </summary>
        /// <param name="changes">The changes, in any order.</param>
        /// <exception cref="ArgumentNullException"><paramref name="changes"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// A change has <see langword="null"/> new text, two changes overlap, or two insertions share an offset.
        /// </exception>
        public TextChangeSet(IEnumerable<TextChange> changes)
        {
            ArgumentNullException.ThrowIfNull(changes);

            TextChange[] sorted = [.. changes];
            Array.Sort(sorted, static (a, b) => a.Span.Start != b.Span.Start
                ? a.Span.Start.CompareTo(b.Span.Start)
                : a.Span.Length.CompareTo(b.Span.Length));

            for (int i = 0; i < sorted.Length; i++)
            {
                if (sorted[i].NewText == null)
                    throw new ArgumentException("A change's new text can't be null.", nameof(changes));

                if (i == 0)
                    continue;

                TextChange previous = sorted[i - 1];
                bool overlaps = previous.Span.End > sorted[i].Span.Start;
                bool sharedInsertion = previous.Span.Length == 0 && sorted[i].Span.Length == 0 && previous.Span.Start == sorted[i].Span.Start;
                if (overlaps || sharedInsertion)
                    throw new ArgumentException("Changes in a set must not overlap.", nameof(changes));
            }

            this.changes = sorted;
        }

        /// <summary>
        /// Gets a change set that changes nothing.
        /// </summary>
        public static TextChangeSet Empty { get; } = new([]);

        /// <inheritdoc/>
        public int Count => changes.Length;

        /// <inheritdoc/>
        public TextChange this[int index] => changes[index];

        /// <summary>
        /// Applies every change to <paramref name="text"/> in one pass.
        /// </summary>
        /// <param name="text">The original text the changes were expressed against.</param>
        /// <returns>The changed text.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">A change lies outside <paramref name="text"/>.</exception>
        public string Apply(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (changes.Length == 0)
                return text;

            if (changes[^1].Span.End > text.Length)
                throw new ArgumentException("A change lies outside the text.", nameof(text));

            int delta = 0;
            foreach (TextChange change in changes)
                delta += change.NewText.Length - change.Span.Length;

            var builder = new StringBuilder(text.Length + delta);
            int copied = 0;
            foreach (TextChange change in changes)
            {
                builder.Append(text, copied, change.Span.Start - copied);
                builder.Append(change.NewText);
                copied = change.Span.End;
            }

            builder.Append(text, copied, text.Length - copied);
            return builder.ToString();
        }

        /// <summary>
        /// Creates the change set that turns the changed text back into <paramref name="originalText"/>.
        /// </summary>
        /// <param name="originalText">The text this set was expressed against.</param>
        /// <returns>The inverse change set, expressed against the changed text.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="originalText"/> is <see langword="null"/>.</exception>
        public TextChangeSet Invert(string originalText)
        {
            ArgumentNullException.ThrowIfNull(originalText);

            var inverse = new TextChange[changes.Length];
            int delta = 0;
            for (int i = 0; i < changes.Length; i++)
            {
                TextChange change = changes[i];
                inverse[i] = new TextChange(
                    new TextSpan(change.Span.Start + delta, change.NewText.Length),
                    originalText.Substring(change.Span.Start, change.Span.Length));
                delta += change.NewText.Length - change.Span.Length;
            }

            return new TextChangeSet(inverse);
        }

        /// <summary>
        /// Maps an offset in the original text to the same character's offset in the changed text.
        /// </summary>
        /// <param name="position">An offset in the original text.</param>
        /// <returns>
        /// The offset in the changed text, or <see langword="null"/> when the character at
        /// <paramref name="position"/> was replaced or deleted. An insertion exactly at <paramref name="position"/>
        /// counts as coming before it, so the character shifts past the inserted text.
        /// </returns>
        public int? MapPosition(int position)
        {
            int delta = 0;
            foreach (TextChange change in changes)
            {
                if (change.Span.Length == 0)
                {
                    if (change.Span.Start > position)
                        break;

                    delta += change.NewText.Length;
                    continue;
                }

                if (change.Span.End <= position)
                {
                    delta += change.NewText.Length - change.Span.Length;
                    continue;
                }

                if (change.Span.Start <= position)
                    return null;

                break;
            }

            return position + delta;
        }

        /// <inheritdoc/>
        public IEnumerator<TextChange> GetEnumerator() => ((IEnumerable<TextChange>)changes).GetEnumerator();

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
```

`sources/IcyUI.Design/Text/MarkupText.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Text
{
    /// <summary>
    /// An immutable snapshot of a markup document's text, with a version that grows by one with every change.
    /// </summary>
    public sealed class MarkupText
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MarkupText"/> class.
        /// </summary>
        /// <param name="text">The markup text.</param>
        /// <param name="version">The snapshot's version.</param>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="version"/> is negative.</exception>
        public MarkupText(string text, int version = 0)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentOutOfRangeException.ThrowIfNegative(version);
            Text = text;
            Version = version;
        }

        /// <summary>
        /// Gets the markup text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the snapshot's version.
        /// </summary>
        public int Version { get; }

        /// <summary>
        /// Applies <paramref name="changes"/> and returns the next snapshot.
        /// </summary>
        /// <param name="changes">The changes, expressed against <see cref="Text"/>.</param>
        /// <returns>A snapshot with the changed text and the next version.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="changes"/> is <see langword="null"/>.</exception>
        public MarkupText Apply(TextChangeSet changes)
        {
            ArgumentNullException.ThrowIfNull(changes);
            return new MarkupText(changes.Apply(Text), Version + 1);
        }

        /// <inheritdoc/>
        public override string ToString() => Text;
    }
}
```

`sources/IcyUI.Design/Text/LineMap.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Text
{
    /// <summary>
    /// Converts the 1-based (line, column) positions <see cref="System.Xml.IXmlLineInfo"/> reports into offsets.
    /// </summary>
    /// <remarks>
    /// XML treats <c>\r\n</c>, <c>\r</c> and <c>\n</c> each as one line break, and counts a column per UTF-16
    /// character (a tab is one column), so this does the same.
    /// </remarks>
    internal sealed class LineMap
    {
        private readonly int[] lineStarts;

        public LineMap(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var starts = new List<int> { 0 };
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    starts.Add(i + 1);
                }
                else if (text[i] == '\n')
                {
                    starts.Add(i + 1);
                }
            }

            lineStarts = [.. starts];
        }

        public int LineCount => lineStarts.Length;

        public int ToOffset(int line, int column) =>
            TryToOffset(line, column, out int offset)
                ? offset
                : throw new ArgumentOutOfRangeException(nameof(line), $"Line {line}, column {column} is outside the text.");

        public bool TryToOffset(int line, int column, out int offset)
        {
            offset = 0;
            if (line < 1 || line > lineStarts.Length || column < 1)
                return false;

            offset = lineStarts[line - 1] + column - 1;
            return true;
        }
    }
}
```

- [ ] **Step 6: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.Text"`
Expected: PASS, Total 20.

- [ ] **Step 7: Build the solution and check warnings**

Run: `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3`
Expected: `Build succeeded.`, with the warning count equal to the Step 1 baseline.

- [ ] **Step 8: Commit**

```bash
git add sources/IcyUI.Design sources/IcyUI.sln sources/IcyUI.Tests/IcyUI.Tests.csproj sources/IcyUI.Tests/Design CLAUDE.md
git commit -m "Add IcyUI.Design with the markup text model

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Span-preserving markup syntax tree

**Files:**
- Create in `sources/IcyUI.Design/Syntax/`: `MarkupSyntaxNode.cs`, `ElementSyntax.cs`, `AttributeSyntax.cs`, `TextSyntax.cs`, `CommentSyntax.cs`, `CDataSyntax.cs`, `ProcessingInstructionSyntax.cs`, `Diagnostic.cs`, `DocumentSyntax.cs`, `MarkupParser.cs`, `MarkupEscaping.cs`
- Test: `sources/IcyUI.Tests/Design/Syntax/MarkupParserTests.cs`, `sources/IcyUI.Tests/Design/Syntax/SyntaxAssert.cs`

**Interfaces:**
- Consumes: `TextSpan`, `LineMap` (Task 1).
- Produces (namespace `Icy.Design.Syntax`):
  - `public abstract class MarkupSyntaxNode` with `TextSpan Span`, `ElementSyntax? Parent`.
  - `public sealed class ElementSyntax : MarkupSyntaxNode` with `Name`, `Prefix`, `LocalName`, `NameSpan`, `Attributes`, `StartTagEnd`, `StartTagSpan`, `IsSelfClosing`, `Content`, `EndTagSpan`, `IsMissingEndTag`, `IsPropertyElement`, `Elements`, `ContentElements`, `FindAttribute(string)`, `DescendantsAndSelf()`, `IsAncestorOf(ElementSyntax)`.
  - `public sealed class AttributeSyntax : MarkupSyntaxNode` with `Name`, `Prefix`, `LocalName`, `NameSpan`, `ValueSpan`, `Quote`, `Value`, `IsMissingValue`, `IsNamespaceDeclaration`.
  - `TextSyntax`, `CommentSyntax`, `CDataSyntax`, `ProcessingInstructionSyntax` (span only).
  - `public readonly record struct Diagnostic(TextSpan Span, string Message)`.
  - `public sealed class DocumentSyntax` with `static Parse(string)`, `Text`, `Nodes`, `Root`, `Diagnostics`, `HasErrors`, `Elements`, `FindElementAt(int start)`, `GetNamespacesInScope(ElementSyntax, bool includeSelf = false)`, `ResolvePrefix(ElementSyntax, string prefix)`; internal `FindElementByNameStart(int)`, `FindAttributeByNameStart(int)`.
  - `internal static class MarkupEscaping` with `DecodeAttributeValue(string text, TextSpan valueSpan)`, `EscapeAttributeValue(string value, char quote)`.
  - `internal sealed class MarkupParser` with `static DocumentSyntax Parse(string)`, `static bool IsNameStartChar(char)`, `static bool IsNameChar(char)`, `static bool IsValidName(string)`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/Syntax/SyntaxAssert.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Xunit;

namespace Icy.Tests.Design.Syntax
{
    /// <summary>
    /// Asserts the parser's core guarantee: every character of the text belongs to exactly one node, in order, so
    /// the tree reproduces the text byte for byte.
    /// </summary>
    internal static class SyntaxAssert
    {
        public static void Tiles(DocumentSyntax document)
        {
            AssertSequence(document.Nodes, 0, document.Text.Length);
            foreach (MarkupSyntaxNode node in document.Nodes)
                AssertElement(document.Text, node);
        }

        private static void AssertElement(string text, MarkupSyntaxNode node)
        {
            if (node is not ElementSyntax element)
                return;

            Assert.Equal(element.Span.Start + 1, element.NameSpan.Start);
            Assert.Equal(element.Name, text.Substring(element.NameSpan.Start, element.NameSpan.Length));

            int cursor = element.NameSpan.End;
            foreach (AttributeSyntax attribute in element.Attributes)
            {
                Assert.True(attribute.Span.Start >= cursor, $"Attribute '{attribute.Name}' starts before the previous one ends.");
                Assert.Equal(attribute.Name, text.Substring(attribute.NameSpan.Start, attribute.NameSpan.Length));
                Assert.Same(element, attribute.Parent);
                cursor = attribute.Span.End;
            }

            Assert.True(element.StartTagEnd >= cursor);
            int contentEnd = element.EndTagSpan?.Start ?? element.Span.End;
            AssertSequence(element.Content, element.StartTagEnd, contentEnd);
            Assert.Equal(element.Span.End, element.EndTagSpan?.End ?? contentEnd);

            foreach (MarkupSyntaxNode child in element.Content)
            {
                Assert.Same(element, child.Parent);
                AssertElement(text, child);
            }
        }

        private static void AssertSequence(IReadOnlyList<MarkupSyntaxNode> nodes, int start, int end)
        {
            int cursor = start;
            foreach (MarkupSyntaxNode node in nodes)
            {
                Assert.Equal(cursor, node.Span.Start);
                cursor = node.Span.End;
            }

            Assert.Equal(end, cursor);
        }
    }
}
```

`sources/IcyUI.Tests/Design/Syntax/MarkupParserTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.Markup;
using Icy.SharedSamples;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Design.Syntax
{
    public class MarkupParserTests
    {
        public static TheoryData<string, string> SampleDocuments => new()
        {
            { nameof(MarkupDemo), MarkupDemo.Markup },
            { nameof(ControlTemplateDemo), ControlTemplateDemo.Markup },
            { "DefaultTheme", ReadDefaultTheme() },
        };

        public static TheoryData<string, string> PositionDocuments
        {
            get
            {
                TheoryData<string, string> data = SampleDocuments;
                data.Add("CrlfAndTabs", "<StackPanel\r\n\tOrientation=\"Horizontal\">\r\n\t<Border\r\n\t\tWidth=\"1\"\r\n\t\tx:Name=\"b\"/>\r\n</StackPanel>");
                return data;
            }
        }

        public static TheoryData<string> MalformedDocuments => new()
        {
            "<A>",
            "<A><B></A>",
            "<A></B></A>",
            "<A x=\"1></A>",
            "<A x></A>",
            "<A x=1/>",
            "<A>a < b</A>",
            "<A><!-- open",
            "<A/><B/>",
            string.Empty,
            "<!DOCTYPE A><A/>",
            "<A",
            "</A>",
        };

        [Fact]
        public void Parse_RecordsElementAndAttributeSpans()
        {
            const string text = "<Border Width=\"10\" x:Name='b'>\n  <TextBlock>Hi</TextBlock>\n</Border>";

            DocumentSyntax document = DocumentSyntax.Parse(text);

            Assert.Empty(document.Diagnostics);
            ElementSyntax root = Assert.IsType<ElementSyntax>(document.Root);
            Assert.Equal("Border", root.Name);
            Assert.Equal(new TextSpan(1, 6), root.NameSpan);

            AttributeSyntax width = root.Attributes[0];
            Assert.Equal("Width", width.Name);
            Assert.Equal(new TextSpan(15, 2), width.ValueSpan);
            Assert.Equal('"', width.Quote);
            Assert.Equal("10", width.Value);

            AttributeSyntax name = root.Attributes[1];
            Assert.Equal("x", name.Prefix);
            Assert.Equal("Name", name.LocalName);
            Assert.Equal('\'', name.Quote);
            Assert.Equal("b", name.Value);

            Assert.Equal(30, root.StartTagEnd);
            ElementSyntax child = Assert.Single(root.Elements);
            Assert.Equal(33, child.Span.Start);
            Assert.Same(child, document.FindElementAt(33));
            Assert.Equal(text.Length, root.EndTagSpan!.Value.End);
            SyntaxAssert.Tiles(document);
        }

        [Fact]
        public void Parse_DecodesEntitiesAndNormalizesWhitespaceInValues()
        {
            DocumentSyntax document = DocumentSyntax.Parse("<A T=\"a &amp; b&#10;c&#x41;\td\"/>");

            Assert.Equal("a & b\ncA d", document.Root!.Attributes[0].Value);
        }

        [Fact]
        public void Parse_KeepsCommentsCDataAndProcessingInstructionsAsNodes()
        {
            DocumentSyntax document = DocumentSyntax.Parse("<?xml-stylesheet x?><!-- c --><A><![CDATA[<b>]]></A>");

            Assert.Empty(document.Diagnostics);
            Assert.IsType<ProcessingInstructionSyntax>(document.Nodes[0]);
            Assert.IsType<CommentSyntax>(document.Nodes[1]);
            Assert.IsType<CDataSyntax>(Assert.Single(document.Root!.Content));
            SyntaxAssert.Tiles(document);
        }

        [Theory]
        [MemberData(nameof(SampleDocuments))]
        public void RoundTrip_SampleDocumentsParseCleanlyAndTile(string name, string text)
        {
            DocumentSyntax document = DocumentSyntax.Parse(text);

            Assert.True(document.Diagnostics.Count == 0, $"{name}: {string.Join("; ", document.Diagnostics)}");
            SyntaxAssert.Tiles(document);
        }

        // Pins the convention the design layer's correlation relies on: XmlReader reports an element's and an
        // attribute's (line, column) at the first character of its name.
        [Theory]
        [MemberData(nameof(PositionDocuments))]
        public void Positions_MatchXmlReaderLineInfo(string name, string text)
        {
            DocumentSyntax document = DocumentSyntax.Parse(text);
            var lines = new LineMap(text);
            XDocument xml = ParseLikeTheLoader(text);

            foreach (XElement element in xml.Root!.DescendantsAndSelf())
            {
                var info = (IXmlLineInfo)element;
                ElementSyntax? found = document.FindElementByNameStart(lines.ToOffset(info.LineNumber, info.LinePosition));
                Assert.True(found != null, $"{name}: no element at {info.LineNumber}:{info.LinePosition}");
                Assert.Equal(element.Name.LocalName, found!.LocalName);

                foreach (XAttribute attribute in element.Attributes())
                {
                    var attributeInfo = (IXmlLineInfo)attribute;
                    AttributeSyntax? match = document.FindAttributeByNameStart(lines.ToOffset(attributeInfo.LineNumber, attributeInfo.LinePosition));
                    Assert.True(match != null, $"{name}: no attribute at {attributeInfo.LineNumber}:{attributeInfo.LinePosition}");
                    Assert.Equal(attribute.Name.LocalName, match!.LocalName);
                }
            }
        }

        [Theory]
        [MemberData(nameof(MalformedDocuments))]
        public void Malformed_NeverThrowsReportsAndStillTiles(string text)
        {
            DocumentSyntax document = DocumentSyntax.Parse(text);

            Assert.NotEmpty(document.Diagnostics);
            SyntaxAssert.Tiles(document);
        }

        [Fact]
        public void GetNamespacesInScope_CollectsAncestorDeclarationsInnermostFirst()
        {
            DocumentSyntax document = DocumentSyntax.Parse("<A xmlns:ui=\"u1\" xmlns=\"d\"><B xmlns:ui=\"u2\"><C xmlns:z=\"z\"/></B></A>");
            ElementSyntax c = document.Elements.Single(x => x.Name == "C");

            IReadOnlyDictionary<string, string> inherited = document.GetNamespacesInScope(c);
            IReadOnlyDictionary<string, string> all = document.GetNamespacesInScope(c, includeSelf: true);

            Assert.Equal("u2", inherited["ui"]);
            Assert.Equal("d", inherited[string.Empty]);
            Assert.False(inherited.ContainsKey("z"));
            Assert.Equal("z", all["z"]);
            Assert.Equal(MarkupNamespaces.Directives, document.ResolvePrefix(c, "x"));
        }

        [Theory]
        [InlineData('"')]
        [InlineData('\'')]
        public void EscapeAttributeValue_RoundTripsThroughTheParser(char quote)
        {
            const string value = "it's \"q\" <b> & c\n\t";
            string escaped = MarkupEscaping.EscapeAttributeValue(value, quote);

            DocumentSyntax document = DocumentSyntax.Parse($"<A T={quote}{escaped}{quote}/>");

            Assert.Empty(document.Diagnostics);
            Assert.Equal(value, document.Root!.Attributes[0].Value);
        }

        private static XDocument ParseLikeTheLoader(string text)
        {
            var nameTable = new NameTable();
            var namespaces = new XmlNamespaceManager(nameTable);
            namespaces.AddNamespace("x", MarkupNamespaces.Directives);
            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };
            using XmlReader reader = XmlReader.Create(new StringReader(text), settings, new XmlParserContext(nameTable, namespaces, null, XmlSpace.None));
            return XDocument.Load(reader, LoadOptions.SetLineInfo);
        }

        private static string ReadDefaultTheme()
        {
            Assembly assembly = typeof(UIElement).Assembly;
            string resource = assembly.GetManifestResourceNames().Single(x => x.EndsWith("DefaultTheme.xml", StringComparison.Ordinal));
            using Stream stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.Syntax"`
Expected: the build FAILS with `CS0246`/`CS0234` (`Icy.Design.Syntax` types not found).

- [ ] **Step 3: Implement the node types**

`sources/IcyUI.Design/Syntax/MarkupSyntaxNode.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// The base class of every node in a markup syntax tree.
    /// </summary>
    /// <remarks>
    /// Nodes are immutable once <see cref="DocumentSyntax.Parse(string)"/> returns. Every node carries the exact
    /// range of source text it came from, delimiters included, so an edit can be expressed as a minimal
    /// <see cref="TextChange"/>.
    /// </remarks>
    public abstract class MarkupSyntaxNode
    {
        private protected MarkupSyntaxNode(TextSpan span)
        {
            Span = span;
        }

        /// <summary>
        /// Gets the range of source text this node covers, including its markup delimiters.
        /// </summary>
        public TextSpan Span { get; internal set; }

        /// <summary>
        /// Gets the element this node belongs to: the owner of an attribute, or the element whose content holds this
        /// node. <see langword="null"/> for a top-level node.
        /// </summary>
        public ElementSyntax? Parent { get; internal set; }
    }
}
```

`sources/IcyUI.Design/Syntax/TextSyntax.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A run of character data: element text, indentation whitespace, or characters the parser couldn't place
    /// anywhere else (reported with a <see cref="Diagnostic"/>).
    /// </summary>
    public sealed class TextSyntax : MarkupSyntaxNode
    {
        internal TextSyntax(TextSpan span)
            : base(span)
        {
        }
    }
}
```

`sources/IcyUI.Design/Syntax/CommentSyntax.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A <c>&lt;!-- ... --&gt;</c> comment.
    /// </summary>
    public sealed class CommentSyntax : MarkupSyntaxNode
    {
        internal CommentSyntax(TextSpan span)
            : base(span)
        {
        }
    }
}
```

`sources/IcyUI.Design/Syntax/CDataSyntax.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A <c>&lt;![CDATA[ ... ]]&gt;</c> section.
    /// </summary>
    public sealed class CDataSyntax : MarkupSyntaxNode
    {
        internal CDataSyntax(TextSpan span)
            : base(span)
        {
        }
    }
}
```

`sources/IcyUI.Design/Syntax/ProcessingInstructionSyntax.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A <c>&lt;? ... ?&gt;</c> processing instruction, or an unsupported <c>&lt;!DOCTYPE ...&gt;</c> declaration
    /// kept verbatim.
    /// </summary>
    public sealed class ProcessingInstructionSyntax : MarkupSyntaxNode
    {
        internal ProcessingInstructionSyntax(TextSpan span)
            : base(span)
        {
        }
    }
}
```

`sources/IcyUI.Design/Syntax/Diagnostic.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// Describes a problem in markup text, or a reason an edit couldn't be made.
    /// </summary>
    /// <param name="Span">The range of text the problem is about.</param>
    /// <param name="Message">A human-readable description of the problem.</param>
    public readonly record struct Diagnostic(TextSpan Span, string Message)
    {
        /// <inheritdoc/>
        public override string ToString() => $"{Span}: {Message}";
    }
}
```

`sources/IcyUI.Design/Syntax/AttributeSyntax.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// An attribute in an element's start tag: <c>Name="Value"</c>.
    /// </summary>
    public sealed class AttributeSyntax : MarkupSyntaxNode
    {
        internal AttributeSyntax(TextSpan span, string name, TextSpan nameSpan, TextSpan valueSpan, char quote, string value, bool isMissingValue)
            : base(span)
        {
            Name = name;
            NameSpan = nameSpan;
            ValueSpan = valueSpan;
            Quote = quote;
            Value = value;
            IsMissingValue = isMissingValue;
        }

        /// <summary>
        /// Gets the attribute's name as written, prefix included (<c>x:Name</c>, <c>Grid.Row</c>).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the part of <see cref="Name"/> before the first <c>:</c>, or an empty string when there is none.
        /// </summary>
        public string Prefix => Name.IndexOf(':', StringComparison.Ordinal) is int colon and > 0 ? Name[..colon] : string.Empty;

        /// <summary>
        /// Gets the part of <see cref="Name"/> after the first <c>:</c>, or the whole name when there is none.
        /// </summary>
        public string LocalName => Name.IndexOf(':', StringComparison.Ordinal) is int colon and > 0 ? Name[(colon + 1)..] : Name;

        /// <summary>
        /// Gets the range of the name.
        /// </summary>
        public TextSpan NameSpan { get; }

        /// <summary>
        /// Gets the range of the value, quotes excluded. Empty, right after the name, when the value is missing.
        /// </summary>
        public TextSpan ValueSpan { get; }

        /// <summary>
        /// Gets the quote character around the value: <c>"</c>, <c>'</c>, or <c>\0</c> when the value is unquoted
        /// or missing.
        /// </summary>
        public char Quote { get; }

        /// <summary>
        /// Gets the value as an XML reader would report it: entities and character references decoded, and literal
        /// tabs and line breaks normalized to spaces.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Gets a value indicating whether the attribute has no <c>=value</c> part at all.
        /// </summary>
        public bool IsMissingValue { get; }

        /// <summary>
        /// Gets a value indicating whether this is an <c>xmlns</c> or <c>xmlns:prefix</c> declaration.
        /// </summary>
        public bool IsNamespaceDeclaration => Name == "xmlns" || Name.StartsWith("xmlns:", StringComparison.Ordinal);
    }
}
```

`sources/IcyUI.Design/Syntax/ElementSyntax.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// An element: its start tag with attributes, its content, and its end tag.
    /// </summary>
    public sealed class ElementSyntax : MarkupSyntaxNode
    {
        private IReadOnlyList<AttributeSyntax> attributes = [];
        private IReadOnlyList<MarkupSyntaxNode> content = [];

        internal ElementSyntax(int start, string name, TextSpan nameSpan)
            : base(new TextSpan(start, 0))
        {
            Name = name;
            NameSpan = nameSpan;
        }

        /// <summary>
        /// Gets the element's name as written, prefix included (<c>ui:Border</c>, <c>Grid.RowDefinitions</c>).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the part of <see cref="Name"/> before the first <c>:</c>, or an empty string when there is none.
        /// </summary>
        public string Prefix => Name.IndexOf(':', StringComparison.Ordinal) is int colon and > 0 ? Name[..colon] : string.Empty;

        /// <summary>
        /// Gets the part of <see cref="Name"/> after the first <c>:</c>, or the whole name when there is none.
        /// </summary>
        public string LocalName => Name.IndexOf(':', StringComparison.Ordinal) is int colon and > 0 ? Name[(colon + 1)..] : Name;

        /// <summary>
        /// Gets the range of the name in the start tag.
        /// </summary>
        public TextSpan NameSpan { get; }

        /// <summary>
        /// Gets the attributes, in source order.
        /// </summary>
        public IReadOnlyList<AttributeSyntax> Attributes => attributes;

        /// <summary>
        /// Gets the offset just past the start tag's closing <c>&gt;</c> or <c>/&gt;</c>.
        /// </summary>
        public int StartTagEnd { get; internal set; }

        /// <summary>
        /// Gets the range of the start tag.
        /// </summary>
        public TextSpan StartTagSpan => TextSpan.FromBounds(Span.Start, StartTagEnd);

        /// <summary>
        /// Gets a value indicating whether the element is written as <c>&lt;Name/&gt;</c>.
        /// </summary>
        public bool IsSelfClosing { get; internal set; }

        /// <summary>
        /// Gets the nodes between the start and end tags, in source order, whitespace included.
        /// </summary>
        public IReadOnlyList<MarkupSyntaxNode> Content => content;

        /// <summary>
        /// Gets the range of the end tag, or <see langword="null"/> for a self-closing element or one whose end tag
        /// is missing.
        /// </summary>
        public TextSpan? EndTagSpan { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the element was never properly closed.
        /// </summary>
        public bool IsMissingEndTag { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether this is a property element such as <c>&lt;Grid.RowDefinitions&gt;</c>.
        /// </summary>
        public bool IsPropertyElement => LocalName.Contains('.', StringComparison.Ordinal);

        /// <summary>
        /// Gets the child elements, property elements included.
        /// </summary>
        public IEnumerable<ElementSyntax> Elements
        {
            get
            {
                foreach (MarkupSyntaxNode node in content)
                {
                    if (node is ElementSyntax element)
                        yield return element;
                }
            }
        }

        /// <summary>
        /// Gets the child elements that are content, excluding property elements.
        /// </summary>
        public IEnumerable<ElementSyntax> ContentElements => Elements.Where(x => !x.IsPropertyElement);

        /// <summary>
        /// Finds an attribute by its name as written.
        /// </summary>
        /// <param name="name">The attribute name, prefix included.</param>
        /// <returns>The attribute, or <see langword="null"/> when the element has none of that name.</returns>
        public AttributeSyntax? FindAttribute(string name)
        {
            foreach (AttributeSyntax attribute in attributes)
            {
                if (string.Equals(attribute.Name, name, StringComparison.Ordinal))
                    return attribute;
            }

            return null;
        }

        /// <summary>
        /// Enumerates this element and every element below it, in document order.
        /// </summary>
        /// <returns>The elements, this one first.</returns>
        public IEnumerable<ElementSyntax> DescendantsAndSelf()
        {
            var stack = new Stack<ElementSyntax>();
            stack.Push(this);
            while (stack.Count > 0)
            {
                ElementSyntax current = stack.Pop();
                yield return current;

                for (int i = current.content.Count - 1; i >= 0; i--)
                {
                    if (current.content[i] is ElementSyntax child)
                        stack.Push(child);
                }
            }
        }

        /// <summary>
        /// Determines whether this element contains <paramref name="other"/> at any depth.
        /// </summary>
        /// <param name="other">The element to look for.</param>
        /// <returns><see langword="true"/> when <paramref name="other"/> is below this element.</returns>
        public bool IsAncestorOf(ElementSyntax other)
        {
            ArgumentNullException.ThrowIfNull(other);
            for (ElementSyntax? parent = other.Parent; parent != null; parent = parent.Parent)
            {
                if (ReferenceEquals(parent, this))
                    return true;
            }

            return false;
        }

        internal void SetAttributes(IReadOnlyList<AttributeSyntax> value) => attributes = value;

        internal void SetContent(IReadOnlyList<MarkupSyntaxNode> value) => content = value;
    }
}
```

- [ ] **Step 4: Implement escaping, the parser and the document**

`sources/IcyUI.Design/Syntax/MarkupEscaping.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Buffers;
using System.Globalization;
using System.Text;
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// Decodes and encodes attribute values exactly as an XML reader sees them.
    /// </summary>
    internal static class MarkupEscaping
    {
        private static readonly SearchValues<char> DecodeTriggers = SearchValues.Create("&\r\n\t");

        /// <summary>
        /// Decodes an attribute value the way <see cref="System.Xml.XmlReader"/> does: literal tabs and line breaks
        /// become spaces (a <c>\r\n</c> pair becomes one), then entity and character references are expanded.
        /// </summary>
        public static string DecodeAttributeValue(string text, TextSpan valueSpan)
        {
            ReadOnlySpan<char> raw = text.AsSpan(valueSpan.Start, valueSpan.Length);
            if (raw.IndexOfAny(DecodeTriggers) < 0)
                return raw.ToString();

            var builder = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '\r')
                {
                    builder.Append(' ');
                    if (i + 1 < raw.Length && raw[i + 1] == '\n')
                        i++;
                    continue;
                }

                if (c is '\n' or '\t')
                {
                    builder.Append(' ');
                    continue;
                }

                if (c == '&')
                {
                    int semicolon = raw[i..].IndexOf(';');
                    if (semicolon > 1 && TryDecodeReference(raw.Slice(i + 1, semicolon - 1), out string? decoded))
                    {
                        builder.Append(decoded);
                        i += semicolon;
                        continue;
                    }
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Escapes a value for an attribute quoted with <paramref name="quote"/>, so that decoding it gives
        /// <paramref name="value"/> back exactly. Literal tabs and line breaks become character references, since
        /// a reader would otherwise normalize them to spaces.
        /// </summary>
        public static string EscapeAttributeValue(string value, char quote)
        {
            ArgumentNullException.ThrowIfNull(value);

            var builder = new StringBuilder(value.Length + 8);
            foreach (char c in value)
            {
                switch (c)
                {
                    case '&': builder.Append("&amp;"); break;
                    case '<': builder.Append("&lt;"); break;
                    case '"' when quote == '"': builder.Append("&quot;"); break;
                    case '\'' when quote == '\'': builder.Append("&apos;"); break;
                    case '\n': builder.Append("&#10;"); break;
                    case '\r': builder.Append("&#13;"); break;
                    case '\t': builder.Append("&#9;"); break;
                    default: builder.Append(c); break;
                }
            }

            return builder.ToString();
        }

        private static bool TryDecodeReference(ReadOnlySpan<char> name, out string? value)
        {
            value = name switch
            {
                "lt" => "<",
                "gt" => ">",
                "amp" => "&",
                "quot" => "\"",
                "apos" => "'",
                _ => null,
            };
            if (value != null)
                return true;

            if (name.Length < 2 || name[0] != '#')
                return false;

            bool hex = name[1] is 'x' or 'X';
            ReadOnlySpan<char> digits = hex ? name[2..] : name[1..];
            NumberStyles style = hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None;
            if (!int.TryParse(digits, style, CultureInfo.InvariantCulture, out int code) || code > 0x10FFFF || code is >= 0xD800 and <= 0xDFFF)
                return false;

            value = char.ConvertFromUtf32(code);
            return true;
        }
    }
}
```

`sources/IcyUI.Design/Syntax/MarkupParser.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// A span-preserving, error-tolerant parser for the XML subset IcyUI markup uses.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every character of the input ends up inside exactly one top-level node's span, so the tree always
    /// reproduces the text byte for byte. Malformed input never throws: the parser reports a
    /// <see cref="Diagnostic"/>, keeps the offending characters as text, and carries on.
    /// </para>
    /// <para>
    /// The parser only builds syntax. Namespace prefixes are resolved later, with the loader's rules, by
    /// <see cref="DocumentSyntax.ResolvePrefix"/>. Tokens are offsets into the text; strings are only created for
    /// the names and values the tree exposes.
    /// </para>
    /// </remarks>
    internal sealed class MarkupParser
    {
        private readonly string text;
        private readonly List<Diagnostic> diagnostics = [];
        private readonly List<ElementSyntax> openElements = [];
        private readonly Dictionary<int, ElementSyntax> elementsByStart = [];
        private readonly Dictionary<int, ElementSyntax> elementsByNameStart = [];
        private readonly Dictionary<int, AttributeSyntax> attributesByNameStart = [];
        private int position;

        private MarkupParser(string text)
        {
            this.text = text;
        }

        public static DocumentSyntax Parse(string text)
        {
            ArgumentNullException.ThrowIfNull(text);

            var parser = new MarkupParser(text);
            List<MarkupSyntaxNode> nodes = parser.ParseContent(null);

            ElementSyntax? root = null;
            foreach (MarkupSyntaxNode node in nodes)
            {
                if (node is not ElementSyntax element)
                    continue;

                if (root == null)
                    root = element;
                else
                    parser.Report(element.NameSpan, "A document can have only one root element.");
            }

            if (root == null)
                parser.Report(new TextSpan(text.Length, 0), "The document has no root element.");

            return new DocumentSyntax(text, nodes, root, parser.diagnostics, parser.elementsByStart, parser.elementsByNameStart, parser.attributesByNameStart);
        }

        public static bool IsNameStartChar(char c) => char.IsLetter(c) || c is '_' or ':';

        public static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '_' or ':' or '.' or '-';

        public static bool IsValidName(string name)
        {
            if (string.IsNullOrEmpty(name) || !IsNameStartChar(name[0]))
                return false;

            foreach (char c in name)
            {
                if (!IsNameChar(c))
                    return false;
            }

            return true;
        }

        private List<MarkupSyntaxNode> ParseContent(ElementSyntax? parent)
        {
            var nodes = new List<MarkupSyntaxNode>();
            while (position < text.Length)
            {
                MarkupSyntaxNode node;
                if (text[position] != '<')
                {
                    node = ParseText();
                }
                else if (StartsWith("</"))
                {
                    // Inside an element, the caller handles its own end tag (or decides this one isn't it).
                    if (parent != null)
                        break;
                    node = ParseStrayEndTag();
                }
                else if (StartsWith("<!--"))
                {
                    node = new CommentSyntax(ScanDelimited("<!--", "-->", "comment"));
                }
                else if (StartsWith("<![CDATA["))
                {
                    node = new CDataSyntax(ScanDelimited("<![CDATA[", "]]>", "CDATA section"));
                }
                else if (StartsWith("<?"))
                {
                    node = new ProcessingInstructionSyntax(ScanDelimited("<?", "?>", "processing instruction"));
                }
                else if (StartsWith("<!"))
                {
                    node = ParseDocumentType();
                }
                else if (position + 1 < text.Length && IsNameStartChar(text[position + 1]))
                {
                    node = ParseElement(parent);
                }
                else
                {
                    var span = new TextSpan(position, 1);
                    position++;
                    Report(span, "'<' must start a tag. Escape it as '&lt;' in text.");
                    node = new TextSyntax(span);
                }

                node.Parent = parent;
                nodes.Add(node);
            }

            return nodes;
        }

        private TextSyntax ParseText()
        {
            int start = position;
            int next = text.IndexOf('<', position);
            position = next < 0 ? text.Length : next;
            return new TextSyntax(TextSpan.FromBounds(start, position));
        }

        private TextSyntax ParseStrayEndTag()
        {
            int start = position;
            position += 2;
            string name = ReadName();
            SkipRestOfTag();
            var span = TextSpan.FromBounds(start, position);
            Report(span, name.Length > 0 ? $"Unexpected end tag '{name}'." : "Unexpected end tag.");
            return new TextSyntax(span);
        }

        private ProcessingInstructionSyntax ParseDocumentType()
        {
            int start = position;
            position += 2;
            SkipRestOfTag();
            var span = TextSpan.FromBounds(start, position);
            Report(span, "Document type declarations aren't supported.");
            return new ProcessingInstructionSyntax(span);
        }

        private TextSpan ScanDelimited(string open, string close, string what)
        {
            int start = position;
            int end = text.IndexOf(close, position + open.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                position = text.Length;
                var span = TextSpan.FromBounds(start, position);
                Report(span, $"The {what} isn't closed with '{close}'.");
                return span;
            }

            position = end + close.Length;
            return TextSpan.FromBounds(start, position);
        }

        private ElementSyntax ParseElement(ElementSyntax? parent)
        {
            int start = position++;
            int nameStart = position;
            string name = ReadName();
            var element = new ElementSyntax(start, name, new TextSpan(nameStart, name.Length)) { Parent = parent };
            elementsByStart[start] = element;
            elementsByNameStart[nameStart] = element;

            var attributes = new List<AttributeSyntax>();
            bool closed = false;
            while (position < text.Length)
            {
                SkipWhitespace();
                if (position >= text.Length)
                    break;

                char c = text[position];
                if (c == '>')
                {
                    position++;
                    closed = true;
                    break;
                }

                if (c == '/' && position + 1 < text.Length && text[position + 1] == '>')
                {
                    position += 2;
                    element.IsSelfClosing = true;
                    closed = true;
                    break;
                }

                if (c == '<')
                    break;

                if (IsNameStartChar(c))
                {
                    attributes.Add(ParseAttribute(element));
                    continue;
                }

                Report(new TextSpan(position, 1), $"Unexpected character '{c}' in the start tag of '{name}'.");
                position++;
            }

            element.StartTagEnd = position;
            element.SetAttributes(attributes);

            if (!closed)
            {
                Report(TextSpan.FromBounds(start, position), $"The start tag of '{name}' isn't closed.");
                element.IsMissingEndTag = true;
                element.Span = TextSpan.FromBounds(start, position);
                return element;
            }

            if (element.IsSelfClosing)
            {
                element.Span = TextSpan.FromBounds(start, position);
                return element;
            }

            openElements.Add(element);
            var content = new List<MarkupSyntaxNode>();
            while (true)
            {
                content.AddRange(ParseContent(element));
                if (position >= text.Length)
                {
                    Report(element.NameSpan, $"'{name}' has no end tag.");
                    element.IsMissingEndTag = true;
                    break;
                }

                int endStart = position;
                position += 2;
                string closing = ReadName();
                if (string.Equals(closing, name, StringComparison.Ordinal))
                {
                    SkipWhitespace();
                    if (position < text.Length && text[position] == '>')
                        position++;
                    else
                        Report(TextSpan.FromBounds(endStart, position), $"The end tag of '{name}' isn't closed.");

                    element.EndTagSpan = TextSpan.FromBounds(endStart, position);
                    break;
                }

                if (IsOpenAncestor(closing))
                {
                    // It closes an ancestor: this element simply never got its own end tag.
                    position = endStart;
                    Report(element.NameSpan, $"'{name}' has no end tag.");
                    element.IsMissingEndTag = true;
                    break;
                }

                // It matches nothing that is open: keep it as text so the round trip holds, and carry on.
                SkipRestOfTag();
                var stray = new TextSyntax(TextSpan.FromBounds(endStart, position)) { Parent = element };
                Report(stray.Span, $"Unexpected end tag '{closing}'.");
                content.Add(stray);
            }

            openElements.RemoveAt(openElements.Count - 1);
            element.SetContent(content);
            element.Span = TextSpan.FromBounds(start, position);
            return element;
        }

        private AttributeSyntax ParseAttribute(ElementSyntax owner)
        {
            int start = position;
            string name = ReadName();
            var nameSpan = new TextSpan(start, name.Length);
            int afterName = position;

            SkipWhitespace();
            if (position >= text.Length || text[position] != '=')
            {
                position = afterName;
                Report(nameSpan, $"Attribute '{name}' has no value.");
                return Register(new AttributeSyntax(nameSpan, name, nameSpan, new TextSpan(afterName, 0), '\0', string.Empty, isMissingValue: true), owner);
            }

            position++;
            SkipWhitespace();

            TextSpan valueSpan;
            char quote = '\0';
            if (position < text.Length && text[position] is '"' or '\'')
            {
                quote = text[position];
                int valueStart = position + 1;
                int close = text.IndexOf(quote, valueStart);
                int nextTag = text.IndexOf('<', valueStart);
                if (close < 0 || (nextTag >= 0 && nextTag < close))
                {
                    // '<' can never appear in a valid value, so it is a safe place to stop an unclosed one.
                    int valueEnd = nextTag >= 0 ? nextTag : text.Length;
                    valueSpan = TextSpan.FromBounds(valueStart, valueEnd);
                    position = valueEnd;
                    Report(TextSpan.FromBounds(start, valueEnd), $"The value of '{name}' isn't closed with {quote}.");
                }
                else
                {
                    valueSpan = TextSpan.FromBounds(valueStart, close);
                    position = close + 1;
                }
            }
            else
            {
                int valueStart = position;
                while (position < text.Length && !char.IsWhiteSpace(text[position]) && text[position] is not ('>' or '<') && !StartsWith("/>"))
                    position++;

                valueSpan = TextSpan.FromBounds(valueStart, position);
                Report(TextSpan.FromBounds(start, position), $"The value of '{name}' must be quoted.");
            }

            string value = MarkupEscaping.DecodeAttributeValue(text, valueSpan);
            return Register(new AttributeSyntax(TextSpan.FromBounds(start, position), name, nameSpan, valueSpan, quote, value, isMissingValue: false), owner);
        }

        private AttributeSyntax Register(AttributeSyntax attribute, ElementSyntax owner)
        {
            attribute.Parent = owner;
            attributesByNameStart[attribute.NameSpan.Start] = attribute;
            return attribute;
        }

        private bool IsOpenAncestor(string name)
        {
            for (int i = openElements.Count - 2; i >= 0; i--)
            {
                if (string.Equals(openElements[i].Name, name, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private string ReadName()
        {
            int start = position;
            while (position < text.Length && IsNameChar(text[position]))
                position++;

            return text[start..position];
        }

        private void SkipWhitespace()
        {
            while (position < text.Length && char.IsWhiteSpace(text[position]))
                position++;
        }

        private void SkipRestOfTag()
        {
            while (position < text.Length && text[position] is not ('>' or '<'))
                position++;

            if (position < text.Length && text[position] == '>')
                position++;
        }

        private bool StartsWith(string value) => text.AsSpan(position).StartsWith(value, StringComparison.Ordinal);

        private void Report(TextSpan span, string message) => diagnostics.Add(new Diagnostic(span, message));
    }
}
```

`sources/IcyUI.Design/Syntax/DocumentSyntax.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Markup;

namespace Icy.Design.Syntax
{
    /// <summary>
    /// The span-preserving syntax tree of one markup text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every character of <see cref="Text"/> belongs to exactly one node in <see cref="Nodes"/> or below, so the
    /// tree reproduces the text byte for byte, comments, formatting and entities included.
    /// </para>
    /// <para>
    /// Parsing never throws. Malformed text produces <see cref="Diagnostics"/>, and the offending characters are
    /// kept as <see cref="TextSyntax"/> nodes.
    /// </para>
    /// </remarks>
    public sealed class DocumentSyntax
    {
        private readonly Dictionary<int, ElementSyntax> elementsByStart;
        private readonly Dictionary<int, ElementSyntax> elementsByNameStart;
        private readonly Dictionary<int, AttributeSyntax> attributesByNameStart;

        internal DocumentSyntax(
            string text,
            IReadOnlyList<MarkupSyntaxNode> nodes,
            ElementSyntax? root,
            IReadOnlyList<Diagnostic> diagnostics,
            Dictionary<int, ElementSyntax> elementsByStart,
            Dictionary<int, ElementSyntax> elementsByNameStart,
            Dictionary<int, AttributeSyntax> attributesByNameStart)
        {
            Text = text;
            Nodes = nodes;
            Root = root;
            Diagnostics = diagnostics;
            this.elementsByStart = elementsByStart;
            this.elementsByNameStart = elementsByNameStart;
            this.attributesByNameStart = attributesByNameStart;
        }

        /// <summary>
        /// Gets the text the tree was parsed from.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the top-level nodes: the root element and any whitespace, comments and processing instructions
        /// around it.
        /// </summary>
        public IReadOnlyList<MarkupSyntaxNode> Nodes { get; }

        /// <summary>
        /// Gets the root element, or <see langword="null"/> when the text has none.
        /// </summary>
        public ElementSyntax? Root { get; }

        /// <summary>
        /// Gets the problems found while parsing; empty for well-formed markup.
        /// </summary>
        public IReadOnlyList<Diagnostic> Diagnostics { get; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Diagnostics"/> is non-empty.
        /// </summary>
        public bool HasErrors => Diagnostics.Count > 0;

        /// <summary>
        /// Gets every element under <see cref="Root"/>, the root first, in document order.
        /// </summary>
        public IEnumerable<ElementSyntax> Elements => Root?.DescendantsAndSelf() ?? [];

        /// <summary>
        /// Parses <paramref name="text"/>.
        /// </summary>
        /// <param name="text">The markup text.</param>
        /// <returns>The syntax tree.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        public static DocumentSyntax Parse(string text) => MarkupParser.Parse(text);

        /// <summary>
        /// Finds the element whose start tag begins (its <c>&lt;</c>) at <paramref name="start"/>.
        /// </summary>
        /// <param name="start">The offset of the element's <c>&lt;</c>.</param>
        /// <returns>The element, or <see langword="null"/> when none starts there.</returns>
        public ElementSyntax? FindElementAt(int start) => elementsByStart.GetValueOrDefault(start);

        /// <summary>
        /// Collects the namespace declarations in effect for <paramref name="element"/>, keyed by prefix (the empty
        /// string for the default namespace).
        /// </summary>
        /// <param name="element">The element to collect for.</param>
        /// <param name="includeSelf">
        /// <see langword="true"/> to include <paramref name="element"/>'s own declarations; <see langword="false"/>
        /// to collect only what it inherits from its ancestors.
        /// </param>
        /// <returns>The declarations, an inner declaration hiding an outer one with the same prefix.</returns>
        public IReadOnlyDictionary<string, string> GetNamespacesInScope(ElementSyntax element, bool includeSelf = false)
        {
            ArgumentNullException.ThrowIfNull(element);

            var chain = new List<ElementSyntax>();
            for (ElementSyntax? current = includeSelf ? element : element.Parent; current != null; current = current.Parent)
                chain.Add(current);

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = chain.Count - 1; i >= 0; i--)
            {
                foreach (AttributeSyntax attribute in chain[i].Attributes)
                {
                    if (attribute.IsNamespaceDeclaration)
                        result[attribute.Name == "xmlns" ? string.Empty : attribute.Name[6..]] = attribute.Value;
                }
            }

            return result;
        }

        /// <summary>
        /// Resolves a namespace prefix the way the markup loader does, including its predeclared <c>x</c> prefix.
        /// </summary>
        /// <param name="element">The element the prefix is used on.</param>
        /// <param name="prefix">The prefix, or an empty string for the default namespace.</param>
        /// <returns>The namespace URI, or <see langword="null"/> when the prefix isn't declared.</returns>
        public string? ResolvePrefix(ElementSyntax element, string prefix)
        {
            if (GetNamespacesInScope(element, includeSelf: true).TryGetValue(prefix, out string? uri))
                return uri;

            return prefix == "x" ? MarkupNamespaces.Directives : null;
        }

        internal ElementSyntax? FindElementByNameStart(int offset) => elementsByNameStart.GetValueOrDefault(offset);

        internal AttributeSyntax? FindAttributeByNameStart(int offset) => attributesByNameStart.GetValueOrDefault(offset);
    }
}
```

- [ ] **Step 5: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.Syntax"`
Expected: PASS, Total 27. If `Positions_MatchXmlReaderLineInfo` fails, **stop**: the correlation in Task 6 depends on the convention it pins. Report the observed (line, column) and adjust `DesignDocument.ToOffset` in Task 6 accordingly, rather than loosening the test.

- [ ] **Step 6: Build and check warnings, then commit**

Run: `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI.Design/Syntax sources/IcyUI.Tests/Design/Syntax
git commit -m "Add the span-preserving, error-tolerant markup syntax tree

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Core load observer for documents

**Files:**
- Create: `sources/IcyUI/Markup/MarkupLoadScopeKind.cs`, `sources/IcyUI/Markup/MarkupLoadScope.cs`, `sources/IcyUI/Markup/IMarkupLoadObserver.cs`
- Modify: `sources/IcyUI/Markup/MarkupConfiguration.cs` (add `LoadObserver` after `Activator`)
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs` (`LoadCore`, `CreateObject`, `AssignValue`, `MarkupLoadContext`)
- Test: `sources/IcyUI.Tests/Markup/RecordingLoadObserver.cs`, `sources/IcyUI.Tests/Markup/MarkupLoadObserverTests.cs`

**Interfaces:**
- Produces (namespace `Icy.Markup`):
  - `[Flags] public enum MarkupLoadScopeKind { None = 0, Document = 1, MergedDictionary = 2, TemplateContent = 4, DataTemplateContent = 8, All = 15 }`
  - `public sealed class MarkupLoadScope` with `Kind`, `SourcePath`, `SourceText` (`string?`), `NameScope` (`MarkupNameScope?`, weak), `Root` (`object?`, weak); internal `Observer`, `SetRoot(object)`, `static int CreatedOnCurrentThread`.
  - `public interface IMarkupLoadObserver` with `ObservedKinds`, `DocumentStarted(scope)`, `ObjectCreated(scope, XElement node, object instance)`, `MemberApplied(scope, XObject node, object target, MarkupMember member, object? value, IBinding? binding)`, `DocumentCompleted(scope, object root)`, `DocumentFailed(scope, MarkupException error)`.
  - `MarkupConfiguration.LoadObserver` (`IMarkupLoadObserver?`, default `null`).

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Markup/RecordingLoadObserver.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml;
using System.Xml.Linq;
using Icy.Data.Bindings;
using Icy.Markup;

namespace Icy.Tests.Markup
{
    /// <summary>
    /// Records every notification as a short string, so tests can assert on the exact order.
    /// </summary>
    internal sealed class RecordingLoadObserver(MarkupLoadScopeKind observedKinds = MarkupLoadScopeKind.All) : IMarkupLoadObserver
    {
        public List<string> Events { get; } = [];

        public List<MarkupLoadScope> Scopes { get; } = [];

        public List<(XObject Node, object Target, MarkupMember Member, IBinding? Binding)> Members { get; } = [];

        public MarkupLoadScopeKind ObservedKinds { get; } = observedKinds;

        public void DocumentStarted(MarkupLoadScope scope)
        {
            Scopes.Add(scope);
            Events.Add($"Started:{scope.Kind}");
        }

        public void ObjectCreated(MarkupLoadScope scope, XElement node, object instance)
        {
            var info = (IXmlLineInfo)node;
            Events.Add($"Created:{instance.GetType().Name}@{info.LineNumber}:{info.LinePosition}");
        }

        public void MemberApplied(MarkupLoadScope scope, XObject node, object target, MarkupMember member, object? value, IBinding? binding)
        {
            Members.Add((node, target, member, binding));
            Events.Add($"Applied:{member.Name}");
        }

        public void DocumentCompleted(MarkupLoadScope scope, object root) => Events.Add($"Completed:{root.GetType().Name}");

        public void DocumentFailed(MarkupLoadScope scope, MarkupException error) => Events.Add("Failed");
    }
}
```

`sources/IcyUI.Tests/Markup/MarkupLoadObserverTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Text;
using System.Xml.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Data.Bindings;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupLoadObserverTests
    {
        [Fact]
        public void Load_ReportsEveryStepInDocumentOrder()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();

            loader.Load("<StackPanel Orientation=\"Horizontal\">\n  <Border Width=\"10\"/>\n</StackPanel>");

            Assert.Equal(
            [
                "Started:Document",
                "Created:StackPanel@1:2",
                "Applied:Orientation",
                "Created:Border@2:4",
                "Applied:Width",
                "Completed:StackPanel",
            ],
            observer.Events);
        }

        [Fact]
        public void Scope_CarriesTheExactSourceTextPathAndRoot()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();
            const string text = "<StackPanel x:Name=\"root\"/>";

            UIElement root = loader.Load(text, "Pages/Main.xml");

            MarkupLoadScope scope = Assert.Single(observer.Scopes);
            Assert.Equal(MarkupLoadScopeKind.Document, scope.Kind);
            Assert.Equal(text, scope.SourceText);
            Assert.Equal("Pages/Main.xml", scope.SourcePath);
            Assert.Same(root, scope.Root);
            Assert.Same(MarkupNameScope.GetScope(root), scope.NameScope);
        }

        [Fact]
        public void Scope_SourceTextExcludesAByteOrderMark()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();
            const string text = "<Border Width=\"3\"/>";
            byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(text)];

            loader.Load(new MemoryStream(bytes));

            Assert.Equal(text, Assert.Single(observer.Scopes).SourceText);
        }

        [Fact]
        public void MemberApplied_ReportsTheBindingAMarkupExtensionCreated()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();

            var block = (TextBlock)loader.Load("<TextBlock Text=\"{Binding Path=Message}\"/>");

            (XObject node, object target, MarkupMember member, IBinding? binding) = Assert.Single(observer.Members);
            Assert.IsType<XAttribute>(node);
            Assert.Same(block, target);
            Assert.Equal("Text", member.Name);
            Assert.NotNull(binding);
            Assert.Contains(binding, block.Bindings);
        }

        [Fact]
        public void LoadFailure_ReportsFailedAndStillThrows()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();

            Assert.Throws<MarkupException>(() => loader.Load("<StackPanel Bogus=\"1\"/>"));

            Assert.Equal(["Started:Document", "Created:StackPanel@1:2", "Failed"], observer.Events);
        }

        [Fact]
        public void MalformedXml_ReportsFailed()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create();

            Assert.Throws<MarkupException>(() => loader.Load("<StackPanel>"));

            Assert.Equal(["Started:Document", "Failed"], observer.Events);
        }

        [Fact]
        public void ObserverThatObservesNothing_IsNeverCalled()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.None);

            loader.Load("<StackPanel><Border Width=\"10\"/></StackPanel>");

            Assert.Empty(observer.Events);
        }

        internal static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        private static (MarkupLoader Loader, RecordingLoadObserver Observer) Create(MarkupLoadScopeKind kinds = MarkupLoadScopeKind.All)
        {
            IcyConfiguration configuration = CreateConfiguration();
            var observer = new RecordingLoadObserver(kinds);
            configuration.Types.Markup.LoadObserver = observer;
            return (new MarkupLoader(configuration), observer);
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupLoadObserverTests"`
Expected: the build FAILS (`IMarkupLoadObserver`, `MarkupLoadScope`, `MarkupLoadScopeKind`, `LoadObserver` not found).

- [ ] **Step 3: Add the seam types**

`sources/IcyUI/Markup/MarkupLoadScopeKind.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// Identifies what kind of load a <see cref="MarkupLoadScope"/> covers. Combine values to choose what an
    /// <see cref="IMarkupLoadObserver"/> wants to hear about through <see cref="IMarkupLoadObserver.ObservedKinds"/>.
    /// </summary>
    [Flags]
    public enum MarkupLoadScopeKind
    {
        /// <summary>
        /// No load at all. An observer whose <see cref="IMarkupLoadObserver.ObservedKinds"/> is this is never called.
        /// </summary>
        None = 0,

        /// <summary>
        /// A document loaded through one of the <see cref="MarkupLoader"/> <c>Load</c>/<c>LoadObject</c> overloads.
        /// </summary>
        Document = 1,

        /// <summary>
        /// A resource dictionary document loaded on behalf of another document's
        /// <c>&lt;ResourceDictionary Source="..."/&gt;</c>.
        /// </summary>
        MergedDictionary = 2,

        /// <summary>
        /// A <see cref="Icy.UI.Styles.ControlTemplate"/>'s content, built for one templated control. This happens
        /// every time a control applies a template, so it can be very frequent.
        /// </summary>
        TemplateContent = 4,

        /// <summary>
        /// A <see cref="Icy.UI.Styles.DataTemplate"/>'s content, built for one data item. Pooled item containers do
        /// this whenever they need a new item view, so it can be very frequent.
        /// </summary>
        DataTemplateContent = 8,

        /// <summary>
        /// Every kind of load.
        /// </summary>
        All = Document | MergedDictionary | TemplateContent | DataTemplateContent,
    }
}
```

`sources/IcyUI/Markup/MarkupLoadScope.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Markup
{
    /// <summary>
    /// An opaque handle to one observed load: one document, merged dictionary or template instantiation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A scope exists only while a <see cref="MarkupConfiguration.LoadObserver"/> is installed and observes the
    /// load's <see cref="Kind"/>. Without an observer the loader creates none, so ordinary loads pay nothing for
    /// this.
    /// </para>
    /// <para>
    /// A scope never keeps the loaded tree alive: <see cref="Root"/> and <see cref="NameScope"/> are weak, and
    /// become <see langword="null"/> once the tree has been collected. Pass the scope back to
    /// <see cref="IMarkupBuilder"/> to build or update objects in the same document context.
    /// </para>
    /// </remarks>
    public sealed class MarkupLoadScope
    {
        [ThreadStatic]
        private static int createdOnCurrentThread;

        private readonly WeakReference<MarkupNameScope> nameScope;
        private WeakReference<object>? root;

        internal MarkupLoadScope(MarkupLoadScopeKind kind, string? sourcePath, string? sourceText, MarkupNameScope nameScope, IMarkupLoadObserver observer)
        {
            Kind = kind;
            SourcePath = sourcePath;
            SourceText = sourceText;
            this.nameScope = new WeakReference<MarkupNameScope>(nameScope);
            Observer = observer;
            createdOnCurrentThread++;
        }

        /// <summary>
        /// Gets the kind of load this scope covers.
        /// </summary>
        public MarkupLoadScopeKind Kind { get; }

        /// <summary>
        /// Gets the path the document was loaded from, as passed to the loader, or <see langword="null"/>.
        /// </summary>
        /// <remarks>
        /// Often an asset name resolved through an <see cref="Icy.Assets.IAssetContext"/> rather than a file path.
        /// </remarks>
        public string? SourcePath { get; }

        /// <summary>
        /// Gets the exact text the loader parsed, without any byte order mark, or <see langword="null"/> for
        /// <see cref="MarkupLoadScopeKind.TemplateContent"/> and <see cref="MarkupLoadScopeKind.DataTemplateContent"/>
        /// scopes, which are built from an already-parsed element.
        /// </summary>
        public string? SourceText { get; }

        /// <summary>
        /// Gets the name scope the load registers <c>x:Name</c>s into, or <see langword="null"/> once the loaded tree
        /// has been collected.
        /// </summary>
        public MarkupNameScope? NameScope => nameScope.TryGetTarget(out MarkupNameScope? value) ? value : null;

        /// <summary>
        /// Gets the root object the load produced, or <see langword="null"/> before it completes and after the root
        /// has been collected.
        /// </summary>
        public object? Root => root != null && root.TryGetTarget(out object? value) ? value : null;

        /// <summary>
        /// Gets how many scopes were created on the calling thread so far. Tests use it to prove that unobserved
        /// loads create none.
        /// </summary>
        internal static int CreatedOnCurrentThread => createdOnCurrentThread;

        /// <summary>
        /// Gets the observer this scope reports to.
        /// </summary>
        internal IMarkupLoadObserver Observer { get; }

        internal void SetRoot(object value) => root = new WeakReference<object>(value);
    }
}
```

`sources/IcyUI/Markup/IMarkupLoadObserver.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.Data.Bindings;

namespace Icy.Markup
{
    /// <summary>
    /// Receives notifications about what <see cref="MarkupLoader"/> builds, and from which markup node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Install one through <see cref="MarkupConfiguration.LoadObserver"/>. Design tooling uses it to map each live
    /// object back to the markup it came from. When no observer is installed (the default), the loader does no extra
    /// work and allocates nothing extra.
    /// </para>
    /// <para>
    /// For every observed load the calls arrive on the loading thread, in this order:
    /// </para>
    /// <list type="number">
    /// <item><description><see cref="DocumentStarted"/>, once.</description></item>
    /// <item><description>
    /// <see cref="ObjectCreated"/> for each element, before any of its attributes are applied, then
    /// <see cref="MemberApplied"/> for each attribute or property element applied to it, in document order.
    /// </description></item>
    /// <item><description><see cref="DocumentCompleted"/> or <see cref="DocumentFailed"/>, once.</description></item>
    /// </list>
    /// <para>
    /// Calls made through <see cref="IMarkupBuilder"/> report <see cref="ObjectCreated"/> and
    /// <see cref="MemberApplied"/> against the scope that was passed in, without a start or completion.
    /// </para>
    /// <para>
    /// Every node's <see cref="System.Xml.IXmlLineInfo"/> points at the first character of its name.
    /// </para>
    /// </remarks>
    public interface IMarkupLoadObserver
    {
        /// <summary>
        /// Gets the kinds of load this observer wants to hear about. The loader creates no scope and makes no call
        /// for any other kind.
        /// </summary>
        MarkupLoadScopeKind ObservedKinds { get; }

        /// <summary>
        /// Called when an observed load starts.
        /// </summary>
        /// <param name="scope">The new load's scope.</param>
        void DocumentStarted(MarkupLoadScope scope);

        /// <summary>
        /// Called right after an element's object is constructed, before its attributes are applied.
        /// </summary>
        /// <param name="scope">The load's scope.</param>
        /// <param name="node">The element the object was built from.</param>
        /// <param name="instance">The constructed object.</param>
        void ObjectCreated(MarkupLoadScope scope, XElement node, object instance);

        /// <summary>
        /// Called right after an attribute, property element or element text is applied to a member.
        /// </summary>
        /// <param name="scope">The load's scope.</param>
        /// <param name="node">The <see cref="XAttribute"/> or <see cref="XElement"/> the value came from.</param>
        /// <param name="target">The object whose member was set.</param>
        /// <param name="member">The member that was set.</param>
        /// <param name="value">
        /// The value assigned, or <see cref="MarkupValue.Unset"/> when a markup extension chose not to assign one.
        /// </param>
        /// <param name="binding">
        /// The binding a markup extension such as <c>{Binding}</c> attached to <paramref name="target"/> while
        /// resolving this value, or <see langword="null"/> when it attached none.
        /// </param>
        void MemberApplied(MarkupLoadScope scope, XObject node, object target, MarkupMember member, object? value, IBinding? binding);

        /// <summary>
        /// Called when an observed load completes successfully.
        /// </summary>
        /// <param name="scope">The load's scope.</param>
        /// <param name="root">The root object the load produced.</param>
        void DocumentCompleted(MarkupLoadScope scope, object root);

        /// <summary>
        /// Called when an observed load fails, just before the loader rethrows <paramref name="error"/>.
        /// </summary>
        /// <param name="scope">The load's scope.</param>
        /// <param name="error">The error the load is about to throw.</param>
        void DocumentFailed(MarkupLoadScope scope, MarkupException error);
    }
}
```

In `sources/IcyUI/Markup/MarkupConfiguration.cs`, add right after the `Activator` property:

```csharp
        /// <summary>
        /// Gets or sets the observer notified about what every <see cref="MarkupLoader"/> using this configuration
        /// builds, and from which markup node.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <see langword="null"/> by default, and then loading does no extra work at all. Design tooling installs an
        /// observer here, typically only in development builds; see <see cref="IMarkupLoadObserver"/>.
        /// </para>
        /// <para>
        /// This is a single slot. Tooling that installs an observer should refuse to replace one it didn't install.
        /// </para>
        /// </remarks>
        public IMarkupLoadObserver? LoadObserver { get; set; }
```

- [ ] **Step 4: Notify from the loader**

In `sources/IcyUI/Markup/MarkupLoader.cs`:

1. Add `using Icy.Data.Bindings;` to the usings.

2. Replace `LoadCore` with:

```csharp
        private object LoadCore(TextReader reader, string? sourcePath, out XElement root)
        {
            var names = new MarkupNameScope();
            MarkupLoadScope? scope = null;
            if (ObserverFor(MarkupLoadScopeKind.Document) is { } observer)
            {
                // Parse exactly the text the observer gets, so its positions and the tree always agree.
                string text = reader.ReadToEnd();
                reader = new StringReader(text);
                scope = new MarkupLoadScope(MarkupLoadScopeKind.Document, sourcePath, text, names, observer);
                observer.DocumentStarted(scope);
            }

            var context = new MarkupLoadContext(sourcePath, names) { Scope = scope };

            try
            {
                XDocument document = ParseDocument(reader, sourcePath);
                root = document.Root
                    ?? throw new MarkupException("The document is empty.", sourcePath);

                // Construct the whole tree against the configuration's registry, so every element captures the same
                // one the loader resolves properties through.
                using (PropertyRegistry.UseScope(registry))
                {
                    object instance = CreateObject(root, context);
                    if (instance is UIElement element)
                        MarkupNameScope.SetScope(element, names);

                    Complete(scope, instance);
                    return instance;
                }
            }
            catch (MarkupException ex) when (scope != null)
            {
                scope.Observer.DocumentFailed(scope, ex);
                throw;
            }
        }
```

3. Add these private helpers right after `LoadCore`:

```csharp
        private static void Complete(MarkupLoadScope? scope, object root)
        {
            if (scope == null)
                return;

            scope.SetRoot(root);
            scope.Observer.DocumentCompleted(scope, root);
        }

        private static void NotifyObjectCreated(MarkupLoadContext context, XElement element, object instance)
        {
            if (context.Scope is { } scope)
                scope.Observer.ObjectCreated(scope, element, instance);
        }

        /// <summary>
        /// Returns the installed observer when it wants to hear about <paramref name="kind"/>, so an unobserved load
        /// never creates a scope.
        /// </summary>
        private IMarkupLoadObserver? ObserverFor(MarkupLoadScopeKind kind) =>
            markup.LoadObserver is { } observer && (observer.ObservedKinds & kind) != 0 ? observer : null;
```

4. In `CreateObject`, notify for every object built. Replace the three early returns and add one notification before the attributes are applied:

```csharp
            if (instance is ResourceDictionary && element.Attribute("Source") is { } source)
            {
                ResourceDictionary merged = LoadMergedDictionary(source.Value, element, context);
                NotifyObjectCreated(context, element, merged);
                return merged;
            }
```

In the `ControlTemplate` branch, replace `return instance;` after `controlTemplate.SetContent(...)` with:

```csharp
                NotifyObjectCreated(context, element, instance);
                return instance;
```

Do the same in the `DataTemplate` branch. Then, right before `Type? previousSetterTargetType = context.SetterTargetType;`, add:

```csharp
            NotifyObjectCreated(context, element, instance);
```

5. Replace `AssignValue` with:

```csharp
        private void AssignValue(object instance, MarkupMember member, object? value, IXmlLineInfo? node, MarkupLoadContext context)
        {
            MarkupLoadScope? scope = context.Scope;
            IBindingTarget? bindingTarget = scope != null ? instance as IBindingTarget : null;
            int bindingsBefore = bindingTarget?.Bindings.Count ?? 0;

            object? resolved = ConvertValue(value, member.PropertyType, node, context, instance, member);
            if (!ReferenceEquals(resolved, MarkupValue.Unset))
                member.SetValue(instance, resolved);

            if (scope != null && node is XObject xmlNode)
            {
                // A markup extension such as {Binding} attaches its binding instead of returning a value, so the only
                // way to report it is to notice the target gained one while the value was resolved.
                IBinding? binding = bindingTarget != null && bindingTarget.Bindings.Count > bindingsBefore
                    ? bindingTarget.Bindings.Last()
                    : null;
                scope.Observer.MemberApplied(scope, xmlNode, instance, member, resolved, binding);
            }
        }
```

6. Add to `MarkupLoadContext`, after `TemplatedControl`:

```csharp
            /// <summary>
            /// Gets the observed scope this load reports to, or <see langword="null"/> when nothing observes it.
            /// </summary>
            public MarkupLoadScope? Scope { get; init; }
```

- [ ] **Step 5: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupLoadObserverTests"`
Expected: PASS, Total 7.

- [ ] **Step 6: Run the whole suite to prove the null-observer path is unchanged**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: PASS; Total = previous total (1085) + every test added so far; no failures.

- [ ] **Step 7: Build, check warnings, commit**

Run: `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI/Markup sources/IcyUI.Tests/Markup
git commit -m "Add an optional load observer to the markup loader

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Template and merged-dictionary scopes, and the zero-cost guarantee

**Files:**
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs` (`LoadTemplateContent`, `LoadDataTemplateContent`, `LoadMergedDictionary`, `LoadCore`)
- Test: `sources/IcyUI.Tests/Markup/MarkupLoadObserverKindsTests.cs`

**Interfaces:**
- Consumes: Task 3's seam (`ObserverFor`, `Complete`, `MarkupLoadContext.Scope`).
- Produces: template loads report `TemplateContent`/`DataTemplateContent` scopes with `SourceText == null`; a load started from a `<ResourceDictionary Source="..."/>` reports `MergedDictionary`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Markup/MarkupLoadObserverKindsTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Markup;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupLoadObserverKindsTests
    {
        private const string Page = "<StackPanel Orientation=\"Horizontal\"><Border Width=\"10\"/><TextBlock Text=\"Hi\"/></StackPanel>";

        [Fact]
        public void DataTemplateBuild_ReportsADataTemplateContentScopeWithoutText()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.All);
            var template = (DataTemplate)loader.LoadObject("<DataTemplate><Border Height=\"20\"/></DataTemplate>");
            observer.Events.Clear();
            observer.Scopes.Clear();

            template.Build(new object());

            Assert.Equal(["Started:DataTemplateContent", "Created:Border@1:16", "Applied:Height", "Completed:Border"], observer.Events);
            Assert.Null(Assert.Single(observer.Scopes).SourceText);
        }

        [Fact]
        public void ControlTemplateLoadContent_ReportsATemplateContentScope()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.All);
            var template = (ControlTemplate)loader.LoadObject("<ControlTemplate TargetType=\"Button\"><Border/></ControlTemplate>");
            observer.Scopes.Clear();

            template.LoadContent(new Button());

            Assert.Equal(MarkupLoadScopeKind.TemplateContent, Assert.Single(observer.Scopes).Kind);
        }

        [Fact]
        public void TemplateKindsNotObserved_ProduceNoCalls()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.Document | MarkupLoadScopeKind.MergedDictionary);
            var template = (DataTemplate)loader.LoadObject("<DataTemplate><Border Height=\"20\"/></DataTemplate>");
            observer.Events.Clear();

            for (int i = 0; i < 10; i++)
                template.Build(new object());

            Assert.Empty(observer.Events);
        }

        [Fact]
        public void MergedDictionarySource_ReportsAMergedDictionaryScope()
        {
            (MarkupLoader loader, RecordingLoadObserver observer) = Create(MarkupLoadScopeKind.All, registerDictionaryImporter: true);

            loader.LoadObject(
                """
                <ResourceDictionary>
                  <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source="Resources/theme.xml"/>
                  </ResourceDictionary.MergedDictionaries>
                </ResourceDictionary>
                """);

            Assert.Equal([MarkupLoadScopeKind.Document, MarkupLoadScopeKind.MergedDictionary], observer.Scopes.Select(x => x.Kind));
            Assert.NotNull(observer.Scopes[1].SourceText);
        }

        [Fact]
        public void UnobservedLoads_CreateNoScopesAndAllocateNothingExtra()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();
            var loader = new MarkupLoader(configuration);

            long withoutObserver = MeasureLoad(loader);

            configuration.Types.Markup.LoadObserver = new RecordingLoadObserver(MarkupLoadScopeKind.None);
            int scopesBefore = MarkupLoadScope.CreatedOnCurrentThread;
            long withFilteredObserver = MeasureLoad(loader);

            Assert.Equal(scopesBefore, MarkupLoadScope.CreatedOnCurrentThread);
            Assert.Equal(withoutObserver, withFilteredObserver);

            // Sanity check that the measurement can see the observed path's cost at all: it copies the source text.
            configuration.Types.Markup.LoadObserver = new RecordingLoadObserver(MarkupLoadScopeKind.Document);
            long observed = MeasureLoad(loader);
            Assert.True(observed >= withoutObserver + (Page.Length * sizeof(char)), $"observed {observed}, unobserved {withoutObserver}");
        }

        private static long MeasureLoad(MarkupLoader loader)
        {
            // Warm up JIT and every lazily-filled cache on this path, so only the load itself is measured.
            for (int i = 0; i < 5; i++)
                loader.Load(Page);

            long before = GC.GetAllocatedBytesForCurrentThread();
            loader.Load(Page);
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        private static (MarkupLoader Loader, RecordingLoadObserver Observer) Create(MarkupLoadScopeKind kinds, bool registerDictionaryImporter = false)
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();
            if (registerDictionaryImporter)
                configuration.Assets.AssetResolver.RegisterImporter(new ResourceDictionaryImporter(configuration));

            var observer = new RecordingLoadObserver(kinds);
            configuration.Types.Markup.LoadObserver = observer;
            return (new MarkupLoader(configuration), observer);
        }
    }
}
```

`Created:Border@1:16`: in `<DataTemplate><Border Height="20"/></DataTemplate>`, `Border`'s name starts at column 16.

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupLoadObserverKindsTests"`
Expected: FAIL. The template tests see no events, and the merged test sees two `Document` scopes. The allocation test should already pass, since no unobserved path allocates a scope yet.

- [ ] **Step 3: Implement the kinds**

In `MarkupLoader`:

1. Add a thread-static field at the top of the class, after the existing fields:

```csharp
        // Set by LoadMergedDictionary around the asset pipeline call, so the nested load it triggers (in another
        // MarkupLoader instance, through ResourceDictionaryImporter) reports itself as a merged dictionary.
        [ThreadStatic]
        private static MarkupLoadScopeKind? pendingKind;
```

2. In `LoadCore`, take the pending kind first and use it instead of the hard-coded `Document`:

```csharp
            MarkupLoadScopeKind kind = pendingKind ?? MarkupLoadScopeKind.Document;
            pendingKind = null;

            var names = new MarkupNameScope();
            MarkupLoadScope? scope = null;
            if (ObserverFor(kind) is { } observer)
            {
                string text = reader.ReadToEnd();
                reader = new StringReader(text);
                scope = new MarkupLoadScope(kind, sourcePath, text, names, observer);
                observer.DocumentStarted(scope);
            }
```

3. Replace `LoadMergedDictionary`'s last line with:

```csharp
            try
            {
                pendingKind = MarkupLoadScopeKind.MergedDictionary;
                return configuration.Assets.AssetResolver.LoadAsset<ResourceDictionary>(assetContext, path);
            }
            finally
            {
                // A cached asset never reaches LoadCore, so never let the flag leak into an unrelated later load.
                pendingKind = null;
            }
```

4. Replace the body of `LoadTemplateContent` after its argument checks with:

```csharp
            var names = new MarkupNameScope();
            MarkupLoadScope? scope = BeginTemplateScope(MarkupLoadScopeKind.TemplateContent, sourcePath, names);
            var context = new MarkupLoadContext(sourcePath, names) { TemplatedControl = templatedControl, Scope = scope };
            return BuildTemplateRoot(content, context, scope, names, sourcePath);
```

and the body of `LoadDataTemplateContent` after its argument check with:

```csharp
            var names = new MarkupNameScope();
            MarkupLoadScope? scope = BeginTemplateScope(MarkupLoadScopeKind.DataTemplateContent, sourcePath, names);
            var context = new MarkupLoadContext(sourcePath, names) { Scope = scope };
            return BuildTemplateRoot(content, context, scope, names, sourcePath);
```

5. Add these private helpers next to `Complete`:

```csharp
        private MarkupLoadScope? BeginTemplateScope(MarkupLoadScopeKind kind, string? sourcePath, MarkupNameScope names)
        {
            if (ObserverFor(kind) is not { } observer)
                return null;

            var scope = new MarkupLoadScope(kind, sourcePath, sourceText: null, names, observer);
            observer.DocumentStarted(scope);
            return scope;
        }

        private UIElement BuildTemplateRoot(XElement content, MarkupLoadContext context, MarkupLoadScope? scope, MarkupNameScope names, string? sourcePath)
        {
            try
            {
                using (PropertyRegistry.UseScope(registry))
                {
                    object instance = CreateObject(content, context);
                    if (instance is not UIElement element)
                    {
                        throw MarkupException.At(
                            $"A template's root element must be a '{nameof(UIElement)}', but '{instance.GetType().Name}' isn't one.",
                            content,
                            sourcePath);
                    }

                    MarkupNameScope.SetScope(element, names);
                    Complete(scope, element);
                    return element;
                }
            }
            catch (MarkupException ex) when (scope != null)
            {
                scope.Observer.DocumentFailed(scope, ex);
                throw;
            }
        }
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupLoadObserver"`
Expected: PASS, Total 12 (7 + 5).

If `UnobservedLoads_CreateNoScopesAndAllocateNothingExtra` fails only on the allocation **equality**, don't loosen it. Find what differs: run the measurement twice in a row with no observer. If even that differs, a cache is still warming up, so raise the warm-up count. Otherwise the observed-kind check is allocating, and that has to be fixed.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (check that Total grew by 5 and nothing fails), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI.Tests/Markup/MarkupLoadObserverKindsTests.cs
git commit -m "Report template and merged-dictionary loads as their own observer scopes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: `IMarkupBuilder` and `MarkupNameScope.Unregister`

**Files:**
- Create: `sources/IcyUI/Markup/IMarkupBuilder.cs`
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs` (implement `IMarkupBuilder` explicitly; let `ParseDocument` take extra namespaces)
- Modify: `sources/IcyUI/Markup/MarkupNameScope.cs` (add `Unregister`)
- Test: `sources/IcyUI.Tests/Markup/MarkupBuilderTests.cs`

**Interfaces:**
- Consumes: `MarkupLoadScope`, `IMarkupLoadObserver` (Tasks 3–4).
- Produces:
  - `public interface IMarkupBuilder` with `XElement ParseFragment(string text, IReadOnlyDictionary<string, string> namespaces)`, `object BuildFragment(MarkupLoadScope scope, XElement fragment, UIElement? liveParent)`, `void ApplyAttribute(MarkupLoadScope scope, object target, XAttribute attribute)`.
  - `public class MarkupLoader(IcyConfiguration configuration) : IMarkupBuilder`; callers get the builder as `IMarkupBuilder builder = new MarkupLoader(configuration);`.
  - `public bool MarkupNameScope.Unregister(string name)`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Markup/MarkupBuilderTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml;
using System.Xml.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Markup
{
    public class MarkupBuilderTests
    {
        private static readonly Dictionary<string, string> NoNamespaces = [];

        [Fact]
        public void ParseFragment_ResolvesSuppliedPrefixesAndThePredeclaredX()
        {
            IMarkupBuilder builder = new MarkupLoader(MarkupLoadObserverTests.CreateConfiguration());

            XElement fragment = builder.ParseFragment(
                "<ui:Border x:Name=\"b\"/>",
                new Dictionary<string, string> { ["ui"] = MarkupNamespaces.Default });

            Assert.Equal(XName.Get("Border", MarkupNamespaces.Default), fragment.Name);
            Assert.NotNull(fragment.Attribute(MarkupNamespaces.DirectivesNamespace + "Name"));
        }

        [Fact]
        public void ParseFragment_UnknownPrefix_ThrowsMarkupException()
        {
            IMarkupBuilder builder = new MarkupLoader(MarkupLoadObserverTests.CreateConfiguration());

            Assert.Throws<MarkupException>(() => builder.ParseFragment("<ui:Border/>", NoNamespaces));
        }

        [Fact]
        public void BuildFragment_ResolvesStaticResourcesFromLiveAncestors()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked(
                """
                <StackPanel>
                  <StackPanel.Resources>
                    <Style x:Key="Accent" TargetType="Border" Width="42"/>
                  </StackPanel.Resources>
                </StackPanel>
                """);

            var built = (Border)builder.BuildFragment(scope, builder.ParseFragment("<Border Style=\"{StaticResource Accent}\"/>", NoNamespaces), root);

            Assert.Equal(42f, built.Style!.Setters["Width"]);
        }

        [Fact]
        public void BuildFragment_RegistersNamesAndReportsToTheScopesObserver()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked("<StackPanel/>");
            var observer = (RecordingLoadObserver)scope.Observer;
            observer.Events.Clear();

            object built = builder.BuildFragment(scope, builder.ParseFragment("<Border x:Name=\"added\" Width=\"5\"/>", NoNamespaces), root);

            Assert.Same(built, scope.NameScope!.Find("added"));
            Assert.Equal(["Created:Border@1:2", "Applied:Width"], observer.Events);
        }

        [Fact]
        public void BuildFragment_BindingFollowsTheDataContextOnceInserted()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked("<StackPanel/>");
            root.DataContext = new Model { Message = "hello" };

            var block = (TextBlock)builder.BuildFragment(scope, builder.ParseFragment("<TextBlock Text=\"{Binding Path=Message}\"/>", NoNamespaces), root);
            ((StackPanel)root).Children.Add(block);

            Assert.Equal("hello", block.Text);
        }

        [Fact]
        public void ApplyAttribute_SetsPropertiesAttachedPropertiesAndNames()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked("<Grid><Border x:Name=\"cell\"/></Grid>");
            var cell = (Border)scope.NameScope!.Find("cell")!;
            scope.NameScope.Unregister("cell");

            XElement tag = builder.ParseFragment("<Border Width=\"7\" Grid.Row=\"1\" x:Name=\"renamed\"/>", NoNamespaces);
            foreach (XAttribute attribute in tag.Attributes())
                builder.ApplyAttribute(scope, cell, attribute);

            Assert.Equal(7f, cell.Width);
            Assert.Equal(1, Grid.GetRow(cell));
            Assert.Same(cell, scope.NameScope.Find("renamed"));
            Assert.Null(scope.NameScope.Find("cell"));
        }

        [Fact]
        public void ApplyAttribute_BadValue_ThrowsAndLeavesTheValue()
        {
            (IMarkupBuilder builder, MarkupLoadScope scope, UIElement root) = LoadTracked("<StackPanel><Border x:Name=\"b\" Width=\"3\"/></StackPanel>");
            var border = (Border)scope.NameScope!.Find("b")!;

            XAttribute bad = builder.ParseFragment("<Border Width=\"abc\"/>", NoNamespaces).Attribute("Width")!;

            Assert.Throws<MarkupException>(() => builder.ApplyAttribute(scope, border, bad));
            Assert.Equal(3f, border.Width);
        }

        [Fact]
        public void Unregister_RemovesOnlyThatName()
        {
            var names = new MarkupNameScope();
            var a = new Border();
            names.Register("a", a);
            names.Register("b", new Border());

            Assert.True(names.Unregister("a"));
            Assert.False(names.Unregister("a"));
            Assert.Null(names.Find("a"));
            Assert.NotNull(names.Find("b"));
        }

        private static (IMarkupBuilder Builder, MarkupLoadScope Scope, UIElement Root) LoadTracked(string markup)
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();
            var observer = new RecordingLoadObserver(MarkupLoadScopeKind.Document);
            configuration.Types.Markup.LoadObserver = observer;
            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(markup.ReplaceLineEndings("\n"));
            return (loader, observer.Scopes[0], root);
        }

        private sealed class Model : ObservableObject
        {
            private string message = string.Empty;

            public string Message
            {
                get => message;
                set => SetProperty(ref message, value);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupBuilderTests"`
Expected: the build FAILS (`IMarkupBuilder` and `Unregister` not found).

- [ ] **Step 3: Implement**

`sources/IcyUI/Markup/IMarkupBuilder.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.UI;

namespace Icy.Markup
{
    /// <summary>
    /// Builds objects from markup fragments, or applies single attributes, exactly as <see cref="MarkupLoader"/>
    /// would while loading a whole document.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the seam design tooling uses to mirror a markup edit onto an already loaded tree, reusing the loader's
    /// type resolution, value conversion, markup extensions and name scopes instead of duplicating them.
    /// <see cref="MarkupLoader"/> implements it: <c>IMarkupBuilder builder = new MarkupLoader(configuration);</c>.
    /// </para>
    /// <para>
    /// Everything built through a <see cref="MarkupLoadScope"/> is reported to that scope's observer with
    /// <see cref="IMarkupLoadObserver.ObjectCreated"/> and <see cref="IMarkupLoadObserver.MemberApplied"/>, as during
    /// the original load.
    /// </para>
    /// </remarks>
    public interface IMarkupBuilder
    {
        /// <summary>
        /// Parses a markup fragment with the same reader settings the loader uses for documents.
        /// </summary>
        /// <param name="text">The fragment: exactly one element, with any content.</param>
        /// <param name="namespaces">
        /// The namespace declarations the fragment inherits from where it sits in its document, keyed by prefix (the
        /// empty string for the default namespace). The <c>x</c> prefix is always predeclared.
        /// </param>
        /// <returns>The fragment's element, with line information relative to <paramref name="text"/>.</returns>
        /// <exception cref="MarkupException">The fragment isn't well-formed XML, or uses an undeclared prefix.</exception>
        XElement ParseFragment(string text, IReadOnlyDictionary<string, string> namespaces);

        /// <summary>
        /// Builds a fragment's object tree in the context of an observed document.
        /// </summary>
        /// <param name="scope">The scope of the document the fragment belongs to.</param>
        /// <param name="fragment">The fragment, as returned by <see cref="ParseFragment"/>.</param>
        /// <param name="liveParent">
        /// The live element the result will be added to, or <see langword="null"/>. <c>{StaticResource}</c>
        /// resolves through it and its ancestors, as it would through the elements under construction during a load.
        /// The result isn't added to it; that's the caller's job.
        /// </param>
        /// <returns>The built object. <c>x:Name</c>s inside it are registered in the scope's name scope.</returns>
        /// <exception cref="MarkupException">The fragment breaks a rule of the language.</exception>
        /// <exception cref="InvalidOperationException">The scope's tree has already been collected.</exception>
        object BuildFragment(MarkupLoadScope scope, XElement fragment, UIElement? liveParent);

        /// <summary>
        /// Applies one attribute to an already built object, exactly as the loader would.
        /// </summary>
        /// <param name="scope">The scope of the document <paramref name="target"/> belongs to.</param>
        /// <param name="target">The object to apply the attribute to.</param>
        /// <param name="attribute">
        /// The attribute. It must belong to an element (one from <see cref="ParseFragment"/>), so its namespace and
        /// any directive resolve.
        /// </param>
        /// <exception cref="MarkupException">The attribute or its value breaks a rule of the language.</exception>
        /// <exception cref="ArgumentException"><paramref name="attribute"/> doesn't belong to an element.</exception>
        /// <exception cref="InvalidOperationException">The scope's tree has already been collected.</exception>
        void ApplyAttribute(MarkupLoadScope scope, object target, XAttribute attribute);
    }
}
```

In `MarkupNameScope`, add after `Register`:

```csharp
        /// <summary>
        /// Removes <paramref name="name"/> from this scope, so it can be registered again for another element.
        /// </summary>
        /// <param name="name">The name to remove.</param>
        /// <returns><see langword="true"/> when the name was registered.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or empty.</exception>
        public bool Unregister(string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            return names.Remove(name);
        }
```

In `MarkupLoader`:

1. Change the class declaration to `public class MarkupLoader(IcyConfiguration configuration) : IMarkupBuilder`, and add to its `<remarks>`:

```csharp
    /// <para>
    /// The loader also implements <see cref="IMarkupBuilder"/>, the seam design tooling uses to build fragments and
    /// apply single attributes in the context of an already loaded document.
    /// </para>
```

2. Keep `protected static XDocument ParseDocument(TextReader reader, string? sourcePath)` with the same signature, and move its body into a private overload that accepts extra namespaces:

```csharp
        protected static XDocument ParseDocument(TextReader reader, string? sourcePath) =>
            ParseDocument(reader, sourcePath, namespaces: null);

        private static XDocument ParseDocument(TextReader reader, string? sourcePath, IReadOnlyDictionary<string, string>? namespaces)
        {
            ArgumentNullException.ThrowIfNull(reader);

            var nameTable = new NameTable();
            var manager = new XmlNamespaceManager(nameTable);
            manager.AddNamespace("x", MarkupNamespaces.Directives);
            if (namespaces != null)
            {
                foreach ((string prefix, string uri) in namespaces)
                {
                    // "xml" and "xmlns" are reserved and always bound; the manager refuses them.
                    if (prefix is not ("xml" or "xmlns"))
                        manager.AddNamespace(prefix, uri);
                }
            }

            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
            };

            try
            {
                using XmlReader xml = XmlReader.Create(reader, settings, new XmlParserContext(nameTable, manager, null, XmlSpace.None));
                return XDocument.Load(xml, LoadOptions.SetLineInfo);
            }
            catch (XmlException ex)
            {
                throw new MarkupException(ex.Message, sourcePath, ex.LineNumber, ex.LinePosition, ex);
            }
        }
```

Keep the existing XML doc comment on the protected overload.

3. Add the explicit implementations after `LoadDataTemplateContent`:

```csharp
        /// <inheritdoc/>
        XElement IMarkupBuilder.ParseFragment(string text, IReadOnlyDictionary<string, string> namespaces)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(namespaces);

            using var reader = new StringReader(text);
            XDocument document = ParseDocument(reader, sourcePath: null, namespaces);
            return document.Root ?? throw new MarkupException("The fragment is empty.", null);
        }

        /// <inheritdoc/>
        object IMarkupBuilder.BuildFragment(MarkupLoadScope scope, XElement fragment, UIElement? liveParent)
        {
            ArgumentNullException.ThrowIfNull(scope);
            ArgumentNullException.ThrowIfNull(fragment);

            MarkupLoadContext context = CreateBuilderContext(scope, liveParent);
            using (PropertyRegistry.UseScope(registry))
                return CreateObject(fragment, context);
        }

        /// <inheritdoc/>
        void IMarkupBuilder.ApplyAttribute(MarkupLoadScope scope, object target, XAttribute attribute)
        {
            ArgumentNullException.ThrowIfNull(scope);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(attribute);
            XElement owner = attribute.Parent
                ?? throw new ArgumentException("The attribute must belong to an element, so its namespace and directives resolve.", nameof(attribute));

            if (attribute.IsNamespaceDeclaration)
                return;

            MarkupLoadContext context = CreateBuilderContext(scope, target as UIElement);
            using (PropertyRegistry.UseScope(registry))
            {
                if (MarkupNamespaces.IsDirective(attribute.Name.Namespace))
                    ApplyDirective(owner, target, attribute, context);
                else if (attribute.Name.LocalName.Contains('.', StringComparison.Ordinal))
                    ApplyAttachedProperty(target, attribute, context);
                else
                    ApplyProperty(target, attribute.Name.LocalName, attribute.Value, attribute, context);
            }
        }
```

4. Add the shared context helper next to the other private helpers:

```csharp
        /// <summary>
        /// Recreates, for a builder call, the context a whole-document load would have had at that point.
        /// </summary>
        /// <remarks>
        /// The element stack <c>{StaticResource}</c> walks only exists during a load, so it's rebuilt from the live
        /// parent chain. At runtime that chain can reach containers above the document's own root; resources found
        /// there resolve too, which is what an author editing the live page would expect.
        /// </remarks>
        private static MarkupLoadContext CreateBuilderContext(MarkupLoadScope scope, UIElement? innermost)
        {
            MarkupNameScope names = scope.NameScope
                ?? throw new InvalidOperationException("The document this scope belongs to has already been collected.");

            var context = new MarkupLoadContext(scope.SourcePath, names) { Scope = scope };
            for (UIElement? element = innermost; element != null; element = element.Parent)
                context.ElementStack.Insert(0, element);

            return context;
        }
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupBuilderTests"`
Expected: PASS, Total 8.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (no failures, Total grew by 8), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI/Markup sources/IcyUI.Tests/Markup/MarkupBuilderTests.cs
git commit -m "Expose the loader as an IMarkupBuilder for fragments and single attributes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: `DesignSession`, `DesignDocument` and the node↔object map

**Files:**
- Create in `sources/IcyUI.Design/`: `NodeId.cs`, `DesignSession.cs`, `DesignDocument.cs`, `DocumentChangedEventArgs.cs`, `SubtreeReplacedEventArgs.cs`
- Create in `sources/IcyUI.Design/Tracking/`: `DesignLoadObserver.cs`, `ObjectMap.cs`, `AppliedMember.cs`
- Test: `sources/IcyUI.Tests/Design/DesignTestHost.cs`, `sources/IcyUI.Tests/Design/DesignTestTypes.cs`, `sources/IcyUI.Tests/Design/DesignSessionTests.cs`

**Interfaces:**
- Consumes: `IMarkupLoadObserver`, `MarkupLoadScope`, `IMarkupBuilder` (Tasks 3–5); `DocumentSyntax`, `LineMap`, `MarkupText` (Tasks 1–2).
- Produces (namespace `Icy.Design`):
  - `public readonly record struct NodeId(int Value)`.
  - `public sealed class DesignSession : IDisposable` with `static Attach(IcyConfiguration)`, `Configuration`, `Documents`, `FindDocument(object instance, out NodeId node)`, `Dispose()`; internal `Builder`, `GetDocument(MarkupLoadScope)`, `OnDocumentStarted/Completed/Failed(scope)`.
  - `public sealed class DesignDocument` with `Session`, `SourcePath`, `Text`, `Version`, `Syntax`, `NeedsReload`, events `Changed`, `SubtreeReplaced`, `NeedsReloadChanged`, `DiagnosticsChanged`, `GetNode(NodeId)`, `GetNodeId(ElementSyntax)`, `GetObjects(NodeId)`, `TryGetNodeId(object, out NodeId)`; internal `Map`, `Registry`, `Scopes`, `AddScope`, `RemoveScope`, `OnObjectCreated`, `OnMemberApplied`, `EnterFragment(int baseOffset, string fragmentText)`, `SuppressRecording()`, `IsAlive`, `TrackedObjectCount`.
  - `public sealed class DocumentChangedEventArgs(TextChangeSet changes, int version) : EventArgs`.
  - `public sealed class SubtreeReplacedEventArgs(NodeId node, UIElement oldElement, UIElement newElement) : EventArgs`.
  - Internal (namespace `Icy.Design.Tracking`): `ObjectMap` (`Add`, `GetObjects`, `TryGetEntry`, `FindObject`, `Remove`, `RemoveNode`, `RemoveSubtree`, `RemoveScope`, `RecordMember`, `Count`, `FirstAlive`), `ObjectMap.Entry` (`Id`, `Scope`, `Members`), `AppliedMember(MarkupMember Member, IBinding? Binding)`.

- [ ] **Step 1: Write the test host, test types and failing tests**

`sources/IcyUI.Tests/Design/DesignTestTypes.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.UI;

namespace Icy.Tests.Design
{
    /// <summary>
    /// An element with a plain CLR property the property registry knows nothing about, so it has no "unset" state:
    /// clearing it in markup can only be mirrored by rebuilding the element.
    /// </summary>
    internal sealed class ClrBox : UIElement
    {
        public string? Caption { get; set; }

        protected override Size MeasureContent() => Size.Empty;

        protected override void ArrangeContent()
        {
        }
    }

    /// <summary>
    /// An element whose only constructor consumes an attribute, so changing that attribute needs a rebuild.
    /// </summary>
    internal sealed class Labeled : UIElement
    {
        public Labeled(string label)
        {
            Label = label;
        }

        public string Label { get; }

        protected override Size MeasureContent() => Size.Empty;

        protected override void ArrangeContent()
        {
        }
    }
}
```

`sources/IcyUI.Tests/Design/DesignTestHost.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.Design;
using Icy.Markup;
using Icy.Tests.Markup;
using Icy.UI;

namespace Icy.Tests.Design
{
    /// <summary>
    /// A configuration with an attached <see cref="DesignSession"/>, and helpers to load tracked pages.
    /// </summary>
    internal sealed class DesignTestHost : IDisposable
    {
        public DesignTestHost()
        {
            Configuration = MarkupLoadObserverTests.CreateConfiguration();
            Configuration.Types.Markup.RegisterShortName<ClrBox>();
            Configuration.Types.Markup.RegisterShortName<Labeled>();
            Session = DesignSession.Attach(Configuration);
            Loader = new MarkupLoader(Configuration);
        }

        public IcyConfiguration Configuration { get; }

        public DesignSession Session { get; }

        public MarkupLoader Loader { get; }

        public static T Named<T>(UIElement root, string name)
            where T : UIElement =>
            (T)(MarkupNameScope.GetScope(root)?.Find(name) ?? throw new InvalidOperationException($"No element named '{name}'."));

        public (UIElement Root, DesignDocument Document) Load(string markup, string? sourcePath = "test.xml")
        {
            UIElement root = Loader.Load(markup, sourcePath);
            DesignDocument document = Session.FindDocument(root, out _)
                ?? throw new InvalidOperationException("The page wasn't tracked.");
            return (root, document);
        }

        public NodeId IdOf(object instance) =>
            Session.FindDocument(instance, out NodeId id) != null ? id : throw new InvalidOperationException($"'{instance}' isn't tracked.");

        public void Dispose() => Session.Dispose();
    }
}
```

`sources/IcyUI.Tests/Design/DesignSessionTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Bindings;
using Icy.Design;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class DesignSessionTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <Border x:Name="box" Width="10"/>
              <TextBlock x:Name="label" Text="{Binding Path=Message}"/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void Attach_InstallsTheObserverAndRefusesASecondSession()
        {
            using var host = new DesignTestHost();

            Assert.NotNull(host.Configuration.Types.Markup.LoadObserver);
            Assert.Throws<InvalidOperationException>(() => DesignSession.Attach(host.Configuration));
        }

        [Fact]
        public void Dispose_UninstallsTheObserver()
        {
            var host = new DesignTestHost();

            host.Dispose();

            Assert.Null(host.Configuration.Types.Markup.LoadObserver);
        }

        [Fact]
        public void FindDocument_MapsEveryMarkupElementToItsNode()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);

            foreach (string name in new[] { "root", "box", "label" })
            {
                UIElement element = DesignTestHost.Named<UIElement>(root, name);
                Assert.Same(document, host.Session.FindDocument(element, out NodeId id));
                ElementSyntax node = document.GetNode(id)!;
                Assert.Equal(element.GetType().Name, node.Name);
                Assert.Same(element, Assert.Single(document.GetObjects(id)));
            }
        }

        [Fact]
        public void Document_HoldsTheExactSourceText()
        {
            using var host = new DesignTestHost();

            (_, DesignDocument document) = host.Load(Page, "Pages/Main.xml");

            Assert.Equal(Page, document.Text);
            Assert.Equal("Pages/Main.xml", document.SourcePath);
            Assert.Equal(0, document.Version);
        }

        [Fact]
        public void Correlation_WorksWithCrlfTabsAndMultiLineAttributes()
        {
            using var host = new DesignTestHost();
            string markup = "<StackPanel x:Name=\"root\">\r\n\t<Border\r\n\t\tx:Name=\"box\"\r\n\t\tWidth=\"10\"/>\r\n\t<TextBlock x:Name=\"label\"/>\r\n</StackPanel>";

            (UIElement root, DesignDocument document) = host.Load(markup);

            Assert.Equal("Border", document.GetNode(host.IdOf(DesignTestHost.Named<Border>(root, "box")))!.Name);
            Assert.Equal("TextBlock", document.GetNode(host.IdOf(DesignTestHost.Named<TextBlock>(root, "label")))!.Name);
        }

        [Fact]
        public void Map_RecordsTheMemberAndBindingEachAttributeApplied()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var label = DesignTestHost.Named<TextBlock>(root, "label");

            Assert.True(document.Map.TryGetEntry(label, out var entry));
            Assert.Equal("Text", entry!.Members["Text"].Member.Name);
            IBinding binding = entry.Members["Text"].Binding!;
            Assert.Contains(binding, label.Bindings);
        }

        [Fact]
        public void RuntimeContentAndPagesLoadedBeforeAttach_AreUntracked()
        {
            var configuration = Icy.Tests.Markup.MarkupLoadObserverTests.CreateConfiguration();
            UIElement early = new MarkupLoader(configuration).Load("<StackPanel/>");
            using DesignSession session = DesignSession.Attach(configuration);
            var page = (StackPanel)new MarkupLoader(configuration).Load("<StackPanel/>");
            var runtime = new Border();
            page.Children.Add(runtime);

            Assert.Null(session.FindDocument(early, out _));
            Assert.Null(session.FindDocument(runtime, out _));
            Assert.NotNull(session.FindDocument(page, out _));
        }

        [Fact]
        public void SameFileLoadedTwice_SharesOneDocument()
        {
            using var host = new DesignTestHost();

            (UIElement first, DesignDocument document) = host.Load(Page, "Pages/Main.xml");
            (UIElement second, DesignDocument again) = host.Load(Page, "Pages/Main.xml");

            Assert.Same(document, again);
            Assert.Equal(2, document.GetObjects(host.IdOf(first)).Count);
            Assert.Contains(second, document.GetObjects(host.IdOf(first)));
        }

        [Fact]
        public void SamePathWithDifferentText_GetsItsOwnDocument()
        {
            using var host = new DesignTestHost();

            (_, DesignDocument first) = host.Load("<StackPanel/>", "Pages/Main.xml");
            (_, DesignDocument second) = host.Load("<Border/>", "Pages/Main.xml");

            Assert.NotSame(first, second);
        }

        [Fact]
        public void FailedLoad_IsNotTracked()
        {
            using var host = new DesignTestHost();

            Assert.Throws<MarkupException>(() => host.Loader.Load("<StackPanel Bogus=\"1\"/>", "bad.xml"));

            Assert.DoesNotContain(host.Session.Documents, x => x.SourcePath == "bad.xml");
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.DesignSessionTests"`
Expected: the build FAILS (`DesignSession`, `DesignDocument`, `NodeId` not found).

- [ ] **Step 3: Implement the small types**

`sources/IcyUI.Design/NodeId.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design
{
    /// <summary>
    /// Identifies one element of a <see cref="DesignDocument"/>, stably across edits.
    /// </summary>
    /// <remarks>
    /// Every edit re-parses the document into new syntax nodes; a <see cref="NodeId"/> is what stays the same for an
    /// element that survived the edit, including one that was moved. An element an edit inserted gets a new id.
    /// </remarks>
    /// <param name="Value">The id's raw value, unique within its document.</param>
    public readonly record struct NodeId(int Value)
    {
        /// <inheritdoc/>
        public override string ToString() => $"#{Value}";
    }
}
```

`sources/IcyUI.Design/DocumentChangedEventArgs.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design
{
    /// <summary>
    /// Describes a change to a <see cref="DesignDocument"/>'s text.
    /// </summary>
    /// <param name="changes">The changes, expressed against the text as it was right before this change.</param>
    /// <param name="version">The document's version after the change.</param>
    public sealed class DocumentChangedEventArgs(TextChangeSet changes, int version) : EventArgs
    {
        /// <summary>
        /// Gets the changes, expressed against the text as it was right before this change. Applying them to a copy
        /// of the previous text gives the new <see cref="DesignDocument.Text"/>.
        /// </summary>
        public TextChangeSet Changes { get; } = changes;

        /// <summary>
        /// Gets the document's version after the change.
        /// </summary>
        public int Version { get; } = version;
    }
}
```

`sources/IcyUI.Design/SubtreeReplacedEventArgs.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design
{
    /// <summary>
    /// Describes a live subtree an edit had to rebuild instead of updating in place.
    /// </summary>
    /// <remarks>
    /// Code that holds references to elements inside <see cref="OldElement"/> should drop them: they no longer belong
    /// to the page.
    /// </remarks>
    /// <param name="node">The markup element the subtree was built from.</param>
    /// <param name="oldElement">The subtree that was removed from the page.</param>
    /// <param name="newElement">The subtree that took its place.</param>
    public sealed class SubtreeReplacedEventArgs(NodeId node, UIElement oldElement, UIElement newElement) : EventArgs
    {
        /// <summary>
        /// Gets the markup element the subtree was built from.
        /// </summary>
        public NodeId Node { get; } = node;

        /// <summary>
        /// Gets the subtree that was removed from the page.
        /// </summary>
        public UIElement OldElement { get; } = oldElement;

        /// <summary>
        /// Gets the subtree that took its place.
        /// </summary>
        public UIElement NewElement { get; } = newElement;
    }
}
```

`sources/IcyUI.Design/Tracking/AppliedMember.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Bindings;
using Icy.Markup;

namespace Icy.Design.Tracking
{
    /// <summary>
    /// What one markup attribute did to one live object: the member it set, and the binding it attached, if any.
    /// </summary>
    internal sealed record AppliedMember(MarkupMember Member, IBinding? Binding);
}
```

`sources/IcyUI.Design/Tracking/ObjectMap.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Tracking
{
    /// <summary>
    /// Maps markup nodes to the live objects built from them, and back, without keeping any of them alive.
    /// </summary>
    /// <remarks>
    /// One node can have several objects: one per load of the same file. Node → objects uses weak references, and
    /// object → entry uses a <see cref="ConditionalWeakTable{TKey, TValue}"/>, so an element the game drops is
    /// collected as usual.
    /// </remarks>
    internal sealed class ObjectMap
    {
        private readonly Dictionary<NodeId, List<WeakReference<object>>> objectsByNode = [];
        private readonly ConditionalWeakTable<object, Entry> entries = new();

        public int Count
        {
            get
            {
                int count = 0;
                foreach (NodeId id in objectsByNode.Keys.ToList())
                    count += GetObjects(id).Count;
                return count;
            }
        }

        public void Add(NodeId id, object instance, MarkupLoadScope scope)
        {
            if (entries.TryGetValue(instance, out Entry? existing))
            {
                if (existing.Id == id)
                    return;
                Remove(instance);
            }

            entries.Add(instance, new Entry(id, scope));
            if (!objectsByNode.TryGetValue(id, out List<WeakReference<object>>? list))
                objectsByNode[id] = list = [];
            list.Add(new WeakReference<object>(instance));
        }

        public List<(object Instance, MarkupLoadScope Scope)> GetObjects(NodeId id)
        {
            var result = new List<(object, MarkupLoadScope)>();
            if (!objectsByNode.TryGetValue(id, out List<WeakReference<object>>? list))
                return result;

            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (list[i].TryGetTarget(out object? instance) && entries.TryGetValue(instance, out Entry? entry))
                    result.Add((instance, entry.Scope));
                else
                    list.RemoveAt(i);
            }

            if (list.Count == 0)
                objectsByNode.Remove(id);

            result.Reverse();
            return result;
        }

        public object? FindObject(NodeId id, MarkupLoadScope scope)
        {
            foreach ((object instance, MarkupLoadScope owner) in GetObjects(id))
            {
                if (ReferenceEquals(owner, scope))
                    return instance;
            }

            return null;
        }

        public object? FirstAlive()
        {
            foreach (NodeId id in objectsByNode.Keys.ToList())
            {
                if (GetObjects(id) is [var first, ..])
                    return first.Instance;
            }

            return null;
        }

        public bool TryGetEntry(object instance, [NotNullWhen(true)] out Entry? entry) => entries.TryGetValue(instance, out entry);

        public void RecordMember(object target, string attributeName, AppliedMember member)
        {
            if (entries.TryGetValue(target, out Entry? entry))
                entry.Members[attributeName] = member;
        }

        public void Remove(object instance)
        {
            if (!entries.TryGetValue(instance, out Entry? entry))
                return;

            entries.Remove(instance);
            if (objectsByNode.TryGetValue(entry.Id, out List<WeakReference<object>>? list))
                list.RemoveAll(x => !x.TryGetTarget(out object? target) || ReferenceEquals(target, instance));
        }

        public void RemoveNode(NodeId id)
        {
            foreach ((object instance, _) in GetObjects(id))
                entries.Remove(instance);
            objectsByNode.Remove(id);
        }

        /// <summary>
        /// Forgets <paramref name="root"/> and every mapped element whose parent chain leads to it.
        /// </summary>
        public void RemoveSubtree(UIElement root)
        {
            foreach (NodeId id in objectsByNode.Keys.ToList())
            {
                foreach ((object instance, _) in GetObjects(id))
                {
                    if (instance is UIElement element && IsSelfOrDescendant(element, root))
                        Remove(instance);
                }
            }
        }

        public void RemoveScope(MarkupLoadScope scope)
        {
            foreach (NodeId id in objectsByNode.Keys.ToList())
            {
                foreach ((object instance, MarkupLoadScope owner) in GetObjects(id))
                {
                    if (ReferenceEquals(owner, scope))
                        Remove(instance);
                }
            }
        }

        internal static bool IsSelfOrDescendant(UIElement candidate, UIElement root)
        {
            for (UIElement? element = candidate; element != null; element = element.Parent)
            {
                if (ReferenceEquals(element, root))
                    return true;
            }

            return false;
        }

        internal sealed class Entry(NodeId id, MarkupLoadScope scope)
        {
            public NodeId Id { get; } = id;

            public MarkupLoadScope Scope { get; } = scope;

            public Dictionary<string, AppliedMember> Members { get; } = new(StringComparer.Ordinal);
        }
    }
}
```

`sources/IcyUI.Design/Tracking/DesignLoadObserver.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.Data.Bindings;
using Icy.Markup;

namespace Icy.Design.Tracking
{
    /// <summary>
    /// Forwards the loader's notifications to the session and to the document each scope belongs to.
    /// </summary>
    /// <remarks>
    /// It observes documents and merged dictionaries only. Template instantiations are opaque in this phase, and not
    /// observing them keeps pooled item containers free of any tracking cost.
    /// </remarks>
    internal sealed class DesignLoadObserver(DesignSession session) : IMarkupLoadObserver
    {
        public MarkupLoadScopeKind ObservedKinds => MarkupLoadScopeKind.Document | MarkupLoadScopeKind.MergedDictionary;

        public void DocumentStarted(MarkupLoadScope scope) => session.OnDocumentStarted(scope);

        public void ObjectCreated(MarkupLoadScope scope, XElement node, object instance) =>
            session.GetDocument(scope)?.OnObjectCreated(scope, node, instance);

        public void MemberApplied(MarkupLoadScope scope, XObject node, object target, MarkupMember member, object? value, IBinding? binding) =>
            session.GetDocument(scope)?.OnMemberApplied(node, target, member, binding);

        public void DocumentCompleted(MarkupLoadScope scope, object root) => session.OnDocumentCompleted(scope);

        public void DocumentFailed(MarkupLoadScope scope, MarkupException error) => session.OnDocumentFailed(scope);
    }
}
```

- [ ] **Step 4: Implement the session and the document**

`sources/IcyUI.Design/DesignSession.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Runtime.CompilerServices;
using Icy.Configuration;
using Icy.Design.Tracking;
using Icy.Markup;

namespace Icy.Design
{
    /// <summary>
    /// Tracks every markup page a configuration loads, so the pages can be edited live and saved back as markup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attach a session <b>before</b> loading the pages you want to edit; pages loaded earlier aren't tracked.
    /// Release builds of a game shouldn't attach one at all, and needn't reference this assembly:
    /// </para>
    /// <code>
    /// #if DEBUG
    /// using DesignSession session = DesignSession.Attach(configuration);
    /// #endif
    /// </code>
    /// <para>
    /// The session never keeps a page alive. When a page is collected, its document is dropped the next time the
    /// session is asked for documents.
    /// </para>
    /// </remarks>
    public sealed class DesignSession : IDisposable
    {
        private readonly DesignLoadObserver observer;
        private readonly List<DesignDocument> documents = [];
        private readonly ConditionalWeakTable<MarkupLoadScope, DesignDocument> scopeDocuments = new();
        private bool disposed;

        private DesignSession(IcyConfiguration configuration)
        {
            Configuration = configuration;
            observer = new DesignLoadObserver(this);
            Builder = new MarkupLoader(configuration);
        }

        /// <summary>
        /// Gets the configuration this session tracks loads for.
        /// </summary>
        public IcyConfiguration Configuration { get; }

        /// <summary>
        /// Gets the documents of every tracked page still alive.
        /// </summary>
        public IReadOnlyList<DesignDocument> Documents
        {
            get
            {
                documents.RemoveAll(x => !x.IsAlive);
                return [.. documents];
            }
        }

        internal IMarkupBuilder Builder { get; }

        internal bool IsDisposed => disposed;

        /// <summary>
        /// Starts tracking every page <paramref name="configuration"/> loads from now on.
        /// </summary>
        /// <param name="configuration">The configuration whose loads to track.</param>
        /// <returns>The new session. Dispose it to stop tracking.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is <see langword="null"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="configuration"/> already has a <see cref="MarkupConfiguration.LoadObserver"/>, such as another
        /// session's.
        /// </exception>
        public static DesignSession Attach(IcyConfiguration configuration)
        {
            ArgumentNullException.ThrowIfNull(configuration);

            MarkupConfiguration markup = configuration.Types.Markup;
            if (markup.LoadObserver != null)
                throw new InvalidOperationException("This configuration already has a markup load observer. Dispose the other design session first.");

            var session = new DesignSession(configuration);
            markup.LoadObserver = session.observer;
            return session;
        }

        /// <summary>
        /// Finds the document a live object was built from, and the markup element it was built from.
        /// </summary>
        /// <param name="instance">A live object, typically a <see cref="Icy.UI.UIElement"/> of a tracked page.</param>
        /// <param name="node">The markup element <paramref name="instance"/> was built from.</param>
        /// <returns>
        /// The document, or <see langword="null"/> when <paramref name="instance"/> doesn't come from a tracked page:
        /// it was added by code at runtime, or its page was loaded before the session was attached.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> is <see langword="null"/>.</exception>
        public DesignDocument? FindDocument(object instance, out NodeId node)
        {
            ArgumentNullException.ThrowIfNull(instance);

            foreach (DesignDocument document in Documents)
            {
                if (document.TryGetNodeId(instance, out node))
                    return document;
            }

            node = default;
            return null;
        }

        /// <summary>
        /// Stops tracking loads, and detaches every document. Their editors throw <see cref="ObjectDisposedException"/>
        /// from then on.
        /// </summary>
        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            MarkupConfiguration markup = Configuration.Types.Markup;
            if (ReferenceEquals(markup.LoadObserver, observer))
                markup.LoadObserver = null;

            documents.Clear();
        }

        internal DesignDocument? GetDocument(MarkupLoadScope scope) =>
            !disposed && scopeDocuments.TryGetValue(scope, out DesignDocument? document) ? document : null;

        internal void OnDocumentStarted(MarkupLoadScope scope)
        {
            if (disposed)
                return;

            string text = scope.SourceText ?? string.Empty;
            DesignDocument? document = null;
            if (scope.SourcePath != null)
            {
                foreach (DesignDocument candidate in documents)
                {
                    if (candidate.SourcePath == scope.SourcePath && candidate.Text == text)
                    {
                        document = candidate;
                        break;
                    }
                }
            }

            document ??= new DesignDocument(this, scope.SourcePath, text);
            document.AddScope(scope);
            scopeDocuments.AddOrUpdate(scope, document);
        }

        internal void OnDocumentCompleted(MarkupLoadScope scope)
        {
            if (GetDocument(scope) is { } document && !documents.Contains(document))
                documents.Add(document);
        }

        internal void OnDocumentFailed(MarkupLoadScope scope)
        {
            if (GetDocument(scope) is not { } document)
                return;

            document.RemoveScope(scope);
            scopeDocuments.Remove(scope);
        }
    }
}
```

`sources/IcyUI.Design/DesignDocument.cs` (later tasks add members to it):

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml;
using System.Xml.Linq;
using Icy.Data.Bindings;
using Icy.Data.Markup;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.Design.Tracking;
using Icy.Markup;

namespace Icy.Design
{
    /// <summary>
    /// One tracked markup file: its text, its syntax tree, and the live objects built from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The text is the source of truth. Every edit made through <see cref="Editor"/> changes the text with a minimal
    /// <see cref="TextChangeSet"/>, re-parses it, and mirrors the change onto every live tree built from this file.
    /// Saving writes the text as is, so formatting, comments and anything the editor doesn't understand survive.
    /// </para>
    /// <para>
    /// A document must be used on the thread that owns its pages, like the pages themselves.
    /// </para>
    /// </remarks>
    public sealed class DesignDocument
    {
        private readonly List<MarkupLoadScope> scopes = [];
        private MarkupText text;
        private DocumentSyntax syntax;
        private LineMap lineMap;
        private Dictionary<ElementSyntax, NodeId> ids = [];
        private Dictionary<NodeId, ElementSyntax> nodes = [];
        private int nextNodeId;
        private FragmentFrame? fragment;
        private int recordingSuppressed;

        internal DesignDocument(DesignSession session, string? sourcePath, string text)
        {
            Session = session;
            SourcePath = sourcePath;
            this.text = new MarkupText(text);
            syntax = DocumentSyntax.Parse(text);
            lineMap = new LineMap(text);
            foreach (ElementSyntax element in syntax.Elements)
                AssignNewId(element, ids, nodes);
        }

        /// <summary>
        /// Occurs after the text changed, with the exact changes made.
        /// </summary>
        public event EventHandler<DocumentChangedEventArgs>? Changed;

        /// <summary>
        /// Occurs when an edit had to rebuild a live subtree instead of updating it in place.
        /// </summary>
        public event EventHandler<SubtreeReplacedEventArgs>? SubtreeReplaced;

        /// <summary>
        /// Occurs when <see cref="NeedsReload"/> becomes <see langword="true"/>.
        /// </summary>
        public event EventHandler? NeedsReloadChanged;

        /// <summary>
        /// Occurs when the syntax tree's <see cref="DocumentSyntax.Diagnostics"/> changed.
        /// </summary>
        public event EventHandler? DiagnosticsChanged;

        /// <summary>
        /// Gets the session tracking this document.
        /// </summary>
        public DesignSession Session { get; }

        /// <summary>
        /// Gets the path the document was loaded from, as passed to the loader, or <see langword="null"/>.
        /// </summary>
        public string? SourcePath { get; }

        /// <summary>
        /// Gets the current markup text.
        /// </summary>
        public string Text => text.Text;

        /// <summary>
        /// Gets the text's version: 0 when loaded, plus one for every change.
        /// </summary>
        public int Version => text.Version;

        /// <summary>
        /// Gets the current syntax tree.
        /// </summary>
        public DocumentSyntax Syntax => syntax;

        /// <summary>
        /// Gets a value indicating whether an edit changed the text in a way the live pages couldn't follow (an edit
        /// inside a style, a template or a property element, or a change to the root that needs a new root object).
        /// The text is still right; reload the page to see it.
        /// </summary>
        public bool NeedsReload { get; private set; }

        internal ObjectMap Map { get; } = new();

        internal IReadOnlyList<MarkupLoadScope> Scopes => scopes;

        internal PropertyRegistry Registry => Session.Configuration.Types.PropertyRegistry;

        internal bool IsAlive => Map.FirstAlive() != null;

        internal int TrackedObjectCount => Map.Count;

        /// <summary>
        /// Gets the markup element with the given id in the current syntax tree.
        /// </summary>
        /// <param name="id">The element's id.</param>
        /// <returns>The element, or <see langword="null"/> when no element of the current text has that id.</returns>
        public ElementSyntax? GetNode(NodeId id) => nodes.GetValueOrDefault(id);

        /// <summary>
        /// Gets the id of an element of the current syntax tree.
        /// </summary>
        /// <param name="element">An element of <see cref="Syntax"/>.</param>
        /// <returns>The id, or <see langword="null"/> when <paramref name="element"/> isn't part of the current tree.</returns>
        public NodeId? GetNodeId(ElementSyntax element) => ids.TryGetValue(element, out NodeId id) ? id : null;

        /// <summary>
        /// Gets the live objects built from a markup element: one per load of this file still alive.
        /// </summary>
        /// <param name="id">The element's id.</param>
        /// <returns>The live objects; empty when none are alive or the element is opaque (inside a template, say).</returns>
        public IReadOnlyList<object> GetObjects(NodeId id) => [.. Map.GetObjects(id).Select(x => x.Instance)];

        /// <summary>
        /// Finds the markup element a live object was built from.
        /// </summary>
        /// <param name="instance">The live object.</param>
        /// <param name="id">The element's id.</param>
        /// <returns><see langword="true"/> when <paramref name="instance"/> was built from this document.</returns>
        public bool TryGetNodeId(object instance, out NodeId id)
        {
            ArgumentNullException.ThrowIfNull(instance);
            if (Map.TryGetEntry(instance, out ObjectMap.Entry? entry))
            {
                id = entry.Id;
                return true;
            }

            id = default;
            return false;
        }

        internal void AddScope(MarkupLoadScope scope) => scopes.Add(scope);

        internal void RemoveScope(MarkupLoadScope scope)
        {
            scopes.Remove(scope);
            Map.RemoveScope(scope);
        }

        internal void OnObjectCreated(MarkupLoadScope scope, XElement node, object instance)
        {
            if (recordingSuppressed > 0 || ToOffset(node) is not int offset)
                return;

            if (syntax.FindElementByNameStart(offset) is { } element && ids.TryGetValue(element, out NodeId id))
                Map.Add(id, instance, scope);
        }

        internal void OnMemberApplied(XObject node, object target, MarkupMember member, IBinding? binding)
        {
            if (recordingSuppressed > 0 || node is not XAttribute || ToOffset(node) is not int offset)
                return;

            if (syntax.FindAttributeByNameStart(offset) is { } attribute)
                Map.RecordMember(target, attribute.Name, new AppliedMember(member, binding));
        }

        /// <summary>
        /// Makes the line information of objects built from a fragment of the current text correlate with the
        /// document: positions are taken relative to <paramref name="fragmentText"/>, which starts at
        /// <paramref name="baseOffset"/>.
        /// </summary>
        internal IDisposable EnterFragment(int baseOffset, string fragmentText)
        {
            FragmentFrame? previous = fragment;
            fragment = new FragmentFrame(baseOffset, new LineMap(fragmentText));
            return new Restorer(() => fragment = previous);
        }

        /// <summary>
        /// Stops recording builder notifications, for updates that must keep the existing records as they are.
        /// </summary>
        internal IDisposable SuppressRecording()
        {
            recordingSuppressed++;
            return new Restorer(() => recordingSuppressed--);
        }

        internal void RaiseSubtreeReplaced(SubtreeReplacedEventArgs args) => SubtreeReplaced?.Invoke(this, args);

        internal void MarkNeedsReload()
        {
            if (NeedsReload)
                return;

            NeedsReload = true;
            NeedsReloadChanged?.Invoke(this, EventArgs.Empty);
        }

        private void AssignNewId(ElementSyntax element, Dictionary<ElementSyntax, NodeId> idMap, Dictionary<NodeId, ElementSyntax> nodeMap)
        {
            var id = new NodeId(++nextNodeId);
            idMap[element] = id;
            nodeMap[id] = element;
        }

        private int? ToOffset(IXmlLineInfo node)
        {
            if (!node.HasLineInfo())
                return null;

            if (fragment is { } frame)
                return frame.LineMap.TryToOffset(node.LineNumber, node.LinePosition, out int local) ? frame.BaseOffset + local : null;

            return lineMap.TryToOffset(node.LineNumber, node.LinePosition, out int offset) ? offset : null;
        }

        private sealed record FragmentFrame(int BaseOffset, LineMap LineMap);

        private sealed class Restorer(Action restore) : IDisposable
        {
            private Action? restore = restore;

            public void Dispose()
            {
                restore?.Invoke();
                restore = null;
            }
        }
    }
}
```

`Editor` (Task 8) and the other members referenced in the class remarks arrive in later tasks. Until Task 8, leave the `<see cref="Editor"/>` reference as plain text: write it as `<c>Editor</c>` so the build has no `CS1574` warning, and switch it back to `<see cref="Editor"/>` in Task 8.

- [ ] **Step 5: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design.DesignSessionTests"`
Expected: PASS, Total 10.

- [ ] **Step 6: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (no failures), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design
git commit -m "Track loaded markup pages in a design session

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: The edit pipeline: apply, re-sync, roll back

**Files:**
- Create: `sources/IcyUI.Design/EditResult.cs`
- Create in `sources/IcyUI.Design/Editing/`: `EditStep.cs`, `MirrorAction.cs`, `MirrorContext.cs`, `DocumentSnapshot.cs`, `MoveHint.cs`, `DesignEditException.cs`
- Modify: `sources/IcyUI.Design/DesignDocument.cs` (add `Apply`, `Resync`, `Restore`, `VerifyAccess`, `IsEditable`)
- Test: `sources/IcyUI.Tests/Design/DesignDocumentApplyTests.cs`

**Interfaces:**
- Consumes: `DesignDocument` internals (Task 6), `TextChangeSet.MapPosition`/`Invert` (Task 1).
- Produces:
  - `public sealed class EditResult` with `Succeeded`, `Node` (`NodeId?`), `Error` (`Diagnostic?`); internal `static Success(NodeId? node = null)`, `static Failure(TextSpan span, string message)`.
  - Internal (namespace `Icy.Design.Editing`):
    - `EditStep(TextChangeSet change, IReadOnlyList<MirrorAction> actions, string description)`.
    - `abstract class MirrorAction` with `virtual MoveHint? Hint`, `abstract Execute(MirrorContext)`, `abstract Revert(MirrorContext)`, `abstract MirrorAction CreateInverse(MirrorContext)`.
    - `MirrorContext(DesignDocument document, DocumentSnapshot before)` with `Document`, `Before`, `ResultNode`, `GetOldNode(NodeId)`, `GetOldId(ElementSyntax)`.
    - `record DocumentSnapshot(MarkupText Text, DocumentSyntax Syntax, LineMap LineMap, Dictionary<ElementSyntax, NodeId> Ids, Dictionary<NodeId, ElementSyntax> Nodes)`.
    - `readonly record struct MoveHint(NodeId Node, int NewStart)`.
    - `sealed class DesignEditException(string message) : Exception`.
  - `DesignDocument`: internal `EditResult Apply(EditStep step, out EditStep? inverse)`, `void VerifyAccess()`, `bool IsEditable(ElementSyntax element)`, `bool IsEditable(ElementSyntax element, IReadOnlyDictionary<ElementSyntax, NodeId> treeIds)`.

**How `Apply` works.** It applies the text change, re-parses, and re-syncs node ids. Then it runs every mirror action. If any action throws, it restores the old text and tree and reverts the executed actions in reverse order, the failing one first. Only on success does it drop vanished nodes from the map, build the inverse step and raise `Changed`. Node ids are carried across the re-parse by mapping each old element's start offset through the change set (`TextChangeSet.MapPosition`), plus explicit hints for moved elements.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/DesignDocumentApplyTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Editing;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Design
{
    public class DesignDocumentApplyTests
    {
        private static readonly string Page =
            """
            <StackPanel>
              <Border Width="10"/>
              <TextBlock/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void Apply_UpdatesTextAndVersionAndRaisesChanged()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            var raised = new List<DocumentChangedEventArgs>();
            document.Changed += (_, e) => raised.Add(e);
            TextChangeSet change = ReplaceWidth(document, "20");

            EditResult result = document.Apply(new EditStep(change, [], "test"), out _);

            Assert.True(result.Succeeded);
            Assert.Contains("Width=\"20\"", document.Text);
            Assert.Equal(1, document.Version);
            DocumentChangedEventArgs args = Assert.Single(raised);
            Assert.Same(change, args.Changes);
            Assert.Equal(1, args.Version);
        }

        [Fact]
        public void Apply_KeepsTheIdsOfElementsTheChangeDidNotReplace()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            NodeId[] before = [.. document.Syntax.Elements.Select(x => document.GetNodeId(x)!.Value)];

            document.Apply(new EditStep(ReplaceWidth(document, "123456"), [], "test"), out _);

            Assert.Equal(before, document.Syntax.Elements.Select(x => document.GetNodeId(x)!.Value));
        }

        [Fact]
        public void Apply_AReplacedElementGetsANewIdAndTheOldOneVanishes()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            ElementSyntax block = document.Syntax.Elements.Single(x => x.Name == "TextBlock");
            NodeId oldId = document.GetNodeId(block)!.Value;

            document.Apply(new EditStep(new TextChangeSet([new TextChange(block.Span, "<Button/>")]), [], "test"), out _);

            ElementSyntax button = document.Syntax.Elements.Single(x => x.Name == "Button");
            Assert.NotEqual(oldId, document.GetNodeId(button));
            Assert.Null(document.GetNode(oldId));
            Assert.Empty(document.GetObjects(oldId));
        }

        [Fact]
        public void Apply_ReturnsAnInverseThatRestoresTheTextExactly()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);

            document.Apply(new EditStep(ReplaceWidth(document, "20"), [], "test"), out EditStep? inverse);
            document.Apply(inverse!, out _);

            Assert.Equal(Page, document.Text);
        }

        [Fact]
        public void Apply_AFailingAction_RollsBackTheTextAndRevertsInReverseOrder()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            var log = new List<string>();
            int changes = 0;
            document.Changed += (_, _) => changes++;

            EditResult result = document.Apply(
                new EditStep(ReplaceWidth(document, "20"), [new RecordingAction(log, "A"), new RecordingAction(log, "B", fail: true)], "test"),
                out EditStep? inverse);

            Assert.False(result.Succeeded);
            Assert.Equal("boom", result.Error!.Value.Message);
            Assert.Null(inverse);
            Assert.Equal(Page, document.Text);
            Assert.Equal(0, document.Version);
            Assert.Equal(0, changes);
            Assert.Equal(["A.Execute", "B.Execute", "B.Revert", "A.Revert"], log);
        }

        [Fact]
        public void Apply_ThatWouldMakeTheMarkupMalformed_Fails()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            TextSpan endTag = document.Syntax.Root!.EndTagSpan!.Value;

            EditResult result = document.Apply(new EditStep(new TextChangeSet([new TextChange(endTag, string.Empty)]), [], "test"), out _);

            Assert.False(result.Succeeded);
            Assert.Equal(Page, document.Text);
        }

        [Fact]
        public async Task Apply_FromAnotherThread_Throws()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            var step = new EditStep(ReplaceWidth(document, "20"), [], "test");

            await Assert.ThrowsAsync<InvalidOperationException>(() => Task.Run(() => { document.Apply(step, out _); }));
        }

        private static TextChangeSet ReplaceWidth(DesignDocument document, string value)
        {
            AttributeSyntax width = document.Syntax.Elements.Single(x => x.Name == "Border").FindAttribute("Width")!;
            return new TextChangeSet([new TextChange(width.ValueSpan, value)]);
        }

        private sealed class RecordingAction(List<string> log, string name, bool fail = false) : MirrorAction
        {
            public override void Execute(MirrorContext context)
            {
                log.Add($"{name}.Execute");
                if (fail)
                    throw new DesignEditException("boom");
            }

            public override void Revert(MirrorContext context) => log.Add($"{name}.Revert");

            public override MirrorAction CreateInverse(MirrorContext context) => new RecordingAction(log, name + "'");
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DesignDocumentApplyTests"`
Expected: the build FAILS (`EditStep`, `MirrorAction`, `EditResult`, `Apply` not found).

- [ ] **Step 3: Implement the pipeline types**

`sources/IcyUI.Design/EditResult.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Design.Text;

namespace Icy.Design
{
    /// <summary>
    /// The outcome of an edit.
    /// </summary>
    /// <remarks>
    /// An edit is all-or-nothing: when it fails, neither the text nor any live page changed.
    /// </remarks>
    public sealed class EditResult
    {
        private EditResult(bool succeeded, NodeId? node, Diagnostic? error)
        {
            Succeeded = succeeded;
            Node = node;
            Error = error;
        }

        /// <summary>
        /// Gets a value indicating whether the edit was made.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the element the edit created, such as the one <see cref="MarkupEditor.InsertElement"/> inserted;
        /// otherwise <see langword="null"/>.
        /// </summary>
        public NodeId? Node { get; }

        /// <summary>
        /// Gets why the edit failed, or <see langword="null"/> when it succeeded.
        /// </summary>
        public Diagnostic? Error { get; }

        /// <inheritdoc/>
        public override string ToString() => Succeeded ? "Succeeded" : $"Failed: {Error}";

        internal static EditResult Success(NodeId? node = null) => new(true, node, null);

        internal static EditResult Failure(TextSpan span, string message) => new(false, null, new Diagnostic(span, message));
    }
}
```

The `<see cref="MarkupEditor.InsertElement"/>` above needs Task 8; until then write it as `<c>MarkupEditor.InsertElement</c>` and restore the `cref` in Task 8.

`sources/IcyUI.Design/Editing/EditStep.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// One atomic edit: a text change, and the actions that mirror it onto the live pages.
    /// </summary>
    internal sealed class EditStep(TextChangeSet change, IReadOnlyList<MirrorAction> actions, string description)
    {
        public TextChangeSet Change { get; } = change;

        public IReadOnlyList<MirrorAction> Actions { get; } = actions;

        public string Description { get; } = description;
    }
}
```

`sources/IcyUI.Design/Editing/MirrorAction.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Mirrors one part of a text edit onto the live pages.
    /// </summary>
    /// <remarks>
    /// <see cref="Execute"/> runs after the document already holds the new text and tree. When a later action of the
    /// same step fails, the document is restored to the old text and tree first, then <see cref="Revert"/> runs, so
    /// an action must be able to undo whatever part of <see cref="Execute"/> got done, including when
    /// <see cref="Execute"/> itself threw halfway. <see cref="CreateInverse"/> runs only after the whole step
    /// succeeded.
    /// </remarks>
    internal abstract class MirrorAction
    {
        /// <summary>
        /// Gets the element this action moves and where it lands, so its id survives the re-parse.
        /// </summary>
        public virtual MoveHint? Hint => null;

        public abstract void Execute(MirrorContext context);

        public abstract void Revert(MirrorContext context);

        public abstract MirrorAction CreateInverse(MirrorContext context);
    }
}
```

`sources/IcyUI.Design/Editing/MirrorContext.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;

namespace Icy.Design.Editing
{
    /// <summary>
    /// What a <see cref="MirrorAction"/> can see: the document (already holding the new text) and its state from
    /// before the step.
    /// </summary>
    internal sealed class MirrorContext(DesignDocument document, DocumentSnapshot before)
    {
        public DesignDocument Document { get; } = document;

        public DocumentSnapshot Before { get; } = before;

        /// <summary>
        /// Gets or sets the element the step created, reported back through <see cref="EditResult.Node"/>.
        /// </summary>
        public NodeId? ResultNode { get; set; }

        public ElementSyntax? GetOldNode(NodeId id) => Before.Nodes.GetValueOrDefault(id);

        public NodeId? GetOldId(ElementSyntax element) => Before.Ids.TryGetValue(element, out NodeId id) ? id : null;
    }
}
```

`sources/IcyUI.Design/Editing/DocumentSnapshot.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// A document's text, tree and id maps at one moment, so a failed step can put them back.
    /// </summary>
    internal sealed record DocumentSnapshot(
        MarkupText Text,
        DocumentSyntax Syntax,
        LineMap LineMap,
        Dictionary<ElementSyntax, NodeId> Ids,
        Dictionary<NodeId, ElementSyntax> Nodes);
}
```

`sources/IcyUI.Design/Editing/MoveHint.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Tells the re-parse that the element <paramref name="Node"/> now starts at <paramref name="NewStart"/>, so it
    /// keeps its id even though its old text was deleted.
    /// </summary>
    internal readonly record struct MoveHint(NodeId Node, int NewStart);
}
```

`sources/IcyUI.Design/Editing/DesignEditException.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Raised by a mirror action for an edit that can't be made. The pipeline turns it into a failed
    /// <see cref="EditResult"/>.
    /// </summary>
    internal sealed class DesignEditException(string message) : Exception(message);
}
```

- [ ] **Step 4: Add the pipeline to `DesignDocument`**

Add `using Icy.Data.Markup;` (for `DispatcherObject`; it's already there for `PropertyRegistry`) and `using Icy.Design.Editing;`. Then add these members (internal methods after `TryGetNodeId`, private ones after `ToOffset`):

```csharp
        internal EditResult Apply(EditStep step, out EditStep? inverse)
        {
            inverse = null;
            VerifyAccess();

            var before = new DocumentSnapshot(text, syntax, lineMap, ids, nodes);
            MarkupText newText = text.Apply(step.Change);
            DocumentSyntax newSyntax = DocumentSyntax.Parse(newText.Text);
            if (newSyntax.HasErrors && !syntax.HasErrors)
            {
                Diagnostic first = newSyntax.Diagnostics[0];
                return EditResult.Failure(first.Span, $"The edit would make the markup malformed: {first.Message}");
            }

            List<NodeId> vanished = Resync(newText, newSyntax, step.Change, step.Actions);
            var context = new MirrorContext(this, before);
            int executed = 0;
            try
            {
                for (; executed < step.Actions.Count; executed++)
                    step.Actions[executed].Execute(context);
            }
            catch (Exception ex)
            {
                Restore(before);
                for (int i = Math.Min(executed, step.Actions.Count - 1); i >= 0; i--)
                    step.Actions[i].Revert(context);

                if (ex is MarkupException or DesignEditException)
                    return EditResult.Failure(step.Change.Count > 0 ? new TextSpan(step.Change[0].Span.Start, 0) : default, ex.Message);
                throw;
            }

            foreach (NodeId id in vanished)
                Map.RemoveNode(id);

            var inverseActions = new List<MirrorAction>(step.Actions.Count);
            for (int i = step.Actions.Count - 1; i >= 0; i--)
                inverseActions.Add(step.Actions[i].CreateInverse(context));
            inverse = new EditStep(step.Change.Invert(before.Text.Text), inverseActions, step.Description);

            Changed?.Invoke(this, new DocumentChangedEventArgs(step.Change, Version));
            if (before.Syntax.Diagnostics.Count != syntax.Diagnostics.Count)
                DiagnosticsChanged?.Invoke(this, EventArgs.Empty);

            return EditResult.Success(context.ResultNode);
        }

        /// <summary>
        /// Throws when called from a thread other than the one owning this document's pages.
        /// </summary>
        /// <exception cref="InvalidOperationException">The calling thread doesn't own the pages.</exception>
        internal void VerifyAccess()
        {
            if (Map.FirstAlive() is DispatcherObject owner)
                owner.VerifyAccess();
        }

        /// <summary>
        /// Determines whether edits to <paramref name="element"/> can be mirrored onto the live pages in this phase.
        /// </summary>
        internal bool IsEditable(ElementSyntax element) => IsEditable(element, ids);

        internal bool IsEditable(ElementSyntax element, IReadOnlyDictionary<ElementSyntax, NodeId> treeIds)
        {
            // Styles, resources, templates and anything else under a property element are opaque in this phase.
            for (ElementSyntax? current = element; current != null; current = current.Parent)
            {
                if (current.IsPropertyElement)
                    return false;
            }

            if (!treeIds.TryGetValue(element, out NodeId id))
                return false;

            List<(object Instance, MarkupLoadScope Scope)> objects = Map.GetObjects(id);
            return objects.Count > 0 && objects.TrueForAll(x => x.Instance is UI.UIElement);
        }
```

```csharp
        private List<NodeId> Resync(MarkupText newText, DocumentSyntax newSyntax, TextChangeSet change, IReadOnlyList<MirrorAction> actions)
        {
            var newIds = new Dictionary<ElementSyntax, NodeId>();
            var newNodes = new Dictionary<NodeId, ElementSyntax>();

            // Moved elements first: their old text was deleted, so offset mapping alone can't find them.
            foreach (MirrorAction action in actions)
            {
                if (action.Hint is { } hint && nodes.TryGetValue(hint.Node, out ElementSyntax? moved) && newSyntax.FindElementAt(hint.NewStart) is { } target)
                    MatchSubtree(moved, target, newIds, newNodes);
            }

            // Everything else: an element survives when its start offset survives the change.
            foreach ((ElementSyntax oldElement, NodeId id) in ids)
            {
                if (newNodes.ContainsKey(id) || change.MapPosition(oldElement.Span.Start) is not int mapped)
                    continue;

                if (newSyntax.FindElementAt(mapped) is { } candidate && candidate.Name == oldElement.Name && !newIds.ContainsKey(candidate))
                {
                    newIds[candidate] = id;
                    newNodes[id] = candidate;
                }
            }

            foreach (ElementSyntax element in newSyntax.Elements)
            {
                if (!newIds.ContainsKey(element))
                    AssignNewId(element, newIds, newNodes);
            }

            List<NodeId> vanished = [.. ids.Values.Where(x => !newNodes.ContainsKey(x))];
            text = newText;
            syntax = newSyntax;
            lineMap = new LineMap(newText.Text);
            ids = newIds;
            nodes = newNodes;
            return vanished;
        }

        private void MatchSubtree(ElementSyntax oldElement, ElementSyntax newElement, Dictionary<ElementSyntax, NodeId> newIds, Dictionary<NodeId, ElementSyntax> newNodes)
        {
            if (oldElement.Name != newElement.Name || !ids.TryGetValue(oldElement, out NodeId id))
                return;

            newIds[newElement] = id;
            newNodes[id] = newElement;

            // A move carries the element's text over verbatim, so its children line up one to one.
            using IEnumerator<ElementSyntax> oldChildren = oldElement.Elements.GetEnumerator();
            using IEnumerator<ElementSyntax> newChildren = newElement.Elements.GetEnumerator();
            while (oldChildren.MoveNext() && newChildren.MoveNext())
                MatchSubtree(oldChildren.Current, newChildren.Current, newIds, newNodes);
        }

        private void Restore(DocumentSnapshot snapshot)
        {
            text = snapshot.Text;
            syntax = snapshot.Syntax;
            lineMap = snapshot.LineMap;
            ids = snapshot.Ids;
            nodes = snapshot.Nodes;
        }
```

- [ ] **Step 5: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DesignDocumentApplyTests"`
Expected: PASS, Total 7.

- [ ] **Step 6: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (no failures), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/DesignDocumentApplyTests.cs
git commit -m "Add the atomic edit pipeline to design documents

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: `MarkupEditor`: insert, remove and move elements

**Files:**
- Create: `sources/IcyUI.Design/MarkupEditor.cs`
- Create in `sources/IcyUI.Design/Editing/`: `MarkupFormatting.cs`, `LiveContent.cs`, `LiveTree.cs`, `ElementInsertedAction.cs`, `ElementRemovedAction.cs`, `ElementMovedAction.cs`
- Modify: `sources/IcyUI.Design/DesignDocument.cs` (add `Editor`, `FindObject`, `BuildElement`, `ComputeLiveIndex`; restore the `<see cref="Editor"/>` in the class remarks)
- Modify: `sources/IcyUI.Design/EditResult.cs` (restore the `<see cref="MarkupEditor.InsertElement"/>`)
- Test: `sources/IcyUI.Tests/Design/MarkupEditorStructureTests.cs`

**Interfaces:**
- Consumes: `DesignDocument.Apply`, `IsEditable`, `MirrorAction`, `MirrorContext`, `EditStep`, `DesignEditException` (Task 7); `IMarkupBuilder` (Task 5); `ObjectMap` (Task 6).
- Produces:
  - `public sealed class MarkupEditor` with `Document`, `EditResult InsertElement(NodeId parent, int index, string markup)`, `EditResult RemoveElement(NodeId node)`, `EditResult MoveElement(NodeId node, NodeId newParent, int index)`; internal `EditResult Execute(EditStep step)`.
  - `public MarkupEditor DesignDocument.Editor`.
  - Internal `DesignDocument`: `object? FindObject(NodeId, MarkupLoadScope)`, `UIElement BuildElement(MarkupLoadScope scope, ElementSyntax element, UIElement liveParent)`, `int ComputeLiveIndex(ElementSyntax element, object liveParent, MarkupLoadScope scope)`.
  - Internal (namespace `Icy.Design.Editing`):
    - `static class MarkupFormatting`: `GetLineStart`, `StartsLine`, `GetIndentation`, `DetectNewLine`, `DetectIndentUnit`, `Reindent`, `GetRemovalSpan`, `CreateInsertion`.
    - `static class LiveContent`: `TryResolve`, `Accepts`, `IndexOf`, `Count`, `Insert`, `Remove`, `Replace`.
    - `static class LiveTree`: `UnregisterNames(MarkupLoadScope, UIElement)`, `RegisterNames(MarkupLoadScope, IEnumerable<(string, UIElement)>)`.
    - `ElementInsertedAction(NodeId parent, int start)`, `ElementRemovedAction(NodeId node)`, `ElementMovedAction(NodeId node, NodeId newParent, int newStart)`.

**Index semantics.** `index` counts the parent's **content** children, not property elements such as `<Grid.RowDefinitions>`. For `MoveElement`, it counts the new parent's content children with the moved element left out.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/MarkupEditorStructureTests.cs`:

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
    public class MarkupEditorStructureTests
    {
        private const string Page =
            """
            <StackPanel x:Name="root">
              <Border x:Name="a" Width="10"/>
              <Border x:Name="b" Width="20"/>
            </StackPanel>
            """;

        private const string MovePage =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10"/>
              </StackPanel>
              <StackPanel x:Name="right">
                <TextBlock Text="A"/>
              </StackPanel>
            </StackPanel>
            """;

        [Fact]
        public void Insert_AppendsALineStyledChildAndBuildsItLive()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            EditResult result = document.Editor.InsertElement(host.IdOf(root), 2, "<TextBlock Text=\"Hi\"/>");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="a" Width="10"/>
                  <Border x:Name="b" Width="20"/>
                  <TextBlock Text="Hi"/>
                </StackPanel>
                """),
                document.Text);
            var block = Assert.IsType<TextBlock>(((StackPanel)root).Children[2]);
            Assert.Equal("Hi", block.Text);
            Assert.Equal(result.Node, host.IdOf(block));
        }

        [Fact]
        public void Insert_AtAnIndex_GoesBeforeThatChild()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            document.Editor.InsertElement(host.IdOf(root), 1, "<TextBlock/>");

            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="a" Width="10"/>
                  <TextBlock/>
                  <Border x:Name="b" Width="20"/>
                </StackPanel>
                """),
                document.Text);
            Assert.IsType<TextBlock>(((StackPanel)root).Children[1]);
        }

        [Fact]
        public void Insert_IntoASelfClosingParent_ExpandsIt()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <StackPanel x:Name="root">
                  <StackPanel x:Name="inner"/>
                </StackPanel>
                """));
            var inner = DesignTestHost.Named<StackPanel>(root, "inner");

            document.Editor.InsertElement(host.IdOf(inner), 0, "<Border/>");

            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <StackPanel x:Name="inner">
                    <Border/>
                  </StackPanel>
                </StackPanel>
                """),
                document.Text);
            Assert.IsType<Border>(Assert.Single(inner.Children));
        }

        [Fact]
        public void Insert_KeepsCrlfLineEndings()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page.ReplaceLineEndings("\r\n"));

            document.Editor.InsertElement(host.IdOf(root), 2, "<TextBlock/>");

            Assert.Contains("\r\n  <TextBlock/>\r\n", document.Text);
            Assert.DoesNotContain("\n", document.Text.Replace("\r\n", string.Empty, StringComparison.Ordinal));
        }

        [Fact]
        public void Insert_ResolvesAPrefixDeclaredOnAnAncestor()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                $"""
                <ui:StackPanel xmlns:ui="{MarkupNamespaces.Default}">
                  <ui:Border/>
                </ui:StackPanel>
                """));

            EditResult result = document.Editor.InsertElement(host.IdOf(root), 1, "<ui:TextBlock Text=\"p\"/>");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal("p", Assert.IsType<TextBlock>(((StackPanel)root).Children[1]).Text);
        }

        [Fact]
        public void Insert_IntoASingleChildParentThatHasOne_Fails()
        {
            using var host = new DesignTestHost();
            string page = N(
                """
                <StackPanel x:Name="root">
                  <Border x:Name="frame">
                    <TextBlock/>
                  </Border>
                </StackPanel>
                """);
            (UIElement root, DesignDocument document) = host.Load(page);

            EditResult result = document.Editor.InsertElement(host.IdOf(DesignTestHost.Named<Border>(root, "frame")), 1, "<TextBlock/>");

            Assert.False(result.Succeeded);
            Assert.Equal(page, document.Text);
        }

        [Fact]
        public void Insert_IntoAnElementWithText_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <StackPanel x:Name="root">
                  <TextBlock x:Name="t">Hello</TextBlock>
                </StackPanel>
                """));

            EditResult result = document.Editor.InsertElement(host.IdOf(DesignTestHost.Named<TextBlock>(root, "t")), 0, "<Border/>");

            Assert.False(result.Succeeded);
        }

        [Fact]
        public void Insert_UnknownType_FailsAndChangesNothing()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            EditResult result = document.Editor.InsertElement(host.IdOf(root), 0, "<Nope/>");

            Assert.False(result.Succeeded);
            Assert.Equal(N(Page), document.Text);
            Assert.Equal(0, document.Version);
            Assert.Equal(2, ((StackPanel)root).Children.Count);
        }

        [Fact]
        public void Insert_MalformedMarkup_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            Assert.False(document.Editor.InsertElement(host.IdOf(root), 0, "<Border>").Succeeded);
            Assert.False(document.Editor.InsertElement(host.IdOf(root), 0, "<Border/><Border/>").Succeeded);
            Assert.Equal(N(Page), document.Text);
        }

        [Fact]
        public void Insert_AnchorsAfterTheNearestMappedSibling()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var panel = (StackPanel)root;
            var runtime = new Border();
            panel.Children.Insert(1, runtime);

            document.Editor.InsertElement(host.IdOf(root), 1, "<TextBlock/>");

            Assert.IsType<TextBlock>(panel.Children[1]);
            Assert.Same(runtime, panel.Children[2]);
        }

        [Fact]
        public void Insert_WithOnlyLeadingRuntimeContent_GoesBeforeTheFirstMappedChild()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var panel = (StackPanel)root;
            var runtime = new Border();
            panel.Children.Insert(0, runtime);

            document.Editor.InsertElement(host.IdOf(root), 0, "<TextBlock/>");

            Assert.Same(runtime, panel.Children[0]);
            Assert.IsType<TextBlock>(panel.Children[1]);
            Assert.Same(DesignTestHost.Named<Border>(root, "a"), panel.Children[2]);
        }

        [Fact]
        public void Insert_IntoAPropertyElement_ChangesTheTextAndNeedsReload()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <Grid x:Name="root">
                  <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                  </Grid.RowDefinitions>
                </Grid>
                """));
            ElementSyntax rows = document.Syntax.Elements.Single(x => x.Name == "Grid.RowDefinitions");

            EditResult result = document.Editor.InsertElement(document.GetNodeId(rows)!.Value, 1, "<RowDefinition Height=\"10\"/>");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Contains("<RowDefinition Height=\"10\"/>", document.Text);
            Assert.True(document.NeedsReload);
            Assert.Single(((Grid)root).RowDefinitions);
        }

        [Fact]
        public void Remove_DeletesTheWholeLineAndDetachesTheElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var b = DesignTestHost.Named<Border>(root, "b");

            EditResult result = document.Editor.RemoveElement(host.IdOf(b));

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="a" Width="10"/>
                </StackPanel>
                """),
                document.Text);
            Assert.Single(((StackPanel)root).Children);
            Assert.Null(b.Parent);
        }

        [Fact]
        public void Remove_TheRoot_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            Assert.False(document.Editor.RemoveElement(host.IdOf(root)).Succeeded);
        }

        [Fact]
        public void Remove_AnElementTheGameAlreadyDetached_StillEditsTheText()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var b = DesignTestHost.Named<Border>(root, "b");
            ((StackPanel)root).Children.Remove(b);

            EditResult result = document.Editor.RemoveElement(host.IdOf(b));

            Assert.True(result.Succeeded, result.ToString());
            Assert.DoesNotContain("x:Name=\"b\"", document.Text);
        }

        [Fact]
        public void Remove_UnregistersNamesInTheSubtree()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));

            document.Editor.RemoveElement(host.IdOf(DesignTestHost.Named<Border>(root, "b")));

            Assert.Null(MarkupNameScope.GetScope(root)!.Find("b"));
        }

        [Fact]
        public void Move_ReparentsTheSameInstance()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(MovePage));
            var box = DesignTestHost.Named<Border>(root, "box");
            var left = DesignTestHost.Named<StackPanel>(root, "left");
            var right = DesignTestHost.Named<StackPanel>(root, "right");

            EditResult result = document.Editor.MoveElement(host.IdOf(box), host.IdOf(right), 1);

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <StackPanel x:Name="left">
                  </StackPanel>
                  <StackPanel x:Name="right">
                    <TextBlock Text="A"/>
                    <Border x:Name="box" Width="10"/>
                  </StackPanel>
                </StackPanel>
                """),
                document.Text);
            Assert.Empty(left.Children);
            Assert.Same(box, right.Children[1]);
            Assert.Equal("Border", document.GetNode(host.IdOf(box))!.Name);
        }

        [Fact]
        public void Move_WithinTheSameParent_Reorders()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(Page));
            var a = DesignTestHost.Named<Border>(root, "a");
            var b = DesignTestHost.Named<Border>(root, "b");

            document.Editor.MoveElement(host.IdOf(b), host.IdOf(root), 0);

            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="b" Width="20"/>
                  <Border x:Name="a" Width="10"/>
                </StackPanel>
                """),
                document.Text);
            Assert.Equal([b, a], ((StackPanel)root).Children);
        }

        [Fact]
        public void Move_IntoItsOwnDescendant_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(MovePage));

            EditResult result = document.Editor.MoveElement(
                host.IdOf(DesignTestHost.Named<StackPanel>(root, "left")),
                host.IdOf(DesignTestHost.Named<Border>(root, "box")),
                0);

            Assert.False(result.Succeeded);
            Assert.Equal(N(MovePage), document.Text);
        }

        private static string N(string text) => text.ReplaceLineEndings("\n");
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupEditorStructureTests"`
Expected: the build FAILS (`Editor`, `MarkupEditor` not found).

- [ ] **Step 3: Implement formatting, live-content and name helpers**

`sources/IcyUI.Design/Editing/MarkupFormatting.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Text;
using Icy.Design.Syntax;
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Produces text changes that look like a person wrote them: matching the document's line breaks, indentation
    /// and quote style.
    /// </summary>
    internal static class MarkupFormatting
    {
        private static readonly string[] LineBreaks = ["\r\n", "\n", "\r"];

        public static int GetLineStart(string text, int position)
        {
            int start = position;
            while (start > 0 && text[start - 1] is not ('\n' or '\r'))
                start--;
            return start;
        }

        /// <summary>
        /// Determines whether only spaces and tabs come before <paramref name="position"/> on its line.
        /// </summary>
        public static bool StartsLine(string text, int position)
        {
            for (int i = GetLineStart(text, position); i < position; i++)
            {
                if (text[i] is not (' ' or '\t'))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the leading spaces and tabs of the line <paramref name="position"/> is on.
        /// </summary>
        public static string GetIndentation(string text, int position)
        {
            int start = GetLineStart(text, position);
            int end = start;
            while (end < text.Length && text[end] is ' ' or '\t')
                end++;
            return text[start..end];
        }

        /// <summary>
        /// Gets the line break the document uses: its first <c>\r\n</c>, <c>\n</c> or <c>\r</c>, or <c>\n</c> for a
        /// single-line document.
        /// </summary>
        public static string DetectNewLine(string text)
        {
            int index = text.AsSpan().IndexOfAny('\r', '\n');
            if (index < 0)
                return "\n";
            if (text[index] == '\n')
                return "\n";
            return index + 1 < text.Length && text[index + 1] == '\n' ? "\r\n" : "\r";
        }

        /// <summary>
        /// Finds one level of indentation: what the first indented child adds to its parent's indentation. Two
        /// spaces when the document has no example.
        /// </summary>
        public static string DetectIndentUnit(DocumentSyntax syntax)
        {
            string text = syntax.Text;
            foreach (ElementSyntax element in syntax.Elements)
            {
                if (element.Parent is not { } parent || !StartsLine(text, element.Span.Start) || !StartsLine(text, parent.Span.Start))
                    continue;

                string own = GetIndentation(text, element.Span.Start);
                string outer = GetIndentation(text, parent.Span.Start);
                if (own.Length > outer.Length && own.StartsWith(outer, StringComparison.Ordinal))
                    return own[outer.Length..];
            }

            return "  ";
        }

        /// <summary>
        /// Re-indents every line of <paramref name="fragment"/> after the first: strips <paramref name="stripIndent"/>
        /// (the fragment's old indentation, for moved text) and prefixes <paramref name="indent"/>.
        /// </summary>
        public static string Reindent(string fragment, string indent, string newLine, string? stripIndent = null)
        {
            string[] lines = fragment.Split(LineBreaks, StringSplitOptions.None);
            var builder = new StringBuilder(lines[0]);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!string.IsNullOrEmpty(stripIndent) && line.StartsWith(stripIndent, StringComparison.Ordinal))
                    line = line[stripIndent.Length..];
                builder.Append(newLine).Append(indent).Append(line);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Gets what to delete to remove <paramref name="element"/>: its whole line(s), line break included, when it
        /// has them to itself; otherwise just its own span.
        /// </summary>
        public static TextSpan GetRemovalSpan(string text, ElementSyntax element)
        {
            if (!StartsLine(text, element.Span.Start))
                return element.Span;

            int end = element.Span.End;
            while (end < text.Length && text[end] is ' ' or '\t')
                end++;

            if (end < text.Length && text[end] is not ('\r' or '\n'))
                return element.Span;

            if (end < text.Length && text[end] == '\r')
                end++;
            if (end < text.Length && text[end] == '\n')
                end++;

            return TextSpan.FromBounds(GetLineStart(text, element.Span.Start), end);
        }

        /// <summary>
        /// Creates the insertion of <paramref name="fragment"/> as content child number <paramref name="index"/> of
        /// <paramref name="parent"/>.
        /// </summary>
        /// <param name="syntax">The current tree.</param>
        /// <param name="parent">The element to insert into.</param>
        /// <param name="contentChildren">The parent's content children to count <paramref name="index"/> against.</param>
        /// <param name="index">The position among <paramref name="contentChildren"/>.</param>
        /// <param name="fragment">The element's markup.</param>
        /// <param name="stripIndent">The fragment's old indentation, when it's moved text; otherwise <see langword="null"/>.</param>
        /// <returns>The change, and the offset where the inserted element's <c>&lt;</c> lands, in the same coordinates.</returns>
        public static (TextChange Change, int ElementOffset) CreateInsertion(
            DocumentSyntax syntax,
            ElementSyntax parent,
            IReadOnlyList<ElementSyntax> contentChildren,
            int index,
            string fragment,
            string? stripIndent)
        {
            string text = syntax.Text;
            string newLine = DetectNewLine(text);

            if (parent.IsSelfClosing)
            {
                string parentIndent = StartsLine(text, parent.Span.Start) ? GetIndentation(text, parent.Span.Start) : string.Empty;
                string childIndent = parentIndent + DetectIndentUnit(syntax);
                string prefix = ">" + newLine + childIndent;
                string body = Reindent(fragment, childIndent, newLine, stripIndent);
                int slash = parent.StartTagEnd - 2;
                string replacement = prefix + body + newLine + parentIndent + "</" + parent.Name + ">";
                return (new TextChange(new TextSpan(slash, 2), replacement), slash + prefix.Length);
            }

            ElementSyntax? anchor;
            bool before;
            if (contentChildren.Count > 0)
            {
                before = index < contentChildren.Count;
                anchor = before ? contentChildren[index] : contentChildren[^1];
            }
            else
            {
                // No content yet, but maybe property elements: go after the last of them.
                anchor = parent.Elements.LastOrDefault();
                before = false;
            }

            if (anchor == null)
            {
                // Only whitespace between the tags (the caller rejects elements that hold text).
                string parentIndent = StartsLine(text, parent.Span.Start) ? GetIndentation(text, parent.Span.Start) : string.Empty;
                string childIndent = parentIndent + DetectIndentUnit(syntax);
                string prefix = newLine + childIndent;
                string body = Reindent(fragment, childIndent, newLine, stripIndent);
                var inner = TextSpan.FromBounds(parent.StartTagEnd, parent.EndTagSpan!.Value.Start);
                return (new TextChange(inner, prefix + body + newLine + parentIndent), inner.Start + prefix.Length);
            }

            bool lineStyle = StartsLine(text, anchor.Span.Start);
            string indent = lineStyle ? GetIndentation(text, anchor.Span.Start) : string.Empty;
            string content = Reindent(fragment, indent, newLine, stripIndent);
            if (before)
            {
                string suffix = lineStyle ? newLine + indent : string.Empty;
                return (new TextChange(new TextSpan(anchor.Span.Start, 0), content + suffix), anchor.Span.Start);
            }

            string separator = lineStyle ? newLine + indent : string.Empty;
            return (new TextChange(new TextSpan(anchor.Span.End, 0), separator + content), anchor.Span.End + separator.Length);
        }
    }
}
```

`sources/IcyUI.Design/Editing/LiveContent.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Icy.Data.Markup;
using Icy.Markup;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Adds, removes and finds children through an object's markup content property
    /// (<see cref="ContentPropertyAttribute"/>), the same way the loader adds them.
    /// </summary>
    /// <remarks>
    /// Always pass the <b>logical</b> parent: the object built from the parent markup element. A templated control's
    /// content is parented by its chrome or <c>ContentPresenter</c> in the visual tree, so <c>UIElement.Parent</c> is
    /// the wrong object to ask.
    /// </remarks>
    internal static class LiveContent
    {
        public static bool TryResolve(object parent, PropertyRegistry registry, [NotNullWhen(true)] out MarkupMember? member, out IList? list)
        {
            member = null;
            list = null;
            Type type = parent.GetType();
            if (ContentPropertyAttribute.GetContentPropertyName(type) is not { } name || MarkupMember.Resolve(type, name, registry) is not { } resolved)
                return false;

            list = resolved.GetValue(parent) as IList;
            if (list == null && !resolved.CanSet)
                return false;

            member = resolved;
            return true;
        }

        public static bool Accepts(MarkupMember member, IList? list, Type childType)
        {
            Type itemType = list == null
                ? member.PropertyType
                : list.GetType().GetInterfaces()
                    .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IList<>))?
                    .GetGenericArguments()[0] ?? typeof(object);
            return itemType.IsAssignableFrom(childType);
        }

        public static int IndexOf(object parent, object child, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                return -1;

            if (list != null)
                return list.IndexOf(child);

            return ReferenceEquals(member.GetValue(parent), child) ? 0 : -1;
        }

        public static int Count(object parent, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                return 0;

            return list?.Count ?? (member.GetValue(parent) != null ? 1 : 0);
        }

        public static void Insert(object parent, int index, object child, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                throw new DesignEditException($"'{parent.GetType().Name}' can't hold child elements.");

            if (list != null)
                list.Insert(Math.Clamp(index, 0, list.Count), child);
            else
                member.SetValue(parent, child);
        }

        public static void Remove(object parent, object child, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                return;

            if (list != null)
                list.Remove(child);
            else if (ReferenceEquals(member.GetValue(parent), child))
                member.SetValue(parent, null);
        }

        public static void Replace(object parent, object oldChild, object newChild, PropertyRegistry registry)
        {
            if (!TryResolve(parent, registry, out MarkupMember? member, out IList? list))
                throw new DesignEditException($"'{parent.GetType().Name}' can't hold child elements.");

            if (list == null)
            {
                member.SetValue(parent, newChild);
                return;
            }

            // Remove + Insert rather than the indexer: collections such as UIElementCollection wire Parent only in
            // InsertItem/RemoveItem.
            int index = list.IndexOf(oldChild);
            list.RemoveAt(index);
            list.Insert(index, newChild);
        }
    }
}
```

`sources/IcyUI.Design/Editing/LiveTree.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Tracking;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Keeps a document's name scope in step with subtrees leaving or rejoining the page.
    /// </summary>
    internal static class LiveTree
    {
        /// <summary>
        /// Unregisters every name that belongs to <paramref name="root"/> or an element below it. Call it while the
        /// subtree is still attached, since it follows parent chains.
        /// </summary>
        /// <returns>The removed names, to register again when undoing.</returns>
        public static List<(string Name, UIElement Element)> UnregisterNames(MarkupLoadScope scope, UIElement root)
        {
            var removed = new List<(string, UIElement)>();
            if (scope.NameScope is not { } names)
                return removed;

            foreach ((string name, UIElement element) in names.Names.ToList())
            {
                if (ObjectMap.IsSelfOrDescendant(element, root))
                {
                    names.Unregister(name);
                    removed.Add((name, element));
                }
            }

            return removed;
        }

        public static void RegisterNames(MarkupLoadScope scope, IEnumerable<(string Name, UIElement Element)> entries)
        {
            if (scope.NameScope is not { } names)
                return;

            foreach ((string name, UIElement element) in entries)
                names.Register(name, element);
        }
    }
}
```

- [ ] **Step 4: Implement the structural actions**

`sources/IcyUI.Design/Editing/ElementInsertedAction.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Builds the element the text now has at <paramref name="start"/> and adds it to every live copy of its parent.
    /// </summary>
    internal sealed class ElementInsertedAction(NodeId parent, int start) : MirrorAction
    {
        private readonly List<(object Parent, UIElement Child, MarkupLoadScope Scope)> inserted = [];
        private NodeId? insertedNode;

        public override void Execute(MirrorContext context)
        {
            DesignDocument document = context.Document;
            ElementSyntax element = document.Syntax.FindElementAt(start)
                ?? throw new InvalidOperationException("The inserted element isn't where the edit put it.");
            insertedNode = document.GetNodeId(element);
            context.ResultNode = insertedNode;

            if (document.GetNode(parent) is not { } parentElement || !document.IsEditable(parentElement) || element.IsPropertyElement)
            {
                document.MarkNeedsReload();
                return;
            }

            foreach ((object liveParent, MarkupLoadScope scope) in document.Map.GetObjects(parent))
            {
                UIElement built = document.BuildElement(scope, element, (UIElement)liveParent);
                LiveContent.Insert(liveParent, document.ComputeLiveIndex(element, liveParent, scope), built, document.Registry);
                inserted.Add((liveParent, built, scope));
            }
        }

        public override void Revert(MirrorContext context)
        {
            DesignDocument document = context.Document;
            for (int i = inserted.Count - 1; i >= 0; i--)
            {
                (object liveParent, UIElement child, MarkupLoadScope scope) = inserted[i];
                LiveTree.UnregisterNames(scope, child);
                LiveContent.Remove(liveParent, child, document.Registry);
                document.Map.RemoveSubtree(child);
            }

            inserted.Clear();
        }

        public override MirrorAction CreateInverse(MirrorContext context) =>
            new ElementRemovedAction(insertedNode ?? throw new InvalidOperationException("The inserted element has no id."));
    }
}
```

`sources/IcyUI.Design/Editing/ElementRemovedAction.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Takes every live copy of a removed element out of its logical parent.
    /// </summary>
    internal sealed class ElementRemovedAction(NodeId node) : MirrorAction
    {
        private readonly List<Removal> removals = [];
        private NodeId? parentNode;
        private int originalStart;

        public override void Execute(MirrorContext context)
        {
            DesignDocument document = context.Document;
            ElementSyntax element = context.GetOldNode(node)
                ?? throw new InvalidOperationException($"The removed element {node} wasn't in the document.");
            originalStart = element.Span.Start;
            parentNode = element.Parent is { } parent ? context.GetOldId(parent) : null;

            if (parentNode is not NodeId parentId || !document.IsEditable(element, context.Before.Ids))
            {
                document.MarkNeedsReload();
                return;
            }

            foreach ((object instance, MarkupLoadScope scope) in document.Map.GetObjects(node))
            {
                if (instance is not UIElement child || document.FindObject(parentId, scope) is not { } liveParent)
                    continue;

                int index = LiveContent.IndexOf(liveParent, child, document.Registry);
                if (index < 0)
                    continue; // The game already took it out.

                List<(string Name, UIElement Element)> names = LiveTree.UnregisterNames(scope, child);
                LiveContent.Remove(liveParent, child, document.Registry);
                removals.Add(new Removal(liveParent, child, index, scope, names));
            }
        }

        public override void Revert(MirrorContext context)
        {
            for (int i = removals.Count - 1; i >= 0; i--)
            {
                Removal removal = removals[i];
                LiveContent.Insert(removal.Parent, removal.Index, removal.Child, context.Document.Registry);
                LiveTree.RegisterNames(removal.Scope, removal.Names);
            }

            removals.Clear();
        }

        public override MirrorAction CreateInverse(MirrorContext context) =>
            new ElementInsertedAction(parentNode ?? throw new InvalidOperationException("The root element can't be inserted back."), originalStart);

        private sealed record Removal(object Parent, UIElement Child, int Index, MarkupLoadScope Scope, List<(string Name, UIElement Element)> Names);
    }
}
```

`sources/IcyUI.Design/Editing/ElementMovedAction.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Moves every live copy of an element to its new logical parent, keeping the same instance and its runtime state.
    /// </summary>
    internal sealed class ElementMovedAction(NodeId node, NodeId newParent, int newStart) : MirrorAction
    {
        private readonly List<Move> moves = [];
        private NodeId oldParent;
        private int oldStart;

        public override MoveHint? Hint => new MoveHint(node, newStart);

        public override void Execute(MirrorContext context)
        {
            DesignDocument document = context.Document;
            ElementSyntax oldElement = context.GetOldNode(node)
                ?? throw new InvalidOperationException($"The moved element {node} wasn't in the document.");
            oldStart = oldElement.Span.Start;
            ElementSyntax oldParentElement = oldElement.Parent
                ?? throw new InvalidOperationException("The root element can't be moved.");
            oldParent = context.GetOldId(oldParentElement)
                ?? throw new InvalidOperationException("The moved element's old parent has no id.");
            ElementSyntax moved = document.GetNode(node)
                ?? throw new InvalidOperationException("The moved element lost its id.");
            ElementSyntax target = document.GetNode(newParent)
                ?? throw new InvalidOperationException("The new parent lost its id.");

            if (!document.IsEditable(oldElement, context.Before.Ids) || !document.IsEditable(oldParentElement, context.Before.Ids) || !document.IsEditable(target))
            {
                document.MarkNeedsReload();
                return;
            }

            foreach ((object instance, MarkupLoadScope scope) in document.Map.GetObjects(node))
            {
                if (instance is not UIElement child
                    || document.FindObject(oldParent, scope) is not { } from
                    || document.FindObject(newParent, scope) is not { } to)
                {
                    continue;
                }

                int oldIndex = LiveContent.IndexOf(from, child, document.Registry);
                if (oldIndex < 0)
                    continue;

                LiveContent.Remove(from, child, document.Registry);

                // Recorded before inserting, so a failing insert is still put back by Revert.
                moves.Add(new Move(child, from, oldIndex, to));
                LiveContent.Insert(to, document.ComputeLiveIndex(moved, to, scope), child, document.Registry);
            }
        }

        public override void Revert(MirrorContext context)
        {
            for (int i = moves.Count - 1; i >= 0; i--)
            {
                Move move = moves[i];
                LiveContent.Remove(move.To, move.Child, context.Document.Registry);
                LiveContent.Insert(move.From, move.OldIndex, move.Child, context.Document.Registry);
            }

            moves.Clear();
        }

        public override MirrorAction CreateInverse(MirrorContext context) => new ElementMovedAction(node, oldParent, oldStart);

        private sealed record Move(UIElement Child, object From, int OldIndex, object To);
    }
}
```

- [ ] **Step 5: Add the document helpers and the editor**

In `DesignDocument`:

1. Add `using System.Xml.Linq;` (already present), `using Icy.Design.Editing;`, `using Icy.UI;`.
2. Add the public property after `NeedsReload`:

```csharp
        /// <summary>
        /// Gets the editor that changes this document and mirrors every change onto its live pages.
        /// </summary>
        public MarkupEditor Editor { get; }
```

and initialize it at the end of the constructor: `Editor = new MarkupEditor(this);`. Switch the class remarks' `<c>Editor</c>` back to `<see cref="Editor"/>`, and in `EditResult.cs` switch `<c>MarkupEditor.InsertElement</c>` back to `<see cref="MarkupEditor.InsertElement"/>`.

3. Add these internal methods after `IsEditable`:

```csharp
        internal object? FindObject(NodeId id, MarkupLoadScope scope) => Map.FindObject(id, scope);

        /// <summary>
        /// Builds a fresh live subtree from <paramref name="element"/>'s current text, for <paramref name="liveParent"/>.
        /// If the build fails, every name it already registered is unregistered again.
        /// </summary>
        internal UIElement BuildElement(MarkupLoadScope scope, ElementSyntax element, UIElement liveParent)
        {
            MarkupNameScope names = scope.NameScope
                ?? throw new DesignEditException("The page this document was loaded into no longer exists.");
            var namesBefore = new HashSet<string>(names.Names.Keys, StringComparer.Ordinal);
            string elementText = Text.Substring(element.Span.Start, element.Span.Length);

            try
            {
                XElement fragment = Session.Builder.ParseFragment(elementText, syntax.GetNamespacesInScope(element));
                using (EnterFragment(element.Span.Start, elementText))
                {
                    object built = Session.Builder.BuildFragment(scope, fragment, liveParent);
                    return built as UIElement
                        ?? throw new DesignEditException($"'{element.Name}' isn't a UI element, so it can't be placed in the element tree.");
                }
            }
            catch
            {
                foreach (string name in names.Names.Keys.Where(x => !namesBefore.Contains(x)).ToList())
                    names.Unregister(name);
                throw;
            }
        }

        /// <summary>
        /// Computes where <paramref name="element"/>'s live copy goes among <paramref name="liveParent"/>'s children:
        /// right after the nearest earlier sibling that is live there, or else right before the nearest later one, or
        /// else at the end. Runtime content the game added therefore never shifts the markup order.
        /// </summary>
        internal int ComputeLiveIndex(ElementSyntax element, object liveParent, MarkupLoadScope scope)
        {
            List<ElementSyntax> siblings = [.. element.Parent!.ContentElements];
            int position = siblings.IndexOf(element);

            for (int i = position - 1; i >= 0; i--)
            {
                if (FindLiveIndex(siblings[i], liveParent, scope) is int index)
                    return index + 1;
            }

            for (int i = position + 1; i < siblings.Count; i++)
            {
                if (FindLiveIndex(siblings[i], liveParent, scope) is int index)
                    return index;
            }

            return LiveContent.Count(liveParent, Registry);
        }
```

and this private helper after `Restore`:

```csharp
        private int? FindLiveIndex(ElementSyntax sibling, object liveParent, MarkupLoadScope scope)
        {
            if (GetNodeId(sibling) is not NodeId id || FindObject(id, scope) is not { } instance)
                return null;

            int index = LiveContent.IndexOf(liveParent, instance, Registry);
            return index >= 0 ? index : null;
        }
```

`sources/IcyUI.Design/MarkupEditor.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using Icy.Design.Editing;
using Icy.Design.Syntax;
using Icy.Design.Text;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design
{
    /// <summary>
    /// Edits a <see cref="DesignDocument"/>: every edit changes the markup text minimally and is mirrored onto every
    /// live page built from it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every edit is all-or-nothing. When it can't be made, or the live page rejects it (a value that doesn't
    /// convert, say), the method returns a failed <see cref="EditResult"/> and neither the text nor any page changed.
    /// </para>
    /// <para>
    /// Edits to opaque parts of the markup (styles, resources, templates, anything under a property element such as
    /// <c>&lt;Grid.RowDefinitions&gt;</c>) change the text only, and set <see cref="DesignDocument.NeedsReload"/>.
    /// </para>
    /// <para>
    /// Call the editor on the thread that owns the document's pages; other threads get an
    /// <see cref="InvalidOperationException"/>.
    /// </para>
    /// </remarks>
    public sealed class MarkupEditor
    {
        private readonly DesignDocument document;

        internal MarkupEditor(DesignDocument document)
        {
            this.document = document;
        }

        /// <summary>
        /// Gets the document this editor changes.
        /// </summary>
        public DesignDocument Document => document;

        /// <summary>
        /// Inserts an element written in markup as a content child of <paramref name="parent"/>.
        /// </summary>
        /// <param name="parent">The element to insert into.</param>
        /// <param name="index">
        /// The position among <paramref name="parent"/>'s content children (property elements such as
        /// <c>&lt;Grid.RowDefinitions&gt;</c> don't count), from 0 to their count.
        /// </param>
        /// <param name="markup">Exactly one element, such as <c>&lt;Button Padding="12,6"&gt;OK&lt;/Button&gt;</c>.</param>
        /// <returns>The outcome; on success <see cref="EditResult.Node"/> is the inserted element.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="markup"/> is <see langword="null"/>.</exception>
        public EditResult InsertElement(NodeId parent, int index, string markup)
        {
            ArgumentNullException.ThrowIfNull(markup);

            if (document.GetNode(parent) is not { } parentElement)
                return UnknownNode(parent);

            string fragment = markup.Trim();
            DocumentSyntax parsed = DocumentSyntax.Parse(fragment);
            if (parsed.HasErrors || parsed.Root == null || parsed.Nodes.Count != 1)
                return EditResult.Failure(parentElement.NameSpan, "The inserted markup must be exactly one well-formed element.");

            List<ElementSyntax> children = [.. parentElement.ContentElements];
            if (Validate(parentElement, children.Count, index) is { } failure)
                return failure;

            (TextChange change, int offset) = MarkupFormatting.CreateInsertion(document.Syntax, parentElement, children, index, fragment, stripIndent: null);
            return Execute(new EditStep(new TextChangeSet([change]), [new ElementInsertedAction(parent, offset)], $"Insert {parsed.Root.Name}"));
        }

        /// <summary>
        /// Removes an element and everything in it.
        /// </summary>
        /// <param name="node">The element to remove. It can't be the root.</param>
        /// <returns>The outcome.</returns>
        public EditResult RemoveElement(NodeId node)
        {
            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
            if (element.Parent == null)
                return EditResult.Failure(element.NameSpan, "The root element can't be removed.");

            TextSpan removal = MarkupFormatting.GetRemovalSpan(document.Text, element);
            return Execute(new EditStep(new TextChangeSet([new TextChange(removal, string.Empty)]), [new ElementRemovedAction(node)], $"Remove {element.Name}"));
        }

        /// <summary>
        /// Moves an element to another parent, or to another position in the same parent. The live element keeps its
        /// identity and runtime state.
        /// </summary>
        /// <param name="node">The element to move. It can't be the root.</param>
        /// <param name="newParent">The element to move it into. It can't be <paramref name="node"/> or inside it.</param>
        /// <param name="index">
        /// The position among <paramref name="newParent"/>'s content children, counted without <paramref name="node"/>.
        /// </param>
        /// <returns>The outcome.</returns>
        public EditResult MoveElement(NodeId node, NodeId newParent, int index)
        {
            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
            if (document.GetNode(newParent) is not { } target)
                return UnknownNode(newParent);
            if (element.Parent == null)
                return EditResult.Failure(element.NameSpan, "The root element can't be moved.");
            if (ReferenceEquals(element, target) || element.IsAncestorOf(target))
                return EditResult.Failure(target.NameSpan, "An element can't be moved into itself.");

            List<ElementSyntax> children = [.. target.ContentElements.Where(x => !ReferenceEquals(x, element))];
            if (Validate(target, children.Count, index) is { } failure)
                return failure;

            string text = document.Text;
            TextSpan removal = MarkupFormatting.GetRemovalSpan(text, element);
            string fragment = text.Substring(element.Span.Start, element.Span.Length);
            string oldIndent = MarkupFormatting.StartsLine(text, element.Span.Start) ? MarkupFormatting.GetIndentation(text, element.Span.Start) : string.Empty;
            (TextChange insertion, int offset) = MarkupFormatting.CreateInsertion(document.Syntax, target, children, index, fragment, oldIndent);

            // Both changes are expressed against the current text; the moved element's new offset must account for the
            // removal when it comes first.
            int newStart = removal.End <= insertion.Span.Start ? offset - removal.Length : offset;
            var changes = new TextChangeSet([new TextChange(removal, string.Empty), insertion]);
            return Execute(new EditStep(changes, [new ElementMovedAction(node, newParent, newStart)], $"Move {element.Name}"));
        }

        internal EditResult Execute(EditStep step) => document.Apply(step, out _);

        private static EditResult UnknownNode(NodeId id) => EditResult.Failure(default, $"The document has no element {id}.");

        private EditResult? Validate(ElementSyntax parent, int contentCount, int index)
        {
            if ((uint)index > (uint)contentCount)
                return EditResult.Failure(parent.NameSpan, $"Index {index} is outside 0..{contentCount}.");

            string text = document.Text;
            foreach (MarkupSyntaxNode node in parent.Content)
            {
                if (node is TextSyntax or CDataSyntax && !text.AsSpan(node.Span.Start, node.Span.Length).IsWhiteSpace())
                    return EditResult.Failure(parent.NameSpan, $"'{parent.Name}' holds text, so it can't also hold elements.");
            }

            // Opaque parents have no live copy to check; the text still changes and the page needs a reload.
            if (!document.IsEditable(parent))
                return null;

            foreach (object instance in document.GetObjects(document.GetNodeId(parent)!.Value))
            {
                if (!LiveContent.TryResolve(instance, document.Registry, out MarkupMember? member, out IList? list))
                    return EditResult.Failure(parent.NameSpan, $"'{parent.Name}' can't hold child elements.");
                if (!LiveContent.Accepts(member, list, typeof(UIElement)))
                    return EditResult.Failure(parent.NameSpan, $"'{parent.Name}.{member.Name}' can't hold elements.");
                if (list == null && contentCount > 0)
                    return EditResult.Failure(parent.NameSpan, $"'{parent.Name}' holds a single child in '{member.Name}', and already has one.");
            }

            return null;
        }
    }
}
```

- [ ] **Step 6: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupEditorStructureTests"`
Expected: PASS, Total 19.

- [ ] **Step 7: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (no failures), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/MarkupEditorStructureTests.cs
git commit -m "Insert, remove and move markup elements with live mirroring

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Set and clear attributes, rebuilds and `NeedsReload`

**Files:**
- Create in `sources/IcyUI.Design/Editing/`: `AttributeChangedAction.cs`, `AttributeKind.cs`
- Modify: `sources/IcyUI.Design/Editing/MarkupFormatting.cs` (add `CreateAttributeChange`, `GetAttributeRemovalSpan`)
- Modify: `sources/IcyUI.Design/DesignDocument.cs` (add `ClassifyAttribute`, `ApplyAttribute`, `Rebuild`)
- Modify: `sources/IcyUI.Design/MarkupEditor.cs` (add `SetAttribute`, `ClearAttribute`)
- Test: `sources/IcyUI.Tests/Design/MarkupEditorAttributeTests.cs`

**Interfaces:**
- Consumes: Tasks 6–8 (`ObjectMap.Entry.Members`, `AppliedMember`, `BuildElement`, `LiveContent.Replace`, `LiveTree`, `Apply`).
- Produces:
  - `public EditResult MarkupEditor.SetAttribute(NodeId node, string name, string value)` and `public EditResult MarkupEditor.ClearAttribute(NodeId node, string name)`. `name` is written as in markup (`Width`, `Grid.Row`, `x:Name`); `value` is the decoded value and gets escaped for the attribute's quote char.
  - Internal: `enum AttributeKind { Property, Name, Directive, NamespaceDeclaration }`; `DesignDocument.ClassifyAttribute(ElementSyntax, string)`, `ApplyAttribute(MarkupLoadScope, object, ElementSyntax, AttributeSyntax)`, `Rebuild(ElementSyntax)`; `AttributeChangedAction(NodeId node, string name)`.

**Mirroring rule (`AttributeChangedAction`).** The action makes every live copy match the attribute as it is in the **current** text. It drops the recorded `AppliedMember` and unbinds its binding, then:

| Current text | Record existed | Attribute in the other text | Live action |
|---|---|---|---|
| present | yes | any | `ApplyAttribute` (the observer records it again) |
| present | no | absent | `ApplyAttribute` (a newly added attribute) |
| present | no | present | rebuild (constructor-consumed or never recordable) |
| absent | yes, registered member | any | `ClearLocalValue` (falls back to style/default) |
| absent | yes, plain CLR member | any | rebuild |
| absent | no | present | rebuild |

`x:Name` renames in place; other `x:` directives always rebuild. "The other text" is the old text for `Execute`, and the failed new text for `Revert`. So `Revert` is the same sync run against the restored text, after putting back the records `Execute` dropped, minus their already-disposed bindings.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/MarkupEditorAttributeTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using CommunityToolkit.Mvvm.ComponentModel;
using Icy.Design;
using Icy.Design.Syntax;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class MarkupEditorAttributeTests
    {
        [Fact]
        public void Set_ReplacesAnExistingValueInPlace()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>"));
            var b = DesignTestHost.Named<Border>(root, "b");

            EditResult result = document.Editor.SetAttribute(host.IdOf(b), "Width", "20");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"20\"/>\n</StackPanel>"), document.Text);
            Assert.Equal(20f, b.Width);
        }

        [Fact]
        public void Set_AppendsANewAttributeOnTheSameLine()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>"));
            var b = DesignTestHost.Named<Border>(root, "b");

            document.Editor.SetAttribute(host.IdOf(b), "Height", "5");

            Assert.Contains("<Border x:Name=\"b\" Width=\"10\" Height=\"5\"/>", document.Text);
            Assert.Equal(5f, b.Height);
        }

        [Fact]
        public void Set_AppendsOnItsOwnLineWhenAttributesAreOnePerLine()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N(
                """
                <StackPanel x:Name="root">
                  <Border x:Name="b"
                          Width="10"
                          Height="5"/>
                </StackPanel>
                """));
            var b = DesignTestHost.Named<Border>(root, "b");

            document.Editor.SetAttribute(host.IdOf(b), "Margin", "1");

            Assert.Equal(
                N("""
                <StackPanel x:Name="root">
                  <Border x:Name="b"
                          Width="10"
                          Height="5"
                          Margin="1"/>
                </StackPanel>
                """),
                document.Text);
            Assert.Equal(new Thickness(1), b.Margin);
        }

        [Fact]
        public void Set_EscapesForTheExistingQuoteChar()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <TextBlock x:Name=\"t\" Text='a'/>\n</StackPanel>"));
            var t = DesignTestHost.Named<TextBlock>(root, "t");
            const string value = "it's \"q\" <b> & c";

            document.Editor.SetAttribute(host.IdOf(t), "Text", value);

            Assert.Contains("Text='it&apos;s \"q\" &lt;b> &amp; c'", document.Text);
            Assert.Equal(value, t.Text);
        }

        [Fact]
        public void Set_LineBreakSurvivesAsACharacterReference()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <TextBlock x:Name=\"t\" Text=\"a\"/>\n</StackPanel>"));
            var t = DesignTestHost.Named<TextBlock>(root, "t");

            document.Editor.SetAttribute(host.IdOf(t), "Text", "a\nb");

            Assert.Contains("Text=\"a&#10;b\"", document.Text);
            Assert.Equal("a\nb", t.Text);
        }

        [Fact]
        public void Set_InvalidValue_FailsAndLeavesTheTextAndLiveValue()
        {
            using var host = new DesignTestHost();
            string page = N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>");
            (UIElement root, DesignDocument document) = host.Load(page);
            var b = DesignTestHost.Named<Border>(root, "b");

            EditResult result = document.Editor.SetAttribute(host.IdOf(b), "Width", "abc");

            Assert.False(result.Succeeded);
            Assert.Equal(page, document.Text);
            Assert.Equal(10f, b.Width);
        }

        [Fact]
        public void Set_ReplacesABinding()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, Model model) = LoadBound(host);
            var t = DesignTestHost.Named<TextBlock>(root, "t");

            document.Editor.SetAttribute(host.IdOf(t), "Text", "{Binding Path=B}");

            Assert.Equal("b", t.Text);
            Assert.Single(t.Bindings);
            model.A = "changed";
            Assert.Equal("b", t.Text);
        }

        [Fact]
        public void Set_ABindingToALiteral_DisposesTheBinding()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, Model model) = LoadBound(host);
            var t = DesignTestHost.Named<TextBlock>(root, "t");

            document.Editor.SetAttribute(host.IdOf(t), "Text", "x");

            Assert.Empty(t.Bindings);
            model.A = "changed";
            Assert.Equal("x", t.Text);
        }

        [Fact]
        public void Set_AnAttachedProperty()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<Grid x:Name=\"root\">\n  <Border x:Name=\"cell\"/>\n</Grid>"));
            var cell = DesignTestHost.Named<Border>(root, "cell");

            document.Editor.SetAttribute(host.IdOf(cell), "Grid.Row", "1");

            Assert.Contains("<Border x:Name=\"cell\" Grid.Row=\"1\"/>", document.Text);
            Assert.Equal(1, Grid.GetRow(cell));
        }

        [Fact]
        public void Clear_ARegisteredProperty_FallsBackToTheDefault()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>"));
            var b = DesignTestHost.Named<Border>(root, "b");

            document.Editor.ClearAttribute(host.IdOf(b), "Width");

            Assert.Contains("<Border x:Name=\"b\"/>", document.Text);
            Assert.True(float.IsNaN(b.Width));
        }

        [Fact]
        public void Clear_APlainClrProperty_RebuildsTheElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <ClrBox x:Name=\"box\" Caption=\"hi\"/>\n</StackPanel>"));
            var box = DesignTestHost.Named<ClrBox>(root, "box");
            SubtreeReplacedEventArgs? replaced = null;
            document.SubtreeReplaced += (_, e) => replaced = e;

            document.Editor.ClearAttribute(host.IdOf(box), "Caption");

            var rebuilt = Assert.IsType<ClrBox>(Assert.Single(((StackPanel)root).Children));
            Assert.NotSame(box, rebuilt);
            Assert.Null(rebuilt.Caption);
            Assert.Same(box, replaced!.OldElement);
            Assert.Same(rebuilt, replaced.NewElement);
            Assert.Same(rebuilt, DesignTestHost.Named<ClrBox>(root, "box"));
        }

        [Fact]
        public void Set_AConstructorAttribute_RebuildsTheElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Labeled x:Name=\"tag\" label=\"a\"/>\n</StackPanel>"));

            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Labeled>(root, "tag")), "label", "b");

            Assert.Equal("b", Assert.IsType<Labeled>(Assert.Single(((StackPanel)root).Children)).Label);
        }

        [Fact]
        public void Set_AConstructorAttributeOnTheRoot_ChangesTheTextAndNeedsReload()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<Labeled label=\"a\"/>");

            EditResult result = document.Editor.SetAttribute(host.IdOf(root), "label", "b");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Equal("<Labeled label=\"b\"/>", document.Text);
            Assert.True(document.NeedsReload);
            Assert.Equal("a", ((Labeled)root).Label);
        }

        [Fact]
        public void SetXName_RenamesInPlace()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"a\"/>\n  <Border x:Name=\"b\"/>\n</StackPanel>"));
            var a = DesignTestHost.Named<Border>(root, "a");

            document.Editor.SetAttribute(host.IdOf(a), "x:Name", "c");

            MarkupNameScope names = MarkupNameScope.GetScope(root)!;
            Assert.Same(a, names.Find("c"));
            Assert.Null(names.Find("a"));
            Assert.Equal("c", a.Name);
        }

        [Fact]
        public void SetXName_ToATakenName_Fails()
        {
            using var host = new DesignTestHost();
            string page = N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"a\"/>\n  <Border x:Name=\"b\"/>\n</StackPanel>");
            (UIElement root, DesignDocument document) = host.Load(page);
            var a = DesignTestHost.Named<Border>(root, "a");

            EditResult result = document.Editor.SetAttribute(host.IdOf(a), "x:Name", "b");

            Assert.False(result.Succeeded);
            Assert.Equal(page, document.Text);
            Assert.Same(a, MarkupNameScope.GetScope(root)!.Find("a"));
        }

        [Fact]
        public void Set_InsideAStyle_ChangesTheTextAndNeedsReload()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(N(
                """
                <Border x:Name="root">
                  <Border.Resources>
                    <Style x:Key="s" TargetType="Border" Width="1"/>
                  </Border.Resources>
                </Border>
                """));
            ElementSyntax style = document.Syntax.Elements.Single(x => x.Name == "Style");

            EditResult result = document.Editor.SetAttribute(document.GetNodeId(style)!.Value, "Width", "2");

            Assert.True(result.Succeeded, result.ToString());
            Assert.Contains("Width=\"2\"", document.Text);
            Assert.True(document.NeedsReload);
        }

        [Fact]
        public void Set_MirrorsOntoEveryLoadOfTheSameFile()
        {
            using var host = new DesignTestHost();
            string page = N("<StackPanel x:Name=\"root\">\n  <Border x:Name=\"b\" Width=\"10\"/>\n</StackPanel>");
            (UIElement first, DesignDocument document) = host.Load(page, "same.xml");
            (UIElement second, _) = host.Load(page, "same.xml");

            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(first, "b")), "Width", "30");

            Assert.Equal(30f, DesignTestHost.Named<Border>(first, "b").Width);
            Assert.Equal(30f, DesignTestHost.Named<Border>(second, "b").Width);
        }

        [Fact]
        public void Set_ANamespaceDeclaration_Fails()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<StackPanel/>");

            Assert.False(document.Editor.SetAttribute(host.IdOf(root), "xmlns:z", "z").Succeeded);
        }

        private static (UIElement Root, DesignDocument Document, Model Model) LoadBound(DesignTestHost host)
        {
            (UIElement root, DesignDocument document) = host.Load(N("<StackPanel x:Name=\"root\">\n  <TextBlock x:Name=\"t\" Text=\"{Binding Path=A}\"/>\n</StackPanel>"));
            var model = new Model { A = "a", B = "b" };
            root.DataContext = model;
            return (root, document, model);
        }

        private static string N(string text) => text.ReplaceLineEndings("\n");

        internal sealed class Model : ObservableObject
        {
            private string a = string.Empty;
            private string b = string.Empty;

            public string A
            {
                get => a;
                set => SetProperty(ref a, value);
            }

            public string B
            {
                get => b;
                set => SetProperty(ref b, value);
            }
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupEditorAttributeTests"`
Expected: the build FAILS (`SetAttribute`, `ClearAttribute` not found).

- [ ] **Step 3: Implement**

`sources/IcyUI.Design/Editing/AttributeKind.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// How an attribute is mirrored onto a live object.
    /// </summary>
    internal enum AttributeKind
    {
        /// <summary>A property or attached property, applied through the loader.</summary>
        Property,

        /// <summary><c>x:Name</c>, renamed in place in the name scope.</summary>
        Name,

        /// <summary>Any other <c>x:</c> directive, which only takes effect when the element is built.</summary>
        Directive,

        /// <summary>An <c>xmlns</c> declaration, which edits never touch.</summary>
        NamespaceDeclaration,
    }
}
```

Add to `MarkupFormatting`:

```csharp
        /// <summary>
        /// Creates the change that sets attribute <paramref name="name"/> to <paramref name="value"/>: the value in
        /// place when it exists, otherwise a new attribute after the last one (on its own line when the attributes
        /// are written one per line), quoted like its neighbours.
        /// </summary>
        public static TextChange CreateAttributeChange(string text, ElementSyntax element, string name, string value)
        {
            AttributeSyntax? existing = element.FindAttribute(name);
            if (existing is { IsMissingValue: false } && existing.Quote != '\0')
                return new TextChange(existing.ValueSpan, MarkupEscaping.EscapeAttributeValue(value, existing.Quote));

            IReadOnlyList<AttributeSyntax> attributes = element.Attributes;
            char quote = attributes.Count > 0 && attributes[^1].Quote != '\0' ? attributes[^1].Quote : '"';
            string attributeText = $"{name}={quote}{MarkupEscaping.EscapeAttributeValue(value, quote)}{quote}";

            // A broken attribute of the same name (no value, or unquoted) is replaced whole.
            if (existing != null)
                return new TextChange(existing.Span, attributeText);

            if (attributes.Count == 0)
                return new TextChange(new TextSpan(element.NameSpan.End, 0), " " + attributeText);

            AttributeSyntax last = attributes[^1];
            bool onePerLine = StartsLine(text, last.Span.Start) && GetLineStart(text, last.Span.Start) != GetLineStart(text, element.Span.Start);
            string separator = onePerLine ? DetectNewLine(text) + GetIndentation(text, last.Span.Start) : " ";
            return new TextChange(new TextSpan(last.Span.End, 0), separator + attributeText);
        }

        /// <summary>
        /// Gets what to delete to remove an attribute: the attribute and the whitespace before it.
        /// </summary>
        public static TextSpan GetAttributeRemovalSpan(string text, AttributeSyntax attribute)
        {
            int start = attribute.Span.Start;
            while (start > 0 && char.IsWhiteSpace(text[start - 1]))
                start--;
            return TextSpan.FromBounds(start, attribute.Span.End);
        }
```

`sources/IcyUI.Design/Editing/AttributeChangedAction.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Data.Bindings;
using Icy.Design.Syntax;
using Icy.Design.Tracking;
using Icy.Markup;
using Icy.UI;

namespace Icy.Design.Editing
{
    /// <summary>
    /// Makes every live copy of an element match one of its attributes as the current text has it.
    /// </summary>
    internal sealed class AttributeChangedAction(NodeId node, string name) : MirrorAction
    {
        private readonly List<(object Instance, AppliedMember Member)> droppedRecords = [];
        private bool presentBefore;
        private bool presentAfter;

        public override void Execute(MirrorContext context)
        {
            presentBefore = context.GetOldNode(node)?.FindAttribute(name) != null;
            presentAfter = context.Document.GetNode(node)?.FindAttribute(name) != null;
            Sync(context.Document, presentInOtherText: presentBefore);
        }

        public override void Revert(MirrorContext context)
        {
            // The document holds the old text again. Put back the records Execute dropped, without their bindings
            // (Execute already disposed those), so the old value is simply applied again and recreates any binding.
            foreach ((object instance, AppliedMember member) in droppedRecords)
                context.Document.Map.RecordMember(instance, name, member with { Binding = null });
            droppedRecords.Clear();

            Sync(context.Document, presentInOtherText: presentAfter);
        }

        public override MirrorAction CreateInverse(MirrorContext context) => new AttributeChangedAction(node, name);

        private static void SyncName(UIElement element, MarkupLoadScope scope, string? newName)
        {
            MarkupNameScope names = scope.NameScope
                ?? throw new DesignEditException("The page this document was loaded into no longer exists.");

            if (newName != null && names.TryFind(newName, out UIElement? other) && !ReferenceEquals(other, element))
                throw new DesignEditException($"The name '{newName}' is already used in this document.");

            if (element.Name is { } oldName && names.TryFind(oldName, out UIElement? registered) && ReferenceEquals(registered, element))
                names.Unregister(oldName);
            if (newName != null)
                names.Register(newName, element);

            element.Name = newName;
        }

        private void Sync(DesignDocument document, bool presentInOtherText)
        {
            if (document.GetNode(node) is not { } element)
                return;

            if (!document.IsEditable(element))
            {
                document.MarkNeedsReload();
                return;
            }

            AttributeKind kind = document.ClassifyAttribute(element, name);
            if (kind == AttributeKind.Directive)
            {
                document.Rebuild(element);
                return;
            }

            AttributeSyntax? attribute = element.FindAttribute(name);
            foreach ((object instance, MarkupLoadScope scope) in document.Map.GetObjects(node))
            {
                if (kind == AttributeKind.Name)
                {
                    SyncName((UIElement)instance, scope, attribute?.Value);
                    continue;
                }

                AppliedMember? applied = DropRecord(document, instance);
                if (attribute != null)
                {
                    if (applied == null && presentInOtherText)
                    {
                        // Present before but never recorded: consumed by a constructor, or not a plain member.
                        document.Rebuild(element);
                        return;
                    }

                    document.ApplyAttribute(scope, instance, element, attribute);
                }
                else if (applied?.Member.Reference is { } reference)
                {
                    reference.ClearLocalValue(instance);
                }
                else if (applied != null || presentInOtherText)
                {
                    // A plain CLR property has no "unset" to go back to; only a fresh object has its default.
                    document.Rebuild(element);
                    return;
                }
            }
        }

        private AppliedMember? DropRecord(DesignDocument document, object instance)
        {
            if (!document.Map.TryGetEntry(instance, out ObjectMap.Entry? entry) || !entry.Members.Remove(name, out AppliedMember? applied))
                return null;

            droppedRecords.Add((instance, applied));
            if (applied.Binding is { } binding && instance is IBindingTarget target)
                target.Unbind(binding);
            return applied;
        }
    }
}
```

Add to `DesignDocument` (internal methods after `ComputeLiveIndex`):

```csharp
        internal AttributeKind ClassifyAttribute(ElementSyntax element, string name)
        {
            if (name == "xmlns" || name.StartsWith("xmlns:", StringComparison.Ordinal))
                return AttributeKind.NamespaceDeclaration;

            int colon = name.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && syntax.ResolvePrefix(element, name[..colon]) == MarkupNamespaces.Directives)
                return name[(colon + 1)..] == MarkupDirectives.Name ? AttributeKind.Name : AttributeKind.Directive;

            return AttributeKind.Property;
        }

        /// <summary>
        /// Applies one attribute of the current text to a live object, through the loader, so the observer records it
        /// as it would during a load.
        /// </summary>
        internal void ApplyAttribute(MarkupLoadScope scope, object instance, ElementSyntax element, AttributeSyntax attribute)
        {
            // The start tag alone, closed, keeps the attribute at the same offset relative to the element.
            string tag = Text.Substring(element.Span.Start, element.StartTagEnd - element.Span.Start);
            if (!element.IsSelfClosing)
                tag = string.Concat(tag.AsSpan(0, tag.Length - 1), "/>");

            XElement fragment = Session.Builder.ParseFragment(tag, syntax.GetNamespacesInScope(element));
            var tagLines = new LineMap(tag);
            int relative = attribute.NameSpan.Start - element.Span.Start;
            XAttribute xml = fragment.Attributes().First(x =>
                x is IXmlLineInfo info && tagLines.TryToOffset(info.LineNumber, info.LinePosition, out int offset) && offset == relative);

            using (EnterFragment(element.Span.Start, tag))
                Session.Builder.ApplyAttribute(scope, instance, xml);
        }

        /// <summary>
        /// Replaces every live copy of <paramref name="element"/> with a fresh build of its current text. The root can't
        /// be swapped out of a parent, so for the root this only sets <see cref="NeedsReload"/>.
        /// </summary>
        internal void Rebuild(ElementSyntax element)
        {
            if (element.Parent is not { } parentElement || GetNodeId(element) is not NodeId id || GetNodeId(parentElement) is not NodeId parentId)
            {
                MarkNeedsReload();
                return;
            }

            foreach ((object instance, MarkupLoadScope scope) in Map.GetObjects(id))
            {
                if (instance is not UIElement old || FindObject(parentId, scope) is not UIElement liveParent || LiveContent.IndexOf(liveParent, old, Registry) < 0)
                    continue;

                List<(string Name, UIElement Element)> names = LiveTree.UnregisterNames(scope, old);
                UIElement built;
                try
                {
                    built = BuildElement(scope, element, liveParent);
                }
                catch
                {
                    LiveTree.RegisterNames(scope, names);
                    throw;
                }

                LiveContent.Replace(liveParent, old, built, Registry);
                Map.RemoveSubtree(old);
                RaiseSubtreeReplaced(new SubtreeReplacedEventArgs(id, old, built));
            }
        }
```

Add to `MarkupEditor`, after `MoveElement`:

```csharp
        /// <summary>
        /// Sets an attribute, adding it when the element doesn't have it yet.
        /// </summary>
        /// <param name="node">The element.</param>
        /// <param name="name">The attribute name as written in markup: <c>Width</c>, <c>Grid.Row</c>, <c>x:Name</c>.</param>
        /// <param name="value">
        /// The value as the property should receive it, or a markup extension such as <c>{Binding Path=Name}</c>. It is
        /// escaped for the attribute's quote character; don't escape it yourself.
        /// </param>
        /// <returns>The outcome.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="value"/> is <see langword="null"/>.</exception>
        public EditResult SetAttribute(NodeId node, string name, string value)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);

            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
            if (ValidateAttributeName(element, name) is { } failure)
                return failure;

            TextChange change = MarkupFormatting.CreateAttributeChange(document.Text, element, name, value);
            return Execute(new EditStep(new TextChangeSet([change]), [new AttributeChangedAction(node, name)], $"Set {name}"));
        }

        /// <summary>
        /// Removes an attribute, so the property falls back to its style or default value.
        /// </summary>
        /// <param name="node">The element.</param>
        /// <param name="name">The attribute name as written in markup.</param>
        /// <returns>The outcome; a success that changed nothing when the element has no such attribute.</returns>
        /// <exception cref="ArgumentException"><paramref name="name"/> is <see langword="null"/> or empty.</exception>
        public EditResult ClearAttribute(NodeId node, string name)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
            if (ValidateAttributeName(element, name) is { } failure)
                return failure;
            if (element.FindAttribute(name) is not { } attribute)
                return EditResult.Success();

            TextSpan removal = MarkupFormatting.GetAttributeRemovalSpan(document.Text, attribute);
            return Execute(new EditStep(new TextChangeSet([new TextChange(removal, string.Empty)]), [new AttributeChangedAction(node, name)], $"Clear {name}"));
        }
```

and this private helper next to `Validate`:

```csharp
        private EditResult? ValidateAttributeName(ElementSyntax element, string name)
        {
            if (!MarkupParser.IsValidName(name))
                return EditResult.Failure(element.NameSpan, $"'{name}' isn't a valid attribute name.");
            if (document.ClassifyAttribute(element, name) == AttributeKind.NamespaceDeclaration)
                return EditResult.Failure(element.NameSpan, "Namespace declarations can't be edited.");
            return null;
        }
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~MarkupEditorAttributeTests"`
Expected: PASS, Total 18.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (no failures), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/MarkupEditorAttributeTests.cs
git commit -m "Set and clear markup attributes with live mirroring and rebuild fallback

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 10: Undo, redo and transactions

**Files:**
- Create: `sources/IcyUI.Design/UndoStack.cs`
- Create in `sources/IcyUI.Design/Editing/`: `UndoEntry.cs`, `UndoItem.cs`
- Modify: `sources/IcyUI.Design/MarkupEditor.cs` (`UndoStack`, `BeginTransaction`, recording in `Execute`)
- Test: `sources/IcyUI.Tests/Design/UndoStackTests.cs`

**Interfaces:**
- Consumes: `DesignDocument.Apply` returning the inverse `EditStep` (Task 7).
- Produces:
  - `public sealed class UndoStack` with event `Changed`, `CanUndo`, `CanRedo`, `UndoDescription`, `RedoDescription`, `Undo()`, `Redo()`, `Clear()`; internal `Record(EditStep inverse, string description)`, `BeginTransaction(string)`.
  - `public UndoStack MarkupEditor.UndoStack` and `public IDisposable MarkupEditor.BeginTransaction(string description)`.
  - Internal `UndoEntry(string description)` with `Description`, `Items`; `UndoItem` with `static ForStep(EditStep)`, `EditStep CreateStep(DesignDocument)`.

**Replay.** Undoing an entry applies its items' steps in reverse order through `DesignDocument.Apply`. Each application returns the step that would undo it again, and those steps form the redo entry, and the other way round. Steps are recomputed on every replay, so ids of re-inserted elements never go stale. If a replayed step fails, both stacks are cleared, because the history no longer matches the text.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/UndoStackTests.cs`:

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
    public class UndoStackTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <StackPanel x:Name="left">
                <Border x:Name="box" Width="10"/>
              </StackPanel>
              <StackPanel x:Name="right"/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void UndoAndRedo_OfASetAttribute()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            MarkupEditor editor = document.Editor;
            editor.SetAttribute(host.IdOf(box), "Width", "{}20");
            string edited = document.Text;

            Assert.True(editor.UndoStack.Undo().Succeeded);
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, box.Width);

            Assert.True(editor.UndoStack.Redo().Succeeded);
            Assert.Equal(edited, document.Text);
            Assert.Equal(20f, box.Width);
        }

        [Fact]
        public void UndoAndRedo_OfAnInsert()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var right = DesignTestHost.Named<StackPanel>(root, "right");
            document.Editor.InsertElement(host.IdOf(right), 0, "<TextBlock Text=\"new\"/>");

            document.Editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
            Assert.Empty(right.Children);

            document.Editor.UndoStack.Redo();
            Assert.Equal("new", Assert.IsType<TextBlock>(Assert.Single(right.Children)).Text);
        }

        [Fact]
        public void Undo_OfARemove_RestoresTheTextExactlyAndAFreshElement()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var left = DesignTestHost.Named<StackPanel>(root, "left");
            document.Editor.RemoveElement(host.IdOf(DesignTestHost.Named<Border>(root, "box")));

            document.Editor.UndoStack.Undo();

            Assert.Equal(Page, document.Text);
            var restored = Assert.IsType<Border>(Assert.Single(left.Children));
            Assert.Equal(10f, restored.Width);
            Assert.Same(restored, MarkupNameScope.GetScope(root)!.Find("box"));
        }

        [Fact]
        public void Undo_OfAMove_PutsTheSameInstanceBack()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            var box = DesignTestHost.Named<Border>(root, "box");
            var left = DesignTestHost.Named<StackPanel>(root, "left");
            document.Editor.MoveElement(host.IdOf(box), host.IdOf(DesignTestHost.Named<StackPanel>(root, "right")), 0);

            document.Editor.UndoStack.Undo();

            Assert.Equal(Page, document.Text);
            Assert.Same(box, Assert.Single(left.Children));
        }

        [Fact]
        public void UndoingEverything_RestoresTheTextByteForByte_AndRedoingEverythingRestoresTheResult()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            MarkupEditor editor = document.Editor;
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            NodeId right = host.IdOf(DesignTestHost.Named<StackPanel>(root, "right"));
            editor.SetAttribute(box, "Height", "5");
            editor.InsertElement(right, 0, "<TextBlock/>");
            editor.MoveElement(box, right, 1);
            editor.ClearAttribute(box, "Width");
            string final = document.Text;

            while (editor.UndoStack.CanUndo)
                Assert.True(editor.UndoStack.Undo().Succeeded);
            Assert.Equal(Page, document.Text);

            while (editor.UndoStack.CanRedo)
                Assert.True(editor.UndoStack.Redo().Succeeded);
            Assert.Equal(final, document.Text);
        }

        [Fact]
        public void Transaction_IsOneUndoStep()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            MarkupEditor editor = document.Editor;
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));

            using (editor.BeginTransaction("Resize"))
            {
                editor.SetAttribute(box, "Height", "5");
                editor.SetAttribute(box, "Margin", "2");
            }

            Assert.Equal("Resize", editor.UndoStack.UndoDescription);
            editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
            Assert.False(editor.UndoStack.CanUndo);
        }

        [Fact]
        public void NewEdit_ClearsRedo()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId box = host.IdOf(DesignTestHost.Named<Border>(root, "box"));
            document.Editor.SetAttribute(box, "Height", "5");
            document.Editor.UndoStack.Undo();

            document.Editor.SetAttribute(box, "Margin", "2");

            Assert.False(document.Editor.UndoStack.CanRedo);
        }

        [Fact]
        public void Undo_OfABindingChange_RestoresTheOriginalBinding()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<TextBlock Text=\"{Binding Path=A}\"/>");
            var model = new MarkupEditorAttributeTests.Model { A = "a", B = "b" };
            root.DataContext = model;
            document.Editor.SetAttribute(host.IdOf(root), "Text", "{Binding Path=B}");

            document.Editor.UndoStack.Undo();

            var block = (TextBlock)root;
            Assert.Equal("a", block.Text);
            Assert.Single(block.Bindings);
            model.A = "again";
            Assert.Equal("again", block.Text);
        }

        [Fact]
        public void Undo_InsideATransaction_Throws()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);

            using (document.Editor.BeginTransaction("open"))
                Assert.Throws<InvalidOperationException>(() => document.Editor.UndoStack.Undo());
        }

        [Fact]
        public void FailedEdit_RecordsNothing()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);

            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "box")), "Width", "abc");

            Assert.False(document.Editor.UndoStack.CanUndo);
        }
    }
}
```

`UndoAndRedo_OfASetAttribute` sets `{}20`, the markup escape for a literal that would otherwise start with `{`. That keeps the step on the normal path even after Task 11 adds the coalescing fast path, which skips `{`-values, so this test exercises plain undo of a normal step.

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~UndoStackTests"`
Expected: the build FAILS (`UndoStack`, `BeginTransaction` not found).

- [ ] **Step 3: Implement**

`sources/IcyUI.Design/Editing/UndoItem.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// One thing to replay when an <see cref="UndoEntry"/> is undone or redone.
    /// </summary>
    internal sealed class UndoItem
    {
        private readonly EditStep step;

        private UndoItem(EditStep step)
        {
            this.step = step;
        }

        public static UndoItem ForStep(EditStep step) => new(step);

        public EditStep CreateStep(DesignDocument document) => step;
    }
}
```

`sources/IcyUI.Design/Editing/UndoEntry.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// One undo or redo step as the user sees it: an edit, a transaction, or a run of coalesced edits.
    /// </summary>
    internal sealed class UndoEntry(string description)
    {
        public string Description { get; } = description;

        /// <summary>
        /// Gets the items, in the order their edits were made. Replaying goes through them backwards.
        /// </summary>
        public List<UndoItem> Items { get; } = [];
    }
}
```

`sources/IcyUI.Design/UndoStack.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editing;

namespace Icy.Design
{
    /// <summary>
    /// The undo and redo history of one <see cref="DesignDocument"/>'s edits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Undoing restores the text byte for byte and mirrors the change onto the live pages, like any edit. An element
    /// that comes back after its removal is undone is built fresh; a moved element that is moved back keeps its
    /// identity.
    /// </para>
    /// <para>
    /// Group several edits into one step with <see cref="MarkupEditor.BeginTransaction"/>.
    /// </para>
    /// </remarks>
    public sealed class UndoStack
    {
        private readonly DesignDocument document;
        private readonly List<UndoEntry> undo = [];
        private readonly List<UndoEntry> redo = [];
        private UndoEntry? transaction;
        private int transactionDepth;

        internal UndoStack(DesignDocument document)
        {
            this.document = document;
        }

        /// <summary>
        /// Occurs when the history changed: an edit was recorded, or a step was undone or redone.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets a value indicating whether there is a step to undo.
        /// </summary>
        public bool CanUndo => undo.Count > 0;

        /// <summary>
        /// Gets a value indicating whether there is a step to redo.
        /// </summary>
        public bool CanRedo => redo.Count > 0;

        /// <summary>
        /// Gets a description of the step <see cref="Undo"/> would undo, or <see langword="null"/>.
        /// </summary>
        public string? UndoDescription => undo.Count > 0 ? undo[^1].Description : null;

        /// <summary>
        /// Gets a description of the step <see cref="Redo"/> would redo, or <see langword="null"/>.
        /// </summary>
        public string? RedoDescription => redo.Count > 0 ? redo[^1].Description : null;

        /// <summary>
        /// Undoes the latest step.
        /// </summary>
        /// <returns>The outcome; a failure when there is nothing to undo.</returns>
        /// <exception cref="InvalidOperationException">A transaction is open.</exception>
        public EditResult Undo() => Replay(undo, redo);

        /// <summary>
        /// Redoes the latest undone step.
        /// </summary>
        /// <returns>The outcome; a failure when there is nothing to redo.</returns>
        /// <exception cref="InvalidOperationException">A transaction is open.</exception>
        public EditResult Redo() => Replay(redo, undo);

        /// <summary>
        /// Forgets the whole history.
        /// </summary>
        public void Clear()
        {
            undo.Clear();
            redo.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        internal IDisposable BeginTransaction(string description)
        {
            if (transactionDepth++ == 0)
                transaction = new UndoEntry(description);
            return new TransactionScope(this);
        }

        internal void Record(EditStep inverse, string description)
        {
            redo.Clear();
            if (transaction != null)
            {
                transaction.Items.Add(UndoItem.ForStep(inverse));
                return;
            }

            var entry = new UndoEntry(description);
            entry.Items.Add(UndoItem.ForStep(inverse));
            undo.Add(entry);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private EditResult Replay(List<UndoEntry> from, List<UndoEntry> to)
        {
            if (transactionDepth > 0)
                throw new InvalidOperationException("Undo and redo aren't possible while a transaction is open.");
            if (from.Count == 0)
                return EditResult.Failure(default, "There is nothing to replay.");

            UndoEntry entry = from[^1];
            from.RemoveAt(from.Count - 1);

            var replayed = new UndoEntry(entry.Description);
            for (int i = entry.Items.Count - 1; i >= 0; i--)
            {
                EditResult result = document.Apply(entry.Items[i].CreateStep(document), out EditStep? inverse);
                if (!result.Succeeded)
                {
                    // The history no longer matches the text; replaying any of it later would corrupt the document.
                    undo.Clear();
                    redo.Clear();
                    Changed?.Invoke(this, EventArgs.Empty);
                    return result;
                }

                replayed.Items.Add(UndoItem.ForStep(inverse!));
            }

            to.Add(replayed);
            Changed?.Invoke(this, EventArgs.Empty);
            return EditResult.Success();
        }

        private void EndTransaction()
        {
            if (--transactionDepth > 0)
                return;

            UndoEntry entry = transaction!;
            transaction = null;
            if (entry.Items.Count > 0)
            {
                undo.Add(entry);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class TransactionScope(UndoStack owner) : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                owner.EndTransaction();
            }
        }
    }
}
```

In `MarkupEditor`:

1. Add after the `Document` property:

```csharp
        /// <summary>
        /// Gets the undo and redo history of this editor's edits.
        /// </summary>
        public UndoStack UndoStack { get; }
```

and in the constructor: `UndoStack = new UndoStack(document);`.

2. Add after `ClearAttribute`:

```csharp
        /// <summary>
        /// Groups every edit made until the returned object is disposed into one undo step.
        /// </summary>
        /// <param name="description">What the step does, as <see cref="UndoStack.UndoDescription"/> reports it.</param>
        /// <returns>The transaction; dispose it to close it. Transactions nest; only the outermost one records a step.</returns>
        /// <exception cref="ArgumentException"><paramref name="description"/> is <see langword="null"/> or empty.</exception>
        public IDisposable BeginTransaction(string description)
        {
            ArgumentException.ThrowIfNullOrEmpty(description);
            return UndoStack.BeginTransaction(description);
        }
```

3. Replace `Execute` with:

```csharp
        internal EditResult Execute(EditStep step)
        {
            EditResult result = document.Apply(step, out EditStep? inverse);
            if (result.Succeeded)
                UndoStack.Record(inverse!, step.Description);
            return result;
        }
```

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~UndoStackTests"`
Expected: PASS, Total 10.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (no failures), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/UndoStackTests.cs
git commit -m "Add undo, redo and transactions to the markup editor

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 11: Coalesced attribute edits with deferred re-parse

**Files:**
- Modify: `sources/IcyUI.Design/DesignDocument.cs` (pending values, `FlushPending`, `TryFastSetAttribute`, `ParseCount`; `Syntax`/`GetNode` flush)
- Modify: `sources/IcyUI.Design/Editing/UndoEntry.cs`, `UndoItem.cs`, `sources/IcyUI.Design/UndoStack.cs` (`CoalesceWindow`, `Clock`, `RecordCoalesced`)
- Modify: `sources/IcyUI.Design/MarkupEditor.cs` (`SetAttribute` tries the fast path first)
- Test: `sources/IcyUI.Tests/Design/CoalescingTests.cs`

**Interfaces:**
- Consumes: Tasks 7–10.
- Produces:
  - `public TimeSpan UndoStack.CoalesceWindow` (default 500 ms); internal `TimeProvider UndoStack.Clock`, `RecordCoalesced(NodeId node, string name, string originalRaw, string description)`.
  - Internal `DesignDocument.FlushPending()`, `TryFastSetAttribute(NodeId, string, string, out EditResult?, out string? originalRaw)`, `int ParseCount`.
  - `UndoItem.ForCoalesced(NodeId node, string name, string originalRaw)`, `UndoItem.CoalescedKey`; `UndoEntry.CoalesceKey`, `UndoEntry.LastEdit`.

**The fast path.** `SetAttribute` on an **existing, quoted, literal** attribute of an editable element whose live copies all have a recorded member takes it. That covers every frame of a drag, and excludes values or old values starting with `{`, `x:` directives and new attributes. On this path:
- the value is applied to every live copy through a synthesized one-attribute start tag, with recording suppressed so the existing record stays;
- the text is patched without re-parsing; the patch is kept as a pending value relative to the last parsed text;
- `Changed` is raised with the exact patch.

The pending values are folded into a real re-parse (offset-mapped, so every id survives) as soon as anything needs the tree: `Syntax`, `GetNode`, any normal edit, undo/redo, saving, or a fast edit on a **different** element. Undo of fast edits restores each attribute's value from before the first of a coalesced run.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/CoalescingTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design;
using Icy.Design.Syntax;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Design
{
    public class CoalescingTests
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <Border x:Name="a" Width="10" Height="10"/>
              <Border x:Name="b" Width="10"/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void RepeatedSets_WithinTheWindow_AreOneUndoStep()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, FakeClock clock) = Load(host);
            var a = DesignTestHost.Named<Border>(root, "a");
            NodeId id = host.IdOf(a);

            foreach (string width in new[] { "11", "12", "13" })
            {
                Assert.True(document.Editor.SetAttribute(id, "Width", width).Succeeded);
                clock.Advance(TimeSpan.FromMilliseconds(16));
            }

            Assert.Contains("x:Name=\"a\" Width=\"13\"", document.Text);
            Assert.Equal(13f, a.Width);

            document.Editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, a.Width);
            Assert.False(document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void Sets_FurtherApartThanTheWindow_AreSeparateSteps()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, FakeClock clock) = Load(host);
            NodeId id = host.IdOf(DesignTestHost.Named<Border>(root, "a"));

            document.Editor.SetAttribute(id, "Width", "11");
            clock.Advance(TimeSpan.FromSeconds(1));
            document.Editor.SetAttribute(id, "Width", "12");

            document.Editor.UndoStack.Undo();
            Assert.Contains("x:Name=\"a\" Width=\"11\"", document.Text);
        }

        [Fact]
        public void FastSets_DeferTheReparseUntilTheTreeIsNeeded()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            NodeId id = host.IdOf(DesignTestHost.Named<Border>(root, "a"));
            int parses = document.ParseCount;

            for (int i = 0; i < 20; i++)
                document.Editor.SetAttribute(id, "Width", (20 + i).ToString(System.Globalization.CultureInfo.InvariantCulture));

            Assert.Equal(parses, document.ParseCount);
            AttributeSyntax width = document.Syntax.Elements.Single(x => x.FindAttribute("x:Name")?.Value == "a").FindAttribute("Width")!;
            Assert.Equal(parses + 1, document.ParseCount);
            Assert.Equal("39", document.Text.Substring(width.ValueSpan.Start, width.ValueSpan.Length));
            Assert.Equal(id, document.GetNodeId(width.Parent!));
        }

        [Fact]
        public void FastSet_InvalidValue_FailsAndKeepsTheLiveValue()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            var a = DesignTestHost.Named<Border>(root, "a");

            EditResult result = document.Editor.SetAttribute(host.IdOf(a), "Width", "abc");

            Assert.False(result.Succeeded);
            Assert.Equal(Page, document.Text);
            Assert.Equal(10f, a.Width);
        }

        [Fact]
        public void FastSets_OnAnotherElement_FlushTheFirstOnesPendingValues()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);

            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "a")), "Width", "123");
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "b")), "Width", "456");

            Assert.Contains("x:Name=\"a\" Width=\"123\"", document.Text);
            Assert.Contains("x:Name=\"b\" Width=\"456\"", document.Text);
            Assert.Empty(document.Syntax.Diagnostics);
        }

        [Fact]
        public void AStructuralEdit_AfterFastSets_SeesTheirText()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "a")), "Width", "123");

            document.Editor.InsertElement(host.IdOf(root), 2, "<TextBlock/>");

            Assert.Contains("x:Name=\"a\" Width=\"123\"", document.Text);
            Assert.Contains("<TextBlock/>", document.Text);
            document.Editor.UndoStack.Undo();
            document.Editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
        }

        [Fact]
        public void ATransactionDraggingTwoAttributes_IsOneUndoStepRestoringBoth()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            var a = DesignTestHost.Named<Border>(root, "a");
            NodeId id = host.IdOf(a);

            using (document.Editor.BeginTransaction("Resize"))
            {
                for (int i = 1; i <= 10; i++)
                {
                    document.Editor.SetAttribute(id, "Width", (10 + i).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    document.Editor.SetAttribute(id, "Height", (10 + (2 * i)).ToString(System.Globalization.CultureInfo.InvariantCulture));
                }
            }

            Assert.Equal((20f, 30f), (a.Width, a.Height));
            document.Editor.UndoStack.Undo();
            Assert.Equal(Page, document.Text);
            Assert.Equal((10f, 10f), (a.Width, a.Height));
            Assert.False(document.Editor.UndoStack.CanUndo);
        }

        [Fact]
        public void ChangedEvents_ReplayToTheSameText()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document, _) = Load(host);
            string mirror = document.Text;
            document.Changed += (_, e) => mirror = e.Changes.Apply(mirror);
            NodeId a = host.IdOf(DesignTestHost.Named<Border>(root, "a"));

            document.Editor.SetAttribute(a, "Width", "1");
            document.Editor.SetAttribute(a, "Height", "22222");
            document.Editor.SetAttribute(a, "Width", "333");
            document.Editor.InsertElement(host.IdOf(root), 0, "<TextBlock/>");
            document.Editor.UndoStack.Undo();

            Assert.Equal(document.Text, mirror);
        }

        [Fact]
        public void BindingValues_TakeTheNormalPath()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load("<TextBlock Text=\"x\"/>");
            int parses = document.ParseCount;

            document.Editor.SetAttribute(host.IdOf(root), "Text", "{Binding Path=A}");

            Assert.Equal(parses + 1, document.ParseCount);
        }

        private static (UIElement Root, DesignDocument Document, FakeClock Clock) Load(DesignTestHost host)
        {
            (UIElement root, DesignDocument document) = host.Load(Page);
            var clock = new FakeClock();
            document.Editor.UndoStack.Clock = clock;
            return (root, document, clock);
        }

        private sealed class FakeClock : TimeProvider
        {
            private DateTimeOffset now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

            public override DateTimeOffset GetUtcNow() => now;

            public void Advance(TimeSpan by) => now += by;
        }
    }
}
```

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~CoalescingTests"`
Expected: the build FAILS (`ParseCount`, `UndoStack.Clock` not found).

- [ ] **Step 3: Implement**

In `DesignDocument`:

1. Add fields:

```csharp
        private readonly Dictionary<(NodeId Node, string Name), PendingValue> pending = [];
        private string parsedText;
```

Initialize `parsedText = text;` in the constructor, and set `parsedText = newText.Text;` at the end of `Resync`.

2. Add `internal int ParseCount { get; private set; }`, and route every parse through one private helper. Replace each `DocumentSyntax.Parse(...)` call in the class (constructor and `Apply`) with `Parse(...)`:

```csharp
        private DocumentSyntax Parse(string value)
        {
            ParseCount++;
            return DocumentSyntax.Parse(value);
        }
```

3. Make the public tree accessors flush first:

```csharp
        public DocumentSyntax Syntax
        {
            get
            {
                FlushPending();
                return syntax;
            }
        }
```

```csharp
        public ElementSyntax? GetNode(NodeId id)
        {
            FlushPending();
            return nodes.GetValueOrDefault(id);
        }
```

Keep the existing XML docs. Also call `FlushPending();` as the first statement of `Apply`, right after `VerifyAccess();`.

4. Add the fast path and the flush:

```csharp
        /// <summary>
        /// Folds pending fast-path values into a real re-parse. Every id survives, because the values only changed
        /// inside start tags.
        /// </summary>
        internal void FlushPending()
        {
            if (pending.Count == 0)
                return;

            var change = new TextChangeSet(pending.Values.Select(x => new TextChange(x.ParsedSpan, x.Raw)));
            pending.Clear();
            foreach (NodeId id in Resync(text, Parse(text.Text), change, []))
                Map.RemoveNode(id);
        }

        /// <summary>
        /// Sets an existing literal attribute without re-parsing, when every live copy can take the new value in place.
        /// </summary>
        /// <returns>
        /// <see langword="false"/> when the edit needs the normal path. Otherwise <see langword="true"/>, with the outcome
        /// in <paramref name="result"/> and, on success, the value text before this edit in <paramref name="originalRaw"/>.
        /// </returns>
        internal bool TryFastSetAttribute(NodeId node, string name, string value, [NotNullWhen(true)] out EditResult? result, out string? originalRaw)
        {
            result = null;
            originalRaw = null;

            // Pending values must all belong to one element, so their parsed spans stay valid.
            if (pending.Keys.Any(x => x.Node != node))
                FlushPending();

            if (value.StartsWith('{')
                || !nodes.TryGetValue(node, out ElementSyntax? element)
                || element.FindAttribute(name) is not { IsMissingValue: false } attribute
                || attribute.Quote == '\0'
                || ClassifyAttribute(element, name) != AttributeKind.Property
                || !IsEditable(element))
            {
                return false;
            }

            var key = (node, name);
            string currentRaw = pending.TryGetValue(key, out PendingValue? current)
                ? current.Raw
                : parsedText.Substring(attribute.ValueSpan.Start, attribute.ValueSpan.Length);
            if (currentRaw.StartsWith('{'))
                return false;

            List<(object Instance, MarkupLoadScope Scope)> objects = Map.GetObjects(node);
            foreach ((object instance, _) in objects)
            {
                if (!Map.TryGetEntry(instance, out ObjectMap.Entry? entry) || !entry.Members.ContainsKey(name))
                    return false;
            }

            VerifyAccess();
            string raw = MarkupEscaping.EscapeAttributeValue(value, attribute.Quote);
            int applied = 0;
            try
            {
                for (; applied < objects.Count; applied++)
                    ApplyAttributeValue(objects[applied].Scope, objects[applied].Instance, element, name, raw, attribute.Quote);
            }
            catch (MarkupException ex)
            {
                for (int i = 0; i <= applied && i < objects.Count; i++)
                    ApplyAttributeValue(objects[i].Scope, objects[i].Instance, element, name, currentRaw, attribute.Quote);

                result = EditResult.Failure(attribute.ValueSpan, ex.Message);
                return true;
            }

            var patch = new TextChangeSet([new TextChange(new TextSpan(CurrentOffset(attribute.ValueSpan.Start, key), currentRaw.Length), raw)]);
            pending[key] = new PendingValue(attribute.ValueSpan, raw);
            text = new MarkupText(patch.Apply(text.Text), text.Version + 1);
            originalRaw = currentRaw;

            Changed?.Invoke(this, new DocumentChangedEventArgs(patch, Version));
            result = EditResult.Success();
            return true;
        }
```

and these private helpers after `FindLiveIndex`:

```csharp
        /// <summary>
        /// Maps an offset in the last parsed text to the current text, through every other pending value.
        /// </summary>
        private int CurrentOffset(int parsedOffset, (NodeId Node, string Name) key)
        {
            int offset = parsedOffset;
            foreach (((NodeId Node, string Name) other, PendingValue value) in pending)
            {
                if (other != key && value.ParsedSpan.Start < parsedOffset)
                    offset += value.Raw.Length - value.ParsedSpan.Length;
            }

            return offset;
        }

        private void ApplyAttributeValue(MarkupLoadScope scope, object instance, ElementSyntax element, string name, string raw, char quote)
        {
            // A one-attribute start tag is enough for the loader; the existing record of the member stays as it is.
            XElement fragment = Session.Builder.ParseFragment($"<{element.Name} {name}={quote}{raw}{quote}/>", syntax.GetNamespacesInScope(element, includeSelf: true));
            using (SuppressRecording())
                Session.Builder.ApplyAttribute(scope, instance, fragment.Attributes().Single(x => !x.IsNamespaceDeclaration));
        }
```

and a nested record next to `FragmentFrame`:

```csharp
        private sealed record PendingValue(TextSpan ParsedSpan, string Raw);
```

(Add `using System.Diagnostics.CodeAnalysis;` for `NotNullWhen`.)

In `UndoEntry`, add:

```csharp
        /// <summary>
        /// Gets the attribute a run of coalesced edits targets, or <see langword="null"/> for any other entry.
        /// </summary>
        public (NodeId Node, string Name)? CoalesceKey { get; init; }

        /// <summary>
        /// Gets or sets when the entry last grew.
        /// </summary>
        public DateTimeOffset LastEdit { get; set; }
```

Replace `UndoItem` with:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// One thing to replay when an <see cref="UndoEntry"/> is undone or redone: a recorded step, or the value an
    /// attribute had before a run of coalesced edits.
    /// </summary>
    internal sealed class UndoItem
    {
        private readonly EditStep? step;
        private readonly NodeId node;
        private readonly string? name;
        private readonly string? originalRaw;

        private UndoItem(EditStep? step, NodeId node, string? name, string? originalRaw)
        {
            this.step = step;
            this.node = node;
            this.name = name;
            this.originalRaw = originalRaw;
        }

        public (NodeId Node, string Name)? CoalescedKey => name != null ? (node, name) : null;

        public static UndoItem ForStep(EditStep step) => new(step, default, null, null);

        public static UndoItem ForCoalesced(NodeId node, string name, string originalRaw) => new(null, node, name, originalRaw);

        public EditStep CreateStep(DesignDocument document)
        {
            if (step != null)
                return step;

            // GetNode flushes pending values, so the span is the attribute's current one.
            var attribute = document.GetNode(node)?.FindAttribute(name!)
                ?? throw new InvalidOperationException($"The attribute '{name}' of element {node} no longer exists.");
            return new EditStep(
                new TextChangeSet([new TextChange(attribute.ValueSpan, originalRaw!)]),
                [new AttributeChangedAction(node, name!)],
                $"Set {name}");
        }
    }
}
```

In `UndoStack`, add after `RedoDescription`:

```csharp
        /// <summary>
        /// Gets or sets how close together repeated sets of the same attribute must be to merge into one undo step,
        /// as during a drag. 500 ms by default.
        /// </summary>
        public TimeSpan CoalesceWindow { get; set; } = TimeSpan.FromMilliseconds(500);

        internal TimeProvider Clock { get; set; } = TimeProvider.System;
```

and after `Record`:

```csharp
        internal void RecordCoalesced(NodeId node, string name, string originalRaw, string description)
        {
            (NodeId, string) key = (node, name);
            DateTimeOffset now = Clock.GetUtcNow();
            redo.Clear();

            UndoEntry? target = transaction;
            if (target == null && undo.Count > 0 && undo[^1].CoalesceKey == key && now - undo[^1].LastEdit <= CoalesceWindow)
                target = undo[^1];

            if (target == null)
            {
                target = new UndoEntry(description) { CoalesceKey = key };
                undo.Add(target);
            }

            target.LastEdit = now;

            // The first edit of a run knows the value to go back to; later ones in the same run don't add anything.
            if (!target.Items.Exists(x => x.CoalescedKey == key))
                target.Items.Add(UndoItem.ForCoalesced(node, name, originalRaw));

            Changed?.Invoke(this, EventArgs.Empty);
        }
```

In `MarkupEditor.SetAttribute`, try the fast path **before** `document.GetNode` (which would flush every frame):

```csharp
            ArgumentException.ThrowIfNullOrEmpty(name);
            ArgumentNullException.ThrowIfNull(value);

            if (MarkupParser.IsValidName(name) && document.TryFastSetAttribute(node, name, value, out EditResult? fast, out string? originalRaw))
            {
                if (fast.Succeeded)
                    UndoStack.RecordCoalesced(node, name, originalRaw!, $"Set {name}");
                return fast;
            }

            if (document.GetNode(node) is not { } element)
                return UnknownNode(node);
```

The rest of `SetAttribute` stays as Task 9 wrote it.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~Icy.Tests.Design"`
Expected: PASS for `CoalescingTests` (Total 9 there) and every earlier design test. Tasks 9–10 tests now partly go through the fast path, and must still pass unchanged.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (no failures), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/CoalescingTests.cs
git commit -m "Coalesce repeated attribute edits and defer their re-parse

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 12: Saving, disposal, lifetime, pooling and performance

**Files:**
- Modify: `sources/IcyUI.Design/DesignDocument.cs` (`Save`, `SaveAs`, `ThrowIfDisposed`)
- Modify: `sources/IcyUI.Design/DesignSession.cs` (`SourcePathResolver`)
- Modify: `sources/IcyUI.Design/MarkupEditor.cs`, `sources/IcyUI.Design/UndoStack.cs` (throw after dispose)
- Test: `sources/IcyUI.Tests/Design/DesignLifetimeTests.cs`

**Interfaces:**
- Produces:
  - `public void DesignDocument.Save()`, `public void DesignDocument.SaveAs(string filePath)`.
  - `public Func<string, string?> DesignSession.SourcePathResolver` (default: an existing file path resolves to its full path; anything else to `null`).
  - Internal `DesignDocument.ThrowIfDisposed()`; every public `MarkupEditor` edit method and `UndoStack.Undo`/`Redo` call it first.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Design/DesignLifetimeTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Icy.Design;
using Icy.Design.Editing;
using Icy.Design.Text;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using Xunit.Abstractions;

namespace Icy.Tests.Design
{
    public class DesignLifetimeTests(ITestOutputHelper output)
    {
        private static readonly string Page =
            """
            <StackPanel x:Name="root">
              <Border x:Name="a" Width="10"/>
              <Border x:Name="b" Width="20"/>
            </StackPanel>
            """.ReplaceLineEndings("\n");

        [Fact]
        public void Save_WritesTheTextToTheResolvedFile()
        {
            using var host = new DesignTestHost();
            string path = Path.Combine(Path.GetTempPath(), $"icy-design-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, Page);
            try
            {
                (UIElement root, DesignDocument document) = host.Load(File.ReadAllText(path), path);
                document.Editor.SetAttribute(host.IdOf(DesignTestHost.Named<Border>(root, "a")), "Width", "99");

                document.Save();

                Assert.Equal(document.Text, File.ReadAllText(path));
                Assert.Contains("Width=\"99\"", File.ReadAllText(path));
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Save_WithAPathThatIsNoFile_Throws()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page, "Pages/NotAFile.xml");

            Assert.Throws<InvalidOperationException>(document.Save);
        }

        [Fact]
        public void SaveAs_WritesUtf8WithoutABom()
        {
            using var host = new DesignTestHost();
            (_, DesignDocument document) = host.Load(Page);
            string path = Path.Combine(Path.GetTempPath(), $"icy-design-{Guid.NewGuid():N}.xml");
            try
            {
                document.SaveAs(path);

                byte[] bytes = File.ReadAllBytes(path);
                Assert.Equal(Encoding.UTF8.GetBytes(Page), bytes);
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Editor_AfterSessionDispose_Throws()
        {
            var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(Page);
            NodeId a = host.IdOf(DesignTestHost.Named<Border>(root, "a"));

            host.Dispose();

            Assert.Throws<ObjectDisposedException>(() => document.Editor.SetAttribute(a, "Width", "1"));
            Assert.Throws<ObjectDisposedException>(() => document.Editor.InsertElement(a, 0, "<Border/>"));
            Assert.Throws<ObjectDisposedException>(() => document.Editor.UndoStack.Undo());
        }

        [Fact]
        public void AnElementRemovedThroughTheEditor_IsCollectable()
        {
            using var host = new DesignTestHost();
            WeakReference removed = RemoveThroughEditor(host);

            CollectEverything();

            Assert.False(removed.IsAlive);
        }

        [Fact]
        public void AnElementTheGameRemoved_IsCollectable()
        {
            using var host = new DesignTestHost();
            WeakReference removed = RemoveThroughGame(host);

            CollectEverything();

            Assert.False(removed.IsAlive);
        }

        [Fact]
        public void ACollectedPage_IsDroppedFromTheDocuments()
        {
            using var host = new DesignTestHost();
            LoadAndForget(host);

            CollectEverything();

            Assert.Empty(host.Session.Documents);
        }

        [Fact]
        public void PooledTemplateItems_AddNothingToTheMap()
        {
            using var host = new DesignTestHost();
            (UIElement root, DesignDocument document) = host.Load(
                """
                <StackPanel x:Name="root">
                  <ItemsControl x:Name="list">
                    <ItemsControl.ItemTemplate>
                      <DataTemplate><Border Height="20"/></DataTemplate>
                    </ItemsControl.ItemTemplate>
                  </ItemsControl>
                </StackPanel>
                """.ReplaceLineEndings("\n"));
            var list = DesignTestHost.Named<ItemsControl>(root, "list");
            int tracked = document.TrackedObjectCount;
            int documents = host.Session.Documents.Count;

            for (int i = 0; i < 50; i++)
                list.ItemTemplate!.Build(new object());

            Assert.Equal(tracked, document.TrackedObjectCount);
            Assert.Equal(documents, host.Session.Documents.Count);
        }

        [Fact]
        public void ParseAndMatch_OfA100KbDocument_IsMeasured()
        {
            using var host = new DesignTestHost();
            var builder = new StringBuilder("<StackPanel x:Name=\"root\">\n");
            for (int i = 0; builder.Length < 100_000; i++)
                builder.Append($"  <Border x:Name=\"b{i}\" Width=\"10\" Height=\"20\" Margin=\"1,2,3,4\" HorizontalAlignment=\"Left\"/>\n");
            builder.Append("</StackPanel>");
            (UIElement root, DesignDocument document) = host.Load(builder.ToString());
            Assert.Empty(document.Syntax.Diagnostics);

            var timings = new List<double>();
            for (int i = 0; i < 7; i++)
            {
                var width = document.Syntax.Root!.Elements.First().FindAttribute("Width")!;
                var step = new EditStep(new TextChangeSet([new TextChange(width.ValueSpan, (11 + i).ToString(System.Globalization.CultureInfo.InvariantCulture))]), [], "measure");
                var watch = Stopwatch.StartNew();
                document.Apply(step, out _);
                timings.Add(watch.Elapsed.TotalMilliseconds);
            }

            timings.Sort();
            var fast = Stopwatch.StartNew();
            NodeId first = host.IdOf(((StackPanel)root).Children[0]);
            for (int i = 0; i < 60; i++)
                document.Editor.SetAttribute(first, "Width", (30 + i).ToString(System.Globalization.CultureInfo.InvariantCulture));
            double perFrame = fast.Elapsed.TotalMilliseconds / 60;

            output.WriteLine($"{document.Text.Length} chars: parse + match median {timings[timings.Count / 2]:0.00} ms (budget 5 ms); fast-path set {perFrame:0.000} ms per frame.");
        }

        private static void CollectEverything()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RemoveThroughEditor(DesignTestHost host)
        {
            (UIElement root, DesignDocument document) = host.Load(Page);
            var b = DesignTestHost.Named<Border>(root, "b");
            document.Editor.RemoveElement(host.IdOf(b));
            return new WeakReference(b);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RemoveThroughGame(DesignTestHost host)
        {
            (UIElement root, _) = host.Load(Page);
            var b = DesignTestHost.Named<Border>(root, "b");
            ((StackPanel)root).Children.Remove(b);
            Icy.Markup.MarkupNameScope.GetScope(root)!.Unregister("b");
            return new WeakReference(b);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void LoadAndForget(DesignTestHost host) => host.Load(Page);
    }
}
```

`RemoveThroughGame` also unregisters the name: the page's own `MarkupNameScope` (core, not design code) holds named elements strongly, exactly as it does without any design session.

- [ ] **Step 2: Run the tests to make sure they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DesignLifetimeTests"`
Expected: the build FAILS (`Save`, `SaveAs` not found).

- [ ] **Step 3: Implement**

In `DesignSession`, add after `Documents`:

```csharp
        /// <summary>
        /// Gets or sets how <see cref="DesignDocument.Save"/> turns a document's <see cref="DesignDocument.SourcePath"/>
        /// into a file to write.
        /// </summary>
        /// <remarks>
        /// A source path is whatever the page was loaded with, often an asset name such as <c>Pages/Main.xml</c>
        /// resolved through an <see cref="Icy.Assets.IAssetContext"/>. By default an existing file path resolves to its
        /// full path and anything else to <see langword="null"/>, which makes <see cref="DesignDocument.Save"/> throw.
        /// Set this to map asset names to source files in your project.
        /// </remarks>
        /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
        public Func<string, string?> SourcePathResolver
        {
            get => sourcePathResolver;
            set => sourcePathResolver = value ?? throw new ArgumentNullException(nameof(value));
        }
```

with the field `private Func<string, string?> sourcePathResolver = static path => File.Exists(path) ? Path.GetFullPath(path) : null;`.

In `DesignDocument`, add a static field `private static readonly UTF8Encoding Utf8WithoutBom = new(encoderShouldEmitUTF8Identifier: false);` (`using System.Text;`), and these members:

```csharp
        /// <summary>
        /// Writes the text to the file <see cref="SourcePath"/> resolves to through
        /// <see cref="DesignSession.SourcePathResolver"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">The document has no source path, or it doesn't resolve to a file.</exception>
        /// <exception cref="IOException">The file couldn't be written.</exception>
        public void Save()
        {
            string? path = SourcePath != null ? Session.SourcePathResolver(SourcePath) : null;
            if (path == null)
            {
                throw new InvalidOperationException(
                    $"'{SourcePath ?? "(no source path)"}' doesn't resolve to a file. Use SaveAs, or set DesignSession.SourcePathResolver.");
            }

            SaveAs(path);
        }

        /// <summary>
        /// Writes the text, exactly as it is, to <paramref name="filePath"/> as UTF-8 without a byte order mark.
        /// </summary>
        /// <param name="filePath">The file to write.</param>
        /// <exception cref="ArgumentException"><paramref name="filePath"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="IOException">The file couldn't be written.</exception>
        public void SaveAs(string filePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(filePath);
            File.WriteAllText(filePath, Text, Utf8WithoutBom);
        }

        internal void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Session.IsDisposed, this);
```

`Text` already includes pending fast-path values, so saving needs no flush.

Call `document.ThrowIfDisposed();` as the first statement of `MarkupEditor.InsertElement`, `RemoveElement`, `MoveElement`, `SetAttribute`, `ClearAttribute` and `BeginTransaction`, and of `UndoStack.Undo` and `UndoStack.Redo` (turn those two into block bodies). Document it on each with `/// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>`.

- [ ] **Step 4: Run the tests to make sure they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DesignLifetimeTests" --logger "console;verbosity=detailed"`
Expected: PASS, Total 9. Copy the measurement line (`… chars: parse + match median … ms`) into the task report.

If a collectability test fails, **don't** loosen it. Find the root with a memory profiler or `dotnet-gcdump`, and report what holds the element. If the culprit is core code that holds elements strongly without any design session too, report it as a pre-existing issue instead of working around it here.

- [ ] **Step 5: Full suite, warnings, commit**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` (no failures), then `dotnet build "sources/IcyUI.sln" 2>&1 | tail -3` (warnings at baseline).

```bash
git add sources/IcyUI.Design sources/IcyUI.Tests/Design/DesignLifetimeTests.cs
git commit -m "Save design documents and guard their lifetime

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 13: `DesignDemo` in both sample hosts

**Files:**
- Create: `sources/Shared Samples/DesignDemo.cs`
- Create: `sources/MonoGame Sample/Samples/DesignSample.cs`
- Modify: `sources/MonoGame Sample/SampleGame.cs` (register last), `sources/MonoGame Sample/MonoGame Sample.csproj` (link + project reference)
- Modify: `sources/Stride Sample/SampleGame.cs` (field, build last, add, cycle 18, visibility, doc list), `sources/Stride Sample/Stride Sample.csproj` (link + project reference)
- Modify: `sources/IcyUI.Tests/IcyUI.Tests.csproj` (link the demo)
- Test: `sources/IcyUI.Tests/Samples/DesignDemoTests.cs`

**Interfaces:**
- Consumes: `DesignSession.Attach`, `FindDocument`, `MarkupEditor`, `UndoStack` (Tasks 6–12).
- Produces: `public static class DesignDemo` with `const string Markup` and `static UIElement Build(IcyConfiguration configuration, string fontFamily)`.

**Cross-engine note.** The demo is pure core plus `IcyUI.Design`; both hosts only reference and show it. Nothing changes in `IcyUI.MonoGame` or `IcyUI.Stride`, and `IcyUI.FNA` needs no work. The demo attaches a design session to the host's **shared** configuration for the app's lifetime, so pages loaded after it are tracked too. That's harmless, but it's why both hosts build it last.

- [ ] **Step 1: Write the failing test**

Add to `sources/IcyUI.Tests/IcyUI.Tests.csproj`, in the item group that links sample demos:

```xml
    <Compile Include="..\Shared Samples\DesignDemo.cs" Link="Samples\DesignDemo.cs" />
```

`sources/IcyUI.Tests/Samples/DesignDemoTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Markup;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    public class DesignDemoTests
    {
        [Fact]
        public void Build_TracksThePageAndShowsItsMarkup()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            var root = (StackPanel)DesignDemo.Build(configuration, "Airfool");

            Assert.Equal(4, root.Children.Count);
            Assert.Equal(DesignDemo.Markup, Assert.IsType<TextBlock>(root.Children[3]).Text);
            Assert.NotNull(configuration.Types.Markup.LoadObserver);
        }

        [Fact]
        public void Build_TwiceOnOneConfiguration_ReusesItsSession()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            DesignDemo.Build(configuration, "Airfool");
            DesignDemo.Build(configuration, "Airfool");
        }
    }
}
```

- [ ] **Step 2: Run the test to make sure it fails**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DesignDemoTests"`
Expected: the build FAILS (`DesignDemo.cs` doesn't exist).

- [ ] **Step 3: Write the demo**

`sources/Shared Samples/DesignDemo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Icy.Configuration;
using Icy.Design;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;

namespace Icy.SharedSamples
{
    /// <summary>
    /// Edits a markup page live through <see cref="MarkupEditor"/>, and shows the markup text every edit produces.
    /// The manual check for the markup design document (Phase 10.1).
    /// </summary>
    public static class DesignDemo
    {
        /// <summary>
        /// The page the demo edits.
        /// </summary>
        public const string Markup =
            """
            <Border Padding="14" Background="#FF26262C" BorderBrush="#FF464652" BorderThickness="1">
              <StackPanel x:Name="content" Orientation="Vertical">
                <TextBlock x:Name="title" FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">Edited live, saved as markup</TextBlock>
                <StackPanel x:Name="row" Orientation="Horizontal">
                  <Border x:Name="first" Background="LightCoral" Width="100" Height="60" Margin="0,0,8,0"/>
                  <Border Background="LightSkyBlue" Width="100" Height="60" Margin="0,0,8,0"/>
                </StackPanel>
              </StackPanel>
            </Border>
            """;

        // A configuration has a single load-observer slot, so one session per configuration.
        private static readonly ConditionalWeakTable<IcyConfiguration, DesignSession> Sessions = new();

        /// <summary>
        /// Builds the demo: the tracked page, a toolbar of edits, a status line, and the live markup text.
        /// </summary>
        /// <param name="configuration">The host's configuration.</param>
        /// <param name="fontFamily">The font family to use.</param>
        /// <returns>The demo's root element.</returns>
        public static UIElement Build(IcyConfiguration configuration, string fontFamily)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            configuration.Fonts.DefaultFontFamily = fontFamily;

            // Real games attach a session only in development builds. The demo keeps one attached for the app's
            // whole life, so every page loaded after this one is tracked too; the hosts build this demo last.
            DesignSession session = Sessions.GetValue(configuration, DesignSession.Attach);
            UIElement page = new MarkupLoader(configuration).Load(Markup, nameof(DesignDemo));
            DesignDocument document = session.FindDocument(page, out _)!;
            MarkupEditor editor = document.Editor;
            MarkupNameScope names = MarkupNameScope.GetScope(page)!;

            NodeId IdOf(string name)
            {
                session.FindDocument(names.Find(name)!, out NodeId id);
                return id;
            }

            var status = new TextBlock { Text = "Ready", Margin = new Thickness(0, 6, 0, 6) };
            var markupView = new TextBlock { FontSize = 12, Text = document.Text };
            document.Changed += (_, _) => markupView.Text = document.Text;

            int inserted = 0;
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            toolbar.Children.Add(CreateButton("Bigger title", status, () =>
            {
                var title = (TextBlock)names.Find("title")!;
                return editor.SetAttribute(IdOf("title"), "FontSize", (title.FontSize + 2).ToString(CultureInfo.InvariantCulture));
            }));
            toolbar.Children.Add(CreateButton("Insert button", status, () =>
                editor.InsertElement(IdOf("row"), 0, $"<Button Padding=\"12,6\" Margin=\"0,0,8,0\">New {++inserted}</Button>")));
            toolbar.Children.Add(CreateButton("Move first box", status, () =>
                editor.MoveElement(IdOf("first"), IdOf("content"), 1)));
            toolbar.Children.Add(CreateButton("Undo", status, editor.UndoStack.Undo));
            toolbar.Children.Add(CreateButton("Redo", status, editor.UndoStack.Redo));

            var root = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(20),
            };
            root.Children.Add(page);
            root.Children.Add(toolbar);
            root.Children.Add(status);
            root.Children.Add(markupView);
            return root;
        }

        private static Button CreateButton(string label, TextBlock status, Func<EditResult> action)
        {
            var button = new Button
            {
                Content = new TextBlock { Text = label },
                Padding = new Thickness(12, 6),
                Margin = new Thickness(0, 0, 8, 0),
            };
            button.Click += (_, _) =>
            {
                EditResult result = action();
                status.Text = result.Succeeded ? "OK" : $"Failed: {result.Error?.Message}";
            };
            return button;
        }
    }
}
```

- [ ] **Step 4: Run the test to make sure it passes**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DesignDemoTests"`
Expected: PASS, Total 2.

- [ ] **Step 5: Register the demo in the MonoGame host**

In `sources/MonoGame Sample/MonoGame Sample.csproj`, add `<ProjectReference Include="..\IcyUI.Design\IcyUI.Design.csproj" />` next to the other project references, and `<Compile Include="..\Shared Samples\DesignDemo.cs" Link="Samples\DesignDemo.cs" />` after the `ScalingDemo.cs` link.

`sources/MonoGame Sample/Samples/DesignSample.cs`:

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
    /// Runs <see cref="DesignDemo"/> - a markup page edited live, with the markup text it produces - as its own
    /// selectable sample.
    /// </summary>
    internal class DesignSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public DesignSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "Design Demo")
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

            demoRoot = DesignDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
```

In `sources/MonoGame Sample/SampleGame.cs`, add it as the **last** entry of the `samplesRunner.Prepare([...])` list:

```csharp
                new ScalingSample(this, uiConfiguration, canvas),
                new DesignSample(this, uiConfiguration, canvas)
```

- [ ] **Step 6: Register the demo in the Stride host**

In `sources/Stride Sample/Stride Sample.csproj`, add `<ProjectReference Include="..\IcyUI.Design\IcyUI.Design.csproj" />` and `<Compile Include="..\Shared Samples\DesignDemo.cs" Link="DesignDemo.cs" />`.

In `sources/Stride Sample/SampleGame.cs`:
- the class doc list: replace `<see cref="PropertyGridDemo"/>, and <see cref="ScalingDemo"/>` with `<see cref="PropertyGridDemo"/>, <see cref="ScalingDemo"/>, and <see cref="DesignDemo"/>`;
- add the field `private UIElement? designRoot;` after `scalingRoot`;
- after `scalingRoot = ScalingDemo.Build(configuration, "Airfool");`, add `designRoot = DesignDemo.Build(configuration, "Airfool");` (it must be the last demo built);
- after `canvas.Add(scalingRoot);`, add `canvas.Add(designRoot);`;
- change `selectedDemo = (selectedDemo + 1) % 17;` to `% 18`;
- in `UpdateSelectedDemo`, add `designRoot!.IsVisible = selectedDemo == 17;`.

- [ ] **Step 7: Build everything and run the whole suite**

Run:
```bash
dotnet build "sources/IcyUI.sln" 2>&1 | tail -3
dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"
```
Expected: build succeeds with warnings at the Task 1 baseline. Tests pass with **Total = 1085 + 151** (every test this plan adds: 20 + 27 + 7 + 5 + 8 + 10 + 7 + 19 + 18 + 10 + 9 + 9 + 2), and none fail. If Total differs, find out why before going on.

- [ ] **Step 8: Commit**

```bash
git add "sources/Shared Samples/DesignDemo.cs" "sources/MonoGame Sample" "sources/Stride Sample" sources/IcyUI.Tests
git commit -m "Add DesignDemo to both sample hosts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

- [ ] **Step 9: Hand over for review and Ivan's smoke test**

Run the whole-branch review of this phase's commits (the process's step 4). Then give Ivan this checklist, for both engines and both machines. Don't claim any of it yourself.

1. Open **Design Demo** (MonoGame: PageUp/PageDown; Stride: PageDown/Shift+Tab, the last demo).
2. **Bigger title**: the title grows, and the markup text below shows `FontSize="20"`, then `22`, and so on, with the rest of the text unchanged.
3. **Insert button**: a "New 1" button appears first in the row, and its line appears in the markup with matching indentation.
4. **Move first box**: the red box moves between the title and the row. It's the same box, not a copy.
5. **Undo** repeatedly back to the start: the markup text is exactly the original again. **Redo** goes forward again.
6. The other demos still behave as before.
7. On the ARM64 laptop with MonoGame, the known DesktopGL shutdown hang is expected; it's unrelated.

---

## Self-review notes

- **Spec coverage:**

| Spec section | Where it's implemented |
|---|---|
| Core seam | Tasks 3–5 |
| Syntax layer | Task 2 |
| Document and map | Task 6 |
| Re-sync | Task 7 |
| Operations | Tasks 8–9 |
| Rebuild, `NeedsReload` and opaque regions | Tasks 8–9 |
| Atomicity | Task 7 |
| Undo and transactions | Task 10 |
| Coalescing and deferred re-parse | Task 11 |
| Threading | Task 7 |
| Session, opt-in and pruning | Tasks 6 and 12 |
| Saving | Task 12 |
| Performance | Tasks 4, 11 and 12 |
| Demo | Task 13 |
| `CLAUDE.md` layout row | Task 1 |

- **Deviations** from the spec are listed at the top of this plan. They're for Ivan to confirm.
- **Known minor, accepted:**
  - `BindingExtension` hooks `DataContextChanged` with a closure that keeps updating a disposed binding's `Source` after the binding is replaced. `Binding` ignores updates once disposed, so it's harmless, but each re-bind leaks one closure for the element's lifetime. Fix it in core when bindings get an unbind hook.
  - `NeedsReload` is never reset in 10.1; reloading is 10.2/10.5's job.
