using System.Collections.Generic;
using System.Reflection;
using Icy.Assets;
using Icy.Configuration;
using Icy.Rendering.Brushes;
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
        public void ThemedDropdown_AppliesWithoutThrowing()
        {
            var canvas = CreateThemedCanvas();

            var dropdown = new Dropdown { ItemsSource = new List<object> { "One", "Two" }, Width = 200 };
            var exception = Record.Exception(() =>
            {
                canvas.Add(dropdown);
                canvas.Render();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void ThemedDropdown_RepointsToggleAtTheTemplatesPart()
        {
            // Regression coverage: Selector had no OnApplyTemplate override, so applying the default theme's
            // Dropdown ControlTemplate replaced Chrome wholesale while Selector's own `toggle` field kept pointing
            // at the now-orphaned default toggle - a themed Dropdown rendered as an inert empty box. "Didn't throw"
            // never caught that, so this asserts the repoint actually happened.
            var canvas = CreateThemedCanvas();
            var dropdown = new Dropdown { ItemsSource = new List<object> { "One", "Two" }, Width = 200 };
            canvas.Add(dropdown);
            canvas.Render();

            ToggleButton themedToggle = GetToggle(dropdown);
            ToggleButton untemplatedToggle = GetToggle(new Dropdown());

            Assert.NotSame(untemplatedToggle, themedToggle);

            // The live toggle must be a genuine part of the applied template's visual tree, not a detached orphan:
            // walking its parents has to arrive at the Dropdown itself, through the templated Chrome.
            var ancestors = new List<UIElement>();
            for (UIElement? current = themedToggle; current != null; current = current.Parent)
                ancestors.Add(current);

            Assert.Contains(dropdown, ancestors);
            Assert.NotNull(dropdown.Template);
        }

        [Fact]
        public void ThemedDropdown_GetsAnOpaquePopupBackdrop()
        {
            var canvas = CreateThemedCanvas();
            var dropdown = new Dropdown { ItemsSource = new List<object> { "One", "Two" }, Width = 200 };
            canvas.Add(dropdown);
            canvas.Render();

            AssertOpaque(dropdown.PopupBackground);
            AssertOpaque(dropdown.PopupBorderBrush);
            Assert.True(dropdown.PopupBorderThickness.Left > 0);
        }

        [Fact]
        public void ThemedComboBox_AppliesWithoutThrowing()
        {
            var canvas = CreateThemedCanvas();

            var comboBox = new ComboBox { ItemsSource = new List<object> { "One", "Two" }, Width = 200 };
            var exception = Record.Exception(() =>
            {
                canvas.Add(comboBox);
                canvas.Render();
            });

            Assert.Null(exception);
        }

        [Fact]
        public void ThemedComboBox_GetsAnOpaquePopupBackdrop()
        {
            var canvas = CreateThemedCanvas();
            var comboBox = new ComboBox { ItemsSource = new List<object> { "One", "Two" }, Width = 200 };
            canvas.Add(comboBox);
            canvas.Render();

            AssertOpaque(comboBox.PopupBackground);
            AssertOpaque(comboBox.PopupBorderBrush);
            Assert.True(comboBox.PopupBorderThickness.Left > 0);
        }

        private static Canvas CreateThemedCanvas()
        {
            var builder = new IcyConfigurationBuilder();
            builder.ConfigureRendering(new FakeRenderContext())
                   .ConfigureInput(new FakeInputSystem())
                   .ConfigureTypes()
                   .ConfigureAssets();
            var config = builder.Build().UseDefaultTheme();
            return new Canvas(config) { IsInputEnabled = true, IsVisible = true };
        }

        private static void AssertOpaque(IBrush? brush)
        {
            var solid = Assert.IsType<SolidColorBrush>(brush);
            Assert.True(solid.Color.A > 0, $"Expected an opaque popup brush, got {solid.Color}.");
        }

        private static ToggleButton GetToggle(Selector selector) =>
            (ToggleButton)typeof(Selector).GetProperty("Toggle", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(selector)!;
    }
}
