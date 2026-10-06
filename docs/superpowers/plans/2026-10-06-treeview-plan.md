# TreeView Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a flattened, virtualizing `TreeView` to core IcyUI, plus `ItemsControl.ScrollIntoView` and a `TreeViewDemo` in both sample hosts.

**Architecture:** `TreeView` is a composite `Control`. Its `Chrome` border hosts a `ScrollViewer`, and the `ScrollViewer` hosts an internal `TreeViewList : SelectingItemsControl`. The list virtualizes a flat `RangeObservableCollection<FlatRow>` of the *visible* rows. Expanding or collapsing a node is one range `Add`/`Remove` on that collection, which goes through `ItemsControl`'s existing incremental paths. The tree owns expansion state, selection by data item, live-collection subscriptions and keyboard navigation. Containers (`TreeViewItem`) are re-stamped from their `FlatRow` on every realization, because pooling only rebinds `DataContext`.

**Tech Stack:** C# / .NET 10, xUnit, IcyUI markup (`DefaultTheme.xml`), StyleCop (core library).

**Spec:** `docs/superpowers/specs/2026-10-06-treeview-design.md` (read its "Plan-time revisions" section: this plan implements the revised design).

## Global Constraints

- Core `IcyUI` only. No engine types. `IcyUI.MonoGame`, `IcyUI.Stride` and `IcyUI.FNA` are untouched.
- Every public type and member gets complete XML documentation (`<summary>`, `<remarks>` with `<list>`/`<para>` where useful, `<see cref>`/`<see langword>`, `<exception>`).
- Match the surrounding style: file copyright header, block-scoped `namespace X { }`, StyleCop rules, one type per file.
- `Indent` defaults to `16`. Rows in tests are 20 px (`<Border Height="20"/>` template).
- Layout limits use `float.NaN` for "unset". Never pass them to `float.Clamp`/`Math.Min`/`Math.Max` directly.
- Every new demo is registered in **both** `MonoGame Sample` and `Stride Sample` `SampleGame.cs`.
- Build: `dotnet build "sources/IcyUI.sln"`. Test: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"`. `dotnet test` prints "Passed!" even when the test host crashes, so **always check the `Total:` count**.
- Commit to `platform-independent`. End every commit message with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **The same item under two parents.** Expanding it expands both rows, a change to its children updates both, and it holds one subscription, not two. *(Tests: Task 4 `ADuplicatedItem_ExpandsEverywhere`, Task 6 `ADuplicatedItem_IsSubscribedOnce_AndUpdatesEveryRow`.)*
2. **Children that aren't observable, or a `ChildrenPath` that resolves to a string.** A plain array still shows children. A string is never treated as a collection of characters. *(Task 4 `PlainEnumerableChildren_Work_AndStringsAreNotChildren`.)*
3. **Retargeting `ItemsSource` while something is selected.** The selection clears when the item isn't in the new tree, and survives when it is. *(Task 5 `RetargetingItemsSource_ClearsASelectionThatIsGone`.)*
4. **Detach, mutate, reattach** (a `KeepAlive` page). Changes made while detached show up after reattach, and nothing stays subscribed while detached. *(Task 6 `Detaching_Unsubscribes_AndReattaching_ReflectsMissedChanges`.)*
5. **A node that contains itself (a cycle).** Expanding it shows the repeated child once and never loops, and `Reveal` terminates. *(Task 4 `ACycle_IsShownOnce_AndNeverLoops`, Task 5 `Reveal_TerminatesOnACycle`.)*

## File Structure

