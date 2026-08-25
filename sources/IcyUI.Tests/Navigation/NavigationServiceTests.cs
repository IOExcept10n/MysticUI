using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.Navigation;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Navigation
{
    /// <summary>
    /// Covers <see cref="NavigationService"/> - the M3 slice of the markup system: <see cref="Page"/>/<see cref="Frame"/>
    /// wiring, back/forward history, <see cref="Page.KeepAlive"/>, and loading a page by path through the asset
    /// pipeline.
    /// </summary>
    public class NavigationServiceTests
    {
        private static Frame CreateAttachedFrame()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var configuration = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            configuration.Assets.AssetResolver.RegisterImporter(new MarkupImporter(configuration));

            var canvas = new Canvas(configuration);
            var frame = new Frame();
            canvas.Add(frame);
            return frame;
        }

        [Fact]
        public void NavigateTo_SetsCurrentPageAndFrameContent()
        {
            Frame frame = CreateAttachedFrame();
            var page = new Page();

            Page result = frame.Navigation.NavigateTo(page);

            Assert.Same(page, result);
            Assert.Same(page, frame.Navigation.CurrentPage);
            Assert.Same(page, frame.Content);
        }

        [Fact]
        public void NavigateTo_InitializesPreparesAndNotifiesTheIncomingPage()
        {
            Frame frame = CreateAttachedFrame();
            var page = new RecordingPage();

            frame.Navigation.NavigateTo(page);

            Assert.True(page.IsInitialized);
            Assert.Equal(1, page.PrepareCount);
            Assert.Equal(1, page.NavigatedToCount);
            Assert.Equal(0, page.NavigatedFromCount);
        }

        [Fact]
        public void NavigateTo_NotifiesThePreviousPageBeforeSwitching()
        {
            Frame frame = CreateAttachedFrame();
            var first = new RecordingPage();
            var second = new RecordingPage();
            frame.Navigation.NavigateTo(first);

            frame.Navigation.NavigateTo(second);

            Assert.Equal(1, first.NavigatedFromCount);
            Assert.Equal(0, second.NavigatedFromCount);
        }

        [Fact]
        public void NavigateTo_WithoutKeepAliveOnThePreviousPage_DropsItFromHistory()
        {
            Frame frame = CreateAttachedFrame();
            var first = new Page { KeepAlive = false };
            var second = new Page();
            frame.Navigation.NavigateTo(first);

            frame.Navigation.NavigateTo(second);

            Assert.Null(frame.Navigation.TryNavigateBack());
        }

        [Fact]
        public void NavigateTo_WithKeepAliveOnThePreviousPage_LetsTryNavigateBackReturnToTheSameInstance()
        {
            Frame frame = CreateAttachedFrame();
            var first = new Page { KeepAlive = true };
            var second = new Page();
            frame.Navigation.NavigateTo(first);

            frame.Navigation.NavigateTo(second);
            Page? back = frame.Navigation.TryNavigateBack();

            Assert.Same(first, back);
            Assert.Same(first, frame.Content);
            Assert.Same(first, frame.Navigation.CurrentPage);
        }

        [Fact]
        public void TryNavigateBack_ThenTryNavigateNext_ReturnsToTheOriginalPage()
        {
            Frame frame = CreateAttachedFrame();
            var first = new Page { KeepAlive = true };
            var second = new Page { KeepAlive = true };
            frame.Navigation.NavigateTo(first);
            frame.Navigation.NavigateTo(second);
            frame.Navigation.TryNavigateBack();

            Page? forward = frame.Navigation.TryNavigateNext();

            Assert.Same(second, forward);
            Assert.Same(second, frame.Content);
        }

        [Fact]
        public void TryNavigateBack_WithNoHistory_ReturnsNull()
        {
            Frame frame = CreateAttachedFrame();
            frame.Navigation.NavigateTo(new Page());

            Assert.Null(frame.Navigation.TryNavigateBack());
        }

        [Fact]
        public void NavigateTo_AfterGoingBack_ClearsForwardHistory()
        {
            Frame frame = CreateAttachedFrame();
            var first = new Page { KeepAlive = true };
            var second = new Page { KeepAlive = true };
            var third = new Page();
            frame.Navigation.NavigateTo(first);
            frame.Navigation.NavigateTo(second);
            frame.Navigation.TryNavigateBack();

            frame.Navigation.NavigateTo(third);

            Assert.Null(frame.Navigation.TryNavigateNext());
        }

        [Fact]
        public void Navigating_CanceledEvent_LeavesTheCurrentPageUnchanged()
        {
            Frame frame = CreateAttachedFrame();
            var first = new Page();
            var second = new Page();
            frame.Navigation.NavigateTo(first);
            frame.Navigation.Navigating += (_, args) => args.Cancel = true;

            Page result = frame.Navigation.NavigateTo(second);

            Assert.Same(first, result);
            Assert.Same(first, frame.Navigation.CurrentPage);
            Assert.Same(first, frame.Content);
        }

        [Fact]
        public void Navigating_EventCarriesThePreviousAndNextPage()
        {
            Frame frame = CreateAttachedFrame();
            var first = new Page();
            var second = new Page();
            frame.Navigation.NavigateTo(first);
            NavigationEventArgs? seen = null;
            frame.Navigation.Navigating += (_, args) => seen = args;

            frame.Navigation.NavigateTo(second);

            Assert.NotNull(seen);
            Assert.Same(first, seen!.PreviousPage);
            Assert.Same(second, seen.NextPage);
        }

        [Fact]
        public void Clear_ResetsCurrentPageFrameContentAndHistory()
        {
            Frame frame = CreateAttachedFrame();
            var first = new Page { KeepAlive = true };
            var second = new Page();
            frame.Navigation.NavigateTo(first);
            frame.Navigation.NavigateTo(second);

            frame.Navigation.Clear();

            Assert.Null(frame.Navigation.CurrentPage);
            Assert.Null(frame.Content);
            Assert.Null(frame.Navigation.TryNavigateBack());
        }

        [Fact]
        public void Navigate_LoadsAPageFromTheAssetPipelineAndSetsItsPath()
        {
            Frame frame = CreateAttachedFrame();

            Page? page = frame.Navigation.Navigate("Resources/TestPage.xml");

            Assert.NotNull(page);
            Assert.Equal("Resources/TestPage.xml", page!.Path);
            Assert.Same(page, frame.Content);
            Assert.Same(page, frame.Navigation.CurrentPage);
        }

        [Fact]
        public void Navigate_APathThatDoesNotExist_ReturnsNull()
        {
            Frame frame = CreateAttachedFrame();

            Page? page = frame.Navigation.Navigate("Resources/DoesNotExist.xml");

            Assert.Null(page);
            Assert.Null(frame.Navigation.CurrentPage);
        }

        [Fact]
        public void Navigate_ARootThatIsNotAPage_Throws()
        {
            Frame frame = CreateAttachedFrame();

            Assert.Throws<InvalidOperationException>(() => frame.Navigation.Navigate("Resources/TestNonPageRoot.xml"));
        }

        private sealed class RecordingPage : Page
        {
            public int NavigatedFromCount { get; private set; }

            public int NavigatedToCount { get; private set; }

            public int PrepareCount { get; private set; }

            public override void Prepare() => PrepareCount++;

            protected internal override void OnNavigatedFrom(object sender, NavigationEventArgs args) => NavigatedFromCount++;

            protected internal override void OnNavigatedTo(object sender, NavigationEventArgs args) => NavigatedToCount++;
        }
    }
}
