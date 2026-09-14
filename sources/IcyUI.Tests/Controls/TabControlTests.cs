// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class TabControlTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var tabControl = new TabControl();

            Assert.Equal(-1, tabControl.SelectedIndex);
            Assert.Null(tabControl.SelectedItem);
        }

        [Fact]
        public void ItemsSource_OfTabItems_RealizesOneHeaderPerItem_WithNoExplicitItemTemplate()
        {
            var tabControl = new TabControl
            {
                ItemsSource = new List<object>
                {
                    new TabItem { Header = "First" },
                    new TabItem { Header = "Second" },
                },
            };

            tabControl.Measure();

            Assert.Equal(2, GetRealizedContainers(tabControl).Count);
        }

        [Fact]
        public void SelectingATab_ShowsItsContent_AndSelectingAnother_SwapsIt()
        {
            var firstContent = new TextBlock { Text = "A" };
            var secondContent = new TextBlock { Text = "B" };
            var tabControl = new TabControl
            {
                ItemsSource = new List<object>
                {
                    new TabItem { Header = "First", Content = firstContent },
                    new TabItem { Header = "Second", Content = secondContent },
                },
            };
            tabControl.Measure();

            tabControl.SelectedIndex = 0;
            Assert.Same(firstContent, GetContentPresenter(tabControl).Content);

            tabControl.SelectedIndex = 1;
            Assert.Same(secondContent, GetContentPresenter(tabControl).Content);
        }

        [Fact]
        public void HeadersAreArranged_LeftToRight_InSelectionOrder()
        {
            var tabControl = new TabControl
            {
                ItemsSource = new List<object>
                {
                    new TabItem { Header = "First" },
                    new TabItem { Header = "Second" },
                },
                Width = 400,
                Height = 200,
            };
            tabControl.Measure();
            tabControl.Arrange(new Rectangle(0, 0, 400, 200));

            var containers = GetRealizedContainers(tabControl);
            Assert.True(containers[1].ActualBounds.X >= containers[0].ActualBounds.X + containers[0].ActualBounds.Width);
            Assert.Equal(containers[0].ActualBounds.Y, containers[1].ActualBounds.Y);
        }

        [Fact]
        public void TappingADisabledTabsHeader_DoesNotChangeSelection()
        {
            var tabControl = new TabControl
            {
                ItemsSource = new List<object>
                {
                    new TabItem { Header = "First" },
                    new TabItem { Header = "Second", IsEnabled = false },
                },
                Width = 400,
                Height = 200,
            };
            tabControl.Measure();
            tabControl.Arrange(new Rectangle(0, 0, 400, 200));
            tabControl.SelectedIndex = 0;

            var disabledHeader = GetRealizedContainers(tabControl)[1];
            Assert.False(disabledHeader.IsEnabled);

            InvokeOnTap(disabledHeader);

            Assert.Equal(0, tabControl.SelectedIndex);
        }

        [Fact]
        public void RemovingAndReAddingATabAtTheSameIndex_ShowsTheNewItemsHeader_NotAStalePooledOne()
        {
            // Regression test for a pooling/binding mismatch: TabControl.CreateContainer builds an unbound header
            // tree (no {Binding}), so ItemsControl.RentContainer's pooled-reuse path - which only reassigns
            // Content.DataContext - can't refresh it. Without PoolingEnabled = false, a recycled container here
            // would keep showing a previous tab's header text.
            var items = new ObservableCollection<TabItem>
            {
                new TabItem { Header = "First" },
                new TabItem { Header = "Second" },
            };
            var tabControl = new TabControl { ItemsSource = items };
            tabControl.Measure();

            items.RemoveAt(0);
            items.Insert(0, new TabItem { Header = "Replaced" });
            tabControl.Measure();

            string headerText = GetHeaderText(GetRealizedContainers(tabControl)[0]);
            Assert.Equal("Replaced", headerText);
        }

        private static string GetHeaderText(ItemContainer container)
        {
            var stackPanel = (StackPanel)container.Content!;
            var presenter = (ContentPresenter)stackPanel.Children.OfType<ContentPresenter>().Single();
            var textBlock = (TextBlock)presenter.Content!;
            return textBlock.Text;
        }

        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl).GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(control)!;

        private static ContentPresenter GetContentPresenter(TabControl tabControl) =>
            (ContentPresenter)typeof(TabControl).GetField("contentPresenter", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(tabControl)!;

        private static void InvokeOnTap(UIElement element) =>
            typeof(UIElement).GetMethod("OnTap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.Invoke(element, null);
    }
}