| File | Responsibility |
|---|---|
| `sources/IcyUI/UI/IVirtualizingScrollInfo.cs` (modify) | New `ScrollToVerticalOffsetRequested` event. |
| `sources/IcyUI/UI/Controls/ItemsControl.cs` (modify) | `ScrollIntoView(int)`, `GetItemExtent(int)`, the event. |
| `sources/IcyUI/UI/Controls/WrapGrid.cs` (modify) | `GetItemExtent` override for the uniform grid. |
| `sources/IcyUI/UI/Controls/ScrollViewer.cs` (modify) | Applies `ScrollToVerticalOffsetRequested`. |
| `sources/IcyUI/UI/Controls/SelectingItemsControl.cs` (modify) | `protected virtual void OnContainerTapped(int index)` hook. |
| `sources/IcyUI/Data/RangeObservableCollection.cs` (create) | Internal collection that raises one notification per range. |
| `sources/IcyUI/UI/Controls/TreeViewNode.cs` (create) | Data class for static and markup trees. |
| `sources/IcyUI/UI/Styles/HierarchicalDataTemplate.cs` (create) | `DataTemplate` with `ChildrenPath`. |
| `sources/IcyUI/Markup/MarkupLoader.cs` (modify) | Reads `ChildrenPath` in the `DataTemplate` special case. |
| `sources/IcyUI/UI/Controls/TreeViewExpander.cs` (create) | The chevron part: an `Icon` that reports its own taps. |
| `sources/IcyUI/UI/Controls/TreeViewItem.cs` (create) | The row container: depth, chevron, selectability, tap suppression. |
| `sources/IcyUI/UI/Controls/TreeViewItemEventArgs.cs` (create) | Payload of `ItemExpanded`/`ItemCollapsed`. |
| `sources/IcyUI/UI/Controls/FlatRow.cs` (create) | Internal: one visible row (item, depth, parent row). |
| `sources/IcyUI/UI/Controls/TreeViewList.cs` (create) | Internal: virtualizes the rows, stamps containers, forwards taps. |
| `sources/IcyUI/UI/Controls/TreeView.cs` (create) | The control: roots, children, expansion, selection, live data, navigation, reveal. |
| `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (modify) | `TreeViewItem` template and style, `TreeView` style. |
| `sources/IcyUI.Tests/Controls/ItemsControlScrollIntoViewTests.cs` (create) | Task 1 tests. |
| `sources/IcyUI.Tests/Controls/ScrollViewerTests.cs` (modify) | The fake content implements the new event. |
| `sources/IcyUI.Tests/Data/RangeObservableCollectionTests.cs` (create) | Task 2 tests. |
| `sources/IcyUI.Tests/Controls/TreeViewNodeTests.cs` (create) | Task 2 tests (node and template markup). |
| `sources/IcyUI.Tests/Controls/TreeViewItemTests.cs` (create) | Task 3 tests. |
| `sources/IcyUI.Tests/Controls/TreeViewTestKit.cs` (create) | Shared test helpers for every TreeView test file. |
| `sources/IcyUI.Tests/Controls/TreeViewFlatteningTests.cs` (create) | Task 4 tests. |
| `sources/IcyUI.Tests/Controls/TreeViewSelectionTests.cs` (create) | Task 5 tests. |
| `sources/IcyUI.Tests/Controls/TreeViewLiveDataTests.cs` (create) | Task 6 tests. |
| `sources/IcyUI.Tests/Input/FakeInputSystem.cs` (modify) | `RaiseFocusChanging` returns its args. |
| `sources/IcyUI.Tests/Controls/TreeViewNavigationTests.cs` (create) | Task 7 tests. |
| `sources/IcyUI.Tests/Controls/TreeViewPoolingTests.cs` (create) | Task 8 tests. |
| `sources/Shared Samples/TreeViewDemo.cs` (create) | The demo. |
| `sources/MonoGame Sample/Samples/TreeViewSample.cs` (create), `MonoGame Sample.csproj`, `SampleGame.cs` (modify) | MonoGame registration. |
| `sources/Stride Sample/Stride Sample.csproj`, `SampleGame.cs` (modify) | Stride registration. |
| `sources/IcyUI.Tests/IcyUI.Tests.csproj` (modify), `sources/IcyUI.Tests/Samples/TreeViewDemoTests.cs` (create) | Demo linked and exercised by tests. |

---

### Task 1: `ItemsControl.ScrollIntoView` and the container-tap hook

**Files:**
- Modify: `sources/IcyUI/UI/IVirtualizingScrollInfo.cs`
- Modify: `sources/IcyUI/UI/Controls/ItemsControl.cs`
- Modify: `sources/IcyUI/UI/Controls/WrapGrid.cs`
- Modify: `sources/IcyUI/UI/Controls/ScrollViewer.cs` (`EnsureVirtualizingSubscription`, around line 390)
- Modify: `sources/IcyUI/UI/Controls/SelectingItemsControl.cs` (`Container_Tapped`, around line 165)
- Modify: `sources/IcyUI.Tests/Controls/ScrollViewerTests.cs` (`FakeVirtualizingContent`, around line 279)
- Test: `sources/IcyUI.Tests/Controls/ItemsControlScrollIntoViewTests.cs`

**Interfaces:**
- Produces:
  - `event EventHandler<float>? IVirtualizingScrollInfo.ScrollToVerticalOffsetRequested` (absolute offset).
  - `public void ItemsControl.ScrollIntoView(int index)`.
  - `protected virtual (float Top, float Height) ItemsControl.GetItemExtent(int index)`.
  - `protected virtual void SelectingItemsControl.OnContainerTapped(int index)` (default: `SelectedIndex = index`).

- [ ] **Step 1: Write the failing tests**

Create `sources/IcyUI.Tests/Controls/ItemsControlScrollIntoViewTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
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
    public class ItemsControlScrollIntoViewTests
    {
        [Fact]
        public void AnItemBelowTheViewport_IsScrolledUpToItsBottomEdge()
        {
            (ScrollViewer scrollViewer, ItemsControl list) = CreateList(1000);

            list.ScrollIntoView(50);

            // Item 50 spans [2000, 2040); the viewport is 400 high, so its bottom edge lands on the viewport's.
            Assert.Equal(1640f, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void AnItemAboveTheViewport_IsScrolledDownToItsTopEdge()
        {
            (ScrollViewer scrollViewer, ItemsControl list) = CreateList(1000);
            scrollViewer.VerticalOffset = 2000;

            list.ScrollIntoView(10);

            Assert.Equal(400f, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void AVisibleItem_DoesNotScroll()
        {
            (ScrollViewer scrollViewer, ItemsControl list) = CreateList(1000);

            list.ScrollIntoView(3);

            Assert.Equal(0f, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void AfterScrolling_TheItemIsRealized()
        {
            (_, ItemsControl list) = CreateList(1000);

            list.ScrollIntoView(50);

            Assert.Contains(50, GetRealizedContainers(list).Keys);
        }

        [Fact]
        public void AnIndexOutOfRange_Throws()
        {
            (_, ItemsControl list) = CreateList(10);

            Assert.Throws<ArgumentOutOfRangeException>(() => list.ScrollIntoView(10));
            Assert.Throws<ArgumentOutOfRangeException>(() => list.ScrollIntoView(-1));
        }

        [Fact]
        public void WithoutAHost_NothingHappens()
        {
            var list = new ItemsControl { ItemsSource = Enumerable.Range(0, 10).Cast<object>().ToList() };

            list.ScrollIntoView(5);
        }

        [Fact]
        public void AWrapGrid_ScrollsByRows()
        {
            var grid = new WrapGrid
            {
                ItemWidth = 100,
                ItemHeight = 50,
                ItemsSource = Enumerable.Range(0, 100).Cast<object>().ToList(),
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="50"/></DataTemplate>"""),
            };
            var scrollViewer = new ScrollViewer { Width = 300, Height = 400, Content = grid };
            scrollViewer.Arrange(new Rectangle(0, 0, 300, 400));
            int columns = Math.Max(1, (int)(grid.ContentBounds.Width / 100f));

            grid.ScrollIntoView(30);

            Assert.Equal(((30 / columns) * 50f) + 50f - 400f, scrollViewer.VerticalOffset);
        }

        [Fact]
        public void TheContainerTapHook_DecidesWhatATapDoes()
        {
            var input = new FakeInputSystem();
            var config = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext { ViewportSize = new Size(800, 600) }, new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            var listBox = new RecordingListBox
            {
                ItemsSource = new List<object> { "a", "b", "c" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
            };
            var scrollViewer = new ScrollViewer { Content = listBox, Width = 100, Height = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.Add(scrollViewer);
            canvas.Render();

            input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(new Point(scrollViewer.ActualBounds.X + 5, scrollViewer.ActualBounds.Y + 25), 1));

            Assert.Equal([1], listBox.Tapped);
            Assert.Equal(-1, listBox.SelectedIndex);
        }

        private static (ScrollViewer ScrollViewer, ItemsControl List) CreateList(int count)
        {
            var list = new ItemsControl
            {
                ItemsSource = Enumerable.Range(0, count).Cast<object>().ToList(),
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>"""),
            };
            var scrollViewer = new ScrollViewer { Width = 300, Height = 400, Content = list };
            scrollViewer.Arrange(new Rectangle(0, 0, 300, 400));
            return (scrollViewer, list);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            return (DataTemplate)new MarkupLoader(configuration).LoadObject(markup);
        }

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;

        private sealed class RecordingListBox : ListBox
        {
            public List<int> Tapped { get; } = [];

            protected override void OnContainerTapped(int index) => Tapped.Add(index);
        }
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlScrollIntoViewTests"`
Expected: build FAILS with `'ItemsControl' does not contain a definition for 'ScrollIntoView'` and `no suitable method found to override` (`OnContainerTapped`).

- [ ] **Step 3: Add the event to `IVirtualizingScrollInfo`**

In `sources/IcyUI/UI/IVirtualizingScrollInfo.cs`, after `VerticalOffsetCorrectionRequested`, add:

```csharp
        /// <summary>
        /// Occurs when this content asks its host <see cref="Controls.ScrollViewer"/> to scroll to an absolute vertical
        /// offset - for example to bring one of its items into view (see <see cref="Controls.ItemsControl.ScrollIntoView(int)"/>).
        /// </summary>
        /// <remarks>
        /// The event's <see langword="float"/> payload is the new <see cref="Controls.ScrollViewer.VerticalOffset"/>, not
        /// a delta. The host clamps it to its scrollable range.
        /// </remarks>
        event EventHandler<float>? ScrollToVerticalOffsetRequested;
```

- [ ] **Step 4: Implement it in `ItemsControl`**

In `sources/IcyUI/UI/Controls/ItemsControl.cs`, after the `VerticalOffsetCorrectionRequested` event declaration, add:

```csharp
        /// <inheritdoc/>
        public event EventHandler<float>? ScrollToVerticalOffsetRequested;
```

After `OnViewportChanged`, add:

```csharp
        /// <summary>
        /// Scrolls the hosting <see cref="ScrollViewer"/> just enough to show the item at <paramref name="index"/> entirely.
        /// </summary>
        /// <param name="index">The index of the item to show, within <c>[0, item count)</c>.</param>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>An item above the viewport is aligned with its top edge, and one below with its bottom edge.</description></item>
        /// <item><description>An item that is already fully visible doesn't scroll anything.</description></item>
        /// <item><description>
        /// Positions come from measured heights where they're known and from the running estimate elsewhere. Once the
        /// item is realized, the usual height correction (<see cref="IVirtualizingScrollInfo.VerticalOffsetCorrectionRequested"/>)
        /// settles any difference.
        /// </description></item>
        /// <item><description>Without a host that has laid this control out, nothing happens.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is outside the item range.</exception>
        public void ScrollIntoView(int index)
        {
            Guard.IsInRange(index, 0, items.Count);
            if (viewportHeight <= 0)
                return;

            (float top, float height) = GetItemExtent(index);
            float target;
            if (top < verticalOffset)
                target = top;
            else if (top + height > verticalOffset + viewportHeight)
                target = Math.Min(top, top + height - viewportHeight);
            else
                return;

            ScrollToVerticalOffsetRequested?.Invoke(this, target);
        }

        /// <summary>
        /// Gets where the item at <paramref name="index"/> sits in this control's scrollable content - walked from the
        /// current viewport anchor through measured heights and the running estimate, the same geometry
        /// <see cref="RealizeRange(int, float)"/> positions containers with. A subclass with different geometry (e.g. a
        /// uniform grid) overrides this.
        /// </summary>
        /// <param name="index">The item's index.</param>
        /// <returns>The item's top offset and its height.</returns>
        protected virtual (float Top, float Height) GetItemExtent(int index)
        {
            float top = anchorOffset;
            if (index >= anchorIndex)
            {
                for (int i = anchorIndex; i < index; i++)
                    top += Math.Max(HeightOrEstimate(i), 1f);
            }
            else
            {
                for (int i = index; i < anchorIndex; i++)
                    top -= Math.Max(HeightOrEstimate(i), 1f);
            }

            return (top, Math.Max(HeightOrEstimate(index), 1f));
        }
```

- [ ] **Step 5: Override the geometry in `WrapGrid`**

In `sources/IcyUI/UI/Controls/WrapGrid.cs`, after the `LocateViewportStart` override, add:

```csharp
        /// <inheritdoc/>
        /// <remarks>Rows are uniform: the item's row times <see cref="ItemHeight"/>.</remarks>
        protected override (float Top, float Height) GetItemExtent(int index) => (index / ColumnsPerRow * ItemHeight, ItemHeight);
```

- [ ] **Step 6: Apply the request in `ScrollViewer`**

In `sources/IcyUI/UI/Controls/ScrollViewer.cs`, replace the body of `EnsureVirtualizingSubscription` and add a handler next to `OnVerticalOffsetCorrectionRequested`:

```csharp
        private void EnsureVirtualizingSubscription()
        {
            if (ReferenceEquals(subscribedVirtualizingContent, Content))
                return;

            if (subscribedVirtualizingContent != null)
            {
                subscribedVirtualizingContent.VerticalOffsetCorrectionRequested -= OnVerticalOffsetCorrectionRequested;
                subscribedVirtualizingContent.ScrollToVerticalOffsetRequested -= OnScrollToVerticalOffsetRequested;
            }

            subscribedVirtualizingContent = Content as IVirtualizingScrollInfo;

            if (subscribedVirtualizingContent != null)
            {
                subscribedVirtualizingContent.VerticalOffsetCorrectionRequested += OnVerticalOffsetCorrectionRequested;
                subscribedVirtualizingContent.ScrollToVerticalOffsetRequested += OnScrollToVerticalOffsetRequested;
            }
        }

        private void OnVerticalOffsetCorrectionRequested(object? sender, float delta) => VerticalOffset += delta;

        private void OnScrollToVerticalOffsetRequested(object? sender, float offset) => VerticalOffset = offset;
```

Also update the `EnsureVirtualizingSubscription` summary to mention both events: "…so a `VerticalOffsetCorrectionRequested` or `ScrollToVerticalOffsetRequested` it raises actually reaches `VerticalOffset`…".

- [ ] **Step 7: Add the tap hook to `SelectingItemsControl`**

In `sources/IcyUI/UI/Controls/SelectingItemsControl.cs`, replace `Container_Tapped` and add the hook before it:

```csharp
        /// <summary>
        /// Called when a realized container is tapped. Selects <paramref name="index"/> by default.
        /// </summary>
        /// <param name="index">The tapped container's item index.</param>
        /// <remarks>
        /// A subclass whose items aren't all selectable (e.g. a tree whose category rows only expand) overrides this to
        /// decide what a tap does.
        /// </remarks>
        protected virtual void OnContainerTapped(int index) => SelectedIndex = index;

        /// <summary>
        /// Handles a realized <see cref="SelectorItem"/>'s <see cref="SelectorItem.Tapped"/> event by passing the item's
        /// current index, looked up via <see cref="containerIndices"/>, to <see cref="OnContainerTapped(int)"/>.
        /// </summary>
        /// <param name="sender">The tapped <see cref="SelectorItem"/>.</param>
        /// <param name="e">Unused.</param>
        private void Container_Tapped(object? sender, EventArgs e)
        {
            if (sender is SelectorItem item && containerIndices.TryGetValue(item, out int index))
                OnContainerTapped(index);
        }
```

- [ ] **Step 8: Keep the test fake compiling**

In `sources/IcyUI.Tests/Controls/ScrollViewerTests.cs`, inside `FakeVirtualizingContent`, next to its `VerticalOffsetCorrectionRequested`, add:

```csharp
            public event EventHandler<float>? ScrollToVerticalOffsetRequested
            {
                add { }
                remove { }
            }
```

- [ ] **Step 9: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~ItemsControlScrollIntoViewTests|FullyQualifiedName~ListBoxTests|FullyQualifiedName~ScrollViewerTests|FullyQualifiedName~WrapGridTests"`
Expected: PASS. Then run the whole suite and check `Total:` (expect 1440 + 8).

- [ ] **Step 10: Commit**

```bash
git add sources/IcyUI/UI/IVirtualizingScrollInfo.cs sources/IcyUI/UI/Controls/ItemsControl.cs sources/IcyUI/UI/Controls/WrapGrid.cs sources/IcyUI/UI/Controls/ScrollViewer.cs sources/IcyUI/UI/Controls/SelectingItemsControl.cs sources/IcyUI.Tests/Controls/ScrollViewerTests.cs sources/IcyUI.Tests/Controls/ItemsControlScrollIntoViewTests.cs
git commit -m "Add ItemsControl.ScrollIntoView and a container-tap hook

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: `RangeObservableCollection`, `TreeViewNode` and `HierarchicalDataTemplate`

**Files:**
- Create: `sources/IcyUI/Data/RangeObservableCollection.cs`
- Create: `sources/IcyUI/UI/Controls/TreeViewNode.cs`
- Create: `sources/IcyUI/UI/Styles/HierarchicalDataTemplate.cs`
- Modify: `sources/IcyUI/Markup/MarkupLoader.cs` (the `DataTemplate` special case in `CreateObject`, around line 633)
- Test: `sources/IcyUI.Tests/Data/RangeObservableCollectionTests.cs`, `sources/IcyUI.Tests/Controls/TreeViewNodeTests.cs`

**Interfaces:**
- Produces:
  - `internal sealed class Icy.Data.RangeObservableCollection<T> : ObservableCollection<T>` with `void InsertRange(int index, IReadOnlyList<T> range)`, `List<T> RemoveRange(int index, int count)`, `void ResetTo(IEnumerable<T> items)`.
  - `public class Icy.UI.Controls.TreeViewNode : INotifyPropertyChanged` with `object? Header`, `ObservableCollection<TreeViewNode> Children` (content property), `bool IsExpanded`, `bool IsSelectable` (default `true`), `object? Tag`, and constructors `()` and `(object? header)`.
  - `public class Icy.UI.Styles.HierarchicalDataTemplate : DataTemplate` with `string? ChildrenPath` and `IEnumerable? GetChildren(object item)`.

- [ ] **Step 1: Write the failing tests**

Create `sources/IcyUI.Tests/Data/RangeObservableCollectionTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Generic;
using System.Collections.Specialized;
using Icy.Data;
using Xunit;

namespace Icy.Tests.Data
{
    public class RangeObservableCollectionTests
    {
        [Fact]
        public void InsertRange_RaisesOneAddForTheWholeRange()
        {
            var collection = new RangeObservableCollection<int> { 1, 4 };
            var events = Record(collection);

            collection.InsertRange(1, [2, 3]);

            Assert.Equal([1, 2, 3, 4], collection);
            NotifyCollectionChangedEventArgs e = Assert.Single(events);
            Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
            Assert.Equal(1, e.NewStartingIndex);
            Assert.Equal([2, 3], e.NewItems!);
        }

        [Fact]
        public void RemoveRange_RaisesOneRemove_AndReturnsTheRemovedItems()
        {
            var collection = new RangeObservableCollection<int> { 1, 2, 3, 4 };
            var events = Record(collection);

            List<int> removed = collection.RemoveRange(1, 2);

            Assert.Equal([2, 3], removed);
            Assert.Equal([1, 4], collection);
            NotifyCollectionChangedEventArgs e = Assert.Single(events);
            Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.Equal(1, e.OldStartingIndex);
            Assert.Equal([2, 3], e.OldItems!);
        }

        [Fact]
        public void EmptyRanges_RaiseNothing()
        {
            var collection = new RangeObservableCollection<int> { 1 };
            var events = Record(collection);

            collection.InsertRange(0, []);
            collection.RemoveRange(0, 0);

            Assert.Empty(events);
        }

        [Fact]
        public void ResetTo_ReplacesEverything_WithOneReset()
        {
            var collection = new RangeObservableCollection<int> { 1, 2 };
            var events = Record(collection);

            collection.ResetTo([7, 8, 9]);

            Assert.Equal([7, 8, 9], collection);
            Assert.Equal(NotifyCollectionChangedAction.Reset, Assert.Single(events).Action);
        }

        private static List<NotifyCollectionChangedEventArgs> Record(INotifyCollectionChanged collection)
        {
            var events = new List<NotifyCollectionChangedEventArgs>();
            collection.CollectionChanged += (_, e) => events.Add(e);
            return events;
        }
    }
}
```

Create `sources/IcyUI.Tests/Controls/TreeViewNodeTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Generic;
using System.ComponentModel;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Markup;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TreeViewNodeTests
    {
        [Fact]
        public void Defaults_AreSelectableAndCollapsed()
        {
            var node = new TreeViewNode("A");

            Assert.True(node.IsSelectable);
            Assert.False(node.IsExpanded);
            Assert.Empty(node.Children);
            Assert.Equal("A", node.ToString());
        }

        [Fact]
        public void Setters_RaisePropertyChanged()
        {
            var node = new TreeViewNode();
            var changed = new List<string?>();
            ((INotifyPropertyChanged)node).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            node.Header = "H";
            node.IsExpanded = true;
            node.IsSelectable = false;
            node.Tag = 5;

            Assert.Equal([nameof(TreeViewNode.Header), nameof(TreeViewNode.IsExpanded), nameof(TreeViewNode.IsSelectable), nameof(TreeViewNode.Tag)], changed);
        }

        [Fact]
        public void ANodeTree_LoadsFromMarkup()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            var root = (TreeViewNode)new MarkupLoader(configuration).LoadObject(
                """
                <TreeViewNode Header="Basics" IsSelectable="False" IsExpanded="True">
                  <TreeViewNode Header="Controls" Tag="controls"/>
                  <TreeViewNode Header="Styles"/>
                </TreeViewNode>
                """);

            Assert.Equal("Basics", root.Header);
            Assert.False(root.IsSelectable);
            Assert.True(root.IsExpanded);
            Assert.Equal(["Controls", "Styles"], root.Children.Select(x => x.ToString()));
            Assert.Equal("controls", root.Children[0].Tag);
        }

        [Fact]
        public void AHierarchicalDataTemplate_LoadsItsChildrenPath()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            var template = (HierarchicalDataTemplate)new MarkupLoader(configuration).LoadObject(
                """<HierarchicalDataTemplate ChildrenPath="Children"><TextBlock/></HierarchicalDataTemplate>""");
            var node = new TreeViewNode("A") { Children = { new TreeViewNode("B") } };

            Assert.Equal("Children", template.ChildrenPath);
            Assert.Same(node.Children, template.GetChildren(node));
            Assert.Null(template.GetChildren(42));
        }
    }
}
```

(Add `using System.Linq;` at the top for `Select`.)

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~RangeObservableCollectionTests|FullyQualifiedName~TreeViewNodeTests"`
Expected: build FAILS (`RangeObservableCollection`, `TreeViewNode`, `HierarchicalDataTemplate` not found).

- [ ] **Step 3: Create `RangeObservableCollection<T>`**

`sources/IcyUI/Data/RangeObservableCollection.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Icy.Data
{
    /// <summary>
    /// An <see cref="ObservableCollection{T}"/> that inserts and removes whole ranges with a single
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/> notification.
    /// </summary>
    /// <remarks>
    /// <see cref="UI.Controls.ItemsControl"/> handles a multi-item <see cref="NotifyCollectionChangedAction.Add"/> or
    /// <see cref="NotifyCollectionChangedAction.Remove"/> in one pass, so a range costs one de-realization instead of one
    /// per item. <see cref="UI.Controls.TreeView"/> uses it for the rows an expand or collapse adds or removes.
    /// </remarks>
    /// <typeparam name="T">The item type.</typeparam>
    internal sealed class RangeObservableCollection<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Inserts <paramref name="range"/> at <paramref name="index"/> and raises one <see cref="NotifyCollectionChangedAction.Add"/>.
        /// </summary>
        /// <param name="index">Where the first item goes.</param>
        /// <param name="range">The items, in order. An empty range changes nothing and raises nothing.</param>
        public void InsertRange(int index, IReadOnlyList<T> range)
        {
            if (range.Count == 0)
                return;

            CheckReentrancy();
            var added = new List<T>(range);
            if (Items is List<T> list)
                list.InsertRange(index, added);
            else
            {
                for (int i = 0; i < added.Count; i++)
                    Items.Insert(index + i, added[i]);
            }

            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)added, index));
        }

        /// <summary>
        /// Removes <paramref name="count"/> items from <paramref name="index"/> and raises one <see cref="NotifyCollectionChangedAction.Remove"/>.
        /// </summary>
        /// <param name="index">The first item to remove.</param>
        /// <param name="count">How many to remove. Zero changes nothing and raises nothing.</param>
        /// <returns>The removed items, in order.</returns>
        public List<T> RemoveRange(int index, int count)
        {
            if (count == 0)
                return [];

            CheckReentrancy();
            List<T> removed;
            if (Items is List<T> list)
            {
                removed = list.GetRange(index, count);
                list.RemoveRange(index, count);
            }
            else
            {
                removed = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    removed.Add(Items[index]);
                    Items.RemoveAt(index);
                }
            }

            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (IList)removed, index));
            return removed;
        }

        /// <summary>
        /// Replaces the whole content with <paramref name="items"/> and raises one <see cref="NotifyCollectionChangedAction.Reset"/>.
        /// </summary>
        /// <param name="items">The new content.</param>
        public void ResetTo(IEnumerable<T> items)
        {
            CheckReentrancy();
            Items.Clear();
            foreach (T item in items)
                Items.Add(item);

            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void Raise(NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(e);
        }
    }
}
```

- [ ] **Step 4: Create `TreeViewNode`**

`sources/IcyUI/UI/Controls/TreeViewNode.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Icy.Markup;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A ready-made data item for a <see cref="TreeView"/>: a header, child nodes, and per-node expansion and
    /// selectability. Lets a static tree be written directly in markup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Children"/> is the content property, so nested <c>&lt;TreeViewNode&gt;</c> elements need no wrapper:
    /// </para>
    /// <code language="xml">
    /// &lt;TreeView&gt;
    ///   &lt;TreeViewNode Header="Basics" IsSelectable="False" IsExpanded="True"&gt;
    ///     &lt;TreeViewNode Header="Controls"/&gt;
    ///   &lt;/TreeViewNode&gt;
    /// &lt;/TreeView&gt;
    /// </code>
    /// <para>
    /// A <see cref="TreeView"/> reads <see cref="IsExpanded"/> as the node's expansion state and keeps it in step both
    /// ways, and treats <see cref="IsSelectable"/> as the node's selectability unless
    /// <see cref="TreeView.IsItemSelectable"/> is set. Without an item template, a row shows <see cref="ToString"/>.
    /// </para>
    /// </remarks>
    [ContentProperty(nameof(Children))]
    public class TreeViewNode : INotifyPropertyChanged
    {
        private object? header;
        private bool isExpanded;
        private bool isSelectable = true;
        private object? tag;

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeViewNode"/> class with no header.
        /// </summary>
        public TreeViewNode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeViewNode"/> class.
        /// </summary>
        /// <param name="header">The header to show.</param>
        public TreeViewNode(object? header)
        {
            this.header = header;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets or sets what the node's row shows.</summary>
        public object? Header
        {
            get => header;
            set => Set(ref header, value);
        }

        /// <summary>Gets the child nodes. A <see cref="TreeView"/> follows changes to this collection while the node is visible.</summary>
        public ObservableCollection<TreeViewNode> Children { get; } = [];

        /// <summary>Gets or sets a value indicating whether the node shows its children. Defaults to <see langword="false"/>.</summary>
        public bool IsExpanded
        {
            get => isExpanded;
            set => Set(ref isExpanded, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the node can be selected. Defaults to <see langword="true"/>. Tapping a
        /// non-selectable node with children opens or closes it instead.
        /// </summary>
        public bool IsSelectable
        {
            get => isSelectable;
            set => Set(ref isSelectable, value);
        }

        /// <summary>Gets or sets any data the application wants to attach to the node.</summary>
        public object? Tag
        {
            get => tag;
            set => Set(ref tag, value);
        }

        /// <summary>Returns the text of <see cref="Header"/>.</summary>
        /// <returns><see cref="Header"/>'s text, or an empty string when there is none.</returns>
        public override string ToString() => header?.ToString() ?? string.Empty;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
```

- [ ] **Step 5: Create `HierarchicalDataTemplate`**

`sources/IcyUI/UI/Styles/HierarchicalDataTemplate.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using Icy.Data.Bindings;

namespace Icy.UI.Styles
{
    /// <summary>
    /// A <see cref="DataTemplate"/> that also says where an item's children are, for a <see cref="Controls.TreeView"/>.
    /// </summary>
    /// <remarks>
    /// <code language="xml">
    /// &lt;TreeView.ItemTemplate&gt;
    ///   &lt;HierarchicalDataTemplate ChildrenPath="Children"&gt;
    ///     &lt;TextBlock Text="{Binding Name}"/&gt;
    ///   &lt;/HierarchicalDataTemplate&gt;
    /// &lt;/TreeView.ItemTemplate&gt;
    /// </code>
    /// <para>
    /// <see cref="ChildrenPath"/> is resolved against each item's runtime type. A value that isn't an
    /// <see cref="IEnumerable"/>, or is a <see cref="string"/>, means "no children".
    /// </para>
    /// </remarks>
    public class HierarchicalDataTemplate : DataTemplate
    {
        private string? childrenPath;
        private DynamicPropertyPath? path;

        /// <summary>
        /// Gets or sets the property path, relative to an item, of the item's children collection; or
        /// <see langword="null"/> for items without children.
        /// </summary>
        public string? ChildrenPath
        {
            get => childrenPath;
            set
            {
                childrenPath = value;
                path = string.IsNullOrEmpty(value) ? null : new DynamicPropertyPath(value);
            }
        }

        /// <summary>
        /// Gets <paramref name="item"/>'s children through <see cref="ChildrenPath"/>.
        /// </summary>
        /// <param name="item">The data item.</param>
        /// <returns>The children, or <see langword="null"/> when the path is unset, can't be resolved, or isn't a collection.</returns>
        public IEnumerable? GetChildren(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return path?.GetValue(item) is IEnumerable children and not string ? children : null;
        }
    }
}
```

- [ ] **Step 6: Read `ChildrenPath` in the loader's `DataTemplate` special case**

In `sources/IcyUI/Markup/MarkupLoader.cs`, in `CreateObject`, inside `if (instance is Icy.UI.Styles.DataTemplate dataTemplate)`, right before `dataTemplate.SetContent(...)`, add:

```csharp
                // The template never runs its own attribute resolution (see above), so a hierarchical template's one
                // attribute is read here explicitly.
                if (dataTemplate is Icy.UI.Styles.HierarchicalDataTemplate hierarchical
                    && element.Attribute(nameof(Icy.UI.Styles.HierarchicalDataTemplate.ChildrenPath)) is { } childrenPath)
                {
                    hierarchical.ChildrenPath = childrenPath.Value;
                }
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~RangeObservableCollectionTests|FullyQualifiedName~TreeViewNodeTests"`
Expected: PASS (8 tests). If `ANodeTree_LoadsFromMarkup` fails because `Header="Basics"` can't convert to `object`, check `ConvertValue` for `object` targets and assign the string as is. Don't change `Header`'s type.

- [ ] **Step 8: Commit**

```bash
git add sources/IcyUI/Data/RangeObservableCollection.cs sources/IcyUI/UI/Controls/TreeViewNode.cs sources/IcyUI/UI/Styles/HierarchicalDataTemplate.cs sources/IcyUI/Markup/MarkupLoader.cs sources/IcyUI.Tests/Data/RangeObservableCollectionTests.cs sources/IcyUI.Tests/Controls/TreeViewNodeTests.cs
git commit -m "Add TreeViewNode, HierarchicalDataTemplate and a range collection

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: `TreeViewExpander`, `TreeViewItem` and their theme

**Files:**
- Create: `sources/IcyUI/UI/Controls/TreeViewExpander.cs`
- Create: `sources/IcyUI/UI/Controls/TreeViewItem.cs`
- Modify: `sources/IcyUI/Resources/Themes/DefaultTheme.xml` (after the `SelectorItem` style)
- Test: `sources/IcyUI.Tests/Controls/TreeViewItemTests.cs`

**Interfaces:**
- Produces:
  - `public class TreeViewExpander : Icon` with `event EventHandler? Tapped`.
  - `public class TreeViewItem : SelectorItem` with `int Depth`, `bool HasChildren`, `bool IsExpanded`, `bool IsSelectable` (public get, private set), `event EventHandler? ExpanderTapped`, constants `ExpanderPartName = "PART_Expander"` and `IndentPartName = "PART_Indent"`, plus internal `void Update(int depth, float indentWidth, bool hasChildren, bool isExpanded, bool isSelectable)`, `float IndentWidth`, `TreeViewExpander? Expander`, `UIElement? IndentPart`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Controls/TreeViewItemTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TreeViewItemTests
    {
        [Fact]
        public void Update_StampsTheRowState()
        {
            var item = new TreeViewItem();
            var changed = new List<string?>();
            ((INotifyPropertyChanged)item).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

            item.Update(depth: 2, indentWidth: 32, hasChildren: true, isExpanded: true, isSelectable: false);

            Assert.Equal(2, item.Depth);
            Assert.True(item.HasChildren);
            Assert.True(item.IsExpanded);
            Assert.False(item.IsSelectable);
            Assert.Equal(32f, item.IndentWidth);
            Assert.Contains(nameof(TreeViewItem.Depth), changed);
        }

        [Fact]
        public void TheThemeTemplate_IndentsAndShowsTheChevron()
        {
            (Canvas canvas, _) = CreateThemedCanvas();
            TreeViewItem item = AddItem(canvas);

            item.Update(depth: 2, indentWidth: 32, hasChildren: true, isExpanded: false, isSelectable: true);
            Assert.Equal(32f, item.IndentPart!.Width);
            Assert.Equal(IconKind.ChevronRight, item.Expander!.Kind);
            Assert.Equal(1f, item.Expander.Opacity);

            item.Update(depth: 2, indentWidth: 32, hasChildren: true, isExpanded: true, isSelectable: true);
            Assert.Equal(IconKind.ChevronDown, item.Expander.Kind);

            item.Update(depth: 0, indentWidth: 0, hasChildren: false, isExpanded: false, isSelectable: true);
            Assert.Equal(0f, item.Expander.Opacity);
            Assert.False(item.Expander.IsHitTestVisible);
        }

        [Fact]
        public void TappingTheChevron_RaisesExpanderTapped_AndNotTapped()
        {
            (Canvas canvas, FakeInputSystem input) = CreateThemedCanvas();
            TreeViewItem item = AddItem(canvas);
            item.Update(0, 0, hasChildren: true, isExpanded: false, isSelectable: true);
            canvas.Render();
            int expanderTaps = 0, rowTaps = 0;
            item.ExpanderTapped += (_, _) => expanderTaps++;
            item.Tapped += (_, _) => rowTaps++;

            Tap(input, item.Expander!);

            Assert.Equal(1, expanderTaps);
            Assert.Equal(0, rowTaps);
        }

        [Fact]
        public void TappingTheContent_RaisesTapped_AndNotExpanderTapped()
        {
            (Canvas canvas, FakeInputSystem input) = CreateThemedCanvas();
            TreeViewItem item = AddItem(canvas);
            item.Update(0, 0, hasChildren: true, isExpanded: false, isSelectable: true);
            canvas.Render();
            int expanderTaps = 0, rowTaps = 0;
            item.ExpanderTapped += (_, _) => expanderTaps++;
            item.Tapped += (_, _) => rowTaps++;

            Tap(input, item.Content!);
            Tap(input, item.Content!);

            Assert.Equal(0, expanderTaps);
            Assert.Equal(2, rowTaps);
        }

        private static TreeViewItem AddItem(Canvas canvas)
        {
            var item = new TreeViewItem
            {
                Content = new Border { Width = 60, Height = 20 },
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(item);
            canvas.Render();
            return item;
        }

        private static void Tap(FakeInputSystem input, UIElement element)
        {
            Rectangle b = element.ActualBounds;
            input.Events.Touch.RaiseTap(new TouchInfo(new Point(b.X + (b.Width / 2), b.Y + (b.Height / 2)), 1));
        }

        private static (Canvas Canvas, FakeInputSystem Input) CreateThemedCanvas()
        {
            var input = new FakeInputSystem();
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new Size(800, 600) })
                   .ConfigureInput(input)
                   .ConfigureTypes()
                   .ConfigureAssets();
            IcyConfiguration config = builder.Build().UseDefaultTheme();
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
        }
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewItemTests"`
Expected: build FAILS (`TreeViewItem` not found).

- [ ] **Step 3: Create `TreeViewExpander`**

`sources/IcyUI/UI/Controls/TreeViewExpander.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// The chevron of a <see cref="TreeViewItem"/>: an <see cref="Icon"/> that reports its own taps, so a tap on it opens
    /// or closes the row without selecting it.
    /// </summary>
    /// <remarks>
    /// Taps bubble to every ancestor with no "handled" flag (see <see cref="UIElement.OnTap"/>). This part raises
    /// <see cref="Tapped"/> first, and its <see cref="TreeViewItem"/> then skips its own tap handling for that tap. Name
    /// it <see cref="TreeViewItem.ExpanderPartName"/> in a <see cref="TreeViewItem"/> template.
    /// </remarks>
    public class TreeViewExpander : Icon
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TreeViewExpander"/> class, showing <see cref="IconKind.ChevronRight"/>.
        /// </summary>
        public TreeViewExpander()
        {
            Kind = IconKind.ChevronRight;
        }

        /// <summary>
        /// Occurs when the chevron is tapped while enabled.
        /// </summary>
        public event EventHandler? Tapped;

        /// <inheritdoc/>
        protected internal override void OnTap()
        {
            base.OnTap();
            if (IsEnabled)
                Tapped?.Invoke(this, EventArgs.Empty);
        }
    }
}
```

- [ ] **Step 4: Create `TreeViewItem`**

`sources/IcyUI/UI/Controls/TreeViewItem.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// A row of a <see cref="TreeView"/>: a <see cref="SelectorItem"/> that knows its depth, whether it has children and
    /// is expanded, and whether it can be selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree sets every row property each time the row is realized, because a pooled row is reused for another item
    /// with only its content's <see cref="UIElement.DataContext"/> rebound.
    /// </para>
    /// <para>Template parts:</para>
    /// <list type="bullet">
    /// <item><description><see cref="IndentPartName"/>: any element; its <see cref="UIElement.Width"/> is set to the row's indent.</description></item>
    /// <item><description>
    /// <see cref="ExpanderPartName"/>: a <see cref="TreeViewExpander"/>. It shows <see cref="IconKind.ChevronDown"/>
    /// when expanded and <see cref="IconKind.ChevronRight"/> otherwise, and is transparent and not hit-testable when the
    /// row has no children, so leaves keep their alignment.
    /// </description></item>
    /// </list>
    /// </remarks>
    public class TreeViewItem : SelectorItem
    {
        /// <summary>The name of the template part that shows the chevron.</summary>
        public const string ExpanderPartName = "PART_Expander";

        /// <summary>The name of the template part whose width indents the row.</summary>
        public const string IndentPartName = "PART_Indent";

        private int depth;
        private bool hasChildren;
        private bool isExpanded;
        private bool isSelectable = true;
        private float indentWidth;
        private TreeViewExpander? expander;
        private UIElement? indentPart;
        private bool expanderTapped;

        /// <summary>
        /// Occurs when the row's chevron is tapped. The same tap doesn't raise <see cref="SelectorItem.Tapped"/>.
        /// </summary>
        public event EventHandler? ExpanderTapped;

        /// <summary>Gets the row's depth: <c>0</c> for a root.</summary>
        public int Depth
        {
            get => depth;
            private set => SetProperty(ref depth, value);
        }

        /// <summary>Gets a value indicating whether the row's item has children.</summary>
        public bool HasChildren
        {
            get => hasChildren;
            private set => SetProperty(ref hasChildren, value);
        }

        /// <summary>Gets a value indicating whether the row's item is expanded.</summary>
        public bool IsExpanded
        {
            get => isExpanded;
            private set => SetProperty(ref isExpanded, value);
        }

        /// <summary>Gets a value indicating whether the row's item can be selected.</summary>
        public bool IsSelectable
        {
            get => isSelectable;
            private set => SetProperty(ref isSelectable, value);
        }

        /// <summary>Gets the indent last stamped on the row, in layout units.</summary>
        internal float IndentWidth => indentWidth;

        /// <summary>Gets the template's chevron part, if any.</summary>
        internal TreeViewExpander? Expander => expander;

        /// <summary>Gets the template's indent part, if any.</summary>
        internal UIElement? IndentPart => indentPart;

        /// <summary>
        /// Stamps the row state for the item this row shows now.
        /// </summary>
        /// <param name="depth">The row's depth.</param>
        /// <param name="indentWidth">The indent, in layout units.</param>
        /// <param name="hasChildren">Whether the item has children.</param>
        /// <param name="isExpanded">Whether the item is expanded.</param>
        /// <param name="isSelectable">Whether the item can be selected.</param>
        internal void Update(int depth, float indentWidth, bool hasChildren, bool isExpanded, bool isSelectable)
        {
            Depth = depth;
            HasChildren = hasChildren;
            IsExpanded = isExpanded;
            IsSelectable = isSelectable;
            this.indentWidth = indentWidth;
            UpdateParts();
        }

        /// <inheritdoc/>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (expander != null)
                expander.Tapped -= Expander_Tapped;

            expander = GetTemplateChild<TreeViewExpander>(ExpanderPartName);
            indentPart = GetTemplateChild<UIElement>(IndentPartName);
            if (expander != null)
                expander.Tapped += Expander_Tapped;
            UpdateParts();
        }

        /// <inheritdoc/>
        /// <remarks>Skipped once right after a tap on the chevron, which bubbles here as well.</remarks>
        protected internal override void OnTap()
        {
            if (expanderTapped)
            {
                expanderTapped = false;
                return;
            }

            base.OnTap();
        }

        private void Expander_Tapped(object? sender, EventArgs e)
        {
            expanderTapped = true;
            ExpanderTapped?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateParts()
        {
            if (indentPart != null)
                indentPart.Width = indentWidth;
            if (expander != null)
            {
                expander.Kind = isExpanded ? IconKind.ChevronDown : IconKind.ChevronRight;
                expander.Opacity = hasChildren ? 1 : 0;
                expander.IsHitTestVisible = hasChildren;
            }
        }
    }
}
```

- [ ] **Step 5: Add the theme entries**

In `sources/IcyUI/Resources/Themes/DefaultTheme.xml`, right after the closing `</Style>` of the `SelectorItem` style, add:

```xml
  <!-- TreeViewItem: a SelectorItem row with an indent spacer and a chevron. Implicit styles match exact types only,
       so the SelectorItem states are repeated here, plus SelectedHighlighted for the focused, selected row.
       PART_Indent's width and PART_Expander's chevron are set from code (TreeViewItem.UpdateParts). -->
  <ControlTemplate x:Key="IcyDefaultTreeViewItemTemplate" TargetType="TreeViewItem">
    <Border Background="{TemplateBinding Background}" Padding="{TemplateBinding Padding}">
      <StackPanel Orientation="Horizontal">
        <Border x:Name="PART_Indent" Width="0"/>
        <TreeViewExpander x:Name="PART_Expander" Stroke="#FFB0B0C0" StrokeThickness="2" Margin="0,0,4,0" VerticalAlignment="Center"/>
        <ContentPresenter Content="{TemplateBinding Content}" VerticalAlignment="Center"/>
      </StackPanel>
    </Border>
  </ControlTemplate>

  <Style TargetType="TreeViewItem" Template="{StaticResource IcyDefaultTreeViewItemTemplate}" Background="Transparent" Padding="4,3">
    <Style.StateGroups>
      <VisualStateGroup Name="CommonStates">
        <VisualState Name="Hovered" State="Hovered" Background="#FF33333E"/>
        <VisualState Name="Selected" State="Selected" Background="#FF3C78D8"/>
        <VisualState Name="SelectedHovered" State="Selected,Hovered" Background="#FF3C78D8"/>
        <VisualState Name="Highlighted" State="Highlighted" Background="#FF46465A"/>
        <VisualState Name="HighlightedHovered" State="Highlighted,Hovered" Background="#FF46465A"/>
        <VisualState Name="SelectedHighlighted" State="Selected,Highlighted" Background="#FF4A86E8"/>
        <VisualState Name="Disabled" State="Disabled" Opacity="0.5"/>
      </VisualStateGroup>
    </Style.StateGroups>
  </Style>

  <!-- TreeView: untemplated, like TabControl/ColorPicker - it composes its ScrollViewer and row list in code. -->
  <Style TargetType="TreeView" Background="#FF26262C" BorderBrush="#FF56566A" BorderThickness="1"/>
```

The `TreeView` style references a type created in Task 4. If the theme loader rejects an unknown `TargetType` before then, add this last line in Task 4 instead. Don't stub a type for it.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewItemTests|FullyQualifiedName~ThemeConfigurationTests|FullyQualifiedName~SelectorThemeTests"`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/UI/Controls/TreeViewExpander.cs sources/IcyUI/UI/Controls/TreeViewItem.cs sources/IcyUI/Resources/Themes/DefaultTheme.xml sources/IcyUI.Tests/Controls/TreeViewItemTests.cs
git commit -m "Add the TreeViewItem row container and its chevron part

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: `TreeView` structure: roots, children, expansion and flattening

**Files:**
- Create: `sources/IcyUI/UI/Controls/FlatRow.cs`
- Create: `sources/IcyUI/UI/Controls/TreeViewItemEventArgs.cs`
- Create: `sources/IcyUI/UI/Controls/TreeViewList.cs`
- Create: `sources/IcyUI/UI/Controls/TreeView.cs`
- Test: `sources/IcyUI.Tests/Controls/TreeViewTestKit.cs`, `sources/IcyUI.Tests/Controls/TreeViewFlatteningTests.cs`

**Interfaces:**
- Consumes:
  - `RangeObservableCollection<T>`, `TreeViewNode`, `HierarchicalDataTemplate` (Task 2).
  - `TreeViewItem.Update(...)` and `ExpanderTapped` (Task 3).
  - `SelectingItemsControl.OnContainerTapped(int)` and `ItemsControl.ScrollIntoView(int)` (Task 1).
- Produces (later tasks extend these exact members):
  - `internal sealed class FlatRow` with `object Item`, `int Depth`, `FlatRow? Parent`.
  - `public sealed class TreeViewItemEventArgs : EventArgs` with `object Item`.
  - `internal sealed class TreeViewList : SelectingItemsControl` with `IReadOnlyDictionary<int, ItemContainer> Realized`, `int CurrentIndex`, `void Restamp(FlatRow row)`, `void RestampAll()`.
  - `public class TreeView : Control`:
    - Public: `ObservableCollection<object> Items`, `IEnumerable? ItemsSource`, `Func<object, IEnumerable?>? ChildrenSelector`, `Func<object, bool>? IsItemSelectable`, `DataTemplate? ItemTemplate`, `Func<object, DataTemplate>? ItemTemplateSelector`, `float Indent`, `bool IsExpanded(object)`, `void Expand(object)`, `void Collapse(object)`, events `ItemExpanded`/`ItemCollapsed`.
    - Internal: `IReadOnlyList<FlatRow> Rows`, `TreeViewList List`, `ScrollViewer ScrollViewer`, `IEnumerable? GetChildren(object)`, `bool HasChildren(object)`, `bool IsSelectable(object)`, `DataTemplate ResolveTemplate(object)`, `void Stamp(TreeViewItem, FlatRow)`, `void OnRowTapped(FlatRow)`, `void OnExpanderTapped(FlatRow)`, `void OnRowsChanged()`.
    - Private helpers used by later tasks: `Rebuild()`, `AppendVisible(...)`, `InsertRows(int, List<FlatRow>)`, `RemoveRows(int, int, bool fromData)`, `DescendantCount(int)`, `IndexOfVisible(object)`, `SyncList()`, `SetExpanded(object, bool)`, `Toggle(object)`, `ApplyExpansion(object, bool)`.

- [ ] **Step 1: Write the shared test kit**

`sources/IcyUI.Tests/Controls/TreeViewTestKit.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;

namespace Icy.Tests.Controls
{
    /// <summary>Shared builders and probes for the TreeView test files.</summary>
    internal static class TreeViewTestKit
    {
        public static DataTemplate RowTemplate() => LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>""");

        public static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            return (DataTemplate)new MarkupLoader(configuration).LoadObject(markup);
        }

        public static TreeViewNode Node(string header, params TreeViewNode[] children)
        {
            var node = new TreeViewNode(header);
            foreach (TreeViewNode child in children)
                node.Children.Add(child);
            return node;
        }

        public static TreeViewNode Expanded(string header, params TreeViewNode[] children)
        {
            TreeViewNode node = Node(header, children);
            node.IsExpanded = true;
            return node;
        }

        public static TreeView CreateTree(params TreeViewNode[] roots)
        {
            var tree = new TreeView { ItemTemplate = RowTemplate() };
            foreach (TreeViewNode root in roots)
                tree.Items.Add(root);
            return tree;
        }

        /// <summary>Lays the tree out without a canvas: the inner ScrollViewer realizes the visible rows.</summary>
        public static void Layout(TreeView tree, int width = 200, int height = 400)
        {
            tree.InvalidateArrange();
            tree.Arrange(new Rectangle(0, 0, width, height));
        }

        public static string[] Visible(TreeView tree) => [.. tree.Rows.Select(row => row.Item.ToString() ?? string.Empty)];

        public static int[] Depths(TreeView tree) => [.. tree.Rows.Select(row => row.Depth)];

        public static TreeViewItem Row(TreeView tree, int index) => (TreeViewItem)tree.List.Realized[index];

        public static (Canvas Canvas, FakeInputSystem Input) CreateCanvas(bool themed = false)
        {
            var input = new FakeInputSystem();
            if (themed)
            {
                var builder = new IcyConfigurationBuilder();
                builder.ConfigureRendering(new FakeRenderContext { ViewportSize = new Size(800, 600) })
                       .ConfigureInput(input)
                       .ConfigureTypes()
                       .ConfigureAssets();
                return (new Canvas(builder.Build().UseDefaultTheme()) { IsInputEnabled = true, IsVisible = true }, input);
            }

            var config = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext { ViewportSize = new Size(800, 600) }, new ReflectionConfiguration());
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
        }

        /// <summary>Puts the tree at the canvas's top-left, 200 x 400, and renders once.</summary>
        public static void Host(Canvas canvas, TreeView tree)
        {
            tree.Width = 200;
            tree.Height = 400;
            tree.HorizontalAlignment = HorizontalAlignment.Left;
            tree.VerticalAlignment = VerticalAlignment.Top;
            canvas.Add(tree);
            canvas.Render();
        }

        public static void Tap(FakeInputSystem input, UIElement element)
        {
            Rectangle b = element.ActualBounds;
            input.Events.Touch.RaiseTap(new TouchInfo(new Point(b.X + (b.Width / 2), b.Y + (b.Height / 2)), 1));
        }
    }
}
```

- [ ] **Step 2: Write the failing tests**

`sources/IcyUI.Tests/Controls/TreeViewFlatteningTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewFlatteningTests
    {
        [Fact]
        public void InlineNodes_AreTheRoots()
        {
            TreeView tree = CreateTree(Node("A"), Node("B"));

            Assert.Equal(["A", "B"], Visible(tree));
            Assert.Equal([0, 0], Depths(tree));
        }

        [Fact]
        public void ExpandedNodes_ShowTheirChildren()
        {
            TreeView tree = CreateTree(Expanded("A", Node("B"), Expanded("C", Node("D"))), Node("E"));

            Assert.Equal(["A", "B", "C", "D", "E"], Visible(tree));
            Assert.Equal([0, 1, 1, 2, 0], Depths(tree));
            Assert.Same(tree.Rows[2], tree.Rows[3].Parent);
        }

        [Fact]
        public void Expand_InsertsTheVisibleSubtree_AsOneChange()
        {
            TreeViewNode a = Node("A", Node("B"), Expanded("C", Node("D")));
            TreeView tree = CreateTree(a, Node("E"));
            var events = new List<NotifyCollectionChangedEventArgs>();
            ((INotifyCollectionChanged)tree.Rows).CollectionChanged += (_, e) => events.Add(e);

            tree.Expand(a);

            Assert.Equal(["A", "B", "C", "D", "E"], Visible(tree));
            NotifyCollectionChangedEventArgs change = Assert.Single(events);
            Assert.Equal(NotifyCollectionChangedAction.Add, change.Action);
            Assert.Equal(3, change.NewItems!.Count);
            Assert.True(a.IsExpanded);
        }

        [Fact]
        public void Collapse_HidesTheSubtree_AndReExpandRestoresIt()
        {
            TreeViewNode a = Expanded("A", Expanded("B", Node("C")));
            TreeView tree = CreateTree(a, Node("D"));

            tree.Collapse(a);
            Assert.Equal(["A", "D"], Visible(tree));

            tree.Expand(a);
            Assert.Equal(["A", "B", "C", "D"], Visible(tree));
        }

        [Fact]
        public void ExpandingAnItemThatIsNotVisible_OnlyRecordsTheState()
        {
            var hidden = new object();
            var root = new object();
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ReferenceEquals(x, root) ? new[] { hidden } : null };

            tree.Expand(hidden);

            Assert.True(tree.IsExpanded(hidden));
            Assert.Single(tree.Rows);
        }

        [Fact]
        public void ExpandAndCollapse_RaiseTheirEvents()
        {
            TreeViewNode a = Node("A", Node("B"));
            TreeView tree = CreateTree(a);
            var raised = new List<string>();
            tree.ItemExpanded += (_, e) => raised.Add("+" + e.Item);
            tree.ItemCollapsed += (_, e) => raised.Add("-" + e.Item);

            tree.Expand(a);
            tree.Expand(a);
            tree.Collapse(a);

            Assert.Equal(["+A", "-A"], raised);
        }

        [Fact]
        public void TheChildrenSelector_WinsOverTheNodesOwnChildren()
        {
            TreeViewNode a = Expanded("A", Node("ignored"));
            TreeView tree = CreateTree(a);

            tree.ChildrenSelector = x => ReferenceEquals(x, a) ? new[] { new TreeViewNode("chosen") } : null;

            Assert.Equal(["A", "chosen"], Visible(tree));
        }

        [Fact]
        public void AHierarchicalTemplate_GivesTheChildren()
        {
            var template = (HierarchicalDataTemplate)LoadDataTemplate(
                """<HierarchicalDataTemplate ChildrenPath="Kids"><Border Height="20"/></HierarchicalDataTemplate>""");
            var leaf = new Folder("leaf");
            var root = new Folder("root", leaf);
            var tree = new TreeView { ItemTemplate = template, ItemsSource = new[] { root } };

            tree.Expand(root);

            Assert.Equal(["root", "leaf"], Visible(tree));
        }

        [Fact]
        public void PlainEnumerableChildren_Work_AndStringsAreNotChildren()
        {
            // A plain array (not observable) holds the children; strings would be IEnumerable<char> but never count.
            object[] numbers = ["2", "3"];
            var tree = new TreeView
            {
                ItemTemplate = RowTemplate(),
                ItemsSource = new object[] { "text", numbers },
                ChildrenSelector = x => x as IEnumerable,
            };

            tree.Expand(numbers);

            Assert.Equal(4, tree.Rows.Count);
            Assert.Equal(["2", "3"], Visible(tree)[2..]);
            Assert.False(tree.HasChildren("text"));
        }

        [Fact]
        public void ACycle_IsShownOnce_AndNeverLoops()
        {
            var loop = new Folder("loop");
            loop.Kids.Add(loop);
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { loop }, ChildrenSelector = x => ((Folder)x).Kids };

            tree.Expand(loop);

            Assert.Equal(["loop", "loop"], Visible(tree));
        }

        [Fact]
        public void ADuplicatedItem_ExpandsEverywhere()
        {
            TreeViewNode shared = Node("S", Node("leaf"));
            TreeView tree = CreateTree(Expanded("A", shared), Expanded("B", shared));

            tree.Expand(shared);

            Assert.Equal(["A", "S", "leaf", "B", "S", "leaf"], Visible(tree));
        }

        [Fact]
        public void SelectabilityComesFromThePredicate_ThenTheNode()
        {
            TreeViewNode a = Node("A");
            a.IsSelectable = false;
            TreeViewNode b = Node("B");
            TreeView tree = CreateTree(a, b);

            Assert.False(tree.IsSelectable(a));
            Assert.True(tree.IsSelectable(b));

            tree.IsItemSelectable = x => ReferenceEquals(x, a);
            Assert.True(tree.IsSelectable(a));
            Assert.False(tree.IsSelectable(b));
        }

        [Fact]
        public void ItemsSourceAndInlineItems_CannotBothBeUsed()
        {
            TreeView inline = CreateTree(Node("A"));
            Assert.Throws<InvalidOperationException>(() => inline.ItemsSource = new[] { "x" });

            var bound = new TreeView { ItemsSource = new[] { "x" } };
            Assert.Throws<InvalidOperationException>(() => bound.Items.Add(Node("A")));
        }

        [Fact]
        public void RealizedRows_AreStamped()
        {
            TreeView tree = CreateTree(Expanded("A", Node("B", Node("C"))));
            Layout(tree);

            TreeViewItem a = Row(tree, 0), b = Row(tree, 1);
            Assert.Equal((0, true, true), (a.Depth, a.HasChildren, a.IsExpanded));
            Assert.Equal((1, true, false), (b.Depth, b.HasChildren, b.IsExpanded));
            Assert.Equal(16f, b.IndentWidth);

            tree.Indent = 10;
            Assert.Equal(10f, Row(tree, 1).IndentWidth);
        }

        [Fact]
        public void ExpandingAVisibleRow_RestampsItsChevron()
        {
            TreeViewNode a = Node("A", Node("B"));
            TreeView tree = CreateTree(a);
            Layout(tree);

            tree.Expand(a);

            Assert.True(Row(tree, 0).IsExpanded);
        }

        [Fact]
        public void RowContent_IsBoundToTheItem_NotTheRow()
        {
            TreeViewNode a = Node("A");
            TreeView tree = CreateTree(a);
            Layout(tree);

            Assert.Same(a, Row(tree, 0).Content!.DataContext);
        }

        private sealed class Folder(string name, params Folder[] kids)
        {
            public ObservableCollection<Folder> Kids { get; } = [.. kids];

            public override string ToString() => name;
        }
    }
}
```

- [ ] **Step 3: Run them to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewFlatteningTests"`
Expected: build FAILS (`TreeView` not found).

- [ ] **Step 4: Create `FlatRow` and `TreeViewItemEventArgs`**

`sources/IcyUI/UI/Controls/FlatRow.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// One visible row of a <see cref="TreeView"/>: a data item at a depth, under its parent row.
    /// </summary>
    /// <param name="item">The data item.</param>
    /// <param name="depth">The depth: <c>0</c> for a root.</param>
    /// <param name="parent">The parent row, or <see langword="null"/> for a root.</param>
    internal sealed class FlatRow(object item, int depth, FlatRow? parent)
    {
        /// <summary>Gets the data item.</summary>
        public object Item { get; } = item;

        /// <summary>Gets the depth: <c>0</c> for a root.</summary>
        public int Depth { get; } = depth;

        /// <summary>Gets the parent row, or <see langword="null"/> for a root.</summary>
        public FlatRow? Parent { get; } = parent;

        /// <inheritdoc/>
        public override string ToString() => Item.ToString() ?? string.Empty;
    }
}
```

`sources/IcyUI/UI/Controls/TreeViewItemEventArgs.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// Carries the data item of a <see cref="TreeView.ItemExpanded"/> or <see cref="TreeView.ItemCollapsed"/> event.
    /// </summary>
    /// <param name="item">The item that was expanded or collapsed.</param>
    public sealed class TreeViewItemEventArgs(object item) : EventArgs
    {
        /// <summary>Gets the item that was expanded or collapsed.</summary>
        public object Item { get; } = item;
    }
}
```

- [ ] **Step 5: Create `TreeViewList`**

`sources/IcyUI/UI/Controls/TreeViewList.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// The virtualizing list inside a <see cref="TreeView"/>: shows the tree's visible rows (<see cref="FlatRow"/>s) as
    /// <see cref="TreeViewItem"/>s and hands taps back to the tree.
    /// </summary>
    /// <remarks>
    /// Its items are rows, but each row's content is built from and bound to the row's <em>data item</em>, so templates
    /// and <c>{Binding}</c> see the same object they would in a <see cref="ListBox"/>.
    /// </remarks>
    internal sealed class TreeViewList : SelectingItemsControl
    {
        private readonly TreeView owner;
        private readonly Dictionary<TreeViewItem, FlatRow> rowsByContainer = [];
        private int currentIndex = -1;

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeViewList"/> class.
        /// </summary>
        /// <param name="owner">The tree this list belongs to.</param>
        public TreeViewList(TreeView owner)
        {
            this.owner = owner;
            ItemTemplateSelector = row => owner.ResolveTemplate(((FlatRow)row).Item);
        }

        /// <summary>Gets the realized containers by row index.</summary>
        internal IReadOnlyDictionary<int, ItemContainer> Realized => realizedContainers;

        /// <summary>
        /// Gets or sets the index of the keyboard-current row, shown through <see cref="SelectorItem.IsHighlighted"/>;
        /// <c>-1</c> for none.
        /// </summary>
        internal int CurrentIndex
        {
            get => currentIndex;
            set
            {
                if (currentIndex == value)
                    return;

                if (realizedContainers.TryGetValue(currentIndex, out ItemContainer? old))
                    ((SelectorItem)old).IsHighlighted = false;
                currentIndex = value;
                if (realizedContainers.TryGetValue(currentIndex, out ItemContainer? current))
                    ((SelectorItem)current).IsHighlighted = true;
            }
        }

        /// <summary>Re-stamps the realized container of <paramref name="row"/>, if there is one.</summary>
        /// <param name="row">The row whose state changed.</param>
        internal void Restamp(FlatRow row)
        {
            foreach ((TreeViewItem container, FlatRow shown) in rowsByContainer)
            {
                if (ReferenceEquals(shown, row))
                    owner.Stamp(container, shown);
            }
        }

        /// <summary>Re-stamps every realized container.</summary>
        internal void RestampAll()
        {
            foreach ((TreeViewItem container, FlatRow shown) in rowsByContainer)
                owner.Stamp(container, shown);
        }

        /// <inheritdoc/>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
            => new TreeViewItem { Content = template.Build(((FlatRow)item).Item) };

        /// <inheritdoc/>
        /// <remarks>
        /// Re-stamps everything row-dependent: a pooled container arrives with its content bound to the
        /// <see cref="FlatRow"/> (the base only rebinds <see cref="UIElement.DataContext"/> to the list item) and with the
        /// previous row's depth, chevron and highlight.
        /// </remarks>
        protected override void AttachContainer(ItemContainer container, int index)
        {
            base.AttachContainer(container, index);
            var item = (TreeViewItem)container;
            var row = (FlatRow)GetItemAt(index);
            if (item.Content != null)
                item.Content.DataContext = row.Item;
            rowsByContainer[item] = row;
            item.ExpanderTapped += Item_ExpanderTapped;
            item.IsHighlighted = index == currentIndex;
            owner.Stamp(item, row);
        }

        /// <inheritdoc/>
        protected override void DetachContainer(ItemContainer container)
        {
            var item = (TreeViewItem)container;
            item.ExpanderTapped -= Item_ExpanderTapped;
            rowsByContainer.Remove(item);
            base.DetachContainer(container);
        }

        /// <inheritdoc/>
        protected override void OnContainerTapped(int index) => owner.OnRowTapped((FlatRow)GetItemAt(index));

        /// <inheritdoc/>
        /// <remarks>The tree owns selection by data item and maps it back to a row index after every change.</remarks>
        protected override void OnItemsChanged() => owner.OnRowsChanged();

        private void Item_ExpanderTapped(object? sender, EventArgs e)
        {
            if (sender is TreeViewItem item && rowsByContainer.TryGetValue(item, out FlatRow? row))
                owner.OnExpanderTapped(row);
        }
    }
}
```

- [ ] **Step 6: Create `TreeView` (the structure part)**

`sources/IcyUI/UI/Controls/TreeView.cs`. The members marked *(Task 5)*, *(Task 6)* and *(Task 7)* are added by those tasks. Don't add them yet.

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using Icy.Data;
using Icy.Data.Markup.Attributes;
using Icy.Markup;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Shows hierarchical data as an indented, expandable list. Only visible rows are realized, however large the tree.
    /// </summary>
    /// <remarks>
    /// <para>Where the roots come from: <see cref="ItemsSource"/>, or the inline <see cref="Items"/> (not both).</para>
    /// <para>Where an item's children come from (the first configured source wins):</para>
    /// <list type="number">
    /// <item><description><see cref="ChildrenSelector"/>, when set;</description></item>
    /// <item><description>otherwise the <see cref="HierarchicalDataTemplate.ChildrenPath"/> of the item's template;</description></item>
    /// <item><description>otherwise <see cref="TreeViewNode.Children"/>, for a <see cref="TreeViewNode"/>;</description></item>
    /// <item><description>otherwise none.</description></item>
    /// </list>
    /// <para>
    /// Expansion state belongs to the tree, keyed by item reference. For a <see cref="TreeViewNode"/> it's the node's own
    /// <see cref="TreeViewNode.IsExpanded"/>. An item that appears under two parents is expanded in both places.
    /// </para>
    /// <para>
    /// Internally the tree keeps the visible rows as a flat list. Expanding or collapsing an item inserts or removes its
    /// visible subtree as one change, and only the rows on screen have containers (see <see cref="ItemsControl"/>).
    /// <see langword="null"/> items aren't supported.
    /// </para>
    /// </remarks>
    [ContentProperty(nameof(Items))]
    public class TreeView : Control
    {
        private readonly TreeViewList list;
        private readonly ScrollViewer scrollViewer;
        private readonly RangeObservableCollection<FlatRow> rows = [];
        private readonly HashSet<object> expanded = new(ReferenceEqualityComparer.Instance);
        private IEnumerable? itemsSource;
        private Func<object, IEnumerable?>? childrenSelector;
        private Func<object, bool>? isItemSelectable;
        private DataTemplate? itemTemplate;
        private Func<object, DataTemplate>? itemTemplateSelector;
        private float indent = 16;
        private bool settingNodeState;

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeView"/> class.
        /// </summary>
        public TreeView()
        {
            IsFocusable = true;
            list = new TreeViewList(this) { ItemsSource = rows };
            scrollViewer = new ScrollViewer { Content = list };
            ((Border)Chrome).Child = scrollViewer;
            Items.CollectionChanged += Items_CollectionChanged;

            // Starts following the (empty) inline Items right away, so markup-only trees populate without an attach.
            Rebuild();
        }

        /// <summary>Occurs when an item is expanded, through the API, the UI, or a visible <see cref="TreeViewNode"/>'s <see cref="TreeViewNode.IsExpanded"/>.</summary>
        public event EventHandler<TreeViewItemEventArgs>? ItemExpanded;

        /// <summary>Occurs when an item is collapsed, through the API, the UI, or a visible <see cref="TreeViewNode"/>'s <see cref="TreeViewNode.IsExpanded"/>.</summary>
        public event EventHandler<TreeViewItemEventArgs>? ItemCollapsed;

        /// <summary>
        /// Gets the inline root items, the markup content of the tree. Used when <see cref="ItemsSource"/> isn't set.
        /// </summary>
        /// <exception cref="InvalidOperationException">An item is added while <see cref="ItemsSource"/> is set.</exception>
        public ObservableCollection<object> Items { get; } = [];

        /// <summary>
        /// Gets or sets the root items. An <see cref="INotifyCollectionChanged"/> source is followed live.
        /// </summary>
        /// <exception cref="InvalidOperationException">A source is set while <see cref="Items"/> isn't empty.</exception>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public IEnumerable? ItemsSource
        {
            get => itemsSource;
            set
            {
                if (value != null && Items.Count > 0)
                    throw new InvalidOperationException($"A {nameof(TreeView)} takes its roots from either '{nameof(ItemsSource)}' or inline '{nameof(Items)}', not both.");
                if (SetProperty(ref itemsSource, value))
                    Rebuild();
            }
        }

        /// <summary>
        /// Gets or sets a function that returns an item's children, or <see langword="null"/> for none. When set, it's the
        /// only source of children.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public Func<object, IEnumerable?>? ChildrenSelector
        {
            get => childrenSelector;
            set
            {
                if (SetProperty(ref childrenSelector, value))
                    Rebuild();
            }
        }

        /// <summary>
        /// Gets or sets a function that decides whether an item can be selected. When unset, a <see cref="TreeViewNode"/>
        /// uses its <see cref="TreeViewNode.IsSelectable"/> and every other item is selectable.
        /// </summary>
        /// <remarks>Tapping a non-selectable item with children expands or collapses it instead.</remarks>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public Func<object, bool>? IsItemSelectable
        {
            get => isItemSelectable;
            set
            {
                if (SetProperty(ref isItemSelectable, value))
                    list.RestampAll();
            }
        }

        /// <summary>
        /// Gets or sets the template for each row's content. A <see cref="HierarchicalDataTemplate"/> also supplies children.
        /// Without one, a row shows its item's text.
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
                    Rebuild();
            }
        }

        /// <summary>Gets or sets a function that picks a template per item, overriding <see cref="ItemTemplate"/>.</summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public Func<object, DataTemplate>? ItemTemplateSelector
        {
            get => itemTemplateSelector;
            set
            {
                if (SetProperty(ref itemTemplateSelector, value))
                    Rebuild();
            }
        }

        /// <summary>Gets or sets how far each level is indented, in layout units. Defaults to <c>16</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        [Category("Layout")]
        [DefaultValue(16f)]
        [RegisterReference]
        public float Indent
        {
            get => indent;
            set
            {
                Guard.IsGreaterThanOrEqualTo(value, 0f);
                if (SetProperty(ref indent, value))
                    list.RestampAll();
            }
        }

        /// <summary>Gets the visible rows, top to bottom.</summary>
        internal IReadOnlyList<FlatRow> Rows => rows;

        /// <summary>Gets the inner row list.</summary>
        internal TreeViewList List => list;

        /// <summary>Gets the inner scroll viewer.</summary>
        internal ScrollViewer ScrollViewer => scrollViewer;

        private IEnumerable Roots => itemsSource ?? Items;

        /// <summary>
        /// Gets a value indicating whether <paramref name="item"/> is expanded.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><see langword="true"/> when the item shows its children (or would, once visible).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public bool IsExpanded(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return item is TreeViewNode node ? node.IsExpanded : expanded.Contains(item);
        }

        /// <summary>
        /// Expands <paramref name="item"/>. Every visible row of it shows its children. An item that isn't visible keeps the
        /// state for when it becomes visible.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public void Expand(object item) => SetExpanded(item, true);

        /// <summary>
        /// Collapses <paramref name="item"/>. Its descendants keep their own expansion, so expanding it again restores the
        /// subtree as it was.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public void Collapse(object item) => SetExpanded(item, false);

        /// <summary>Gets <paramref name="item"/>'s children, following the resolution order in the class remarks.</summary>
        /// <param name="item">The item.</param>
        /// <returns>The children, or <see langword="null"/> for none. Never a <see cref="string"/>.</returns>
        internal IEnumerable? GetChildren(object item)
        {
            IEnumerable? children = childrenSelector != null
                ? childrenSelector(item)
                : ResolveTemplate(item) is HierarchicalDataTemplate hierarchical
                    ? hierarchical.GetChildren(item)
                    : (item as TreeViewNode)?.Children;
            return children is string ? null : children;
        }

        /// <summary>Gets a value indicating whether <paramref name="item"/> has at least one child.</summary>
        /// <param name="item">The item.</param>
        /// <returns><see langword="true"/> when the item has children.</returns>
        internal bool HasChildren(object item) => GetChildren(item) switch
        {
            null => false,
            ICollection collection => collection.Count > 0,
            IEnumerable enumerable => enumerable.Cast<object>().Any(),
        };

        /// <summary>Gets a value indicating whether <paramref name="item"/> can be selected.</summary>
        /// <param name="item">The item.</param>
        /// <returns><see langword="true"/> when selectable.</returns>
        internal bool IsSelectable(object item) => isItemSelectable?.Invoke(item) ?? (item as TreeViewNode)?.IsSelectable ?? true;

        /// <summary>Resolves the content template for <paramref name="item"/>.</summary>
        /// <param name="item">The item.</param>
        /// <returns>The selector's template, else <see cref="ItemTemplate"/>, else the built-in text template.</returns>
        internal DataTemplate ResolveTemplate(object item) => itemTemplateSelector?.Invoke(item) ?? itemTemplate ?? DataTemplate.Default;

        /// <summary>Stamps <paramref name="container"/> with <paramref name="row"/>'s state.</summary>
        /// <param name="container">The realized container.</param>
        /// <param name="row">The row it shows.</param>
        internal void Stamp(TreeViewItem container, FlatRow row) =>
            container.Update(row.Depth, row.Depth * indent, HasChildren(row.Item), IsExpanded(row.Item), IsSelectable(row.Item));

        /// <summary>Handles a tap on a row (outside its chevron).</summary>
        /// <param name="row">The tapped row.</param>
        internal void OnRowTapped(FlatRow row)
        {
            if (!IsSelectable(row.Item) && HasChildren(row.Item))
                Toggle(row.Item);
        }

        /// <summary>Handles a tap on a row's chevron.</summary>
        /// <param name="row">The row.</param>
        internal void OnExpanderTapped(FlatRow row) => Toggle(row.Item);

        /// <summary>Called by the list after every change to the rows.</summary>
        internal void OnRowsChanged() => SyncList();

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            Rebuild();
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (itemsSource != null)
                throw new InvalidOperationException($"A {nameof(TreeView)} takes its roots from either '{nameof(ItemsSource)}' or inline '{nameof(Items)}', not both.");
            Rebuild();
        }

        private void Rebuild()
        {
            var built = new List<FlatRow>();
            foreach (object item in Roots)
                AppendVisible(item, 0, null, built);
            rows.ResetTo(built);
        }

        /// <summary>Appends <paramref name="item"/>'s row and, while expanded, its visible descendants.</summary>
        private void AppendVisible(object item, int depth, FlatRow? parent, List<FlatRow> into)
        {
            var row = new FlatRow(item, depth, parent);
            into.Add(row);

            // An item that is its own ancestor (a cycle in the data) is shown, but never expanded again.
            if (IsExpanded(item) && !HasAncestor(parent, item) && GetChildren(item) is { } children)
            {
                foreach (object child in children)
                    AppendVisible(child, depth + 1, row, into);
            }
        }

        private static bool HasAncestor(FlatRow? row, object item)
        {
            for (; row != null; row = row.Parent)
            {
                if (ReferenceEquals(row.Item, item))
                    return true;
            }

            return false;
        }

        private void InsertRows(int index, List<FlatRow> range) => rows.InsertRange(index, range);

        private void RemoveRows(int index, int count, bool fromData) => rows.RemoveRange(index, count);

        /// <summary>Counts the rows below <paramref name="index"/> that are deeper than it: its visible subtree.</summary>
        private int DescendantCount(int index)
        {
            int depth = rows[index].Depth;
            int end = index + 1;
            while (end < rows.Count && rows[end].Depth > depth)
                end++;
            return end - index - 1;
        }

        private int IndexOfVisible(object item)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (ReferenceEquals(rows[i].Item, item))
                    return i;
            }

            return -1;
        }

        private void SyncList()
        {
        }

        private void Toggle(object item) => SetExpanded(item, !IsExpanded(item));

        private void SetExpanded(object item, bool value)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (IsExpanded(item) == value)
                return;

            if (item is TreeViewNode node)
            {
                // The node raises PropertyChanged; the tree's own node handler (Task 6) ignores changes it makes itself.
                settingNodeState = true;
                try
                {
                    node.IsExpanded = value;
                }
                finally
                {
                    settingNodeState = false;
                }
            }
            else if (value)
            {
                expanded.Add(item);
            }
            else
            {
                expanded.Remove(item);
            }

            ApplyExpansion(item, value);
        }

        /// <summary>Shows or hides the subtree of every visible row of <paramref name="item"/>, then raises the event.</summary>
        private void ApplyExpansion(object item, bool value)
        {
            foreach (FlatRow row in rows.Where(r => ReferenceEquals(r.Item, item)).ToList())
            {
                int index = rows.IndexOf(row);
                if (index < 0)
                    continue;

                if (value)
                {
                    var subtree = new List<FlatRow>();
                    if (!HasAncestor(row.Parent, item) && GetChildren(item) is { } children)
                    {
                        foreach (object child in children)
                            AppendVisible(child, row.Depth + 1, row, subtree);
                    }

                    InsertRows(index + 1, subtree);
                }
                else
                {
                    RemoveRows(index + 1, DescendantCount(index), fromData: false);
                }

                list.Restamp(row);
            }

            (value ? ItemExpanded : ItemCollapsed)?.Invoke(this, new TreeViewItemEventArgs(item));
        }
    }
}
```

`settingNodeState` is assigned in Task 4 but only read in Task 6. If the compiler warns `CS0414` (assigned but never used), suppress nothing. The warning goes away in Task 6.

Make `DataTemplate.Default` reachable: it's `internal static` in the same assembly, so no change is needed.

- [ ] **Step 7: Add the `TreeView` theme line if it was deferred**

If Task 3 deferred `<Style TargetType="TreeView" .../>`, add it now after the `TreeViewItem` style.

- [ ] **Step 8: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewFlatteningTests"`
Expected: PASS (16 tests). Then run the full suite and check `Total:`.

