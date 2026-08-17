using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class WindowTests
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
        public void Constructor_IsFocusScopeAndStartsInvisible()
        {
            var window = new Window();

            Assert.True(window.IsFocusScope);
            Assert.False(window.IsVisible);
        }

        [Fact]
        public void Show_MakesVisibleAndFocusesFirstFocusableDescendant()
        {
            var canvas = CreateCanvas();
            var button = new Button();
            var window = new Window { Content = button };
            canvas.Add(window);

            window.Show();

            Assert.True(window.IsVisible);
            Assert.Same(button, canvas.FocusedElement);
        }

        [Fact]
        public void Close_RestoresFocusToElementFocusedBeforeShow()
        {
            var canvas = CreateCanvas();
            var opener = new Button();
            var dialogButton = new Button();
            var window = new Window { Content = dialogButton };
            canvas.Add(opener);
            canvas.Add(window);
            canvas.Focus(opener);

            window.Show();
            Assert.Same(dialogButton, canvas.FocusedElement);

            window.Close();

            Assert.Same(opener, canvas.FocusedElement);
            Assert.False(window.IsVisible);
        }

        [Fact]
        public void OpenedAndClosed_EventsFire()
        {
            var window = new Window();
            bool opened = false;
            bool closed = false;
            window.Opened += (_, _) => opened = true;
            window.Closed += (_, _) => closed = true;

            window.Show();
            Assert.True(opened);

            window.Close();
            Assert.True(closed);
        }
    }
}
