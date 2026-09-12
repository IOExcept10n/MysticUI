using System;
using System.Collections.Generic;
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

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;

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