- [ ] **Step 9: Commit**

```bash
git add sources/IcyUI/UI/Controls/FlatRow.cs sources/IcyUI/UI/Controls/TreeViewItemEventArgs.cs sources/IcyUI/UI/Controls/TreeViewList.cs sources/IcyUI/UI/Controls/TreeView.cs sources/IcyUI/Resources/Themes/DefaultTheme.xml sources/IcyUI.Tests/Controls/TreeViewTestKit.cs sources/IcyUI.Tests/Controls/TreeViewFlatteningTests.cs
git commit -m "Add TreeView with flattened, virtualized rows and expansion

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Selection, taps and `Reveal`

**Files:**
- Modify: `sources/IcyUI/UI/Controls/TreeView.cs`
- Test: `sources/IcyUI.Tests/Controls/TreeViewSelectionTests.cs`

**Interfaces:**
- Consumes: Task 4's `TreeView` internals (`rows`, `list`, `IndexOfVisible`, `SetExpanded`, `Roots`, `GetChildren`).
- Produces:
  - Public: `object? SelectedItem`, `event EventHandler? SelectionChanged`, `bool Reveal(object item)`.
  - Private: `object? selectedItem`, `FlatRow? currentRow`, `void Select(object? item, bool reveal)`, `List<object>? FindPath(object target)`, `void ClearSelectionIfGone()`.
  - Filled-in `SyncList()`, `OnRowTapped`, `OnExpanderTapped` and `OnRowsChanged`.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Controls/TreeViewSelectionTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewSelectionTests
    {
        [Fact]
        public void SelectingAVisibleItem_SelectsItsRow()
        {
            TreeViewNode b = Node("B");
            TreeView tree = CreateTree(Expanded("A", b));
            Layout(tree);
            int changes = 0;
            tree.SelectionChanged += (_, _) => changes++;

            tree.SelectedItem = b;

            Assert.Same(b, tree.SelectedItem);
            Assert.Equal(1, tree.List.SelectedIndex);
            Assert.True(Row(tree, 1).IsSelected);
            Assert.Equal(1, changes);
        }

        [Fact]
        public void SelectingAHiddenItem_ExpandsItsAncestors()
        {
            TreeViewNode c = Node("C");
            TreeViewNode a = Node("A", Node("B", c));
            TreeView tree = CreateTree(a);

            tree.SelectedItem = c;

            Assert.Equal(["A", "B", "C"], Visible(tree));
            Assert.Same(c, tree.SelectedItem);
            Assert.Equal(2, tree.List.SelectedIndex);
        }

        [Fact]
        public void SelectingAnUnknownOrNonSelectableItem_ClearsTheSelection()
        {
            TreeViewNode a = Node("A");
            TreeViewNode locked = Node("L");
            locked.IsSelectable = false;
            TreeView tree = CreateTree(a, locked);
            tree.SelectedItem = a;

            tree.SelectedItem = locked;
            Assert.Null(tree.SelectedItem);

            tree.SelectedItem = a;
            tree.SelectedItem = Node("stranger");
            Assert.Null(tree.SelectedItem);
            Assert.Equal(-1, tree.List.SelectedIndex);
        }

        [Fact]
        public void Reveal_ReportsWhetherTheItemWasFound()
        {
            TreeViewNode c = Node("C");
            TreeView tree = CreateTree(Node("A", Node("B", c)));

            Assert.True(tree.Reveal(c));
            Assert.False(tree.Reveal(Node("stranger")));
        }

        [Fact]
        public void Reveal_TerminatesOnACycle()
        {
            var root = new object();
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ReferenceEquals(x, root) ? new[] { root } : null };

            Assert.False(tree.Reveal(new object()));
            Assert.True(tree.Reveal(root));
        }

        [Fact]
        public void Reveal_ScrollsADeepItemIntoView()
        {
            var roots = Enumerable.Range(0, 60).Select(i => Node($"R{i}")).ToArray();
            TreeViewNode deep = Node("deep");
            roots[59].Children.Add(deep);
            TreeView tree = CreateTree(roots);
            Layout(tree);

            tree.Reveal(deep);
            Layout(tree);

            Assert.True(tree.ScrollViewer.VerticalOffset > 0);
            Assert.Contains(60, tree.List.Realized.Keys);
        }

        [Fact]
        public void CollapsingAnAncestor_KeepsTheSelection()
        {
            TreeViewNode b = Node("B");
            TreeViewNode a = Expanded("A", b);
            TreeView tree = CreateTree(a);
            tree.SelectedItem = b;

            tree.Collapse(a);
            Assert.Same(b, tree.SelectedItem);
            Assert.Equal(-1, tree.List.SelectedIndex);

            tree.Expand(a);
            Assert.Equal(1, tree.List.SelectedIndex);
        }

        [Fact]
        public void RowsInsertedAbove_KeepTheSameItemSelected()
        {
            TreeViewNode a = Node("A", Node("A1"), Node("A2"));
            TreeViewNode b = Node("B");
            TreeView tree = CreateTree(a, b);
            tree.SelectedItem = b;

            tree.Expand(a);

            Assert.Same(b, tree.SelectedItem);
            Assert.Equal(3, tree.List.SelectedIndex);
        }

        [Fact]
        public void RetargetingItemsSource_ClearsASelectionThatIsGone()
        {
            var kept = new object();
            var lost = new object();
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { kept, lost } };

            tree.SelectedItem = kept;
            tree.ItemsSource = new[] { kept };
            Assert.Same(kept, tree.SelectedItem);

            tree.SelectedItem = kept;
            tree.ItemsSource = new[] { lost };
            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void TappingASelectableRow_SelectsIt()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            TreeViewNode b = Node("B");
            TreeView tree = CreateTree(Node("A"), b);
            Host(canvas, tree);

            Tap(input, Row(tree, 1));

            Assert.Same(b, tree.SelectedItem);
        }

        [Fact]
        public void TappingANonSelectableBranch_TogglesIt_WithoutSelecting()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            TreeViewNode category = Node("Cat", Node("leaf"));
            category.IsSelectable = false;
            TreeView tree = CreateTree(category);
            Host(canvas, tree);

            Tap(input, Row(tree, 0));

            Assert.True(category.IsExpanded);
            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void TappingANonSelectableLeaf_DoesNothing()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas();
            TreeViewNode leaf = Node("leaf");
            leaf.IsSelectable = false;
            TreeView tree = CreateTree(leaf);
            Host(canvas, tree);

            Tap(input, Row(tree, 0));

            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void TappingTheChevron_TogglesWithoutSelecting()
        {
            (Canvas canvas, FakeInputSystem input) = CreateCanvas(themed: true);
            TreeViewNode a = Node("A", Node("B"));
            TreeView tree = CreateTree(a);
            Host(canvas, tree);

            Tap(input, Row(tree, 0).Expander!);

            Assert.True(a.IsExpanded);
            Assert.Null(tree.SelectedItem);
        }
    }
}
```

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewSelectionTests"`
Expected: build FAILS (`SelectedItem`, `SelectionChanged`, `Reveal` not found).

