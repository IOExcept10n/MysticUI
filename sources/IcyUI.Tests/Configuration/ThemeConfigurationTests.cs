using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Rendering.Brushes;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Configuration
{
    /// <summary>
    /// Covers the <see cref="ThemeConfiguration"/> facet, <see cref="BuildingExtensions.UseDefaultTheme"/> (which
    /// loads IcyUI's bundled default theme), and <see cref="Canvas.Resources"/>'s role as the final fallback for
    /// implicit-style resolution once an element's own ancestor chain is exhausted.
    /// </summary>
    public class ThemeConfigurationTests
    {
        private static IcyConfiguration CreateConfiguration() =>
            new(new FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());

        [Fact]
        public void Theme_DefaultsToNoTheme()
        {
            IcyConfiguration configuration = CreateConfiguration();

            Assert.Null(configuration.Theme.Theme);
        }

        [Fact]
        public void Canvas_WithNoTheme_HasEmptyResources()
        {
            var canvas = new Canvas(CreateConfiguration());

            Assert.False(canvas.Resources.TryGetValue(ResourceDictionary.GetImplicitStyleKey(typeof(Button)), out _));
        }

        [Fact]
        public void UseDefaultTheme_LoadsTheBundledThemeIntoConfiguration()
        {
            IcyConfiguration configuration = CreateConfiguration();

            IcyConfiguration result = configuration.UseDefaultTheme();

            Assert.Same(configuration, result);
            Assert.NotNull(configuration.Theme.Theme);
        }

        [Fact]
        public void UseDefaultTheme_RegistersImplicitStylesForEveryTier1Control()
        {
            IcyConfiguration configuration = CreateConfiguration().UseDefaultTheme();
            ResourceDictionary theme = configuration.Theme.Theme!;

            foreach (Type controlType in new[]
            {
                typeof(Button), typeof(ToggleButton), typeof(CheckBox), typeof(Slider),
                typeof(ProgressBar), typeof(TextBox), typeof(ScrollViewer), typeof(Window),
            })
            {
                Assert.True(
                    theme.ContainsKey(ResourceDictionary.GetImplicitStyleKey(controlType)),
                    $"Expected the default theme to register an implicit style for {controlType.Name}.");
            }
        }

        [Fact]
        public void Canvas_BuiltWithThemedConfiguration_MergesTheThemeIntoItsOwnResources()
        {
            var canvas = new Canvas(CreateConfiguration().UseDefaultTheme());

            Assert.True(canvas.Resources.TryGetValue(ResourceDictionary.GetImplicitStyleKey(typeof(Button)), out _));
        }

        [Fact]
        public void Button_AddedToThemedCanvas_ResolvesTheImplicitStyleThroughCanvasResources()
        {
            // Regression guard for the actual point of this whole facet: a plain Button, with no Style set anywhere
            // in its own ancestor chain, must still pick up the theme once attached - proving
            // UIElement.ResolveImplicitStyle's new Canvas.Resources fallback actually fires, not just that the
            // dictionary itself contains the right entry (the prior test only proves that).
            var canvas = new Canvas(CreateConfiguration().UseDefaultTheme()) { IsVisible = true };
            var button = new Button();

            canvas.Add(button);

            Assert.NotNull(button.Style);
            var background = Assert.IsType<SolidColorBrush>(button.Background);
            Assert.Equal(Color.FromArgb(0xFF, 0x3A, 0x3A, 0x44).ToArgb(), background.Color.ToArgb());
        }
    }
}
