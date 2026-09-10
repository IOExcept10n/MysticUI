using System.Collections.Generic;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class DropdownTests
    {
        [Fact]
        public void Defaults_MatchSpec()
        {
            var dropdown = new Dropdown();

            Assert.Equal(-1, dropdown.SelectedIndex);
            Assert.False(dropdown.IsOpen);
        }

        [Fact]
        public void Typeahead_JumpsToTheNextMatchingItem()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Banana", "Cherry" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);

            input.Events.Text.RaiseTextInput("b");

            Assert.Equal(1, dropdown.SelectedIndex);
        }

        [Fact]
        public void Typeahead_IsCaseInsensitive()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Banana" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);

            input.Events.Text.RaiseTextInput("B");

            Assert.Equal(1, dropdown.SelectedIndex);
        }

        [Fact]
        public void Typeahead_RepeatedSameLetter_CyclesToTheNextMatch()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Apricot", "Banana" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);

            input.Events.Text.RaiseTextInput("a");
            Assert.Equal(0, dropdown.SelectedIndex);

            input.Events.Text.RaiseTextInput("a");
            Assert.Equal(1, dropdown.SelectedIndex);
        }

        [Fact]
        public void Typeahead_WrapsAtTheEndOfTheList()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Apricot" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);
            dropdown.SelectedIndex = 1;

            input.Events.Text.RaiseTextInput("a");

            Assert.Equal(0, dropdown.SelectedIndex);
        }

        [Fact]
        public void Typeahead_DoesNotRequireThePopupToBeOpen()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Banana" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);

            input.Events.Text.RaiseTextInput("b");

            Assert.False(dropdown.IsOpen);
            Assert.Equal(1, dropdown.SelectedIndex);
        }

        private static void SimulateFocused(Dropdown dropdown, FakeInputSystem input)
        {
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var renderContext = new FakeRenderContext();
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(dropdown);
            canvas.Render();
            canvas.Focus(dropdown);
        }
    }
}