- [ ] **Step 3: Add the selection members to `TreeView`**

Add the fields next to the others:

```csharp
        private object? selectedItem;
        private FlatRow? currentRow;
```

Add the event after `ItemCollapsed`:

```csharp
        /// <summary>Occurs when <see cref="SelectedItem"/> changes.</summary>
        public event EventHandler? SelectionChanged;
```

Add the property after `Indent`:

```csharp
        /// <summary>
        /// Gets or sets the selected data item, or <see langword="null"/> for none.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Setting a selectable item reveals it (see <see cref="Reveal(object)"/>) and selects it.</description></item>
        /// <item><description>Setting an item that isn't in the tree, or can't be selected, clears the selection.</description></item>
        /// <item><description>Collapsing an ancestor keeps the selection; removing the item from the data clears it.</description></item>
        /// </list>
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? SelectedItem
        {
            get => selectedItem;
            set => Select(value, reveal: true);
        }
```

Add `Reveal` after `Collapse`:

```csharp
        /// <summary>
        /// Makes <paramref name="item"/> visible: expands every ancestor on its path from a root, then scrolls its row into
        /// view.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><see langword="false"/> when the item isn't anywhere in the tree.</returns>
        /// <remarks>
        /// An item that is already visible only scrolls. Otherwise the tree searches its data depth-first through child
        /// resolution, which is O(n) in the number of items; the search never revisits an item, so cyclic data terminates.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public bool Reveal(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            int index = IndexOfVisible(item);
            if (index < 0)
            {
                if (FindPath(item) is not { } path)
                    return false;

                for (int i = 0; i < path.Count - 1; i++)
                    Expand(path[i]);
                index = IndexOfVisible(item);
                if (index < 0)
                    return false;
            }

            list.ScrollIntoView(index);
            return true;
        }
```

