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
