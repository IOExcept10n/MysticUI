using System.Collections.Generic;
using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Devices;
using Icy.Input.Events;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
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

            input.Keyboard.RaiseKeyDown(Keys.B);

            Assert.Equal(1, dropdown.SelectedIndex);
        }

        [Fact]
        public void Typeahead_RepeatedSameLetter_CyclesToTheNextMatch()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Apricot", "Banana" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);

            input.Keyboard.RaiseKeyDown(Keys.A);
            Assert.Equal(0, dropdown.SelectedIndex);

            input.Keyboard.RaiseKeyDown(Keys.A);
            Assert.Equal(1, dropdown.SelectedIndex);
        }

        [Fact]
        public void Typeahead_WrapsAtTheEndOfTheList()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Apricot" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);
            dropdown.SelectedIndex = 1;

            input.Keyboard.RaiseKeyDown(Keys.A);

            Assert.Equal(0, dropdown.SelectedIndex);
        }

        [Fact]
        public void Typeahead_DoesNotRequireThePopupToBeOpen()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Banana" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);

            input.Keyboard.RaiseKeyDown(Keys.B);

            Assert.False(dropdown.IsOpen);
            Assert.Equal(1, dropdown.SelectedIndex);
        }

        [Fact]
        public void Typeahead_IgnoresNonLetterKeys()
        {
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Banana" }, IsFocusable = true };
            var input = new FakeInputSystem();
            SimulateFocused(dropdown, input);

            input.Keyboard.RaiseKeyDown(Keys.D1);
            input.Keyboard.RaiseKeyDown(Keys.Space);
            input.Keyboard.RaiseKeyDown(Keys.Enter);

            Assert.Equal(-1, dropdown.SelectedIndex);
        }

        [Fact]
        public void KeyDown_NeverEnablesTextInput_AndStopsFiringAfterFocusIsLost()
        {
            // Regression: a Dropdown never accepts free text, so it must never put the input system into "text
            // input" mode - on some platforms that's what triggers an IME candidate window or on-screen keyboard,
            // which would surprise a user (e.g. typing in Japanese) with a popup over a control that has nowhere
            // to show composed text. See the class remarks on Dropdown for the full reasoning.
            var dropdown = new Dropdown { ItemsSource = new List<object> { "Apple", "Banana" }, IsFocusable = true };
            var input = new FakeInputSystem();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var renderContext = new FakeRenderContext();
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(dropdown);
            canvas.Render();

            canvas.Focus(dropdown);
            input.Keyboard.RaiseKeyDown(Keys.B);
            Assert.Equal(1, dropdown.SelectedIndex);
            Assert.False(input.Events.Text.IsTextInputEnabled);

            canvas.Focus(null);
            dropdown.SelectedIndex = -1;
            input.Keyboard.RaiseKeyDown(Keys.B);

            Assert.Equal(-1, dropdown.SelectedIndex);
            Assert.False(input.Events.Text.IsTextInputEnabled);
        }

        [Fact]
        public void Tapping_FocusesTheDropdownItself_NotItsInternalToggleButton()
        {
            // Regression coverage: ToggleButton/Button default to IsFocusable, and Canvas.OnTap focuses the nearest
            // focusable ancestor of whatever it hit - so tapping a Dropdown used to land focus on the internal
            // toggle, leaving the Dropdown's own focus gate (and therefore navigation + typeahead) inert until the
            // user happened to Tab to it instead.
            var dropdown = new Dropdown
            {
                ItemsSource = new List<object> { "Apple", "Banana" },
                ItemTemplate = LoadDataTemplate("""<DataTemplate><Border Height="20"/></DataTemplate>"""),
                Width = 200,
                Height = 30,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            var input = new FakeInputSystem();
            var config = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };
            canvas.Add(dropdown);
            canvas.Render();

            input.Events.Touch.RaiseTap(new TouchInfo(new Point(dropdown.ActualBounds.X + 5, dropdown.ActualBounds.Y + 5), 1));

            Assert.Same(dropdown, canvas.FocusedElement);

            // The tap must still have reached the toggle itself (a non-focusable element is not an un-tappable one),
            // otherwise the assertion above would hold for the wrong reason.
            Assert.True(dropdown.IsOpen);

            // End to end: with focus on the Dropdown rather than its toggle, typeahead is live straight after a
            // mouse/touch tap - the actual user-visible symptom this fix is about.
            input.Keyboard.RaiseKeyDown(Keys.B);
            Assert.Equal(1, dropdown.SelectedIndex);
        }

        private static DataTemplate LoadDataTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (DataTemplate)loader.LoadObject(markup);
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
