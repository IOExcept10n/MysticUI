using System.Collections.Generic;
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

        private static void SimulateFocused(ComboBox comboBox, FakeInputSystem input)
        {
            var assets = new Icy.Configuration.AssetConfiguration(Icy.Assets.AssetContext.ApplicationContext);
            var renderContext = new Icy.Tests.Rendering.FakeRenderContext();
            var config = new Icy.Configuration.IcyConfiguration(input, assets, renderContext, new Icy.Configuration.ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(comboBox);
            canvas.Render();
            canvas.Focus(GetTextBox(comboBox));
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
