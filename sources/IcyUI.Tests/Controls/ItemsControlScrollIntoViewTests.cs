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
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetProperty("RealizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;

        private sealed class RecordingListBox : ListBox
        {
            public List<int> Tapped { get; } = [];

            protected override void OnContainerTapped(int index) => Tapped.Add(index);
        }
    }
}
