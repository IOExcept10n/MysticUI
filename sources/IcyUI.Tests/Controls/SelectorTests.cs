using System;
using System.Collections.Generic;
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