- [ ] **Step 4: Fill in taps, row changes and syncing**

Replace `OnRowTapped`, `OnExpanderTapped`, `OnRowsChanged` and `SyncList` with:

```csharp
        /// <summary>Handles a tap on a row (outside its chevron): selects it, or toggles a non-selectable branch.</summary>
        /// <param name="row">The tapped row.</param>
        internal void OnRowTapped(FlatRow row)
        {
            currentRow = row;
            if (IsSelectable(row.Item))
                Select(row.Item, reveal: false);
            else if (HasChildren(row.Item))
                Toggle(row.Item);
            SyncList();
        }

        /// <summary>Handles a tap on a row's chevron: toggles it without selecting.</summary>
        /// <param name="row">The row.</param>
        internal void OnExpanderTapped(FlatRow row)
        {
            currentRow = row;
            Toggle(row.Item);
            SyncList();
        }

        /// <summary>
        /// Called by the list after every change to the rows: moves a current row that disappeared to its nearest visible
        /// ancestor, and maps the selection and current row back to row indices.
        /// </summary>
        internal void OnRowsChanged()
        {
            if (currentRow != null && rows.IndexOf(currentRow) < 0)
            {
                FlatRow? ancestor = currentRow.Parent;
                while (ancestor != null && rows.IndexOf(ancestor) < 0)
                    ancestor = ancestor.Parent;
                currentRow = ancestor;
            }

            SyncList();
        }
```

