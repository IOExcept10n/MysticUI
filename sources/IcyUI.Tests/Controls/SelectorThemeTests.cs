using System.Collections.Generic;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class SelectorThemeTests
    {
        [Fact]
        public void ThemedDropdown_HasAToggleButtonAndAppliesWithoutThrowing()
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets();
            var config = builder.Build().UseDefaultTheme();
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };

            var dropdown = new Dropdown { ItemsSource = new List<object> { "One", "Two" }, Width = 200 };
            var exception = Record.Exception(() =>
            {
                canvas.Add(dropdown);
                canvas.Render();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void ThemedComboBox_AppliesWithoutThrowing()
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets();
            var config = builder.Build().UseDefaultTheme();
            var canvas = new Canvas(config) { IsInputEnabled = true, IsVisible = true };

            var comboBox = new ComboBox { ItemsSource = new List<object> { "One", "Two" }, Width = 200 };
            var exception = Record.Exception(() =>
            {
                canvas.Add(comboBox);
                canvas.Render();
            });

            Assert.Null(exception);
        }
    }
}
