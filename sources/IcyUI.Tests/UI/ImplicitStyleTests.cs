using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.UI
{
    /// <summary>
    /// Covers implicit (keyless, exact-type-targeted) <see cref="Style"/> application: a
    /// <c>&lt;Style TargetType="Button"&gt;</c> with no <c>x:Key</c> registers under
    /// <see cref="ResourceDictionary.GetImplicitStyleKey(Type)"/> and is picked up automatically by any exact-type
    /// match that attaches to a <see cref="Canvas"/> with no local <see cref="UIElement.Style"/> already set.
    /// </summary>
    public class ImplicitStyleTests
    {
        private static Canvas CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return new Canvas(config);
        }

        [Fact]
        public void ExactTypeMatch_AppliesAutomaticallyOnAttach()
        {
            var root = new StackPanel();
            var style = new Style(typeof(Button));
            style.Setters["Width"] = 99f;
            root.Resources[ResourceDictionary.GetImplicitStyleKey(typeof(Button))] = style;

            var button = new Button();
            root.Children.Add(button);
            var canvas = CreateCanvas();
            canvas.Add(root);

            Assert.Equal(99f, button.Width);
        }

        [Fact]
        public void SubclassDoesNotMatchABaseTypeImplicitStyle()
        {
            var root = new StackPanel();
            var style = new Style(typeof(Button));
            style.Setters["Width"] = 99f;
            root.Resources[ResourceDictionary.GetImplicitStyleKey(typeof(Button))] = style;

            var toggle = new ToggleButton();
            root.Children.Add(toggle);
            var canvas = CreateCanvas();
            canvas.Add(root);

            Assert.True(float.IsNaN(toggle.Width));
        }

        [Fact]
        public void ExplicitLocalStyle_IsNotOverriddenByAnImplicitOne()
        {
            var root = new StackPanel();
            var implicitStyle = new Style(typeof(Button));
            implicitStyle.Setters["Width"] = 99f;
            root.Resources[ResourceDictionary.GetImplicitStyleKey(typeof(Button))] = implicitStyle;

            var explicitStyle = new Style(typeof(Button));
            explicitStyle.Setters["Width"] = 10f;
            var button = new Button { Style = explicitStyle };
            root.Children.Add(button);
            var canvas = CreateCanvas();
            canvas.Add(root);

            Assert.Equal(10f, button.Width);
        }
    }
}