```csharp
        private void SyncList()
        {
            int selected = selectedItem == null ? -1 : IndexOfVisible(selectedItem);
            if (list.SelectedIndex != selected)
                list.SelectedIndex = selected;
            list.CurrentIndex = IsFocused && currentRow != null ? rows.IndexOf(currentRow) : -1;
        }

        private void Select(object? item, bool reveal)
        {
            if (item != null && (!IsSelectable(item) || !(reveal ? Reveal(item) : IndexOfVisible(item) >= 0)))
                item = null;

            if (SetProperty(ref selectedItem, item, nameof(SelectedItem)))
            {
                SyncList();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                SyncList();
            }
        }

        /// <summary>Finds the path from a root to <paramref name="target"/>, depth-first through child resolution.</summary>
        /// <returns>The items from the root to the target inclusive, or <see langword="null"/> when it isn't in the tree.</returns>
        private List<object>? FindPath(object target)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var path = new List<object>();
            foreach (object root in Roots)
            {
                if (Search(root))
                    return path;
            }

            return null;

            bool Search(object item)
            {
                if (!visited.Add(item))
                    return false;

                path.Add(item);
                if (ReferenceEquals(item, target))
                    return true;
                if (GetChildren(item) is { } children)
                {
                    foreach (object child in children)
                    {
                        if (Search(child))
                            return true;
                    }
                }

                path.RemoveAt(path.Count - 1);
                return false;
            }
        }

        /// <summary>Clears the selection when its item is no longer anywhere in the tree's data.</summary>
        private void ClearSelectionIfGone()
        {
            if (selectedItem != null && IndexOfVisible(selectedItem) < 0 && FindPath(selectedItem) == null)
                Select(null, reveal: false);
        }
```

If `SetProperty` doesn't accept an explicit property name as its third argument, check its signature in `UIElement` and pass the name the way other controls do.

- [ ] **Step 5: Clear a vanished selection on rebuild, and keep it when selectability changes**

At the end of `Rebuild()`, after `rows.ResetTo(built);`, add:

```csharp
            ClearSelectionIfGone();
```

In the `IsItemSelectable` setter, change the body to:

```csharp
                if (SetProperty(ref isItemSelectable, value))
                {
                    list.RestampAll();
                    if (selectedItem != null && !IsSelectable(selectedItem))
                        Select(null, reveal: false);
                }
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeView"`
Expected: PASS (Tasks 3–5).

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/UI/Controls/TreeView.cs sources/IcyUI.Tests/Controls/TreeViewSelectionTests.cs
git commit -m "Add TreeView selection by data item, taps and Reveal

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Live data

**Files:**
- Modify: `sources/IcyUI/UI/Controls/TreeView.cs`
- Test: `sources/IcyUI.Tests/Controls/TreeViewLiveDataTests.cs`

**Interfaces:**
- Consumes: Task 4/5 helpers (`InsertRows`, `RemoveRows`, `DescendantCount`, `AppendVisible`, `ApplyExpansion`, `ClearSelectionIfGone`, `settingNodeState`).
- Produces: ref-counted subscriptions per **visible item**, both to its children collection (when `INotifyCollectionChanged`) and to `TreeViewNode.PropertyChanged`, plus a subscription to the roots collection. All of them are removed on removal, rebuild and detach.

- [ ] **Step 1: Write the failing tests**

`sources/IcyUI.Tests/Controls/TreeViewLiveDataTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewLiveDataTests
    {
        [Fact]
        public void AnAddedChild_LandsAfterItsPrecedingSiblingsSubtree()
        {
            TreeViewNode a = Expanded("A", Expanded("B", Node("B1")), Node("C"));
            TreeView tree = CreateTree(a, Node("Z"));

            a.Children.Insert(1, Node("new"));

            Assert.Equal(["A", "B", "B1", "new", "C", "Z"], Visible(tree));
            Assert.Equal(1, tree.Rows[3].Depth);
        }

        [Fact]
        public void ARemovedChild_TakesItsVisibleSubtree()
        {
            TreeViewNode a = Expanded("A", Expanded("B", Node("B1")), Node("C"));
            TreeView tree = CreateTree(a);

            a.Children.RemoveAt(0);

            Assert.Equal(["A", "C"], Visible(tree));
        }

        [Fact]
        public void ReplaceMoveAndReset_AreFollowed()
        {
            TreeViewNode a = Expanded("A", Node("B"), Expanded("C", Node("C1")), Node("D"));
            TreeView tree = CreateTree(a);

            a.Children.Move(1, 2);
            Assert.Equal(["A", "B", "D", "C", "C1"], Visible(tree));

            a.Children[0] = Node("B2");
            Assert.Equal(["A", "B2", "D", "C", "C1"], Visible(tree));

            a.Children.Clear();
            Assert.Equal(["A"], Visible(tree));
        }

        [Fact]
        public void RootChanges_AreFollowed()
        {
            var roots = new ObservableCollection<TreeViewNode> { Node("A"), Node("C") };
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = roots };

            roots.Insert(1, Node("B"));
            roots.RemoveAt(0);

            Assert.Equal(["B", "C"], Visible(tree));
        }

        [Fact]
        public void InlineItemChanges_AreFollowed()
        {
            TreeView tree = CreateTree(Node("A"));

            tree.Items.Add(Node("B"));

            Assert.Equal(["A", "B"], Visible(tree));
        }

        [Fact]
        public void AChildAddedToACollapsedRow_ShowsItsChevron()
        {
            TreeViewNode a = Node("A");
            TreeView tree = CreateTree(a);
            Layout(tree);
            Assert.False(Row(tree, 0).HasChildren);

            a.Children.Add(Node("B"));

            Assert.True(Row(tree, 0).HasChildren);
            Assert.Equal(["A"], Visible(tree));
        }

        [Fact]
        public void SettingANodesIsExpanded_ExpandsItsRows_AndRaisesTheEvent()
        {
            TreeViewNode a = Node("A", Node("B"));
            TreeView tree = CreateTree(a);
            int expandedEvents = 0;
            tree.ItemExpanded += (_, _) => expandedEvents++;

            a.IsExpanded = true;

            Assert.Equal(["A", "B"], Visible(tree));
            Assert.Equal(1, expandedEvents);
        }

        [Fact]
        public void RemovingTheSelectedItem_ClearsTheSelection()
        {
            TreeViewNode b = Node("B");
            TreeViewNode a = Expanded("A", b);
            TreeView tree = CreateTree(a);
            tree.SelectedItem = b;

            a.Children.Remove(b);

            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void RemovingTheCollapsedAncestorOfTheSelection_ClearsIt()
        {
            TreeViewNode c = Node("C");
            TreeViewNode b = Node("B", c);
            TreeViewNode a = Expanded("A", b);
            TreeView tree = CreateTree(a);
            tree.SelectedItem = c;
            tree.Collapse(b);

            a.Children.Remove(b);

            Assert.Null(tree.SelectedItem);
        }

        [Fact]
        public void CollapsingAndRemoving_Unsubscribe()
        {
            var child = new Folder("child");
            var root = new Folder("root", child);
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ((Folder)x).Kids };

            tree.Expand(root);
            Assert.Equal(1, root.Kids.Subscribers);
            Assert.Equal(1, child.Kids.Subscribers);

            tree.Collapse(root);
            Assert.Equal(1, root.Kids.Subscribers);
            Assert.Equal(0, child.Kids.Subscribers);

            tree.ItemsSource = null;
            Assert.Equal(0, root.Kids.Subscribers);
        }

        [Fact]
        public void ADuplicatedItem_IsSubscribedOnce_AndUpdatesEveryRow()
        {
            TreeViewNode shared = Expanded("S");
            TreeView tree = CreateTree(Expanded("A", shared), Expanded("B", shared));

            shared.Children.Add(Node("leaf"));

            Assert.Equal(["A", "S", "leaf", "B", "S", "leaf"], Visible(tree));
        }

        [Fact]
        public void Detaching_Unsubscribes_AndReattaching_ReflectsMissedChanges()
        {
            (Canvas canvas, _) = CreateCanvas();
            var child = new Folder("child");
            var root = new Folder("root", child);
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ((Folder)x).Kids };
            tree.Expand(root);
            Host(canvas, tree);

            canvas.Remove(tree);
            Assert.Equal(0, root.Kids.Subscribers);
            root.Kids.Add(new Folder("late"));

            canvas.Add(tree);
            Assert.Equal(1, root.Kids.Subscribers);
            Assert.Equal(["root", "child", "late"], Visible(tree));
        }

        private sealed class Folder(string name, params Folder[] kids)
        {
            public CountingCollection<Folder> Kids { get; } = [.. kids];

            public override string ToString() => name;
        }

        private sealed class CountingCollection<T> : ObservableCollection<T>
        {
            public int Subscribers { get; private set; }

            public override event NotifyCollectionChangedEventHandler? CollectionChanged
            {
                add
                {
                    base.CollectionChanged += value;
                    Subscribers++;
                }

                remove
                {
                    base.CollectionChanged -= value;
                    Subscribers--;
                }
            }
        }
    }
}
```

If `Canvas.Remove(UIElement)` has another name, use whatever `Canvas` exposes to remove a root element (see `Canvas.cs` around line 440).

- [ ] **Step 2: Run them to verify they fail**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewLiveDataTests"`
Expected: FAIL. Most tests fail on row assertions, and `InlineItemChanges_AreFollowed` may already pass.

- [ ] **Step 3: Add the subscription bookkeeping**

In `TreeView`, add the fields:

```csharp
        private readonly Dictionary<object, Subscription> subscriptions = new(ReferenceEqualityComparer.Instance);
        private INotifyCollectionChanged? observedRoots;
```

Add the nested class at the end of `TreeView`:

```csharp
        /// <summary>What the tree listens to for one visible item, shared by every row that shows the item.</summary>
        private sealed class Subscription
        {
            public int RefCount { get; set; }

            public INotifyCollectionChanged? Children { get; init; }

            public NotifyCollectionChangedEventHandler? ChildrenHandler { get; init; }

            public TreeViewNode? Node { get; init; }

            public PropertyChangedEventHandler? NodeHandler { get; init; }
        }
```

Add the methods:

```csharp
        /// <inheritdoc/>
        protected override void OnDetached()
        {
            base.OnDetached();
            UnsubscribeAll();
        }

        private void AddRef(object item)
        {
            if (subscriptions.TryGetValue(item, out Subscription? existing))
            {
                existing.RefCount++;
                return;
            }

            INotifyCollectionChanged? children = GetChildren(item) as INotifyCollectionChanged;
            NotifyCollectionChangedEventHandler? childrenHandler = children == null ? null : (_, e) => OnChildrenChanged(item, e);
            TreeViewNode? node = item as TreeViewNode;
            PropertyChangedEventHandler? nodeHandler = node == null ? null : (_, e) => OnNodePropertyChanged(node, e);
            if (children != null)
                children.CollectionChanged += childrenHandler;
            if (node != null)
                node.PropertyChanged += nodeHandler;
            subscriptions[item] = new Subscription { RefCount = 1, Children = children, ChildrenHandler = childrenHandler, Node = node, NodeHandler = nodeHandler };
        }

        private void Release(object item)
        {
            if (!subscriptions.TryGetValue(item, out Subscription? subscription) || --subscription.RefCount > 0)
                return;

            Unsubscribe(subscription);
            subscriptions.Remove(item);
        }

        private static void Unsubscribe(Subscription subscription)
        {
            if (subscription.Children != null)
                subscription.Children.CollectionChanged -= subscription.ChildrenHandler;
            if (subscription.Node != null)
                subscription.Node.PropertyChanged -= subscription.NodeHandler;
        }

        private void UnsubscribeAll()
        {
            foreach (Subscription subscription in subscriptions.Values)
                Unsubscribe(subscription);
            subscriptions.Clear();
            if (observedRoots != null)
            {
                observedRoots.CollectionChanged -= Roots_CollectionChanged;
                observedRoots = null;
            }
        }
```

