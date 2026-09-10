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
    public class SelectorTests
    {
        private sealed class TestSelector : Selector
        {
        }

        private sealed class Person
        {
            public string Name { get; set; } = string.Empty;
            public int Id { get; set; }
        }

        [Fact]
        public void Defaults_MatchSpec()
        {
            var selector = new TestSelector();

            Assert.Equal(-1, selector.SelectedIndex);
            Assert.Null(selector.SelectedItem);
            Assert.Null(selector.SelectedValue);
            Assert.False(selector.IsOpen);
        }

        [Fact]
        public void SelectedIndex_OutOfRange_Throws()
        {
            var selector = new TestSelector { ItemsSource = new List<object> { "a", "b" } };

            Assert.Throws<ArgumentOutOfRangeException>(() => selector.SelectedIndex = 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => selector.SelectedIndex = -2);
        }

        [Fact]
        public void SelectedIndex_And_SelectedItem_StaySynchronized()
        {
            var selector = new TestSelector { ItemsSource = new List<object> { "a", "b", "c" } };

            selector.SelectedIndex = 1;
            Assert.Equal("b", selector.SelectedItem);

            selector.SelectedItem = "c";
            Assert.Equal(2, selector.SelectedIndex);
        }

        [Fact]
        public void SelectedItem_NotInTheList_ResolvesToNoSelection()
        {
            var selector = new TestSelector { ItemsSource = new List<object> { "a", "b" } };

            selector.SelectedItem = "not in the list";

            Assert.Equal(-1, selector.SelectedIndex);
            Assert.Null(selector.SelectedItem);
        }

        [Fact]
        public void SelectionChanged_FiresOncePerRealChange()
        {
            var selector = new TestSelector { ItemsSource = new List<object> { "a", "b" } };
            int raised = 0;
            selector.SelectionChanged += (_, _) => raised++;

            selector.SelectedIndex = 0;
            selector.SelectedIndex = 0;

            Assert.Equal(1, raised);
        }

        [Fact]
        public void DisplayMemberPath_And_SelectedValuePath_ResolveAgainstHeterogeneousItems()
        {
            var people = new List<object> { new Person { Name = "Ann", Id = 1 }, "plain string" };
            var selector = new TestSelector
            {
                ItemsSource = people,
                DisplayMemberPath = "Name",
                SelectedValuePath = "Id",
            };

            selector.SelectedIndex = 0;
            Assert.Equal(1, selector.SelectedValue);

            selector.SelectedIndex = 1;
            Assert.Null(selector.SelectedValue);
        }

        [Fact]
        public void GetDisplayText_UnresolvedPath_FallsBackToToString()
        {
            var selector = new TestSelector { DisplayMemberPath = "NoSuchProperty" };

            string text = InvokeGetDisplayText(selector, 42);

            Assert.Equal("42", text);
        }

        [Fact]
        public void CreateContainer_RealizesSelectorItemsNotBareItemContainers()
        {
            var selector = new TestSelector
            {
                ItemsSource = new List<object> { "a" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };

            InvokeEnsureRealized(selector, 0);

            Assert.IsType<SelectorItem>(GetRealizedContainers(selector)[0]);
        }

        [Fact]
        public void SelectionState_SurvivesPoolAndReuseCycle()
        {
            var selector = new TestSelector
            {
                ItemsSource = new List<object> { "a", "b" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            selector.SelectedIndex = 0;
            InvokeEnsureRealized(selector, 0);
            Assert.True(((SelectorItem)GetRealizedContainers(selector)[0]).IsSelected);

            InvokeDerealize(selector, 0);
            InvokeEnsureRealized(selector, 0);

            Assert.True(((SelectorItem)GetRealizedContainers(selector)[0]).IsSelected);
        }

        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas(int viewportWidth = 800, int viewportHeight = 600)
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext { ViewportSize = new Size(viewportWidth, viewportHeight) };
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config) { IsInputEnabled = true, IsVisible = true }, input);
        }

        [Fact]
        public void GetVisualChildren_NeverIncludesRealizedItems_OnlyChrome()
        {
            var selector = new TestSelector
            {
                ItemsSource = new List<object> { "a" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            InvokeEnsureRealized(selector, 0);

            var children = selector.EnumerateVisualSubtree().ToList();

            Assert.DoesNotContain(children, e => e is SelectorItem);
        }

        [Fact]
        public void MeasureContent_IgnoresItemExtentAndSizesToChromeOnly()
        {
            var selector = new TestSelector
            {
                ItemsSource = Enumerable.Range(0, 50).Cast<object>().ToList(),
                Width = 200,
            };

            Size measured = selector.Measure();

            // Base ItemsControl.MeasureContent would size to 50 * DefaultEstimatedItemHeight (2000px) - this asserts
            // Selector's own override (chrome-only) is what's actually running, not that base behavior.
            Assert.True(measured.Height < 2000, $"Expected chrome-only height, got {measured.Height} (looks like full item extent leaked through)");
        }

        [Fact]
        public void IsOpen_SetTrue_AddsThePopupToCanvasOverlays()
        {
            var (canvas, _) = CreateCanvas();
            var selector = new TestSelector { Width = 200, Height = 30, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            canvas.Add(selector);
            canvas.Render();

            selector.IsOpen = true;

            Assert.Single(canvas.Overlays);
        }

        [Fact]
        public void IsOpen_SetFalse_RemovesThePopupFromCanvasOverlays()
        {
            var (canvas, _) = CreateCanvas();
            var selector = new TestSelector { Width = 200, Height = 30 };
            canvas.Add(selector);
            canvas.Render();
            selector.IsOpen = true;

            selector.IsOpen = false;

            Assert.Empty(canvas.Overlays);
        }

        [Fact]
        public void IsOpen_And_ToggleIsChecked_StaySynchronized()
        {
            var (canvas, _) = CreateCanvas();
            var selector = new TestSelector { Width = 200, Height = 30 };
            canvas.Add(selector);
            canvas.Render();

            selector.IsOpen = true;
            Assert.True(GetToggle(selector).IsChecked);

            GetToggle(selector).IsChecked = false;
            Assert.False(selector.IsOpen);
        }

        [Fact]
        public void Opening_RealizesItemsIntoThePopup()
        {
            var (canvas, _) = CreateCanvas();
            var selector = new TestSelector
            {
                ItemsSource = new List<object> { "a", "b", "c" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(selector);
            canvas.Render();

            selector.IsOpen = true;

            Assert.NotEmpty(GetRealizedContainers(selector));
        }

        [Fact]
        public void RealizedItems_KeepDistinctPositions_AcrossARenderPass()
        {
            // Regression coverage for the PopupItemsHost fix: a plain Panel hosting the realized items would have
            // its base Panel.ArrangeContent re-arrange every child to its own full ContentBounds on the very next
            // layout pass, stomping every SelectorItem back on top of each other.
            var (canvas, _) = CreateCanvas();
            var selector = new TestSelector
            {
                ItemsSource = new List<object> { "a", "b", "c" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(selector);
            canvas.Render();

            selector.IsOpen = true;
            canvas.Render();

            var realizedYs = GetRealizedContainers(selector).Values.Select(c => c.ActualBounds.Y).Distinct().ToList();
            Assert.True(realizedYs.Count > 1, $"Expected distinct Y positions across realized items, got: [{string.Join(", ", realizedYs)}]");
        }

        [Fact]
        public void Closing_DoesNotEagerlyDerealize()
        {
            var (canvas, _) = CreateCanvas();
            var selector = new TestSelector
            {
                ItemsSource = new List<object> { "a", "b", "c" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
            };
            canvas.Add(selector);
            canvas.Render();
            selector.IsOpen = true;
            int realizedWhileOpen = GetRealizedContainers(selector).Count;

            selector.IsOpen = false;

            Assert.Equal(realizedWhileOpen, GetRealizedContainers(selector).Count);
        }

        [Fact]
        public void OpeningNearViewportBottom_PlacesThePopupAboveInstead()
        {
            var (canvas, _) = CreateCanvas(viewportWidth: 800, viewportHeight: 200);
            var selector = new TestSelector
            {
                ItemsSource = Enumerable.Range(0, 20).Cast<object>().ToList(),
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="40"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 160, 0, 0),
            };
            canvas.Add(selector);
            canvas.Render();

            selector.IsOpen = true;
            canvas.Render();

            UIElement popup = canvas.Overlays.Single();
            Assert.True(popup.ActualBounds.Y < selector.ActualBounds.Y);
        }

        [Fact]
        public void OpeningWithRoomBelow_PlacesThePopupBelow()
        {
            var (canvas, _) = CreateCanvas(viewportWidth: 800, viewportHeight: 600);
            var selector = new TestSelector
            {
                ItemsSource = Enumerable.Range(0, 5).Cast<object>().ToList(),
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(selector);
            canvas.Render();

            selector.IsOpen = true;
            canvas.Render();

            UIElement popup = canvas.Overlays.Single();
            Assert.True(popup.ActualBounds.Y >= selector.ActualBounds.Bottom);
        }

        private static ToggleButton GetToggle(Selector selector) =>
            (ToggleButton)typeof(Selector).GetProperty("Toggle", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(selector)!;

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }

        private static string InvokeGetDisplayText(Selector selector, object item) =>
            (string)typeof(Selector).GetMethod("GetDisplayText", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(selector, [item])!;

        private static void InvokeEnsureRealized(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("EnsureRealized", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [index]);

        private static void InvokeDerealize(ItemsControl control, int index) =>
            typeof(ItemsControl).GetMethod("Derealize", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(control, [index]);

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(control)!;
    }
}
