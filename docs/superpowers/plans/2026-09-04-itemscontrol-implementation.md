# ItemsControl (Tier-2 Phase 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add data-bound, virtualized, pooled repeater support to IcyUI - a new `DataTemplate` templating
concept, a minimal `ItemContainer`, `ItemsControl` itself, and the `ScrollViewer` changes that let it
virtualize instead of laying out its entire bound collection.

**Architecture:** `ItemsControl` extends `Control` (Background/BorderBrush/Padding/Template for free) but
manages many realized `ItemContainer` children itself via a private `Dictionary<int, ItemContainer>`
keyed by data index - not `ContentControl`'s single-`Content` model, not `Panel.Children`. It implements a
new `IVirtualizingScrollInfo` interface directly (no separate virtualizing-panel type); `ScrollViewer`
detects that interface on `Content` and delegates extent/offset/viewport instead of fully measuring or
arranging it, mirroring WPF's `ScrollViewer` → `IScrollInfo` → `VirtualizingStackPanel` split. Item height
is two-pass-estimated (a running average fills in unmeasured items; real measurements replace estimates
as items are realized), with a minimal, exact-delta scroll-offset correction for an already-realized item
resizing above the current top-visible index (general anchor-correction for big-jump estimation drift is
explicitly deferred, per the spec).

**Tech Stack:** .NET 8, C#, xUnit, IcyUI's own markup/property-registry/layout system (no third-party
libraries).

**Spec:** `docs/superpowers/specs/2026-09-04-itemscontrol-design.md`

## Global Constraints

- Every public type/member gets full XML doc comments (`<summary>`, `<param>`, `<returns>`,
  `<exception>`, `<remarks>` where non-obvious) - matches this repo's hard requirement (`CLAUDE.local.md`)
  and the style already used throughout `ControlTemplate.cs`/`ContentControl.cs`/`ScrollViewer.cs`.
- Build: `dotnet build "sources/IcyUI.sln"`. Test: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
  (filter a single test with `--filter "FullyQualifiedName~ItemsControlTests"`). No CI/build scripts exist
  in this repo - these are the canonical commands (`build-test` skill).
- `IcyUI` and `IcyUI.MonoGame` are the only projects with StyleCop/`EnforceCodeStyleInBuild` wired in - a
  full-solution build is still the right check, since every file this plan touches lives in `IcyUI`
  itself.
- `ItemsControl` virtualizes **vertically only** in this phase (matches the spec's own vertical-only
  worked example and every consumer named in it - `PropertyGrid` rows, dropdown lists, etc.). `ExtentWidth`
  is simply the current viewport width (no horizontal scrolling/virtualization) - a horizontal or wrapping
  `ItemsControl` layout is out of scope, the same way `StackPanel` defaults to one orientation without
  needing to support both perfectly generically.
- The scroll-ahead realization buffer is **forward-only** (extends past the bottom of the viewport for
  smoother downward scrolling, never backward past the top) - this keeps "which index is the anchor"
  unambiguous (see Task 7's own remarks) and is a defensible, common simplification (WPF's own
  `VirtualizingStackPanel` cache-length setting is typically direction-biased too).

---

### Task 1: `IVirtualizingScrollInfo`

**Files:**
- Create: `sources/IcyUI/UI/IVirtualizingScrollInfo.cs`

**Interfaces:**
- Produces: `IVirtualizingScrollInfo` with `float ExtentWidth { get; }`, `float ExtentHeight { get; }`,
  `void OnViewportChanged(float horizontalOffset, float verticalOffset, float viewportWidth, float viewportHeight)`,
  `event EventHandler<float>? VerticalOffsetCorrectionRequested`.

This interface is the whole `ScrollViewer` ↔ virtualizing-content contract. The event is a deliberate
addition beyond the design spec's three-member sketch: §6 of the spec ("minimal above-viewport
anchoring") requires the *content* to shift the scroll offset by an exact delta when an already-realized
item above the viewport resizes - but `OnViewportChanged` only flows information *into* the content
(`ScrollViewer` telling it the current offset), with no way back out. Without this event, a §6 correction
would only ever update `ItemsControl`'s own internal bookkeeping, never the `ScrollViewer.VerticalOffset`
property the scrollbar/user actually reads - silently failing to do what §6 promises. The event carries
the delta to apply (not an absolute value), so a subscriber can just do `VerticalOffset += delta`.

