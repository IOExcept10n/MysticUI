using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
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
    public class WrapGridTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var grid = new WrapGrid();

            Assert.Equal(-1, grid.SelectedIndex);
            Assert.Null(grid.SelectedItem);
            Assert.Equal(64f, grid.ItemWidth);
            Assert.Equal(64f, grid.ItemHeight);
        }

        [Fact]
        public void ItemWidth_ItemHeight_RejectNonPositiveValues()
        {
            var grid = new WrapGrid();

            Assert.Throws<ArgumentOutOfRangeException>(() => grid.ItemWidth = 0f);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.ItemWidth = -1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.ItemHeight = 0f);
        }

        [Fact]
        public void ComputeExtentHeight_MatchesTheWorkedExampleInTheSpec()
        {
            // ItemWidth = ItemHeight = 64 (default), ContentBounds.Width = 400 -> ColumnsPerRow = 6 (400/64 = 6.25 floors
            // to 6). ItemCount = 100 -> ExtentHeight = ceil(100/6)*64 = 17*64 = 1088.
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 100).Cast<object>().ToList(),
                Width = 400,
            };
            ArrangeAtWidth(grid, 400);

            Assert.Equal(6, InvokeColumnsPerRow(grid));
            Assert.Equal(1088f, grid.ExtentHeight);
        }

        [Fact]
        public void ComputeExtentHeight_ExactMultipleOfColumnsPerRow_NoPartialRow()
        {
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 12).Cast<object>().ToList(),
                Width = 400, // ColumnsPerRow = 6, 12 items = exactly 2 full rows
            };
            ArrangeAtWidth(grid, 400);

            Assert.Equal(2 * 64f, grid.ExtentHeight);
        }

        [Fact]
        public void ColumnsPerRow_NarrowerThanOneItem_ClampsToOne()
        {
            var grid = new WrapGrid { ItemsSource = new List<object> { "a" }, Width = 30 };
            ArrangeAtWidth(grid, 30);

            Assert.Equal(1, InvokeColumnsPerRow(grid));
        }

        [Fact]
        public void OnViewportChanged_RealizesExactlyTheExpectedRange_MatchingTheWorkedExample()
        {
            // Same setup as the ComputeExtentHeight worked example: ColumnsPerRow=6, 100 items.
            // verticalOffset=300, viewportHeight=500 -> firstRow=4 (300/64 floors), firstIndex=24;
            // lastRow=(300+500+100)/64=14 (floors), lastIndex=min(99,15*6-1)=89.
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 100).Cast<object>().ToList(),
                ItemTemplate = template,
                Width = 400,
            };
            ArrangeAtWidth(grid, 400);

            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 300, 400, 500);

            var realized = GetRealizedContainers(grid).Keys;
            Assert.Equal(66, realized.Count); // 11 rows * 6 columns
            Assert.Contains(24, realized);
            Assert.Contains(89, realized);
            Assert.DoesNotContain(23, realized);
            Assert.DoesNotContain(90, realized);
        }

        [Fact]
        public void OnViewportChanged_PositionsEachRealizedItemAtItsRowAndColumn()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
                ItemTemplate = template,
                ItemWidth = 50,
                ItemHeight = 30,
                Width = 200, // ColumnsPerRow = 4
            };
            ArrangeAtWidth(grid, 200);

            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 200, 100);

            // Index 5 -> row 1, column 1 -> (50, 30).
            ItemContainer item5 = GetRealizedContainers(grid)[5];
            Assert.Equal(50, item5.ActualBounds.X);
            Assert.Equal(30, item5.ActualBounds.Y);
        }

        [Fact]
        public void OnViewportChanged_ScrollingDown_DerealizesRowsThatScrolledOut()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 100).Cast<object>().ToList(),
                ItemTemplate = template,
                Width = 400,
            };
            ArrangeAtWidth(grid, 400);
            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 500);
            Assert.Contains(0, GetRealizedContainers(grid).Keys);

            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 5000, 400, 500);

            Assert.DoesNotContain(0, GetRealizedContainers(grid).Keys);
        }

        [Fact]
        public void ResizingWidth_ReflowsAlreadyRealizedItemsToTheirNewColumns()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
                ItemTemplate = template,
                ItemWidth = 50,
                ItemHeight = 30,
                Width = 200, // ColumnsPerRow = 4
            };
            ArrangeAtWidth(grid, 200);
            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 200, 100);
            Assert.Equal(50, GetRealizedContainers(grid)[5].ActualBounds.X); // row 1, col 1 at 4 columns

            // Reassign Width (not just pass a wider rect to ArrangeAtWidth) - Arrange(Rectangle) is a no-op once
            // already arranged and nothing else invalidates it; the explicit Width set here is what actually
            // drives Measure()/CalculateOverflow, and setting it re-invalidates measure/arrange so the resize
            // really takes effect.
            grid.Width = 300; // ColumnsPerRow = 6 now
            ArrangeAtWidth(grid, 300);
            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 300, 100);

            // Index 5 -> row 0, column 5 -> (250, 0) at 6 columns.
            Assert.Equal(250, GetRealizedContainers(grid)[5].ActualBounds.X);
            Assert.Equal(0, GetRealizedContainers(grid)[5].ActualBounds.Y);
        }

        [Fact]
        public void SelectionState_SurvivesPoolAndReuseCycle()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
                ItemTemplate = template,
                Width = 400,
            };
            ArrangeAtWidth(grid, 400);
            grid.SelectedIndex = 5;
            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100);
            Assert.True(((SelectorItem)GetRealizedContainers(grid)[5]).IsSelected);

            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 5000, 400, 100); // scroll item 5 out
            Assert.DoesNotContain(5, GetRealizedContainers(grid).Keys);

            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100); // scroll back
            Assert.True(((SelectorItem)GetRealizedContainers(grid)[5]).IsSelected);
        }

        [Fact]
        public void TappingARealizedItem_SelectsIt()
        {
            // Must be ScrollViewer-hosted, not added straight to the Canvas - ItemsControl's own remarks are
            // explicit that realization only ever happens inside OnViewportChanged, and nothing but a
            // ScrollViewer (or an equivalent IVirtualizingScrollInfo-aware host) ever calls it; without one,
            // nothing is ever realized and a tap would hit nothing.
            var (canvas, input) = CreateCanvas();
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
                ItemTemplate = template,
                ItemWidth = 50,
                ItemHeight = 30,
            };
            var scrollViewer = new ScrollViewer
            {
                Content = grid,
                Width = 200,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(scrollViewer);
            canvas.Render();

            // ColumnsPerRow = 4 at width 200; index 5 -> row 1, col 1 -> tap around (75, 45).
            input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(new Point(75, 45), 1));

            Assert.Equal(5, grid.SelectedIndex);
        }

        [Fact]
        public void Derealize_ThenEnsureRealizedAgain_ReusesThePooledContainer()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
                ItemTemplate = template,
                Width = 400,
            };
            ArrangeAtWidth(grid, 400);
            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100);
            ItemContainer original = GetRealizedContainers(grid)[0];

            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 5000, 400, 100); // scroll item 0 out
            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100); // scroll back

            // The pool is a plain per-template Stack<ItemContainer> (see ItemsControl.RentContainer/Derealize) -
            // not keyed by index, so a container that scrolls back into view isn't guaranteed to land at the same
            // index it started at (ItemsControlTests.Derealize_ThenEnsureRealizedAgain_ReusesThePooledContainer
            // makes the same point: its reused container reappears at a DIFFERENT index, by design). What pooling
            // actually promises is that the instance is reused at all, not discarded - assert that instead of
            // pinning down which index it lands on.
            Assert.Contains(original, GetRealizedContainers(grid).Values);
        }

        [Fact]
        public void ItemsSource_ObservableCollection_Insert_UpdatesItemsAndDerealizesShiftedIndexes()
        {
            var source = new ObservableCollection<object> { "a", "b", "c" };
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid { ItemsSource = source, ItemTemplate = template, Width = 400 };
            ArrangeAtWidth(grid, 400);
            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 400, 100);
            Assert.Contains(2, GetRealizedContainers(grid).Keys); // "c" realized at index 2

            source.Insert(0, "new");

            // "c"'s old index-2 realization must be gone - it would otherwise silently represent "b" now.
            Assert.DoesNotContain(2, GetRealizedContainers(grid).Keys);
        }

        [Fact]
        public void HostedInsideScrollViewer_OnlyRealizesTheVisibleRange()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 10000).Cast<object>().ToList(),
                ItemTemplate = template,
            };
            var scrollViewer = new ScrollViewer { Content = grid, Width = 400, Height = 300 };
            var (canvas, _) = CreateCanvas();
            canvas.Add(scrollViewer);

            canvas.Render();

            var realized = GetRealizedContainers(grid);
            Assert.True(realized.Count < 200, $"expected far fewer than 10000 realized, got {realized.Count}");
        }

        [Fact]
        public void ComputeExtentHeight_BeforeFirstArrange_ReturnsZero_NotASingleColumnGuess()
        {
            // ContentBounds.Width is still 0 before any Arrange has ever run - ColumnsPerRow must not silently
            // assume a single column and report a wildly-oversized extent for that transient state.
            var grid = new WrapGrid { ItemsSource = Enumerable.Range(0, 100).Cast<object>().ToList() };

            Assert.Equal(0f, grid.ExtentHeight);
        }

        [Fact]
        public void ColumnsPerRow_RoundingSafe_DoesNotUnderCountFromFloatingPointImprecision()
        {
            // 87 / 5.8 is mathematically exactly 15, but float32 division yields ~14.999999 - a naive (int)
            // truncation would silently lose a column.
            var grid = new WrapGrid { ItemsSource = new List<object> { "a" }, ItemWidth = 5.8f, Width = 87 };
            ArrangeAtWidth(grid, 87);

            Assert.Equal(15, InvokeColumnsPerRow(grid));
        }

        [Fact]
        public void OnViewportChanged_NegativeVerticalOffset_StillRealizesTheFirstRow_InsteadOfGoingBlank()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
                ItemTemplate = template,
                ItemWidth = 50,
                ItemHeight = 30,
                Width = 200, // ColumnsPerRow = 4
            };
            ArrangeAtWidth(grid, 200);

            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, -500f, 200, 100);

            Assert.NotEmpty(GetRealizedContainers(grid).Keys);
        }

        [Fact]
        public void RealizingItems_DoesNotPopulateTheInheritedSingleColumnHeightCache()
        {
            // Content taller than ItemHeight - if the base's single-column height-cache correction were still
            // active for WrapGrid, this mismatch is exactly what would (mis)trigger it.
            var template = LoadDataTemplate("""<DataTemplate><Border Height="200"/></DataTemplate>""");
            var grid = new WrapGrid
            {
                ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
                ItemTemplate = template,
                ItemWidth = 50,
                ItemHeight = 30,
                Width = 200, // ColumnsPerRow = 4
            };
            ArrangeAtWidth(grid, 200);

            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 0, 200, 100);
            ((IVirtualizingScrollInfo)grid).OnViewportChanged(0, 5000, 200, 100); // scroll far - derealizes the top rows

            Assert.All(GetKnownHeights(grid), Assert.Null);
        }

        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas(int viewportWidth = 800, int viewportHeight = 600)
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext { ViewportSize = new Size(viewportWidth, viewportHeight) };
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
        }

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;

        private static List<float?> GetKnownHeights(ItemsControl control) =>
            (List<float?>)typeof(ItemsControl).GetField("knownHeights", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;

        private static void ArrangeAtWidth(UIElement element, int width)
        {
            element.Measure();
            element.Arrange(new Rectangle(0, 0, width, 1000));
        }

        private static int InvokeColumnsPerRow(WrapGrid grid) =>
            (int)typeof(WrapGrid).GetProperty("ColumnsPerRow", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(grid)!;

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }
    }
}
