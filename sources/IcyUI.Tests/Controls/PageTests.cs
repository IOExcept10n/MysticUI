using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class PageTests
    {
        [Fact]
        public void Initialize_RunsOnInitializeExactlyOnce()
        {
            var page = new CountingPage();

            page.Initialize();
            page.Initialize();

            Assert.True(page.IsInitialized);
            Assert.Equal(1, page.InitializeCount);
        }

        private sealed class CountingPage : Page
        {
            public int InitializeCount { get; private set; }

            protected override void OnInitialize() => InitializeCount++;
        }
    }
}