- [ ] **Step 4: Route every row change through the bookkeeping**

Replace `Rebuild`, `InsertRows`, `RemoveRows` and `Items_CollectionChanged` with:

```csharp
        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Inline items are the roots only without ItemsSource; their changes then arrive through Roots_CollectionChanged.
            if (itemsSource != null)
                throw new InvalidOperationException($"A {nameof(TreeView)} takes its roots from either '{nameof(ItemsSource)}' or inline '{nameof(Items)}', not both.");
        }

        private void Rebuild()
        {
            UnsubscribeAll();
            IEnumerable roots = Roots;
            observedRoots = roots as INotifyCollectionChanged;
            if (observedRoots != null)
                observedRoots.CollectionChanged += Roots_CollectionChanged;

            var built = new List<FlatRow>();
            foreach (object item in roots)
                AppendVisible(item, 0, null, built);
            foreach (FlatRow row in built)
                AddRef(row.Item);
            rows.ResetTo(built);
            ClearSelectionIfGone();
        }

        private void InsertRows(int index, List<FlatRow> range)
        {
            foreach (FlatRow row in range)
                AddRef(row.Item);
            rows.InsertRange(index, range);
        }

        private void RemoveRows(int index, int count, bool fromData)
        {
            if (count == 0)
                return;

            foreach (FlatRow row in rows.RemoveRange(index, count))
                Release(row.Item);
            if (fromData)
                ClearSelectionIfGone();
        }
```

- [ ] **Step 5: Translate collection changes into row ranges**

Add:

```csharp
        private void Roots_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => ApplyChildrenChange(null, -1, e);

        private void OnChildrenChanged(object parentItem, NotifyCollectionChangedEventArgs e)
        {
            foreach (FlatRow parent in rows.Where(r => ReferenceEquals(r.Item, parentItem)).ToList())
            {
                int index = rows.IndexOf(parent);
                if (index < 0)
                    continue;
                if (IsExpanded(parentItem))
                    ApplyChildrenChange(parent, index, e);
                list.Restamp(parent);
            }
        }

        private void OnNodePropertyChanged(TreeViewNode node, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(TreeViewNode.IsExpanded) when !settingNodeState:
                    ApplyExpansion(node, node.IsExpanded);
                    break;
                case nameof(TreeViewNode.IsSelectable):
                    list.RestampAll();
                    if (ReferenceEquals(selectedItem, node) && !node.IsSelectable)
                        Select(null, reveal: false);
                    break;
            }
        }

        /// <summary>
        /// Applies a change of <paramref name="parent"/>'s children (or of the roots, when <paramref name="parent"/> is
        /// <see langword="null"/>) to the rows of its visible subtree.
        /// </summary>
        private void ApplyChildrenChange(FlatRow? parent, int parentIndex, NotifyCollectionChangedEventArgs e)
        {
            int depth = parent == null ? 0 : parent.Depth + 1;
            int start = parentIndex + 1;
            int end = parent == null ? rows.Count : start + DescendantCount(parentIndex);
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add when e.NewStartingIndex >= 0:
                    InsertRows(ChildStart(start, end, depth, e.NewStartingIndex), BuildRows(e.NewItems!, depth, parent));
                    return;

                case NotifyCollectionChangedAction.Remove when e.OldStartingIndex >= 0:
                    RemoveChildren(start, end, depth, e.OldStartingIndex, e.OldItems!.Count);
                    return;

                case NotifyCollectionChangedAction.Replace when e.OldStartingIndex >= 0:
                    foreach (object old in e.OldItems!)
                        expanded.Remove(old);
                    int at = RemoveChildren(start, end, depth, e.OldStartingIndex, e.OldItems.Count);
                    InsertRows(at, BuildRows(e.NewItems!, depth, parent));
                    return;

                case NotifyCollectionChangedAction.Move when e.OldStartingIndex >= 0 && e.NewStartingIndex >= 0:
                    // The moved rows keep their subscriptions and expansion; they're only re-positioned.
                    int from = ChildStart(start, end, depth, e.OldStartingIndex);
                    int to = ChildStart(start, end, depth, e.OldStartingIndex + e.OldItems!.Count);
                    List<FlatRow> moved = rows.RemoveRange(from, to - from);
                    rows.InsertRange(ChildStart(start, end - moved.Count, depth, e.NewStartingIndex), moved);
                    return;

                default:
                    // Reset, or a change without indices: re-flatten this parent's subtree.
                    RemoveRows(start, end - start, fromData: true);
                    IEnumerable children = parent == null ? Roots : GetChildren(parent.Item) ?? Array.Empty<object>();
                    InsertRows(start, BuildRows(children, depth, parent));
                    return;
            }
        }

        /// <summary>Finds the row index where child number <paramref name="k"/> of a subtree starts.</summary>
        /// <returns>The index; <paramref name="end"/> when <paramref name="k"/> is the child count.</returns>
        private int ChildStart(int start, int end, int depth, int k)
        {
            int seen = 0;
            for (int i = start; i < end; i++)
            {
                if (rows[i].Depth != depth)
                    continue;
                if (seen == k)
                    return i;
                seen++;
            }

            return end;
        }

        private int RemoveChildren(int start, int end, int depth, int k, int count)
        {
            int from = ChildStart(start, end, depth, k);
            int to = ChildStart(start, end, depth, k + count);
            RemoveRows(from, to - from, fromData: true);
            return from;
        }

        private List<FlatRow> BuildRows(IEnumerable items, int depth, FlatRow? parent)
        {
            var built = new List<FlatRow>();
            foreach (object item in items)
                AppendVisible(item, depth, parent, built);
            return built;
        }
```

Remove the `if (ItemsSource ...) Rebuild()` path from nowhere else. The `ItemsSource`, `ChildrenSelector`, `ItemTemplate` and `ItemTemplateSelector` setters keep calling `Rebuild()`.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeView"`
Expected: PASS. Run the full suite and check `Total:`.

- [ ] **Step 7: Commit**

```bash
git add sources/IcyUI/UI/Controls/TreeView.cs sources/IcyUI.Tests/Controls/TreeViewLiveDataTests.cs
git commit -m "Follow live changes to TreeView roots, children and nodes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Keyboard and gamepad navigation

**Files:**
- Modify: `sources/IcyUI/UI/Controls/TreeView.cs`
- Modify: `sources/IcyUI.Tests/Input/FakeInputSystem.cs` (`FakeNavigationEvents.RaiseFocusChanging`, around line 309)
- Test: `sources/IcyUI.Tests/Controls/TreeViewNavigationTests.cs`

**Interfaces:**
- Consumes: `currentRow`, `Select`, `SetExpanded`, `SyncList`, `list.ScrollIntoView` (Tasks 1, 4, 5).
- Produces: focus-gated handling of `INavigationEvents.FocusChanging`/`SelectElement`. A boundary press is left unhandled (`Handled == false`).

- [ ] **Step 1: Make the fake return its args**

In `sources/IcyUI.Tests/Input/FakeInputSystem.cs`, replace `RaiseFocusChanging` with:

```csharp
        public AcceptableEventArgs<Vector2> RaiseFocusChanging(Vector2 direction)
        {
            var args = new AcceptableEventArgs<Vector2> { Data = direction };
            FocusChanging?.Invoke(this, args);
            return args;
        }
```

- [ ] **Step 2: Write the failing tests**

`sources/IcyUI.Tests/Controls/TreeViewNavigationTests.cs`. The tree is `A→(B→(C, D), E)` from the spec:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewNavigationTests
    {
        private static readonly Vector2 Up = -Vector2.UnitY;
        private static readonly Vector2 Down = Vector2.UnitY;
        private static readonly Vector2 Left = -Vector2.UnitX;
        private static readonly Vector2 Right = Vector2.UnitX;

        private readonly TreeViewNode a, b, c, d, e;
        private readonly TreeView tree;
        private readonly FakeInputSystem input;

        public TreeViewNavigationTests()
        {
            c = Node("C");
            d = Node("D");
            b = Node("B", c, d);
            e = Node("E");
            a = Expanded("A", b, e);
            tree = CreateTree(a);
            (Canvas canvas, input) = CreateCanvas();
            Host(canvas, tree);
            canvas.Focus(tree);
        }

        private string Current => tree.Rows[tree.List.CurrentIndex].Item.ToString()!;

        [Fact]
        public void Focusing_StartsAtTheSelection_OrTheFirstRow()
        {
            Assert.Equal("A", Current);
        }

        [Fact]
        public void UpAndDown_MoveAndSelect()
        {
            Assert.True(Press(Down));
            Assert.Equal("B", Current);
            Assert.Same(b, tree.SelectedItem);
        }

        [Fact]
        public void UpOnTheFirstRow_LeavesTheTree()
        {
            Assert.False(Press(Up));
            Assert.Equal("A", Current);
        }

        [Fact]
        public void DownOnTheLastRow_LeavesTheTree()
        {
            Press(Down);
            Press(Down);
            Assert.Equal("E", Current);

            Assert.False(Press(Down));
        }

        [Fact]
        public void Right_ExpandsACollapsedBranch_ThenWalksTheRows()
        {
            Press(Down);

            Assert.True(Press(Right));
            Assert.True(b.IsExpanded);
            Assert.Equal("B", Current);

            Press(Right);
            Assert.Equal("C", Current);
            Press(Right);
            Assert.Equal("D", Current);
            Press(Right);
            Assert.Equal("E", Current);
            Assert.False(Press(Right));
        }

        [Fact]
        public void Left_GoesToTheParent_ThenCollapses()
        {
            tree.Expand(b);
            tree.SelectedItem = c;
            ResetFocus();
            Assert.Equal("C", Current);

            Press(Left);
            Assert.Equal("B", Current);
            Press(Left);
            Assert.False(b.IsExpanded);
            Press(Left);
            Assert.Equal("A", Current);
        }

        [Fact]
        public void LeftOnAnExpandedRoot_CollapsesIt_ThenLeavesTheTree()
        {
            Assert.True(Press(Left));
            Assert.False(a.IsExpanded);

            Assert.False(Press(Left));
        }

        [Fact]
        public void ANonSelectableRow_BecomesCurrent_WithoutChangingTheSelection()
        {
            b.IsSelectable = false;
            tree.SelectedItem = a;

            Press(Down);

            Assert.Equal("B", Current);
            Assert.Same(a, tree.SelectedItem);
        }

        [Fact]
        public void Enter_TogglesTheCurrentRow()
        {
            Press(Down);

            input.Events.Navigation.RaiseSelectElement();

            Assert.True(b.IsExpanded);
        }

        [Fact]
        public void WithoutFocus_TheTreeIgnoresNavigation()
        {
            tree.Canvas!.Focus(null);

            Assert.False(Press(Down));
            Assert.Equal(-1, tree.List.CurrentIndex);
        }

        private bool Press(Vector2 direction) => input.Events.Navigation.RaiseFocusChanging(direction).Handled;

        private void ResetFocus()
        {
            tree.Canvas!.Focus(null);
            tree.Canvas.Focus(tree);
        }
    }
}
```

The "focus starts at the selection" rule only applies while the current row is unset. `Left_GoesToTheParent_ThenCollapses` re-focuses after `SelectedItem = c`. Because the current row was already set to A by the constructor's focus, the implementation **must** also move the current row when `SelectedItem` is set from code. That's the step in Step 3.

- [ ] **Step 3: Implement navigation**

Add the field and `using Icy.Input.Events;` and `using System.Numerics;` at the top:

```csharp
        private INavigationEvents? subscribedNavigation;
```

In the constructor, after `Items.CollectionChanged += ...`, add:

```csharp
            FocusChanged += OnFocusChanged;
```

In `Select(...)`, inside the `if (SetProperty(...))` branch, before `SyncList();`, add:

```csharp
                if (item != null)
                    currentRow = rows.FirstOrDefault(r => ReferenceEquals(r.Item, item)) ?? currentRow;
```

In `OnDetached`, after `UnsubscribeAll();`, add `UnsubscribeNavigation();`.

Add the methods:

```csharp
        private void OnFocusChanged(object? sender, EventArgs e)
        {
            if (IsFocused)
            {
                currentRow ??= (selectedItem == null ? null : rows.FirstOrDefault(r => ReferenceEquals(r.Item, selectedItem)))
                    ?? (rows.Count > 0 ? rows[0] : null);
                SubscribeNavigation();
            }
            else
            {
                UnsubscribeNavigation();
            }

            SyncList();
        }

        private void SubscribeNavigation()
        {
            if (Configuration == null || subscribedNavigation != null)
                return;

            subscribedNavigation = Configuration.Input.Events.Navigation;
            subscribedNavigation.FocusChanging += OnNavigationFocusChanging;
            subscribedNavigation.SelectElement += OnNavigationSelectElement;
        }

        private void UnsubscribeNavigation()
        {
            if (subscribedNavigation == null)
                return;

            subscribedNavigation.FocusChanging -= OnNavigationFocusChanging;
            subscribedNavigation.SelectElement -= OnNavigationSelectElement;
            subscribedNavigation = null;
        }

        /// <summary>
        /// Moves through the rows. A press that would leave the tree (past the first or last row, or Left on a root with
        /// nothing to collapse) is left unhandled, so focus navigation can move on to the next control.
        /// </summary>
        private void OnNavigationFocusChanging(object? sender, AcceptableEventArgs<Vector2> e)
        {
            if (rows.Count == 0)
                return;

            int index = currentRow == null ? -1 : rows.IndexOf(currentRow);
            Vector2 direction = e.Data;
            if (index < 0)
            {
                MoveTo(0);
                e.Handled = true;
                return;
            }

            FlatRow row = rows[index];
            if (MathF.Abs(direction.Y) >= MathF.Abs(direction.X))
            {
                int next = direction.Y < 0 ? index - 1 : index + 1;
                if (next < 0 || next >= rows.Count)
                    return;
                MoveTo(next);
            }
            else if (direction.X > 0)
            {
                if (HasChildren(row.Item) && !IsExpanded(row.Item))
                    Expand(row.Item);
                else if (index + 1 < rows.Count)
                    MoveTo(index + 1);
                else
                    return;
            }
            else
            {
                if (HasChildren(row.Item) && IsExpanded(row.Item))
                    Collapse(row.Item);
                else if (row.Parent != null)
                    MoveTo(rows.IndexOf(row.Parent));
                else
                    return;
            }

            e.Handled = true;
        }

        private void OnNavigationSelectElement(object? sender, EventArgs e)
        {
            if (currentRow != null && HasChildren(currentRow.Item))
                Toggle(currentRow.Item);
        }

        /// <summary>Makes row <paramref name="index"/> current, selects it when selectable, and scrolls it into view.</summary>
        private void MoveTo(int index)
        {
            currentRow = rows[index];
            if (IsSelectable(currentRow.Item))
                Select(currentRow.Item, reveal: false);
            SyncList();
            list.ScrollIntoView(rows.IndexOf(currentRow));
        }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeView|FullyQualifiedName~Navigation|FullyQualifiedName~Selector"`
Expected: PASS. Other tests use `RaiseFocusChanging` as a statement, which still compiles.

- [ ] **Step 5: Commit**

```bash
git add sources/IcyUI/UI/Controls/TreeView.cs sources/IcyUI.Tests/Input/FakeInputSystem.cs sources/IcyUI.Tests/Controls/TreeViewNavigationTests.cs
git commit -m "Add keyboard and gamepad navigation to TreeView

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Pooling, stale content and the virtualization bound

**Files:**
- Test: `sources/IcyUI.Tests/Controls/TreeViewPoolingTests.cs`
- Modify (only if a test fails): `sources/IcyUI/UI/Controls/TreeViewList.cs` / `TreeView.cs`

**Interfaces:**
- Consumes: everything above. Produces no new API. This task proves the pooling rule (CLAUDE.md, Process 6) and the performance bound from the spec.

- [ ] **Step 1: Write the tests**

`sources/IcyUI.Tests/Controls/TreeViewPoolingTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Diagnostics;
using Icy.UI.Controls;
using Xunit;
using Xunit.Abstractions;
using static Icy.Tests.Controls.TreeViewTestKit;

namespace Icy.Tests.Controls
{
    public class TreeViewPoolingTests(ITestOutputHelper output)
    {
        [Fact]
        public void ScrollingThroughDeepAndShallowRows_KeepsEveryRecycledRowCorrect()
        {
            // A deep chain (depths 0..29) followed by 60 shallow roots, half of them with children.
            TreeViewNode chain = Expanded("d0");
            TreeViewNode tip = chain;
            for (int i = 1; i < 30; i++)
            {
                TreeViewNode next = Expanded($"d{i}");
                tip.Children.Add(next);
                tip = next;
            }

            var roots = new List<TreeViewNode> { chain };
            for (int i = 0; i < 60; i++)
                roots.Add(i % 2 == 0 ? Node($"s{i}", Node($"s{i}.child")) : Node($"s{i}"));
            TreeView tree = CreateTree([.. roots]);
            Layout(tree);

            for (float offset = 0; offset <= 90 * 20; offset += 70)
            {
                tree.ScrollViewer.VerticalOffset = offset;
                Layout(tree);
                AssertRealizedRowsMatch(tree);
            }

            for (float offset = 90 * 20; offset >= 0; offset -= 90)
            {
                tree.ScrollViewer.VerticalOffset = offset;
                Layout(tree);
                AssertRealizedRowsMatch(tree);
            }
        }

