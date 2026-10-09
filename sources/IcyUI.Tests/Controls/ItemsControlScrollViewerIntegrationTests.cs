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
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetProperty("RealizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;

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
            scrollViewer.VerticalOffset = 8000;
            var containersAfterSecondScroll = GetRealizedContainers(itemsControl).Values.ToHashSet();

            // The first scroll (0->4000) de-realizes the top range, returning its containers to the template's
            // pool; RealizeRange realizes the new range before it de-realizes the old one, so pooled reuse only
            // becomes observable on the NEXT realize pass, not the same one that triggered the de-realization -
            // hence the second scroll here.
            Assert.True(
                containersAtTop.Overlaps(containersAfterSecondScroll),
                "expected at least one ItemContainer instance to be reused (pooled) after a subsequent scroll");
        }
    }
}
