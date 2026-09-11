using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ComboBoxTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var comboBox = new ComboBox();

            Assert.Equal(-1, comboBox.SelectedIndex);
            Assert.Equal(string.Empty, GetTextBox(comboBox).Text);
        }

        [Fact]
        public void Filter_IsCaseInsensitiveContains()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana", "Pineapple" } };

            GetTextBox(comboBox).Text = "APP";

            var visible = InvokeGetFilteredDisplayTexts(comboBox);
            Assert.Equal(new[] { "Apple", "Pineapple" }, visible);
        }

        [Fact]
        public void TypingWhileClosed_OpensAndFiltersFromTheFirstKeystroke()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana" } };

            GetTextBox(comboBox).Text = "a";

            Assert.True(comboBox.IsOpen);
        }

        [Fact]
        public void EnterWithHighlightedMatch_CommitsAndUpdatesText()
        {
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            var input = new FakeInputSystem();
            SimulateFocused(comboBox, input);
            GetTextBox(comboBox).Text = "ban";
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));

            InvokeOnNavigationSelectElement(comboBox);

            Assert.Equal("Banana", comboBox.SelectedItem);
            Assert.Equal("Banana", GetTextBox(comboBox).Text);
            Assert.False(comboBox.IsOpen);
        }

        [Fact]
        public void EnterWithNoMatch_RevertsTextWithoutChangingSelection()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana" } };
            comboBox.SelectedItem = "Apple";
            GetTextBox(comboBox).Text = "zzz";

            InvokeOnNavigationSelectElement(comboBox);

            Assert.Equal("Apple", comboBox.SelectedItem);
            Assert.Equal("Apple", GetTextBox(comboBox).Text);
        }

        [Fact]
        public void Escape_RevertsTextAndCloses()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana" } };
            comboBox.SelectedItem = "Apple";
            comboBox.IsOpen = true;
            GetTextBox(comboBox).Text = "something else";

            InvokeOnNavigationCloseModal(comboBox);

            Assert.False(comboBox.IsOpen);
            Assert.Equal("Apple", GetTextBox(comboBox).Text);
            Assert.Equal("Apple", comboBox.SelectedItem);
        }

        [Fact]
        public void EnterAfterTheFilterNarrowsBelowTheHighlight_CommitsTheRemainingMatchWithoutThrowing()
        {
            // Regression coverage: HighlightedIndex was only ever clamped at arrow-key time, so narrowing the filter
            // below it left it stale - Enter then ran SelectedIndex = HighlightedIndex straight into Selector's own
            // ArgumentOutOfRangeException guard.
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Apricot", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            var input = new FakeInputSystem();
            SimulateFocused(comboBox, input);

            GetTextBox(comboBox).Text = "ap";
            Assert.Equal(new[] { "Apple", "Apricot" }, InvokeGetFilteredDisplayTexts(comboBox));

            // Highlight the second match (index 1 of the two-item filtered view)...
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));

            // ...then narrow the list to a single item, leaving index 1 out of range.
            GetTextBox(comboBox).Text = "appl";
            Assert.Equal(new[] { "Apple" }, InvokeGetFilteredDisplayTexts(comboBox));

            var exception = Record.Exception(() => InvokeOnNavigationSelectElement(comboBox));

            Assert.Null(exception);
            Assert.False(comboBox.IsOpen);

            // The stale highlight is dropped rather than committed, so this takes the revert path and selects
            // nothing - re-highlighting inside the narrowed list is what commits the remaining match.
            Assert.Null(comboBox.SelectedItem);

            GetTextBox(comboBox).Text = "appl";
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));
            InvokeOnNavigationSelectElement(comboBox);

            Assert.Equal("Apple", comboBox.SelectedItem);
            Assert.Equal("Apple", GetTextBox(comboBox).Text);
        }

        [Fact]
        public void AfterCommit_TheFullItemListIsAvailableAgain()
        {
            // Regression coverage: every commit/revert path left base.ItemsSource narrowed to (essentially) the
            // single committed item, so reopening the popup showed one row until the user typed again.
            var comboBox = new ComboBox
            {
                ItemsSource = new List<object> { "Apple", "Banana", "Cherry" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border/></DataTemplate>"""),
            };
            var input = new FakeInputSystem();
            SimulateFocused(comboBox, input);
            GetTextBox(comboBox).Text = "ban";
            InvokeOnNavigationFocusChanging(comboBox, new System.Numerics.Vector2(0, 1));

            InvokeOnNavigationSelectElement(comboBox);

            Assert.Equal("Banana", comboBox.SelectedItem);
            Assert.Equal(new[] { "Apple", "Banana", "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));
        }

        [Fact]
        public void AfterEscapeRevert_TheFullItemListIsAvailableAgain()
        {
            var comboBox = new ComboBox { ItemsSource = new List<object> { "Apple", "Banana", "Cherry" } };
            comboBox.SelectedItem = "Apple";
            comboBox.IsOpen = true;
            GetTextBox(comboBox).Text = "ban";

            InvokeOnNavigationCloseModal(comboBox);

            Assert.Equal(new[] { "Apple", "Banana", "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));
        }

        [Fact]
        public void LiveCollectionChangesOnTheRealSource_ReachTheFilteredView()
        {
            // Regression coverage: base.ItemsSource only ever pointed at a throwaway filtered snapshot, so
            // ItemsControl's own INotifyCollectionChanged observation never watched the user's real collection -
            // mutating it after binding was silently ignored.
            var source = new ObservableCollection<string> { "Apple", "Banana" };
            var comboBox = new ComboBox { ItemsSource = source };

            source.Add("Cherry");

            Assert.Equal(new[] { "Apple", "Banana", "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));

            source.Remove("Banana");

            Assert.Equal(new[] { "Apple", "Cherry" }, InvokeGetFilteredDisplayTexts(comboBox));
        }

        [Fact]
        public void LiveCollectionChanges_RespectTheCurrentFilter()
        {
            var source = new ObservableCollection<string> { "Apple", "Banana" };
            var comboBox = new ComboBox { ItemsSource = source };
            GetTextBox(comboBox).Text = "an";

            source.Add("Mango");

            Assert.Equal(new[] { "Banana", "Mango" }, InvokeGetFilteredDisplayTexts(comboBox));
        }

        [Fact]
        public void FilteringWhileOpen_ResizesThePopupToTheNarrowedList()
        {
            // Popup geometry used to be computed once, at open, and never again - so a ComboBox whose filter cut a
            // 20-item list down to one kept rendering a full-height popup over mostly empty space.
            var comboBox = new ComboBox
            {
                ItemsSource = Enumerable.Range(0, 20).Select(i => (object)$"Item {i}").ToList(),
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var input = new FakeInputSystem();
            Canvas canvas = SimulateFocused(comboBox, input);
            comboBox.IsOpen = true;
            canvas.Render();
            int fullHeight = canvas.Overlays.Single().ActualBounds.Height;

            GetTextBox(comboBox).Text = "Item 7";
            canvas.Render();

            Assert.Equal(new[] { "Item 7" }, InvokeGetFilteredDisplayTexts(comboBox));
            int narrowedHeight = canvas.Overlays.Single().ActualBounds.Height;
            Assert.True(narrowedHeight < fullHeight, $"Expected the popup to shrink with the filtered list, got {narrowedHeight} vs {fullHeight}.");
        }

        private static TextBox GetTextBox(ComboBox comboBox) =>
            (TextBox)typeof(ComboBox).GetField("textBox", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(comboBox)!;

        private static List<string> InvokeGetFilteredDisplayTexts(ComboBox comboBox)
        {
            var itemsControl = (Icy.UI.Controls.ItemsControl)comboBox;
            int count = (int)typeof(ItemsControl).GetProperty("ItemCount", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(itemsControl)!;
            var getItemAt = typeof(ItemsControl).GetMethod("GetItemAt", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var results = new List<string>();
            for (int i = 0; i < count; i++)
                results.Add((string)getItemAt.Invoke(itemsControl, [i])!);
            return results;
        }

        private static void InvokeOnNavigationSelectElement(ComboBox comboBox) =>
            typeof(Selector).GetMethod("OnNavigationSelectElement", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(comboBox, [null, System.EventArgs.Empty]);

        private static void InvokeOnNavigationCloseModal(ComboBox comboBox) =>
            typeof(Selector).GetMethod("OnNavigationCloseModal", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(comboBox, [null, System.EventArgs.Empty]);

        private static void InvokeOnNavigationFocusChanging(ComboBox comboBox, System.Numerics.Vector2 direction)
        {
            var args = new Icy.Data.AcceptableEventArgs<System.Numerics.Vector2> { Data = direction };
            typeof(Selector).GetMethod("OnNavigationFocusChanging", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(comboBox, [null, args]);
        }

        private static Canvas SimulateFocused(ComboBox comboBox, FakeInputSystem input)
        {
            var assets = new Icy.Configuration.AssetConfiguration(Icy.Assets.AssetContext.ApplicationContext);
            var renderContext = new Icy.Tests.Rendering.FakeRenderContext();
            var config = new Icy.Configuration.IcyConfiguration(input, assets, renderContext, new Icy.Configuration.ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(comboBox);
            canvas.Render();
            canvas.Focus(GetTextBox(comboBox));
            return canvas;
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new Icy.Configuration.IcyConfiguration(
                new FakeInputSystem(),
                new Icy.Configuration.AssetConfiguration(Icy.Assets.AssetContext.ApplicationContext),
                new Icy.Tests.Rendering.FakeRenderContext(),
                new Icy.Configuration.ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
        }
    }
}
