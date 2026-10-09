using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class SelectingItemsControlTests
    {
        private sealed class TestSelectingItemsControl : SelectingItemsControl
        {
        }

        private sealed class TrackingSelectingItemsControl : SelectingItemsControl
        {
            private int nextOrder;

            public int? HookOrder { get; private set; }

            public int? EventOrder { get; private set; }

            public TrackingSelectingItemsControl()
            {
                SelectionChanged += (_, _) => EventOrder = nextOrder++;
            }

            protected override void OnSelectionChanged() => HookOrder = nextOrder++;
        }

        [Fact]
        public void Defaults_MatchSpec()
        {
            var control = new TestSelectingItemsControl();

            Assert.Equal(-1, control.SelectedIndex);
            Assert.Null(control.SelectedItem);
        }

        [Fact]
        public void SelectedIndex_OutOfRange_Throws()
        {
            var control = new TestSelectingItemsControl { ItemsSource = new List<object> { "a", "b" } };

            Assert.Throws<ArgumentOutOfRangeException>(() => control.SelectedIndex = 2);
            Assert.Throws<ArgumentOutOfRangeException>(() => control.SelectedIndex = -2);
        }

        [Fact]
        public void SelectedIndex_And_SelectedItem_StaySynchronized()
        {
            var control = new TestSelectingItemsControl { ItemsSource = new List<object> { "a", "b", "c" } };

            control.SelectedIndex = 1;
            Assert.Equal("b", control.SelectedItem);

            control.SelectedItem = "c";
            Assert.Equal(2, control.SelectedIndex);
        }

        [Fact]
        public void SelectedItem_NotInTheList_ResolvesToNoSelection()
        {
            var control = new TestSelectingItemsControl { ItemsSource = new List<object> { "a", "b" } };

            control.SelectedItem = "not in the list";

            Assert.Equal(-1, control.SelectedIndex);
            Assert.Null(control.SelectedItem);
        }

        [Fact]
        public void SelectionChanged_FiresOncePerRealChange()
        {
            var control = new TestSelectingItemsControl { ItemsSource = new List<object> { "a", "b" } };
            int raised = 0;
            control.SelectionChanged += (_, _) => raised++;

            control.SelectedIndex = 0;
            control.SelectedIndex = 0;

            Assert.Equal(1, raised);
        }

        [Fact]
        public void CreateContainer_RealizesSelectorItemsNotBareItemContainers()
        {
            var control = new TestSelectingItemsControl
            {
                ItemsSource = new List<object> { "a" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };

            InvokeEnsureRealized(control, 0);

            Assert.IsType<SelectorItem>(GetRealizedContainers(control)[0]);
        }

        [Fact]
        public void SelectionState_SurvivesPoolAndReuseCycle()
        {
            var control = new TestSelectingItemsControl
            {
                ItemsSource = new List<object> { "a", "b" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            control.SelectedIndex = 0;
            InvokeEnsureRealized(control, 0);
            Assert.True(((SelectorItem)GetRealizedContainers(control)[0]).IsSelected);

            InvokeDerealize(control, 0);
            InvokeEnsureRealized(control, 0);

            Assert.True(((SelectorItem)GetRealizedContainers(control)[0]).IsSelected);
        }

        [Fact]
        public void SelectedIndex_ClearsWhenTheLiveCollectionShrinksPastIt()
        {
            var items = new ObservableCollection<object> { "a", "b", "c" };
            var control = new TestSelectingItemsControl { ItemsSource = items };
            control.SelectedIndex = 2;

            items.RemoveAt(2);
            items.RemoveAt(1);

            Assert.Equal(-1, control.SelectedIndex);
            Assert.Null(control.SelectedItem);
        }

        [Fact]
        public void OnSelectionChanged_FiresBeforeThePublicSelectionChangedEvent()
        {
            var control = new TrackingSelectingItemsControl { ItemsSource = new List<object> { "a", "b" } };

            control.SelectedIndex = 0;

            Assert.Equal(0, control.HookOrder);
            Assert.Equal(1, control.EventOrder);
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
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetProperty("RealizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;
    }
}
