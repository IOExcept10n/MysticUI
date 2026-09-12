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
    public class ListBoxTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var listBox = new ListBox();

            Assert.Equal(-1, listBox.SelectedIndex);
            Assert.Null(listBox.SelectedItem);
        }

        [Fact]
        public void CreateContainer_RealizesSelectorItems()
        {
            var listBox = new ListBox
            {
                ItemsSource = new List<object> { "a" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
            };

            InvokeEnsureRealized(listBox, 0);

            Assert.IsType<SelectorItem>(GetRealizedContainers(listBox)[0]);
        }

        [Fact]
        public void SelectionState_SurvivesPoolAndReuseCycle()
        {
            var listBox = new ListBox
            {
                ItemsSource = new List<object> { "a", "b" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
            };
            listBox.SelectedIndex = 0;
            InvokeEnsureRealized(listBox, 0);
            Assert.True(((SelectorItem)GetRealizedContainers(listBox)[0]).IsSelected);

            InvokeDerealize(listBox, 0);
            InvokeEnsureRealized(listBox, 0);

            Assert.True(((SelectorItem)GetRealizedContainers(listBox)[0]).IsSelected);
        }

        [Fact(Skip = "TODO: Debug canvas tap routing to SelectorItem.Tapped")]
        public void TappingARealizedItem_SelectsIt()
        {
            var (canvas, input) = CreateCanvas();
            var listBox = new ListBox
            {
                ItemsSource = new List<object> { "a", "b", "c" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 100,
                Height = 100,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(listBox);
            canvas.Render();

            Point tapPoint = new(listBox.ActualBounds.X + 5, listBox.ActualBounds.Y + 25); // second row (20px each)
            input.Events.Touch.RaiseTap(new Icy.Input.Events.TouchInfo(tapPoint, 1));

            Assert.Equal(1, listBox.SelectedIndex);
        }

        [Fact]
        public void HostedInsideScrollViewer_OnlyRealizesTheVisibleRange()
        {
            var template = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>""");
            var listBox = new ListBox
            {
                ItemsSource = Enumerable.Range(0, 10000).Cast<object>().ToList(),
                ItemTemplate = template,
            };
            var scrollViewer = new ScrollViewer { Content = listBox, Width = 300, Height = 400 };
            var (canvas, _) = CreateCanvas();
            canvas.Add(scrollViewer);

            canvas.Render();

            var realized = GetRealizedContainers(listBox);
            Assert.True(realized.Count < 50, $"expected far fewer than 10000 realized, got {realized.Count}");
        }

        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas(int viewportWidth = 800, int viewportHeight = 600)
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext { ViewportSize = new Size(viewportWidth, viewportHeight) };
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        private static void InvokeEnsureRealized(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("EnsureRealized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static void InvokeDerealize(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("Derealize", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(control, [index]);

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;
    }
}