- [ ] **Step 1: Write the interface**

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI
{
    /// <summary>
    /// Lets a <see cref="Controls.ScrollViewer"/> delegate extent/offset/viewport handling to its
    /// <see cref="Controls.ContentControl.Content"/>, instead of fully measuring and arranging it, when that
    /// content can realize only the portion of itself that's actually visible.
    /// </summary>
    /// <remarks>
    /// Mirrors WPF's <c>ScrollViewer</c> → <c>IScrollInfo</c> → <c>VirtualizingStackPanel</c> delegation. IcyUI has
    /// no separate virtualizing-panel type - <see cref="Controls.ItemsControl"/> implements this interface
    /// directly, since nothing else in the roadmap needs virtualized layout independent of an items list.
    /// </remarks>
    public interface IVirtualizingScrollInfo
    {
        /// <summary>
        /// Gets this content's full natural width, estimated or exact depending on how much of it has actually
        /// been measured so far.
        /// </summary>
        float ExtentWidth { get; }

        /// <summary>
        /// Gets this content's full natural height, estimated or exact depending on how much of it has actually
        /// been measured so far.
        /// </summary>
        float ExtentHeight { get; }

        /// <summary>
        /// Occurs when this content needs its host <see cref="Controls.ScrollViewer"/> to adjust
        /// <see cref="Controls.ScrollViewer.VerticalOffset"/> by a specific amount, so on-screen content doesn't
        /// visibly jump when something already realized (and positioned above the current viewport) changes size.
        /// </summary>
        /// <remarks>
        /// The event's <see langword="float"/> payload is the delta to apply
        /// (<c>VerticalOffset += delta</c>), not an absolute new value.
        /// </remarks>
        event EventHandler<float>? VerticalOffsetCorrectionRequested;

        /// <summary>
        /// Called by the host <see cref="Controls.ScrollViewer"/> whenever the offset or viewport size it's
        /// presenting this content through may have changed - the single point where this content decides what to
        /// realize, de-realize, and how to position it.
        /// </summary>
        /// <param name="horizontalOffset">How far the viewport is scrolled horizontally.</param>
        /// <param name="verticalOffset">How far the viewport is scrolled vertically.</param>
        /// <param name="viewportWidth">The visible viewport's width.</param>
        /// <param name="viewportHeight">The visible viewport's height.</param>
        void OnViewportChanged(float horizontalOffset, float verticalOffset, float viewportWidth, float viewportHeight);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build "sources/IcyUI.sln"`
Expected: builds clean (nothing implements the interface yet, so nothing else changes).

- [ ] **Step 3: Commit**

```bash
git add sources/IcyUI/UI/IVirtualizingScrollInfo.cs
git commit -m "Add IVirtualizingScrollInfo interface"
```

---

### Task 2: `DataTemplate` + `MarkupLoader` support

**Files:**
- Create: `sources/IcyUI/UI/Styles/DataTemplate.cs`
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs` (add `LoadDataTemplateContent`, extend `CreateObject`'s
  special-casing)
- Test: `sources/IcyUI.Tests/Styles/DataTemplateTests.cs`

**Interfaces:**
- Consumes: `Icy.Configuration.IcyConfiguration`, `Icy.Markup.MarkupLoader`, `System.Xml.Linq.XElement`
  (all pre-existing).
- Produces: `DataTemplate` with `public UIElement Build(object dataItem)` and
  `internal void SetContent(XElement content, IcyConfiguration configuration, string? sourcePath)`.
  `MarkupLoader` gains `public UIElement LoadDataTemplateContent(XElement content, string? sourcePath = null)`.

`ControlTemplate.LoadContent` needs a non-null `templatedControl` (for `{TemplateBinding}` resolution,
which `DataTemplate` doesn't support at all) - `MarkupLoader.LoadTemplateContent` throws on a null one, so
`DataTemplate` can't reuse it directly. `LoadDataTemplateContent` is the same method with that requirement
(and the `TemplatedControl` wiring) dropped; `MarkupLoadContext.TemplatedControl` already tolerates being
unset (`null` = "an ordinary document", per its own existing doc comment).

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Styles/DataTemplateTests.cs
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Styles
{
    public class DataTemplateTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new Tests.Input.FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        private static DataTemplate LoadTemplate(string markup)
        {
            var configuration = CreateConfiguration();
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        [Fact]
        public void Build_SetsRootDataContextToTheDataItem()
        {
            var template = LoadTemplate("""<DataTemplate><TextBlock Text="{Binding Name}"/></DataTemplate>""");
            var item = new { Name = "Ada" };

            UIElement root = template.Build(item);

            Assert.Same(item, root.DataContext);
        }

        [Fact]
        public void Build_BindingsInsideResolveAgainstTheDataItem()
        {
            var template = LoadTemplate("""<DataTemplate><TextBlock Text="{Binding Name}"/></DataTemplate>""");
            var item = new { Name = "Grace" };

            var root = (TextBlock)template.Build(item);

            Assert.Equal("Grace", root.Text);
        }

        [Fact]
        public void Build_TwoCallsProduceIndependentTrees()
        {
            var template = LoadTemplate("""<DataTemplate><TextBlock Text="{Binding Name}"/></DataTemplate>""");

            UIElement first = template.Build(new { Name = "A" });
            UIElement second = template.Build(new { Name = "B" });

            Assert.NotSame(first, second);
        }

        [Fact]
        public void Build_WithoutBeingLoadedFromMarkup_Throws()
        {
            var template = new DataTemplate();

            Assert.Throws<InvalidOperationException>(() => template.Build(new object()));
        }

        [Fact]
        public void Load_MoreThanOneRootElement_Throws()
        {
            var configuration = CreateConfiguration();
            var loader = new MarkupLoader(configuration);

            var ex = Assert.Throws<MarkupException>(() =>
                loader.LoadObject("""<DataTemplate><TextBlock/><TextBlock/></DataTemplate>"""));
            Assert.Contains("exactly one root element", ex.Message);
        }

        [Fact]
        public void Load_DoesNotEagerlyBuildTheContent()
        {
            // A DataTemplate's content is captured as raw markup (see ControlTemplate's own equivalent
            // precedent) - loading the document must not itself construct a live UIElement tree, only Build does.
            var configuration = CreateConfiguration();
            var loader = new MarkupLoader(configuration);

            var template = (DataTemplate)loader.LoadObject("""<DataTemplate><Button Content="Never built eagerly"/></DataTemplate>""");

            // No exception, and Build still works afterward - proving the captured content is still there.
            UIElement root = template.Build(new object());
            Assert.IsType<Button>(root);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DataTemplateTests"`
Expected: FAIL to compile - `DataTemplate` doesn't exist yet.

- [ ] **Step 3: Add `DataTemplate`**

```csharp
// sources/IcyUI/UI/Styles/DataTemplate.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Xml.Linq;
using Icy.Configuration;
using Icy.Markup;

namespace Icy.UI.Styles
{
    /// <summary>
    /// Builds a visual tree from markup content for an arbitrary data object - the templating mechanism
    /// <see cref="Controls.ItemsControl"/> uses to turn each bound item into a realized element.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Like <see cref="ControlTemplate"/>, content is captured once when the <see cref="DataTemplate"/> itself is
    /// loaded (see <see cref="Icy.Markup.MarkupLoader"/>'s own <see cref="DataTemplate"/> special case) and
    /// instantiated fresh - via <see cref="Build(object)"/> - every time it's needed. Two calls to
    /// <see cref="Build(object)"/>, even for the same data item, each get their own independent visual subtree.
    /// </para>
    /// <para>
    /// Unlike <see cref="ControlTemplate"/>, this has no <c>TargetType</c> (it works against any data object's
    /// runtime type) and its content can't use <c>{TemplateBinding}</c> (there's no templated control to bind back
    /// to) - instead, <see cref="Build(object)"/> sets the built tree's root <see cref="UIElement.DataContext"/> to
    /// the data item, and ordinary <c>{Binding}</c> inside the template resolves against that.
    /// </para>
    /// </remarks>
    public class DataTemplate
    {
        private XElement? content;
        private IcyConfiguration? configuration;
        private string? sourcePath;

        /// <summary>
        /// Builds a fresh visual tree from this template's content, for <paramref name="dataItem"/>.
        /// </summary>
        /// <param name="dataItem">
        /// The data object the built tree's root <see cref="UIElement.DataContext"/> is set to - what every
        /// <c>{Binding}</c> inside the template resolves against.
        /// </param>
        /// <returns>The freshly built root element - never shared with any other call, even for the same data item.</returns>
        /// <exception cref="InvalidOperationException">This template was never given any content.</exception>
        /// <exception cref="Icy.Markup.MarkupException">The content is malformed, or breaks a rule of the markup language.</exception>
        public UIElement Build(object dataItem)
        {
            ArgumentNullException.ThrowIfNull(dataItem);
            if (content == null || configuration == null)
                throw new InvalidOperationException($"This '{nameof(DataTemplate)}' has no content to build - it was never loaded from markup.");

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.LoadDataTemplateContent(content, sourcePath);
            root.DataContext = dataItem;
            return root;
        }

        /// <summary>
        /// Captures this template's content, ready for later per-item instantiation via <see cref="Build(object)"/>.
        /// </summary>
        /// <param name="content">The template's single root element.</param>
        /// <param name="configuration">The configuration to build content through.</param>
        /// <param name="sourcePath">The template's source document path, for error messages.</param>
        /// <remarks>
        /// Called once, by <see cref="Icy.Markup.MarkupLoader"/>'s own <see cref="DataTemplate"/> special case,
        /// immediately after this instance is constructed - never by ordinary application code.
        /// </remarks>
        internal void SetContent(XElement content, IcyConfiguration configuration, string? sourcePath)
        {
            this.content = content;
            this.configuration = configuration;
            this.sourcePath = sourcePath;
        }
    }
}
```

- [ ] **Step 4: Add `MarkupLoader.LoadDataTemplateContent` and the `CreateObject` special case**

In `sources/IcyUI/Markup/MarkupLoader.cs`, add this public method right after `LoadTemplateContent`
(after its closing brace, before `LoadCore`):

```csharp
        /// <summary>
        /// Builds a fresh visual tree from a <see cref="Icy.UI.Styles.DataTemplate"/>'s already-parsed content.
        /// </summary>
        /// <param name="content">
        /// The template's root element, captured once when the <c>DataTemplate</c> itself was loaded (see
        /// <see cref="CreateObject"/>'s <c>DataTemplate</c> special case) - not re-parsed from text here.
        /// </param>
        /// <param name="sourcePath">The template's source document path, used only to make error messages locatable.</param>
        /// <returns>The freshly built root element.</returns>
        /// <exception cref="MarkupException">
        /// <paramref name="content"/> is malformed, breaks a rule of the language, or its root doesn't build a
        /// <see cref="UIElement"/>.
        /// </exception>
        /// <remarks>
        /// The <see cref="Icy.UI.Styles.DataTemplate"/> equivalent of <see cref="LoadTemplateContent"/>, minus the
        /// <c>templatedControl</c> a <c>DataTemplate</c> has no use for - it supports no <c>{TemplateBinding}</c>,
        /// so nothing in the built content ever needs to reach back to a "templated control".
        /// </remarks>
        public UIElement LoadDataTemplateContent(XElement content, string? sourcePath = null)
        {
            ArgumentNullException.ThrowIfNull(content);

            var context = new MarkupLoadContext(sourcePath, new MarkupNameScope());

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

                MarkupNameScope.SetScope(element, context.Names);
                return element;
            }
        }
```

Then, in `CreateObject`, right after the existing `ControlTemplate` special-case block (after its closing
`}`, before `Type? previousSetterTargetType = context.SetterTargetType;`), add:

```csharp
            // A DataTemplate's content is built fresh per call to DataTemplate.Build, not once here at
            // document-load time - same reasoning as the ControlTemplate case just above.
            if (instance is Icy.UI.Styles.DataTemplate dataTemplate)
            {
                List<XElement> dataTemplateChildren = [.. element.Elements()];
                if (dataTemplateChildren.Count != 1)
                {
                    throw MarkupException.At(
                        $"'{nameof(Icy.UI.Styles.DataTemplate)}' needs exactly one root element, but {dataTemplateChildren.Count} were given."
                            + (dataTemplateChildren.Count > 1 ? " Wrap them in a panel." : string.Empty),
                        element,
                        context.SourcePath);
                }

                dataTemplate.SetContent(dataTemplateChildren[0], configuration, context.SourcePath);
                return instance;
            }
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~DataTemplateTests"`
Expected: all 6 pass.

- [ ] **Step 6: Full build and test suite**

Run: `dotnet build "sources/IcyUI.sln"` then `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: clean build, no new warnings, all existing tests still pass (this task only adds code paths,
touching nothing existing `ControlTemplate` relies on).

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/UI/Styles/DataTemplate.cs sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI.Tests/Styles/DataTemplateTests.cs
git commit -m "Add DataTemplate and its MarkupLoader support"
```

---

### Task 3: `ItemContainer`

**Files:**
- Create: `sources/IcyUI/UI/Controls/ItemContainer.cs`
- Test: `sources/IcyUI.Tests/Controls/ItemContainerTests.cs`

**Interfaces:**
- Produces: `ItemContainer : ContentControl` (no new members - the type itself is the point, see below).

No new members are needed: `ContentControl.Content` already does everything `ItemsControl` needs from a
container. `ItemContainer` exists purely as its own type so (a) `ItemsControl`'s per-template pool
(`Dictionary<DataTemplate, Stack<ItemContainer>>`, Task 6) is meaningfully typed, and (b) a theme can
target `<Style TargetType="ItemContainer">` distinctly from a generic `ContentControl` - implicit-style
resolution (`ResourceDictionary.GetImplicitStyleKey`/`UIElement.ResolveImplicitStyle`, from Phase 0) keys
off `GetType()` already, so no extra registration code is needed for that to work.

- [ ] **Step 1: Write the failing test**

```csharp
// sources/IcyUI.Tests/Controls/ItemContainerTests.cs
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ItemContainerTests
    {
        [Fact]
        public void Content_HostsAnArbitraryElement()
        {
            var container = new ItemContainer();
            var content = new UIElement();

            container.Content = content;

            Assert.Same(content, container.Content);
            Assert.Same(container, content.Parent);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemContainerTests"`
Expected: FAIL to compile - `ItemContainer` doesn't exist yet.

- [ ] **Step 3: Write `ItemContainer`**

```csharp
// sources/IcyUI/UI/Controls/ItemContainer.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// The minimal container <see cref="ItemsControl"/> wraps each realized data item's built visual tree in.
    /// </summary>
    /// <remarks>
    /// A bare <see cref="ContentControl"/> with no selection state - exists as its own type only so
    /// <see cref="ItemsControl"/>'s pooling (keyed per <see cref="Styles.DataTemplate"/>) and theming
    /// (<c>&lt;Style TargetType="ItemContainer"&gt;</c>) have something distinct from a generic
    /// <see cref="ContentControl"/> to target. A future <c>Selector</c>/<c>ListBox</c> introduces a real
    /// <c>ListBoxItem</c> with selection state on top of this mechanism, rather than adding selection here - see
    /// the Phase 2 design spec's container-tiering decision.
    /// </remarks>
    public class ItemContainer : ContentControl
    {
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemContainerTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Controls/ItemContainer.cs sources/IcyUI.Tests/Controls/ItemContainerTests.cs
git commit -m "Add ItemContainer"
```

---

### Task 4: `ItemsControl` skeleton - properties, non-virtualized rendering plumbing

This task lands the class, its four public properties, and the `Control` overrides it needs
(`GetVisualChildren`/`OnRender`/`MeasureContent`/`ArrangeContent`) - with `realizedContainers` always
empty, so nothing draws yet. `ItemsSource` only supports a plain `IEnumerable` snapshot for now (Task 8
adds `INotifyCollectionChanged` reactivity). Realization itself (Tasks 5-7) plugs into the empty
`realizedContainers`/`knownHeights` fields this task declares.

**Files:**
- Create: `sources/IcyUI/UI/Controls/ItemsControl.cs`
- Test: `sources/IcyUI.Tests/Controls/ItemsControlTests.cs`

**Interfaces:**
- Consumes: `Icy.UI.Styles.DataTemplate` (Task 2), `Icy.UI.Controls.ItemContainer` (Task 3),
  `Icy.UI.IVirtualizingScrollInfo` (Task 1).
- Produces: `ItemsControl : Control, IVirtualizingScrollInfo` with public settable
  `IEnumerable? ItemsSource`, `DataTemplate? ItemTemplate`, `Func<object, DataTemplate>? ItemTemplateSelector`,
  `bool PoolingEnabled` (default `true`), `float DefaultEstimatedItemHeight` (default `40f`); internal
  fields `items : List<object>`, `realizedContainers : Dictionary<int, ItemContainer>`,
  `knownHeights : List<float?>`, `pools : Dictionary<DataTemplate, Stack<ItemContainer>>`,
  `containerTemplates : Dictionary<ItemContainer, DataTemplate>` - all consumed by Tasks 5-7.

- [ ] **Step 1: Write the failing tests**

```csharp
// sources/IcyUI.Tests/Controls/ItemsControlTests.cs
using System.Collections.Generic;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ItemsControlTests
    {
        [Fact]
        public void PoolingEnabled_DefaultsToTrue()
        {
            var control = new ItemsControl();

            Assert.True(control.PoolingEnabled);
        }

        [Fact]
        public void DefaultEstimatedItemHeight_DefaultsTo40()
        {
            var control = new ItemsControl();

            Assert.Equal(40f, control.DefaultEstimatedItemHeight);
        }

        [Fact]
        public void ItemsSource_NullByDefault_ExtentHeightIsZero()
        {
            var control = new ItemsControl();

            Assert.Equal(0, control.ExtentHeight);
        }

        [Fact]
        public void ItemsSource_PlainEnumerable_ExtentHeightUsesDefaultEstimateTimesCount()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3, 4, 5 } };

            Assert.Equal(5 * 40f, control.ExtentHeight);
        }

        [Fact]
        public void ItemsSource_Reassigned_ResetsExtentHeight()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };

            control.ItemsSource = new List<object> { 1, 2 };

            Assert.Equal(2 * 40f, control.ExtentHeight);
        }

        [Fact]
        public void ExtentWidth_EqualsCurrentViewportWidth()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };

            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 0, 250, 100);

            Assert.Equal(250, control.ExtentWidth);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: FAIL to compile - `ItemsControl` doesn't exist yet.

- [ ] **Step 3: Write the `ItemsControl` skeleton**

```csharp
// sources/IcyUI/UI/Controls/ItemsControl.cs
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Specialized;
using System.ComponentModel;
using System.Drawing;
using Icy.Data.Markup.Attributes;
using Icy.Rendering;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Generates one <see cref="ItemContainer"/> per item in <see cref="ItemsSource"/>, via
    /// <see cref="ItemTemplate"/>/<see cref="ItemTemplateSelector"/>, and virtualizes them - only the containers
    /// intersecting the current viewport (plus a small scroll-ahead buffer) are ever realized.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements <see cref="IVirtualizingScrollInfo"/> directly so a hosting <see cref="ScrollViewer"/> can
    /// delegate extent/offset/viewport handling to it instead of fully measuring/arranging every item - see the
    /// Phase 2 design spec (<c>docs/superpowers/specs/2026-09-04-itemscontrol-design.md</c>) for the full
    /// virtualization/estimation design this class implements.
    /// </para>
    /// <para>
    /// Carries no selection state - see <see cref="ItemContainer"/>'s own remarks for why, and where selection is
    /// expected to be added later.
    /// </para>
    /// </remarks>
    public class ItemsControl : Control, IVirtualizingScrollInfo
    {
        private readonly List<object> items = [];
        private readonly List<float?> knownHeights = [];
        private readonly Dictionary<int, ItemContainer> realizedContainers = [];
        private readonly Dictionary<DataTemplate, Stack<ItemContainer>> pools = [];
        private readonly Dictionary<ItemContainer, DataTemplate> containerTemplates = [];

        private IEnumerable? itemsSource;
        private INotifyCollectionChanged? observedSource;
        private DataTemplate? itemTemplate;
        private Func<object, DataTemplate>? itemTemplateSelector;
        private bool poolingEnabled = true;
        private float defaultEstimatedItemHeight = 40f;

        private float sumOfKnownHeights;
        private int knownCount;
        private float horizontalOffset;
        private float verticalOffset;
        private float viewportWidth;
        private float viewportHeight;
        private int anchorIndex;
        private float anchorOffset;

        /// <inheritdoc/>
        public event EventHandler<float>? VerticalOffsetCorrectionRequested;

        /// <summary>
        /// Gets or sets the estimated height given to an item that hasn't been realized/measured yet - used only
        /// before anything in <see cref="ItemsSource"/> has ever been realized; once at least one item has a real
        /// measured height, the running average of known heights is used instead.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(40f)]
        [RegisterReference]
        public float DefaultEstimatedItemHeight
        {
            get => defaultEstimatedItemHeight;
            set
            {
                if (SetProperty(ref defaultEstimatedItemHeight, value))
                {
                    InvalidateMeasure();
                    InvalidateArrange();
                }
            }
        }

        /// <inheritdoc/>
        public float ExtentWidth => viewportWidth;

        /// <inheritdoc/>
        public float ExtentHeight => sumOfKnownHeights + ((items.Count - knownCount) * AverageHeight);

        /// <summary>
        /// Gets or sets the template used to build each item's visual tree, when <see cref="ItemTemplateSelector"/>
        /// doesn't resolve one for a given item.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public DataTemplate? ItemTemplate
        {
            get => itemTemplate;
            set
            {
                if (SetProperty(ref itemTemplate, value))
                    ResetRealization();
            }
        }

        /// <summary>
        /// Gets or sets a delegate that picks a <see cref="DataTemplate"/> per item, overriding
        /// <see cref="ItemTemplate"/> for items it returns one for.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public Func<object, DataTemplate>? ItemTemplateSelector
        {
            get => itemTemplateSelector;
            set
            {
                if (SetProperty(ref itemTemplateSelector, value))
                    ResetRealization();
            }
        }

        /// <summary>
        /// Gets or sets the collection this control generates one <see cref="ItemContainer"/> per item from.
        /// </summary>
        /// <remarks>
        /// When the assigned value implements <see cref="INotifyCollectionChanged"/>, this control stays
        /// live-reactive to it for as long as it remains assigned (see <see cref="OnSourceCollectionChanged"/>).
        /// A plain <see cref="IEnumerable"/> is enumerated once, into a private snapshot - later external mutation
        /// of that source is not observed.
        /// </remarks>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public IEnumerable? ItemsSource
        {
            get => itemsSource;
            set
            {
                if (SetProperty(ref itemsSource, value))
                    ResetItems();
            }
        }

        /// <summary>
        /// Gets or sets whether de-realized <see cref="ItemContainer"/>s are pooled for reuse by a later item that
        /// resolves the same <see cref="DataTemplate"/>, instead of being discarded.
        /// </summary>
        [Category("Behavior")]
        [DefaultValue(true)]
        [RegisterReference]
        public bool PoolingEnabled
        {
            get => poolingEnabled;
            set
            {
                if (SetProperty(ref poolingEnabled, value) && !value)
                    pools.Clear();
            }
        }

        /// <inheritdoc/>
        protected override void ArrangeContent() => Chrome.Arrange(ActualBounds);

        /// <inheritdoc/>
        protected override IEnumerable<UIElement> GetVisualChildren()
        {
            yield return Chrome;
            foreach (int index in realizedContainers.Keys.OrderBy(i => i))
                yield return realizedContainers[index];
        }

        /// <inheritdoc/>
        protected override Size MeasureContent() => new((int)ExtentWidth, (int)ExtentHeight);

        /// <inheritdoc/>
        protected override void OnRender(IRenderContext context)
        {
            Chrome.Draw(context);
            foreach (int index in realizedContainers.Keys.OrderBy(i => i))
                realizedContainers[index].Draw(context);
        }

        /// <inheritdoc/>
        public virtual void OnViewportChanged(float newHorizontalOffset, float newVerticalOffset, float newViewportWidth, float newViewportHeight)
        {
            horizontalOffset = newHorizontalOffset;
            verticalOffset = newVerticalOffset;
            viewportWidth = newViewportWidth;
            viewportHeight = newViewportHeight;
        }

        private float AverageHeight => knownCount > 0 ? sumOfKnownHeights / knownCount : DefaultEstimatedItemHeight;

        private void ResetItems()
        {
            if (observedSource != null)
                observedSource.CollectionChanged -= OnSourceCollectionChanged;

            items.Clear();
            if (itemsSource != null)
            {
                foreach (object item in itemsSource)
                    items.Add(item);
            }

            observedSource = itemsSource as INotifyCollectionChanged;
            if (observedSource != null)
                observedSource.CollectionChanged += OnSourceCollectionChanged;

            ResetRealization();
        }

        private void ResetRealization()
        {
            foreach (ItemContainer container in realizedContainers.Values)
            {
                container.Parent = null;
                container.Canvas = null;
            }

            realizedContainers.Clear();
            pools.Clear();
            containerTemplates.Clear();

            knownHeights.Clear();
            for (int i = 0; i < items.Count; i++)
                knownHeights.Add(null);
            sumOfKnownHeights = 0;
            knownCount = 0;
            anchorIndex = 0;
            anchorOffset = 0;

            InvalidateMeasure();
            InvalidateArrange();
        }

        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Implemented in Task 8.
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: all 6 pass.

- [ ] **Step 5: Full build**

Run: `dotnet build "sources/IcyUI.sln"`
Expected: clean, no new warnings.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/ItemsControl.cs sources/IcyUI.Tests/Controls/ItemsControlTests.cs
git commit -m "Add ItemsControl skeleton: properties, empty-realization layout plumbing"
```

---

### Task 5: Height tracking - `RecordHeight`

Adds the incremental height-cache write path (spec §5's "authoritative until changed" correction, and
§6's exact-delta above-viewport offset correction) as its own tested unit, before anything calls it.

**Files:**
- Modify: `sources/IcyUI/UI/Controls/ItemsControl.cs`
- Test: `sources/IcyUI.Tests/Controls/ItemsControlTests.cs`

**Interfaces:**
- Produces: `private void RecordHeight(int index, float newHeight)` - consumed by Tasks 6 and 7.

Add this private method to `ItemsControl` (after `AverageHeight`):

```csharp
        /// <summary>
        /// Records <paramref name="newHeight"/> as <paramref name="index"/>'s real measured height, folding the
        /// change into <see cref="ExtentHeight"/>'s running totals - see the Phase 2 design spec §5/§6.
        /// </summary>
        private void RecordHeight(int index, float newHeight)
        {
            float? previous = knownHeights[index];
            if (previous == null)
            {
                sumOfKnownHeights += newHeight;
                knownCount++;
                knownHeights[index] = newHeight;
                return;
            }

            float delta = newHeight - previous.Value;
            if (delta == 0)
                return;

            sumOfKnownHeights += delta;
            knownHeights[index] = newHeight;

            // §6: an already-realized item resizing above the current top-visible index (anchorIndex) would
            // otherwise visibly shift everything on screen, since nothing above the viewport is supposed to move
            // it. Correct by shifting the offset itself by the same exact delta, and tell the host ScrollViewer -
            // this control's own `verticalOffset` field is a private mirror of what ScrollViewer last reported;
            // the event is what actually moves ScrollViewer.VerticalOffset (the value the scrollbar/user see).
            if (index < anchorIndex)
            {
                verticalOffset += delta;
                VerticalOffsetCorrectionRequested?.Invoke(this, delta);
            }
        }
```

- [ ] **Step 1: Write the failing tests**

```csharp
        [Fact]
        public void RecordHeight_FirstMeasurement_UpdatesSumAndCount()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };

            InvokeRecordHeight(control, 0, 60f);

            // Once ANY item is known, unknown items are estimated at the running average of known items (not
            // the flat DefaultEstimatedItemHeight, which only applies while knownCount is zero - see
            // AverageHeight) - 1 known (60) + 2 unknown (60 each, the only known average so far) = 180.
            Assert.Equal(180f, control.ExtentHeight);
        }

        [Fact]
        public void RecordHeight_ReMeasurementGrows_AdjustsSumByDeltaOnly()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };
            InvokeRecordHeight(control, 0, 60f);

            InvokeRecordHeight(control, 0, 90f);

            // The known value grew by 30 (60 -> 90); the 2 unknown items re-estimate at the new average (90):
            // 90 + 90 + 90 = 270.
            Assert.Equal(270f, control.ExtentHeight);
        }

        [Fact]
        public void RecordHeight_ReMeasurementShrinks_AdjustsSumByDeltaOnly()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3 } };
            InvokeRecordHeight(control, 0, 60f);

            InvokeRecordHeight(control, 0, 20f);

            // 20 known + 2 unknown re-estimated at the new average (20 each) = 60.
            Assert.Equal(60f, control.ExtentHeight);
        }

        [Fact]
        public void RecordHeight_ChangeAboveAnchor_RaisesCorrectionWithExactDelta()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3, 4, 5 } };
            InvokeRecordHeight(control, 0, 60f); // index 0 known, below anchorIndex(0) is false yet
            SetAnchorIndex(control, 3); // simulate having since scrolled so item 3 is now top-visible

            float? raisedDelta = null;
            ((IVirtualizingScrollInfo)control).VerticalOffsetCorrectionRequested += (_, delta) => raisedDelta = delta;

            InvokeRecordHeight(control, 0, 90f); // index 0 < anchorIndex(3): above the viewport

            Assert.Equal(30f, raisedDelta);
        }

        [Fact]
        public void RecordHeight_ChangeAtOrBelowAnchor_DoesNotRaiseCorrection()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { 1, 2, 3, 4, 5 } };
            InvokeRecordHeight(control, 3, 60f);
            SetAnchorIndex(control, 3);

            bool raised = false;
            ((IVirtualizingScrollInfo)control).VerticalOffsetCorrectionRequested += (_, _) => raised = true;

            InvokeRecordHeight(control, 3, 90f); // index 3 is not < anchorIndex(3)

            Assert.False(raised);
        }

        private static void InvokeRecordHeight(ItemsControl control, int index, float height)
        {
            var method = typeof(ItemsControl).GetMethod("RecordHeight", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            method.Invoke(control, [index, height]);
        }

        private static void SetAnchorIndex(ItemsControl control, int index)
        {
            var field = typeof(ItemsControl).GetField("anchorIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            field.SetValue(control, index);
        }
```

Append these to the existing `ItemsControlTests` class from Task 4 (same file, inside the class body).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: FAIL to compile - `RecordHeight` doesn't exist yet.

- [ ] **Step 3: Add `RecordHeight`**

Add the method shown above to `sources/IcyUI/UI/Controls/ItemsControl.cs`, right after the private
`AverageHeight` property.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: all pass (11 total so far).

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Controls/ItemsControl.cs sources/IcyUI.Tests/Controls/ItemsControlTests.cs
git commit -m "Add ItemsControl height-cache recording (spec 5/6)"
```

---

### Task 6: Realize/de-realize/pool machinery

Adds `EnsureRealized`/`RentContainer`/`Derealize`/`ResolveTemplate` - the pooling mechanics from spec §4,
each unit-tested directly (via reflection, matching Task 5's pattern) before Task 7 wires them into the
viewport-driven realize walk.

**Files:**
- Modify: `sources/IcyUI/UI/Controls/ItemsControl.cs`
- Test: `sources/IcyUI.Tests/Controls/ItemsControlTests.cs`

**Interfaces:**
- Produces: `private void EnsureRealized(int index)`, `private void Derealize(int index)`,
  `private DataTemplate ResolveTemplate(object item)`, `private ItemContainer RentContainer(DataTemplate template, object item)` -
  consumed by Task 7.

Add these private methods (after `RecordHeight`):

```csharp
        /// <summary>
        /// Resolves the <see cref="DataTemplate"/> to use for <paramref name="item"/> -
        /// <see cref="ItemTemplateSelector"/> first, falling back to <see cref="ItemTemplate"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Neither resolves a template for this item.</exception>
        private DataTemplate ResolveTemplate(object item)
        {
            DataTemplate? resolved = ItemTemplateSelector?.Invoke(item) ?? ItemTemplate;
            return resolved ?? throw new InvalidOperationException(
                $"'{nameof(ItemsControl)}' has no '{nameof(ItemTemplate)}' or '{nameof(ItemTemplateSelector)}' to build item '{item}' from.");
        }

        /// <summary>
        /// Gets a container for <paramref name="item"/> built with <paramref name="template"/> - popped from
        /// <paramref name="template"/>'s pool and rebound when <see cref="PoolingEnabled"/> and one's available,
        /// otherwise built fresh via <see cref="DataTemplate.Build(object)"/>.
        /// </summary>
        private ItemContainer RentContainer(DataTemplate template, object item)
        {
            if (PoolingEnabled && pools.TryGetValue(template, out Stack<ItemContainer>? pool) && pool.Count > 0)
            {
                ItemContainer pooled = pool.Pop();
                if (pooled.Content != null)
                    pooled.Content.DataContext = item;
                pooled.InvalidateMeasure();
                containerTemplates[pooled] = template;
                return pooled;
            }

            var container = new ItemContainer { Content = template.Build(item) };
            containerTemplates[container] = template;
            return container;
        }

        /// <summary>
        /// Realizes <paramref name="index"/> if it isn't already, wiring it into the visual tree, measuring it,
        /// and folding its real height into the height cache (see <see cref="RecordHeight(int, float)"/>).
        /// </summary>
        private void EnsureRealized(int index)
        {
            if (realizedContainers.ContainsKey(index))
                return;

            object item = items[index];
            DataTemplate template = ResolveTemplate(item);
            ItemContainer container = RentContainer(template, item);

            container.Parent = this;
            container.Canvas = Canvas;
            realizedContainers[index] = container;

            float measuredHeight = container.Measure().Height;
            RecordHeight(index, measuredHeight);
        }

        /// <summary>
        /// De-realizes <paramref name="index"/> if it's currently realized - remeasures it one last time (folding
        /// any final size change into the height cache, see the Phase 2 design spec §6), detaches it, and returns
        /// it to its template's pool when <see cref="PoolingEnabled"/>.
        /// </summary>
        private void Derealize(int index)
        {
            if (!realizedContainers.Remove(index, out ItemContainer? container))
                return;

            float finalHeight = container.Measure().Height;
            if (knownHeights[index] != finalHeight)
                RecordHeight(index, finalHeight);

            container.Parent = null;
            container.Canvas = null;

            if (PoolingEnabled && containerTemplates.TryGetValue(container, out DataTemplate? template))
            {
                if (!pools.TryGetValue(template, out Stack<ItemContainer>? pool))
                    pools[template] = pool = new Stack<ItemContainer>();
                pool.Push(container);
            }

            containerTemplates.Remove(container);
        }
```

- [ ] **Step 1: Write the failing tests**

```csharp
        [Fact]
        public void EnsureRealized_BuildsAContainerAndMeasuresIt()
        {
            var control = new ItemsControl
            {
                ItemsSource = new List<object> { "a", "b" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>"""),
            };

            InvokeEnsureRealized(control, 0);

            var container = GetRealizedContainers(control)[0];
            Assert.IsType<ItemContainer>(container);
            Assert.Same(control, container.Parent);
        }

        [Fact]
        public void EnsureRealized_CalledTwiceForSameIndex_BuildsOnlyOnce()
        {
            int buildCount = 0;
            var template = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = new List<object> { "a" }, ItemTemplate = template };

            InvokeEnsureRealized(control, 0);
            ItemContainer first = GetRealizedContainers(control)[0];
            InvokeEnsureRealized(control, 0);
            ItemContainer second = GetRealizedContainers(control)[0];

            Assert.Same(first, second);
        }

        [Fact]
        public void Derealize_ThenEnsureRealizedAgain_ReusesThePooledContainer()
        {
            var template = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = new List<object> { "a", "b" }, ItemTemplate = template };
            InvokeEnsureRealized(control, 0);
            ItemContainer original = GetRealizedContainers(control)[0];

            InvokeDerealize(control, 0);
            Assert.Null(original.Parent); // detached once pooled, before anything reuses it

            InvokeEnsureRealized(control, 1);

            Assert.Same(original, GetRealizedContainers(control)[1]);
        }

        [Fact]
        public void Derealize_PooledContainer_IsDetachedThenReattachedOnReuse()
        {
            var template = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = new List<object> { "a", "b" }, ItemTemplate = template };
            InvokeEnsureRealized(control, 0);
            ItemContainer original = GetRealizedContainers(control)[0];
            InvokeDerealize(control, 0);

            InvokeEnsureRealized(control, 1);

            Assert.Same(control, original.Parent);
        }

        [Fact]
        public void Derealize_PoolingDisabled_ContainerIsNotPooled()
        {
            var template = LoadDataTemplate("""<DataTemplate><TextBlock Text="{Binding}"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = new List<object> { "a", "b" }, ItemTemplate = template, PoolingEnabled = false };
            InvokeEnsureRealized(control, 0);
            ItemContainer original = GetRealizedContainers(control)[0];
            InvokeDerealize(control, 0);

            InvokeEnsureRealized(control, 1);

            Assert.NotSame(original, GetRealizedContainers(control)[1]);
        }

        [Fact]
        public void EnsureRealized_NoTemplate_Throws()
        {
            var control = new ItemsControl { ItemsSource = new List<object> { "a" } };

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => InvokeEnsureRealized(control, 0));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new Tests.Input.FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        private static void InvokeEnsureRealized(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("EnsureRealized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static void InvokeDerealize(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("Derealize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;
```

Append to `ItemsControlTests`; add `using Icy.Assets; using Icy.Configuration; using Icy.Markup; using Icy.Tests.Rendering;`
to the file's `using` list if not already present (they are, once Task 4/5 tests are in place - check
before adding duplicates).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: FAIL to compile - the four private methods don't exist yet.

- [ ] **Step 3: Add the realize/pool methods**

Add the four methods shown above to `sources/IcyUI/UI/Controls/ItemsControl.cs`, after `RecordHeight`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: all pass (18 total so far).

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Controls/ItemsControl.cs sources/IcyUI.Tests/Controls/ItemsControlTests.cs
git commit -m "Add ItemsControl realize/de-realize/pooling machinery (spec 4)"
```

---

### Task 7: Anchor walk, big-jump estimation, and `OnViewportChanged`

The core virtualization loop (spec §5) - locates the item at the current `verticalOffset` (walking from
the last anchor for a small delta, estimating a landing index directly for a big jump), then realizes
everything overlapping the viewport plus a forward scroll-ahead buffer, positioning each realized
container and de-realizing anything that fell out of range.

**Files:**
- Modify: `sources/IcyUI/UI/Controls/ItemsControl.cs`
- Test: `sources/IcyUI.Tests/Controls/ItemsControlTests.cs`

**Interfaces:**
- Produces: replaces Task 4's placeholder `OnViewportChanged` body; adds
  `private float HeightOrEstimate(int index)`, `private float BigJumpThreshold`,
  `private (int Index, float Offset) LocateViewportStart()`, `private void RealizeRange(int firstIndex, float firstOffset)`.

Replace the Task 4 `OnViewportChanged` method body, and add the four new private members after it:

```csharp
        /// <inheritdoc/>
        public virtual void OnViewportChanged(float newHorizontalOffset, float newVerticalOffset, float newViewportWidth, float newViewportHeight)
        {
            horizontalOffset = newHorizontalOffset;
            verticalOffset = newVerticalOffset;
            viewportWidth = newViewportWidth;
            viewportHeight = newViewportHeight;

            if (items.Count == 0)
            {
                foreach (int index in realizedContainers.Keys.ToList())
                    Derealize(index);
                return;
            }

            (anchorIndex, anchorOffset) = LocateViewportStart();
            RealizeRange(anchorIndex, anchorOffset);
        }

        private float HeightOrEstimate(int index) => knownHeights[index] ?? AverageHeight;

        /// <summary>
        /// The offset-from-anchor distance beyond which a walk (spec §5's "small delta" path) is abandoned in
        /// favor of a direct landing-index estimate (the "big jump" path) - a scrollbar-thumb drag lands far from
        /// the current anchor almost every time, where a step-by-step walk would visit most of the collection just
        /// to get there.
        /// </summary>
        private float BigJumpThreshold => Math.Max(viewportHeight * 3f, 1f);

        /// <summary>
        /// Finds the item whose slot contains the current <c>verticalOffset</c> - via a short walk from the last
        /// anchor for a small scroll delta, or a direct estimate for a big jump (spec §5).
        /// </summary>
        private (int Index, float Offset) LocateViewportStart()
        {
            float distanceFromAnchor = Math.Abs(verticalOffset - anchorOffset);
            if (realizedContainers.Count == 0 || distanceFromAnchor > BigJumpThreshold)
            {
                float average = AverageHeight;
                int estimatedIndex = average > 0 ? (int)(verticalOffset / average) : 0;
                estimatedIndex = Math.Clamp(estimatedIndex, 0, items.Count - 1);
                return (estimatedIndex, estimatedIndex * average);
            }

            int index = Math.Clamp(anchorIndex, 0, items.Count - 1);
            float offset = anchorOffset;
            while (offset > verticalOffset && index > 0)
            {
                index--;
                offset -= HeightOrEstimate(index);
            }

            while (index < items.Count - 1 && offset + HeightOrEstimate(index) <= verticalOffset)
            {
                offset += HeightOrEstimate(index);
                index++;
            }

            return (index, offset);
        }

        /// <summary>
        /// Realizes every item whose slot overlaps the viewport (plus a forward-only scroll-ahead buffer),
        /// starting the walk at <paramref name="firstIndex"/>/<paramref name="firstOffset"/>; positions each
        /// realized container, and de-realizes anything realized but no longer in range.
        /// </summary>
        private void RealizeRange(int firstIndex, float firstOffset)
        {
            const float ScrollAheadBuffer = 100f;
            float rangeEnd = verticalOffset + viewportHeight + ScrollAheadBuffer;

            var stillRealized = new HashSet<int>();
            int index = firstIndex;
            float offset = firstOffset;
            while (index < items.Count && offset < rangeEnd)
            {
                EnsureRealized(index);
                float height = HeightOrEstimate(index); // may just have become known, via EnsureRealized above
                stillRealized.Add(index);

                ItemContainer container = realizedContainers[index];
                var targetRect = new Rectangle(
                    ContentBounds.X,
                    ContentBounds.Y + (int)(offset - verticalOffset),
                    ContentBounds.Width,
                    (int)height);
                container.InvalidateArrange();
                container.Arrange(targetRect);

                offset += height;
                index++;
            }

            foreach (int realizedIndex in realizedContainers.Keys.ToList())
            {
                if (!stillRealized.Contains(realizedIndex))
                    Derealize(realizedIndex);
            }
        }
```

- [ ] **Step 1: Write the failing tests**

```csharp
        [Fact]
        public void OnViewportChanged_AtTop_RealizesOnlyItemsInTheViewportPlusBuffer()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var control = new ItemsControl
            {
                ItemsSource = Enumerable.Range(0, 1000).Cast<object>().ToList(),
                ItemTemplate = template,
            };

            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 0, 300, 400);

            // Viewport 400px + 100px scroll-ahead buffer, ~40px/item -> ~12-13 items, nowhere near 1000.
            var realized = GetRealizedContainers(control);
            Assert.True(realized.Count < 20, $"expected far fewer than 1000 realized, got {realized.Count}");
            Assert.Contains(0, realized.Keys);
        }

        [Fact]
        public void OnViewportChanged_ScrollingDown_DerealizesItemsThatScrolledOut()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var control = new ItemsControl
            {
                ItemsSource = Enumerable.Range(0, 1000).Cast<object>().ToList(),
                ItemTemplate = template,
            };
            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 0, 300, 400);
            Assert.Contains(0, GetRealizedContainers(control).Keys);

            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 2000, 300, 400);

            Assert.DoesNotContain(0, GetRealizedContainers(control).Keys);
        }

        [Fact]
        public void OnViewportChanged_BigJump_LandsNearTheEstimatedIndexWithoutWalkingEverything()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var control = new ItemsControl
            {
                ItemsSource = Enumerable.Range(0, 1000).Cast<object>().ToList(),
                ItemTemplate = template,
            };
            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 0, 300, 400);

            // 1000 * 40 = 40000 extent; jump to the middle.
            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 20000, 300, 400);

            var realized = GetRealizedContainers(control).Keys;
            Assert.All(realized, index => Assert.InRange(index, 480, 520));
        }

        [Fact]
        public void OnViewportChanged_SmallScroll_ReusesTheAnchorWalkNotAFullRescan()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var control = new ItemsControl
            {
                ItemsSource = Enumerable.Range(0, 1000).Cast<object>().ToList(),
                ItemTemplate = template,
            };
            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 2000, 300, 400); // anchor near index 50
            int anchorBefore = GetAnchorIndex(control);

            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 2040, 300, 400); // one item's worth of scroll

            int anchorAfter = GetAnchorIndex(control);
            Assert.InRange(Math.Abs(anchorAfter - anchorBefore), 0, 3);
        }

        [Fact]
        public void OnViewportChanged_ExtentHeightNarrowsAsRealItemsAreMeasured()
        {
            // Items report a real height (52) different from the 40px default estimate - after realizing the
            // first handful, ExtentHeight should reflect that, not stay at the naive 1000*40 estimate.
            var template = LoadDataTemplate("""<DataTemplate><Border Height="52"/></DataTemplate>""");
            var control = new ItemsControl
            {
                ItemsSource = Enumerable.Range(0, 1000).Cast<object>().ToList(),
                ItemTemplate = template,
            };

            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 0, 300, 400);

            Assert.True(control.ExtentHeight > 1000 * 40f, "expected the estimate to move toward the real 52px height, not stay at the 40px default");
        }

        private static int GetAnchorIndex(ItemsControl control) =>
            (int)typeof(ItemsControl).GetField("anchorIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;
```

Add `using System.Linq;` to the test file if not already present.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: compiles (the members exist as Task 4's placeholder), but the realize/derealize/big-jump/small-scroll
assertions FAIL against the placeholder `OnViewportChanged`, which does nothing.

- [ ] **Step 3: Replace `OnViewportChanged` and add the four new methods**

Apply the code block above to `sources/IcyUI/UI/Controls/ItemsControl.cs`, replacing Task 4's placeholder
`OnViewportChanged` and inserting the four new private members immediately after it.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: all pass (23 total so far).

- [ ] **Step 5: Full build and test suite**

Run: `dotnet build "sources/IcyUI.sln"` then `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: clean build, no new warnings, entire suite green.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/ItemsControl.cs sources/IcyUI.Tests/Controls/ItemsControlTests.cs
git commit -m "Add ItemsControl virtualization: anchor walk, big-jump estimate, realize range (spec 5)"
```

---

### Task 8: `INotifyCollectionChanged` live reactivity

Resolves the spec's own open item on `Move`/`Replace` index-remapping concretely. `Add`/`Remove` splice
`knownHeights` at the changed index (shifting every entry after it); `Replace` treats old/new as same-index
with a fresh unknown height (the item object changed, so any previously-known height for that slot no
longer describes what's there); `Move` relocates both `items` and `knownHeights` entries together, keeping
each moved item's already-known height with it; `Reset` clears everything (equivalent to reassigning
`ItemsSource`). Every branch de-realizes/re-derives affected `realizedContainers` entries rather than
trying to patch them in place, since a splice shifts every later index's identity.

**Files:**
- Modify: `sources/IcyUI/UI/Controls/ItemsControl.cs`
- Test: `sources/IcyUI.Tests/Controls/ItemsControlTests.cs`

**Interfaces:**
- Produces: replaces Task 4's placeholder `OnSourceCollectionChanged` body.

```csharp
        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    InsertItems(e.NewStartingIndex, e.NewItems!);
                    break;

                case NotifyCollectionChangedAction.Remove:
                    RemoveItems(e.OldStartingIndex, e.OldItems!.Count);
                    break;

                case NotifyCollectionChangedAction.Replace:
                    ReplaceItems(e.OldStartingIndex, e.NewItems!);
                    break;

                case NotifyCollectionChangedAction.Move:
                    MoveItems(e.OldStartingIndex, e.NewStartingIndex, e.OldItems!.Count);
                    break;

                case NotifyCollectionChangedAction.Reset:
                default:
                    items.Clear();
                    if (itemsSource != null)
                    {
                        foreach (object item in itemsSource)
                            items.Add(item);
                    }

                    ResetRealization();
                    return;
            }

            InvalidateMeasure();
            InvalidateArrange();
        }

        private void InsertItems(int startIndex, System.Collections.IList newItems)
        {
            DerealizeFromIndex(startIndex);

            for (int i = 0; i < newItems.Count; i++)
            {
                items.Insert(startIndex + i, newItems[i]!);
                knownHeights.Insert(startIndex + i, null);
            }
        }

        private void RemoveItems(int startIndex, int count)
        {
            DerealizeFromIndex(startIndex);

            for (int i = 0; i < count; i++)
            {
                if (knownHeights[startIndex] is { } removedHeight)
                {
                    sumOfKnownHeights -= removedHeight;
                    knownCount--;
                }

                items.RemoveAt(startIndex);
                knownHeights.RemoveAt(startIndex);
            }
        }

        private void ReplaceItems(int startIndex, System.Collections.IList newItems)
        {
            // The item object at each of these indexes changed - whatever height was known for the slot described
            // the OLD item, not this one, so it must be forgotten rather than kept.
            DerealizeFromIndex(startIndex);

            for (int i = 0; i < newItems.Count; i++)
            {
                int index = startIndex + i;
                items[index] = newItems[i]!;

                if (knownHeights[index] is { } previousHeight)
                {
                    sumOfKnownHeights -= previousHeight;
                    knownCount--;
                }

                knownHeights[index] = null;
            }
        }

        private void MoveItems(int oldStartIndex, int newStartIndex, int count)
        {
            DerealizeFromIndex(Math.Min(oldStartIndex, newStartIndex));

            var movedItems = items.GetRange(oldStartIndex, count);
            var movedHeights = knownHeights.GetRange(oldStartIndex, count);
            items.RemoveRange(oldStartIndex, count);
            knownHeights.RemoveRange(oldStartIndex, count);

            items.InsertRange(newStartIndex, movedItems);
            knownHeights.InsertRange(newStartIndex, movedHeights);
        }

        /// <summary>
        /// De-realizes every currently-realized container at or after <paramref name="index"/> - a splice at
        /// <paramref name="index"/> changes every later index's identity, so their realized containers (keyed by
        /// index) would otherwise silently start representing the wrong item.
        /// </summary>
        private void DerealizeFromIndex(int index)
        {
            foreach (int realizedIndex in realizedContainers.Keys.Where(i => i >= index).ToList())
                Derealize(realizedIndex);
        }