        [Fact]
        public void CollapsingAndReExpandingWhileScrolled_KeepsRowsCorrect()
        {
            TreeViewNode[] groups = [.. Enumerable.Range(0, 20).Select(g => Expanded($"g{g}", Node($"g{g}.a"), Node($"g{g}.b", Node($"g{g}.b.x")))) ];
            TreeView tree = CreateTree(groups);
            Layout(tree);
            tree.ScrollViewer.VerticalOffset = 300;
            Layout(tree);

            tree.Collapse(groups[5]);
            Layout(tree);
            AssertRealizedRowsMatch(tree);

            tree.Expand(groups[5]);
            tree.Expand(groups[5].Children[1]);
            Layout(tree);
            AssertRealizedRowsMatch(tree);
        }

        [Fact]
        public void RetargetingToASameShapeTree_LeavesNoOldContent()
        {
            TreeView tree = CreateTree(Expanded("A", Node("A1"), Node("A2")), Node("B"));
            Layout(tree);

            TreeViewNode[] fresh = [Expanded("X", Node("X1"), Node("X2")), Node("Y")];
            tree.Items.Clear();
            foreach (TreeViewNode node in fresh)
                tree.Items.Add(node);
            Layout(tree);

            Assert.Equal(["X", "X1", "X2", "Y"], Visible(tree));
            AssertRealizedRowsMatch(tree);
        }

        [Fact]
        public void ChangingTheChildrenSelector_RebuildsTheRows()
        {
            var root = new object();
            var first = new object[] { "one" };
            var second = new object[] { "two", "three" };
            var tree = new TreeView { ItemTemplate = RowTemplate(), ItemsSource = new[] { root }, ChildrenSelector = x => ReferenceEquals(x, root) ? first : null };
            tree.Expand(root);
            Layout(tree);

            tree.ChildrenSelector = x => ReferenceEquals(x, root) ? second : null;
            Layout(tree);

            Assert.Equal(3, tree.Rows.Count);
            AssertRealizedRowsMatch(tree);
        }

        [Fact]
        public void ATenThousandNodeTree_RealizesOnlyTheViewport()
        {
            TreeViewNode root = Node("root");
            for (int g = 0; g < 100; g++)
            {
                TreeViewNode group = Node($"g{g}");
                for (int i = 0; i < 100; i++)
                    group.Children.Add(Node($"g{g}.{i}"));
                root.Children.Add(group);
            }

            TreeView tree = CreateTree(root);
            Layout(tree);

            var watch = Stopwatch.StartNew();
            tree.Expand(root);
            foreach (TreeViewNode group in root.Children)
                tree.Expand(group);
            watch.Stop();
            Layout(tree);
            output.WriteLine($"Expanding 10,100 rows took {watch.Elapsed.TotalMilliseconds:F1} ms");

            Assert.Equal(1 + 100 + 10_000, tree.Rows.Count);
            Assert.InRange(tree.List.Realized.Count, 1, 30);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"Expanding took {watch.Elapsed.TotalMilliseconds:F0} ms; the budget is 1000 ms.");
        }

        private static void AssertRealizedRowsMatch(TreeView tree)
        {
            foreach ((int index, ItemContainer container) in tree.List.Realized)
            {
                FlatRow row = tree.Rows[index];
                var item = (TreeViewItem)container;
                Assert.Equal(row.Depth, item.Depth);
                Assert.Equal(tree.HasChildren(row.Item), item.HasChildren);
                Assert.Equal(tree.IsExpanded(row.Item), item.IsExpanded);
                Assert.Equal(row.Depth * tree.Indent, item.IndentWidth);
                Assert.Same(row.Item, item.Content!.DataContext);
            }
        }
    }
}
```

- [ ] **Step 2: Run them**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewPoolingTests" --logger "console;verbosity=detailed"`
Expected: PASS. Note the measured expand time printed by the 10k test.

If a stale-content assertion fails, the cause is a row property not stamped in `TreeViewList.AttachContainer`, or a container not re-stamped after a state change. Fix it there, not in the test. If the 10k test exceeds the budget, profile `ApplyExpansion`'s `rows.Where(...).ToList()` plus `IndexOf` per expand first.

- [ ] **Step 3: Commit**

```bash
git add sources/IcyUI.Tests/Controls/TreeViewPoolingTests.cs sources/IcyUI/UI/Controls/TreeViewList.cs sources/IcyUI/UI/Controls/TreeView.cs
git commit -m "Pin TreeView pooling, stale content and the virtualization bound

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

(Include the measured expand time in the commit body.)

---

### Task 9: `TreeViewDemo` in both hosts

**Files:**
- Create: `sources/Shared Samples/TreeViewDemo.cs`
- Create: `sources/MonoGame Sample/Samples/TreeViewSample.cs`
- Modify: `sources/MonoGame Sample/MonoGame Sample.csproj`, `sources/MonoGame Sample/SampleGame.cs`
- Modify: `sources/Stride Sample/Stride Sample.csproj`, `sources/Stride Sample/SampleGame.cs`
- Modify: `sources/IcyUI.Tests/IcyUI.Tests.csproj`
- Test: `sources/IcyUI.Tests/Samples/TreeViewDemoTests.cs`

**Interfaces:**
- Consumes: the public `TreeView`, `TreeViewNode` and `HierarchicalDataTemplate` API.
- Produces: `public static class Icy.SharedSamples.TreeViewDemo` with `const string Markup`, `UIElement Build(IcyConfiguration configuration, string fontFamily)`, and `public sealed class DemoNode` (used by the tests).

- [ ] **Step 1: Write the failing demo test**

`sources/IcyUI.Tests/Samples/TreeViewDemoTests.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Configuration;
using Icy.SharedSamples;
using Icy.Tests.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Samples
{
    public class TreeViewDemoTests
    {
        [Fact]
        public void Build_LoadsTheStaticTree()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();

            UIElement root = TreeViewDemo.Build(configuration, "Airfool");
            TreeView tree = root.FindRequiredControl<TreeView>("StaticTree");

            Assert.Equal(3, tree.Items.Count);
            Assert.Equal(["Basics", "Controls", "Styles", "Items", "Design tools"], tree.Rows.Select(r => r.Item.ToString()));
        }

        [Fact]
        public void TheLiveButtons_EditTheLiveTree()
        {
            IcyConfiguration configuration = MarkupLoadObserverTests.CreateConfiguration();
            UIElement root = TreeViewDemo.Build(configuration, "Airfool");
            TreeView live = root.FindRequiredControl<TreeView>("LiveTree");
            var first = (TreeViewDemo.DemoNode)live.Rows[0].Item;
            live.SelectedItem = first;

            root.FindRequiredControl<Button>("AddButton").OnTap();
            Assert.Equal(4, first.Children.Count);
            Assert.True(live.IsExpanded(first));

            root.FindRequiredControl<Button>("RevealButton").OnTap();
            Assert.Equal("Street", live.SelectedItem!.ToString());
            Assert.Contains("Street", root.FindRequiredControl<TextBlock>("Status").Text);
        }
    }
}
```

Add to `sources/IcyUI.Tests/IcyUI.Tests.csproj`, next to the other `Shared Samples` links:

```xml
    <Compile Include="..\Shared Samples\TreeViewDemo.cs" Link="Samples\TreeViewDemo.cs" />
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewDemoTests"`
Expected: build FAILS (the linked file doesn't exist yet).

- [ ] **Step 3: Write the demo**

`sources/Shared Samples/TreeViewDemo.cs`:

```csharp
// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information

// Linked into both sample hosts; MonoGame Sample has nullable disabled project-wide, Stride Sample has it enabled.
#nullable enable
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;

namespace Icy.SharedSamples
{
    /// <summary>
    /// <see cref="TreeView"/>: a static tree written in markup with non-selectable categories, and a live tree built in
    /// code whose buttons add, remove, rename and reveal nodes.
    /// </summary>
    public static class TreeViewDemo
    {
        /// <summary>
        /// The demo page.
        /// </summary>
        public const string Markup =
            """
            <StackPanel Orientation="Vertical" HorizontalAlignment="Left" VerticalAlignment="Top" Margin="20">
              <TextBlock FontSize="18" Foreground="WhiteSmoke" Margin="0,0,0,6">TreeView</TextBlock>
              <TextBlock FontSize="14" Foreground="WhiteSmoke" Margin="0,0,0,12">Click a row, its chevron, or use the arrow keys. Categories on the left can't be selected: clicking one opens or closes it.</TextBlock>
              <StackPanel Orientation="Horizontal">
                <TreeView x:Name="StaticTree" Width="260" Height="320" Margin="0,0,16,0">
                  <TreeViewNode Header="Basics" IsSelectable="False" IsExpanded="True">
                    <TreeViewNode Header="Controls"/>
                    <TreeViewNode Header="Styles"/>
                  </TreeViewNode>
                  <TreeViewNode Header="Items" IsSelectable="False">
                    <TreeViewNode Header="ListBox"/>
                    <TreeViewNode Header="WrapGrid"/>
                    <TreeViewNode Header="Nested" IsSelectable="False">
                      <TreeViewNode Header="Deep leaf"/>
                    </TreeViewNode>
                  </TreeViewNode>
                  <TreeViewNode Header="Design tools" IsSelectable="False">
                    <TreeViewNode Header="Design"/>
                    <TreeViewNode Header="Editor"/>
                  </TreeViewNode>
                </TreeView>
                <StackPanel Orientation="Vertical">
                  <TreeView x:Name="LiveTree" Width="300" Height="270"/>
                  <StackPanel Orientation="Horizontal" Margin="0,8,0,0">
                    <Button x:Name="AddButton" Padding="10,4" Margin="0,0,6,0">Add child</Button>
                    <Button x:Name="RemoveButton" Padding="10,4" Margin="0,0,6,0">Remove</Button>
                    <Button x:Name="RenameButton" Padding="10,4" Margin="0,0,6,0">Rename</Button>
                    <Button x:Name="RevealButton" Padding="10,4">Reveal deep</Button>
                  </StackPanel>
                </StackPanel>
              </StackPanel>
              <TextBlock x:Name="Status" FontSize="14" Foreground="WhiteSmoke" Margin="0,12,0,0">Nothing selected yet.</TextBlock>
            </StackPanel>
            """;

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

            var loader = new MarkupLoader(configuration);
            UIElement root = loader.Load(Markup, nameof(TreeViewDemo));
            var status = root.FindRequiredControl<TextBlock>("Status");
            var staticTree = root.FindRequiredControl<TreeView>("StaticTree");
            var live = root.FindRequiredControl<TreeView>("LiveTree");

            var roots = new ObservableCollection<DemoNode>
            {
                new("Europe", new("North", new("Oslo"), new("Helsinki")), new("South", new("Rome")), new("West", new("Lisbon"))),
                new("Asia", new("East", new("Tokyo", new("Shibuya", new("Street")))), new("South", new("Mumbai"))),
                new("Americas", new("North", new("Toronto")), new("South", new("Lima"))),
            };
            DemoNode deep = roots[1].Children[0].Children[0].Children[0].Children[0];
            int counter = 0;

            live.ItemTemplate = (DataTemplate)loader.LoadObject("""<DataTemplate><TextBlock Text="{Binding Name}" Foreground="WhiteSmoke"/></DataTemplate>""");
            live.ChildrenSelector = node => ((DemoNode)node).Children;
            live.ItemsSource = roots;

            staticTree.SelectionChanged += (_, _) => status.Text = $"Static tree: {staticTree.SelectedItem?.ToString() ?? "nothing"} selected.";
            live.SelectionChanged += (_, _) => status.Text = $"Live tree: {live.SelectedItem?.ToString() ?? "nothing"} selected.";
            live.ItemExpanded += (_, e) => status.Text = $"Live tree: {e.Item} expanded.";
            live.ItemCollapsed += (_, e) => status.Text = $"Live tree: {e.Item} collapsed.";

            root.FindRequiredControl<Button>("AddButton").Click += (_, _) =>
            {
                DemoNode parent = live.SelectedItem as DemoNode ?? roots[0];
                parent.Children.Add(new DemoNode($"New {++counter}"));
                live.Expand(parent);
            };
            root.FindRequiredControl<Button>("RemoveButton").Click += (_, _) =>
            {
                if (live.SelectedItem is not DemoNode selected)
                    return;
                if (!roots.Remove(selected))
                    FindParent(roots, selected)?.Children.Remove(selected);
            };
            root.FindRequiredControl<Button>("RenameButton").Click += (_, _) =>
            {
                if (live.SelectedItem is DemoNode selected)
                    selected.Name += "*";
            };
            root.FindRequiredControl<Button>("RevealButton").Click += (_, _) => live.SelectedItem = deep;

            return root;
        }

        private static DemoNode? FindParent(ObservableCollection<DemoNode> nodes, DemoNode child)
        {
            foreach (DemoNode node in nodes)
            {
                if (node.Children.Contains(child))
                    return node;
                if (FindParent(node.Children, child) is { } parent)
                    return parent;
            }

            return null;
        }

        /// <summary>
        /// A node of the live tree: a renamable name and its children.
        /// </summary>
        /// <param name="name">The node's name.</param>
        /// <param name="children">The node's initial children.</param>
        public sealed class DemoNode(string name, params DemoNode[] children) : INotifyPropertyChanged
        {
            private string name = name;

            /// <inheritdoc/>
            public event PropertyChangedEventHandler? PropertyChanged;

            /// <summary>Gets or sets the node's name.</summary>
            public string Name
            {
                get => name;
                set
                {
                    name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                }
            }

            /// <summary>Gets the node's children.</summary>
            public ObservableCollection<DemoNode> Children { get; } = new(children);

            /// <inheritdoc/>
            public override string ToString() => name;
        }
    }
}
```

`Button.OnTap()` raises `Click` (see `Button.cs`). The test calls it directly through `InternalsVisibleTo`.

- [ ] **Step 4: Run the demo test to verify it passes**

Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj" --filter "FullyQualifiedName~TreeViewDemoTests"`
Expected: PASS.

- [ ] **Step 5: Register in MonoGame Sample**

`sources/MonoGame Sample/Samples/TreeViewSample.cs`:

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
    /// Runs <see cref="TreeViewDemo"/> as its own selectable sample.
    /// </summary>
    internal class TreeViewSample : SampleBase
    {
        private const string FontFamily = "Airfool";

        private UIElement demoRoot;

        public TreeViewSample(Game game, IcyConfiguration configuration, Canvas canvas)
            : base(game, configuration, canvas, "TreeView Demo")
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

            demoRoot = TreeViewDemo.Build(UIConfiguration, FontFamily);
            demoRoot.IsVisible = Visible;
            Canvas.Add(demoRoot);

            base.LoadContent();
        }
    }
}
```

In `MonoGame Sample.csproj`, add after the `EditorDemo.cs` link:

```xml
    <Compile Include="..\Shared Samples\TreeViewDemo.cs" Link="Samples\TreeViewDemo.cs" />
```

In `MonoGame Sample/SampleGame.cs`, add `new TreeViewSample(this, uiConfiguration, canvas),` **before** `new DesignSample(...)`. The design session tracks every page loaded after it, so the TreeView page must load before it.

- [ ] **Step 6: Register in Stride Sample**

In `Stride Sample.csproj`, add after the `EditorDemo.cs` link:

```xml
    <Compile Include="..\Shared Samples\TreeViewDemo.cs" Link="TreeViewDemo.cs" />
```

In `Stride Sample/SampleGame.cs`:
- Add the field `private UIElement? treeViewRoot;` after `editorRoot`.
- Build it **before** `designRoot`: `treeViewRoot = TreeViewDemo.Build(configuration, "Airfool");`, placed right after `scalingRoot = ...`.
- Add `canvas.Add(treeViewRoot);` after `canvas.Add(editorRoot);`.
- In `UpdateSelectedDemo`, add `treeViewRoot!.IsVisible = selectedDemo == 19;`.
- Change `(selectedDemo + 1) % 19` to `(selectedDemo + 1) % 20`.
- Add `<see cref="TreeViewDemo"/>` to the class summary's demo list.

- [ ] **Step 7: Build everything and run the whole suite**

Run: `dotnet build "sources/IcyUI.sln"` and expect `0 Error(s)`. Check that no new warnings come from the TreeView files.
Run: `dotnet test "sources/IcyUI.Tests/IcyUI.Tests.csproj"` and expect all to pass. Check `Total:`.

- [ ] **Step 8: Commit**

```bash
git add "sources/Shared Samples/TreeViewDemo.cs" "sources/MonoGame Sample/Samples/TreeViewSample.cs" "sources/MonoGame Sample/MonoGame Sample.csproj" "sources/MonoGame Sample/SampleGame.cs" "sources/Stride Sample/Stride Sample.csproj" "sources/Stride Sample/SampleGame.cs" sources/IcyUI.Tests/IcyUI.Tests.csproj sources/IcyUI.Tests/Samples/TreeViewDemoTests.cs
git commit -m "Add TreeViewDemo to both sample hosts

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

After this task: a whole-branch review (CLAUDE.md Process 4), then Ivan's manual smoke test of the TreeView page in both hosts. Never claim that smoke test yourself.
