using Icy.Data.Markup;
using Xunit;

namespace Icy.Tests.Data
{
    public class DispatcherFrameTickerTests
    {
        private sealed class CountingTicker : IFrameTicker
        {
            public TimeSpan Total { get; private set; }

            public void Tick(TimeSpan delta) => Total += delta;
        }

        [Fact]
        public void RegisteredTicker_IsTickedByUpdateAnimations_UntilUnregistered()
        {
            Dispatcher dispatcher = Dispatcher.GetCurrentThreadDispatcher();
            var ticker = new CountingTicker();

            dispatcher.RegisterFrameTicker(ticker);
            dispatcher.UpdateAnimations(TimeSpan.FromMilliseconds(16));
            dispatcher.UnregisterFrameTicker(ticker);
            dispatcher.UpdateAnimations(TimeSpan.FromMilliseconds(16));

            Assert.Equal(TimeSpan.FromMilliseconds(16), ticker.Total);
        }
    }
}