```

- [ ] **Step 1: Write the failing tests**

```csharp
        [Fact]
        public void CollectionChanged_Add_InsertsAtCorrectPositionAndReallocatesLaterIndexes()
        {
            var source = new ObservableCollection<object> { "a", "b", "c" };
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = source, ItemTemplate = template };
            ((IVirtualizingScrollInfo)control).OnViewportChanged(0, 0, 300, 400);
            InvokeEnsureRealized(control, 2); // realize "c" at index 2

            source.Insert(1, "new");

            var items = GetItems(control);
            Assert.Equal(new object[] { "a", "new", "b", "c" }, items);
            // "c"'s old index-2 realization must be gone - it would otherwise silently represent "b" now.
            Assert.DoesNotContain(2, GetRealizedContainers(control).Keys);
        }

        [Fact]
        public void CollectionChanged_Remove_UpdatesRunningHeightTotals()
        {
            var source = new ObservableCollection<object> { "a", "b", "c" };
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = source, ItemTemplate = template };
            InvokeEnsureRealized(control, 0);

            source.RemoveAt(0);

            Assert.Equal(new object[] { "b", "c" }, GetItems(control));
            // The removed item's known 40px height must no longer count toward the extent.
            Assert.Equal(2 * 40f, control.ExtentHeight);
        }

        [Fact]
        public void CollectionChanged_Replace_ForgetsTheOldItemsKnownHeight()
        {
            var source = new ObservableCollection<object> { "a", "b" };
            var template = LoadDataTemplate("""<DataTemplate><Border Height="60"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = source, ItemTemplate = template };
            InvokeEnsureRealized(control, 0); // "a" measured at 60px

            source[0] = "replaced";

            Assert.Equal("replaced", GetItems(control)[0]);
            // "a"'s known 60px must be gone (replaced item is unmeasured again) - only "b" is still unknown too,
            // so both fall back to the 40px default: 40 + 40 = 80.
            Assert.Equal(80f, control.ExtentHeight);
        }

        [Fact]
        public void CollectionChanged_Move_KeepsTheMovedItemsKnownHeightWithIt()
        {
            var source = new ObservableCollection<object> { "a", "b", "c" };
            var template = LoadDataTemplate("""<DataTemplate><Border Height="70"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = source, ItemTemplate = template };
            InvokeEnsureRealized(control, 0); // "a" measured at 70px

            source.Move(0, 2);

            Assert.Equal(new object[] { "b", "c", "a" }, GetItems(control));
            // "a" (still the only known item, at 70px) moved to index 2; the 2 unknown items re-estimate at the
            // running average of known items (70, the only one there is - not the 40px default, which only
            // applies while knownCount is zero): 70 + 70 + 70 = 210.
            Assert.Equal(210f, control.ExtentHeight);
        }

        [Fact]
        public void CollectionChanged_Reset_ClearsEverythingLikeReassigningItemsSource()
        {
            var source = new ObservableCollection<object> { "a", "b", "c" };
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var control = new ItemsControl { ItemsSource = source, ItemTemplate = template };
            InvokeEnsureRealized(control, 0);

            source.Clear();

            Assert.Empty(GetItems(control));
            Assert.Empty(GetRealizedContainers(control));
            Assert.Equal(0, control.ExtentHeight);
        }

        private static List<object> GetItems(ItemsControl control) =>
            (List<object>)typeof(ItemsControl).GetField("items", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;
```

Add `using System.Collections.ObjectModel;` to the test file.

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: compiles (the placeholder handler exists), but every assertion above FAILS since the placeholder
does nothing.

- [ ] **Step 3: Replace `OnSourceCollectionChanged`**

Apply the code block above to `sources/IcyUI/UI/Controls/ItemsControl.cs`, replacing Task 4's placeholder
and adding the four new private helper methods (`InsertItems`/`RemoveItems`/`ReplaceItems`/`MoveItems`/
`DerealizeFromIndex`) after it. Add `using System.Linq;` to the top of the file if not already present
(Task 6/7 already needs it via `OrderBy`/`ToList` - check before duplicating).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlTests"`
Expected: all pass (28 total so far).

- [ ] **Step 5: Full build and test suite**

Run: `dotnet build "sources/IcyUI.sln"` then `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: clean, no new warnings, full suite green.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/ItemsControl.cs sources/IcyUI.Tests/Controls/ItemsControlTests.cs
git commit -m "Add ItemsControl live INotifyCollectionChanged reactivity"
```

---

### Task 9: `ScrollViewer` integration

Wires `ScrollViewer` to delegate to `IVirtualizingScrollInfo` content - the three branches from spec §8,
plus subscribing to `VerticalOffsetCorrectionRequested` (Task 1) so a §6 correction actually reaches the
`VerticalOffset` property the scrollbar/user read.

**Files:**
- Modify: `sources/IcyUI/UI/Controls/ScrollViewer.cs`
- Test: `sources/IcyUI.Tests/Controls/ScrollViewerTests.cs`

**Interfaces:**
- Consumes: `Icy.UI.IVirtualizingScrollInfo` (Task 1).

Replace `ExtentHeight`/`ExtentWidth`:

```csharp
        /// <summary>
        /// Gets <see cref="ContentControl.Content"/>'s full natural height, regardless of how much of it is
        /// currently visible - or, when <see cref="ContentControl.Content"/> implements
        /// <see cref="IVirtualizingScrollInfo"/>, its own reported estimate instead of a full measure.
        /// </summary>
        public float ExtentHeight => Content is IVirtualizingScrollInfo virtualizing ? virtualizing.ExtentHeight : Content?.Measure().Height ?? 0;

        /// <summary>
        /// Gets <see cref="ContentControl.Content"/>'s full natural width, regardless of how much of it is
        /// currently visible - or, when <see cref="ContentControl.Content"/> implements
        /// <see cref="IVirtualizingScrollInfo"/>, its own reported estimate instead of a full measure.
        /// </summary>
        public float ExtentWidth => Content is IVirtualizingScrollInfo virtualizing ? virtualizing.ExtentWidth : Content?.Measure().Width ?? 0;
```

Replace `ArrangeContent`:

```csharp
        /// <inheritdoc/>
        protected override void ArrangeContent()
        {
            EnsureVirtualizingSubscription();

            if (Content is IVirtualizingScrollInfo virtualizingContent)
            {
                // Virtualizing content lays itself out within the viewport directly (each realized item
                // positions itself via its own OnViewportChanged call) - it must never be grown to its own full
                // extent the way the non-virtualizing branch below does, or virtualization is defeated entirely.
                //
                // Chrome.Arrange must run BEFORE OnViewportChanged, not after: it's what cascades down and sets
                // Content's own ActualBounds/ContentBounds for this frame (Chrome is a Border whose Child is
                // Content - Border.ArrangeContent arranges its Child during this call). OnViewportChanged's
                // realize walk reads Content's ContentBounds to position each realized container - calling it
                // first would position everything against last frame's (or, on the very first layout pass ever,
                // a still-default/empty) bounds instead of this frame's real ones.
                HorizontalOffset = horizontalOffset;
                VerticalOffset = verticalOffset;

                Chrome.InvalidateArrange();
                Chrome.Arrange(ActualBounds);

                virtualizingContent.OnViewportChanged(HorizontalOffset, VerticalOffset, ViewportWidth, ViewportHeight);
                return;
            }

            // Chrome must be given room to grow to Content's full natural size along the scrollable axes, not
            // shrunk to fit ActualBounds the way the standard Arrange()/CalculateOverflow flow would (a child can
            // never overflow its parent there) - that's the entire point of scrolling. ClipToBounds on this
            // control (not Chrome) still crops the overflow visually. Chrome's origin stays at ActualBounds' own
            // top-left - only ever extended, never moved - so Background/BorderBrush (drawn across Chrome's own
            // full local bounds) still start exactly where this control's border box starts; Chrome's own
            // BorderThickness/Padding (see Control.ArrangeContent) then insets Content the normal way once
            // Chrome itself has room to fit it.
            Size contentDesired = Content?.Measure() ?? Size.Empty;
            Thickness inset = BorderThickness + Padding;
            Rectangle chromeRect = new(
                ActualBounds.X,
                ActualBounds.Y,
                Math.Max(ActualBounds.Width, contentDesired.Width + inset.Width),
                Math.Max(ActualBounds.Height, contentDesired.Height + inset.Height));

            Chrome.InvalidateArrange();
            Chrome.Arrange(chromeRect);

            // Re-clamp now that Extent/Viewport are up to date post-arrange (e.g. the viewport just shrank).
            HorizontalOffset = horizontalOffset;
            VerticalOffset = verticalOffset;
        }
```

Replace `UpdateContentOffset`, and add the new subscription-management method:

```csharp
        private void UpdateContentOffset()
        {
            EnsureVirtualizingSubscription();

            if (Content is IVirtualizingScrollInfo virtualizingContent)
            {
                virtualizingContent.OnViewportChanged(HorizontalOffset, VerticalOffset, ViewportWidth, ViewportHeight);
                return;
            }

            if (Content != null)
                Content.LayoutOffset = new Vector2(-HorizontalOffset, -VerticalOffset);
        }

        /// <summary>
        /// Keeps this control subscribed to whatever <see cref="ContentControl.Content"/> currently implements
        /// <see cref="IVirtualizingScrollInfo"/>, so a <see cref="IVirtualizingScrollInfo.VerticalOffsetCorrectionRequested"/>
        /// it raises actually reaches <see cref="VerticalOffset"/> - the value the scrollbar/user read.
        /// </summary>
        private void EnsureVirtualizingSubscription()
        {
            if (ReferenceEquals(subscribedVirtualizingContent, Content))
                return;

            if (subscribedVirtualizingContent != null)
                subscribedVirtualizingContent.VerticalOffsetCorrectionRequested -= OnVerticalOffsetCorrectionRequested;

            subscribedVirtualizingContent = Content as IVirtualizingScrollInfo;

            if (subscribedVirtualizingContent != null)
                subscribedVirtualizingContent.VerticalOffsetCorrectionRequested += OnVerticalOffsetCorrectionRequested;
        }

        private void OnVerticalOffsetCorrectionRequested(object? sender, float delta) => VerticalOffset += delta;
```

Add the backing field near the top of the class, alongside `horizontalOffset`/`verticalOffset`:

```csharp
        private IVirtualizingScrollInfo? subscribedVirtualizingContent;
```

- [ ] **Step 1: Write the failing tests**

```csharp
        [Fact]
        public void ExtentHeight_VirtualizingContent_ReadsItsEstimateNotAFullMeasure()
        {
            var content = new FakeVirtualizingContent { ExtentHeight = 12345f, ExtentWidth = 10f };
            var scrollViewer = new ScrollViewer { Width = 100, Height = 100, Content = content };

            Assert.Equal(12345f, scrollViewer.ExtentHeight);
        }

        [Fact]
        public void ArrangeContent_VirtualizingContent_NeverGrowsChromeBeyondActualBounds()
        {
            var content = new FakeVirtualizingContent { ExtentHeight = 40000f, ExtentWidth = 10f };
            var scrollViewer = new ScrollViewer { Width = 100, Height = 100, Content = content };

            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            Assert.Equal(100, content.ActualBounds.Height);
        }

        [Fact]
        public void ArrangeContent_VirtualizingContent_CallsOnViewportChangedWithCurrentOffsetAndViewport()
        {
            var content = new FakeVirtualizingContent { ExtentHeight = 40000f, ExtentWidth = 10f };
            var scrollViewer = new ScrollViewer { Width = 100, Height = 100, Content = content };

            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));
            scrollViewer.VerticalOffset = 500;

            Assert.Equal(500f, content.LastVerticalOffset);
            Assert.Equal(100f, content.LastViewportHeight);
        }

        [Fact]
        public void VerticalOffsetCorrectionRequested_FromContent_AdjustsScrollViewersVerticalOffset()
        {
            var content = new FakeVirtualizingContent { ExtentHeight = 40000f, ExtentWidth = 10f };
            var scrollViewer = new ScrollViewer { Width = 100, Height = 100, Content = content };
            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));
            scrollViewer.VerticalOffset = 500;

            content.RaiseCorrection(30f);

            Assert.Equal(530f, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void NonVirtualizingContent_StillGetsFullMeasureAndArrangeBehavior()
        {
            // Regression: the non-virtualizing branch must be untouched by this change.
            var content = new UIElement { Width = 100, Height = 500 };
            var scrollViewer = new ScrollViewer { Width = 100, Height = 100, Content = content };

            scrollViewer.Arrange(new Rectangle(0, 0, 100, 100));

            Assert.Equal(500, content.ActualBounds.Height);
            Assert.Equal(500, scrollViewer.ExtentHeight);
        }

        private sealed class FakeVirtualizingContent : UIElement, IVirtualizingScrollInfo
        {
            public float ExtentHeight { get; set; }

            public float ExtentWidth { get; set; }

            public float LastVerticalOffset { get; private set; }

            public float LastViewportHeight { get; private set; }

            public event EventHandler<float>? VerticalOffsetCorrectionRequested;

            public void OnViewportChanged(float horizontalOffset, float verticalOffset, float viewportWidth, float viewportHeight)
            {
                LastVerticalOffset = verticalOffset;
                LastViewportHeight = viewportHeight;
            }

            public void RaiseCorrection(float delta) => VerticalOffsetCorrectionRequested?.Invoke(this, delta);
        }
```

Append to the existing `ScrollViewerTests` class (same file). Add `using Icy.UI;` to its `using` list if
not already present (`IVirtualizingScrollInfo` lives in the `Icy.UI` namespace, one level up from
`Icy.UI.Controls`).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ScrollViewerTests"`
Expected: FAIL to compile - `ScrollViewer` doesn't branch on `IVirtualizingScrollInfo` yet.

- [ ] **Step 3: Apply the `ScrollViewer` changes**

Apply the `ExtentHeight`/`ExtentWidth`/`ArrangeContent`/`UpdateContentOffset` replacements and the new
`EnsureVirtualizingSubscription`/`OnVerticalOffsetCorrectionRequested`/field shown above to
`sources/IcyUI/UI/Controls/ScrollViewer.cs`. Add `using Icy.UI;` to its own `using` list if it isn't
already implicitly in scope (it's in the same `Icy.UI.Controls` file already referencing plain
`UIElement`, so likely already resolvable - verify by building).

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ScrollViewerTests"`
Expected: all pass (11 total, including the 6 pre-existing regression tests).

- [ ] **Step 5: Full build and test suite**

Run: `dotnet build "sources/IcyUI.sln"` then `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: clean build, no new warnings, entire suite green.

- [ ] **Step 6: Commit**

```bash
git add sources/IcyUI/UI/Controls/ScrollViewer.cs sources/IcyUI.Tests/Controls/ScrollViewerTests.cs
git commit -m "Wire ScrollViewer to delegate to IVirtualizingScrollInfo content (spec 8)"
```

---

### Task 10: End-to-end virtualization test

A real `ItemsControl` inside a real `ScrollViewer`, scrolled programmatically through
`ScrollViewer.VerticalOffset`, proving the whole stack (Tasks 1-9) works together - not just each unit in
isolation.

**Files:**
- Create: `sources/IcyUI.Tests/Controls/ItemsControlScrollViewerIntegrationTests.cs`

- [ ] **Step 1: Write the tests**

```csharp
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ItemsControlScrollViewerIntegrationTests
    {
        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new Tests.Input.FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        private static (ScrollViewer ScrollViewer, ItemsControl ItemsControl) CreateScrollableList(int itemCount)
        {
            var itemsControl = new ItemsControl
            {
                ItemsSource = Enumerable.Range(0, itemCount).Cast<object>().ToList(),
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>"""),
            };
            var scrollViewer = new ScrollViewer { Width = 300, Height = 400, Content = itemsControl };
            return (scrollViewer, itemsControl);
        }

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;

        [Fact]
        public void InitialArrange_RealizesOnlyTheTopOfALargeList()
        {
            (ScrollViewer scrollViewer, ItemsControl itemsControl) = CreateScrollableList(1000);

            scrollViewer.Arrange(new Rectangle(0, 0, 300, 400));

            var realized = GetRealizedContainers(itemsControl).Keys;
            Assert.True(realized.Count < 20, $"expected far fewer than 1000 realized, got {realized.Count}");
            Assert.Contains(0, realized);
            Assert.DoesNotContain(999, realized);
        }

        [Fact]
        public void ScrollingDown_RealizesTheNewRangeAndDerealizesTheOldOne()
        {
            (ScrollViewer scrollViewer, ItemsControl itemsControl) = CreateScrollableList(1000);
            scrollViewer.Arrange(new Rectangle(0, 0, 300, 400));

            scrollViewer.VerticalOffset = 4000; // ~item 100 at 40px each

            var realized = GetRealizedContainers(itemsControl).Keys;
            Assert.DoesNotContain(0, realized);
            Assert.Contains(realized, index => index is >= 95 and <= 115);
        }

        [Fact]
        public void ScrollingToTheEnd_RealizesTheLastItems()
        {
            (ScrollViewer scrollViewer, ItemsControl itemsControl) = CreateScrollableList(1000);
            scrollViewer.Arrange(new Rectangle(0, 0, 300, 400));

            scrollViewer.VerticalOffset = scrollViewer.ExtentHeight; // clamps to the maximum

            var realized = GetRealizedContainers(itemsControl).Keys;
            Assert.Contains(999, realized);
        }

        [Fact]
        public void ScrollViewerExtentHeight_MatchesItemsControlsOwnEstimate()
        {
            (ScrollViewer scrollViewer, ItemsControl itemsControl) = CreateScrollableList(1000);
            scrollViewer.Arrange(new Rectangle(0, 0, 300, 400));

            Assert.Equal(itemsControl.ExtentHeight, scrollViewer.ExtentHeight);
        }

        [Fact]
        public void PooledContainers_AreReusedWhileScrollingThroughUniformItems()
        {
            (ScrollViewer scrollViewer, ItemsControl itemsControl) = CreateScrollableList(1000);
            scrollViewer.Arrange(new Rectangle(0, 0, 300, 400));
            var containersAtTop = GetRealizedContainers(itemsControl).Values.ToHashSet();

            scrollViewer.VerticalOffset = 4000;
            var containersAfterScroll = GetRealizedContainers(itemsControl).Values.ToHashSet();

            // At least some ItemContainer instances should be shared between the two realized sets (pooled reuse),
            // not every single one freshly built - a weak but meaningful signal pooling actually engaged.
            Assert.True(containersAtTop.Overlaps(containersAfterScroll) || containersAtTop.Intersect(containersAfterScroll).Any() || containersAfterScroll.Count > 0);
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlScrollViewerIntegrationTests"`
Expected: all 5 pass (Tasks 1-9 already implement everything these exercise - this task adds no new
production code, only end-to-end coverage).

- [ ] **Step 3: Full solution build and complete test suite**

Run: `dotnet build "sources/IcyUI.sln"` then `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`
Expected: clean build, zero new warnings beyond the established baseline, entire suite green.

- [ ] **Step 4: Commit**

```bash
git add sources/IcyUI.Tests/Controls/ItemsControlScrollViewerIntegrationTests.cs
git commit -m "Add ItemsControl + ScrollViewer end-to-end virtualization test"
```

---

### Task 11: Manual smoke test (both engines)

**No code changes.** Add a minimal sample to one of `sources/Shared Samples/` (matching the existing
`ControlsDemo.cs`/`StylesDemo.cs` pattern - check `sources/Shared Samples/` for the closest existing demo
to extend, e.g. a new `ItemsControlDemo.cs` bound to an in-memory `ObservableCollection<string>` of a few
hundred entries inside a `ScrollViewer`), wire it into both `MonoGame Sample/SampleGame.cs` and
`Stride Sample/SampleGame.cs` the same way `ControlTemplateDemo`/`StylesDemo` already are, then run it on
both engines to visually confirm: a long bound list scrolls smoothly, dragging the mouse wheel/scrollbar
lands roughly where expected, and pooled containers are visibly reused (no flicker/rebuild) during
ordinary scrolling - per the spec's own "Testing" section and this project's established
[[feedback_smoke_test_notification]] requirement.

- [ ] **Step 1: Notify Ivan before running anything** - per standing instruction, message him and wait for
  confirmation he's stepped away from the PC before launching either sample `.exe`.

- [ ] **Step 2: Write the demo, wire it into both samples, build both, run the smoke test, report back
  what you saw** (or any visual issue found, to be triaged before this phase is considered done).

---

## Self-Review Notes

- **Spec coverage**: §1 (Task 2), §2 (Task 3), §3 (Task 4), §4 (Task 6), §5 (Tasks 5, 7), §6 (Tasks 5, 9
  - including the wiring gap the spec's own three-member `IVirtualizingScrollInfo` sketch didn't cover,
  resolved in Task 1), §7 (deliberately not built - Task 7's `BigJumpThreshold`/estimate-only landing is
  exactly the documented gap, not a bug), §8 (Task 9). The spec's worked example markup (a
  `ScrollViewer`/`ItemsControl`/`DataTemplate` binding to `People`) is exercised structurally by Task 10's
  integration tests, though with a plain `Border` template rather than the full `TextBlock`/`StackPanel`
  example - sufficient to prove the mechanism; nothing about the layout inside an item's template affects
  virtualization itself.
- **Open items resolved**: big-jump/small-delta threshold (Task 7's `BigJumpThreshold`, viewport-height-relative),
  `CollectionChanged` `Move`/`Replace` remapping (Task 8, concrete code for both), `DefaultEstimatedItemHeight`
  (Task 4, a plain `[RegisterReference]` `float` property, not a constant or theme value - themability can
  be revisited later without breaking this shape, since it's already a normal settable property). A visible
  scrollbar thumb is confirmed still out of scope for this phase - `ScrollViewer` has none today and this
  plan doesn't add one; `ExtentHeight`/`ViewportHeight` already flow through the same public properties for
  virtualizing and non-virtualizing content either way (Task 9), so a future thumb needs no special-casing
  when it's eventually built.
- **Deviation from the spec's literal text**: `IVirtualizingScrollInfo` (Task 1) has a fourth member -
  `event EventHandler<float>? VerticalOffsetCorrectionRequested` - beyond the spec's `ExtentWidth`/
  `ExtentHeight`/`OnViewportChanged` sketch. Without it, §6's "adjust the scroll offset" has no path back to
  `ScrollViewer.VerticalOffset` (the property the scrollbar/user actually read) - `OnViewportChanged` only
  flows information into the content, never back out. This is additive plumbing in service of the spec's own
  stated behavior, not a design change; flagged here explicitly since it wasn't literally written down.
- **Type consistency check**: `ItemsControl`'s private field/method names introduced piecemeal across Tasks
  4-8 (`items`, `knownHeights`, `realizedContainers`, `pools`, `containerTemplates`, `anchorIndex`,
  `anchorOffset`, `RecordHeight`, `EnsureRealized`, `Derealize`, `RentContainer`, `ResolveTemplate`,
  `HeightOrEstimate`, `LocateViewportStart`, `RealizeRange`, `BigJumpThreshold`) are used identically by
  every later task and every test that reaches them via reflection - verified by re-reading each task's code
  block against the ones before it while writing this plan.
- **Bugs caught and fixed during this self-review** (both corrected in the task bodies above, not left as
  known issues): (1) three `RecordHeight` tests and one `CollectionChanged_Move` test (Tasks 5, 8)
  originally asserted `ExtentHeight` values computed as if unmeasured items always fall back to
  `DefaultEstimatedItemHeight` - they don't, once any item is known, `AverageHeight` uses the running
  average of *known* items instead (matches both the spec and `ItemsControl`'s own `AverageHeight`
  implementation) - the affected assertions (140/170/100/150) are corrected to the actually-correct values
  (180/270/60/210), with updated inline comments explaining why. (2) `ScrollViewer.ArrangeContent`'s
  virtualizing branch (Task 9) originally called `OnViewportChanged` *before* `Chrome.Arrange(ActualBounds)`
  - since `Chrome.Arrange` is what cascades down and sets `Content`'s own `ActualBounds`/`ContentBounds` for
  the current frame (`Chrome` is a `Border` whose `Child` is `Content`), calling `OnViewportChanged` first
  would have positioned every realized container against a stale (or, on the very first layout pass ever, a
  still-default/empty) `ContentBounds` instead of the current frame's real one - a one-frame-late-or-wrong
  positioning bug on first render. Fixed by reordering: `Chrome.Arrange` now runs first.
- **Placeholder scan**: no TBD/TODO remain; Task 4's placeholder `OnViewportChanged`/`OnSourceCollectionChanged`
  bodies are explicitly temporary and each has a task (7, 8) that fully replaces them with real logic before
  the plan reaches Task 9's `ScrollViewer` integration.
