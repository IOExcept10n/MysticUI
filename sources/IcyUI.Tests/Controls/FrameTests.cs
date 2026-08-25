using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class FrameTests
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
        public void Constructor_CreatesANavigationServiceWithNoCurrentPage()
        {
            var frame = new Frame();

            Assert.NotNull(frame.Navigation);
            Assert.Null(frame.Navigation.CurrentPage);
        }

        [Fact]
        public void Navigation_NavigateTo_WorksBeforeTheFrameIsAttached()
        {
            // NavigateTo(Page) needs no configuration - only Navigate(string) has to load through the asset
            // pipeline - so it works even on a frame that isn't part of a Canvas yet.
            var frame = new Frame();
            var page = new Page();

            Page result = frame.Navigation.NavigateTo(page);

            Assert.Same(page, result);
            Assert.Same(page, frame.Content);
        }

        [Fact]
        public void Navigation_Navigate_BeforeTheFrameIsAttached_Throws()
        {
            var frame = new Frame();

            Assert.Throws<InvalidOperationException>(() => frame.Navigation.Navigate("Resources/TestPage.xml"));
        }

        [Fact]
        public void Navigation_Navigate_AfterTheFrameIsAttached_ResolvesConfigurationFromTheCanvas()
        {
            Canvas canvas = CreateCanvas();
            canvas.Configuration.Assets.AssetResolver.RegisterImporter(new MarkupImporter(canvas.Configuration));
            var frame = new Frame();
            canvas.Add(frame);

            Page? page = frame.Navigation.Navigate("Resources/TestPage.xml");

            Assert.NotNull(page);
        }
    }
}
