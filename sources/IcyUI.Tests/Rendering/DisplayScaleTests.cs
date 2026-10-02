using System.Drawing;
using Icy.Rendering.Display;
using Xunit;

namespace Icy.Tests.Rendering
{
    public class DisplayScaleTests
    {
        private sealed class SequenceProvider(params float[] values) : IDisplayScaleProvider
        {
            private int index;

            public float GetScale(in NativeWindowInfo window) => values[Math.Min(index++, values.Length - 1)];
        }

        [Fact]
        public void Tracker_RaisesChangedOnlyWhenValueChanges()
        {
            var tracker = new DisplayScaleTracker(new SequenceProvider(2f, 2f, 1.5f), () => default);
            int raised = 0;
            tracker.Changed += (_, _) => raised++;

            Assert.True(tracker.Poll());
            Assert.False(tracker.Poll());
            Assert.True(tracker.Poll());

            Assert.Equal(2, raised);
            Assert.Equal(1.5f, tracker.Scale);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-2f)]
        [InlineData(float.NaN)]
        [InlineData(float.PositiveInfinity)]
        public void Tracker_TreatsInvalidProviderValuesAsOne(float value)
        {
            var tracker = new DisplayScaleTracker(new SequenceProvider(2f, value), () => default);
            tracker.Poll();

            tracker.Poll();

            Assert.Equal(1f, tracker.Scale);
        }

        [Fact]
        public void Tracker_StartsAtOneBeforeFirstPoll() =>
            Assert.Equal(1f, new DisplayScaleTracker(new FixedDisplayScaleProvider(2f), () => default).Scale);

        [Fact]
        public void FixedProvider_ReturnsItsValue() =>
            Assert.Equal(1.25f, new FixedDisplayScaleProvider(1.25f).GetScale(default));

        [Theory]
        [InlineData(0f)]
        [InlineData(float.NaN)]
        public void FixedProvider_RejectsInvalidScale(float value) =>
            Assert.ThrowsAny<ArgumentException>(() => new FixedDisplayScaleProvider(value));

        [Fact]
        public void GetProvider_NeverReturnsNull() => Assert.NotNull(DisplayScales.GetProvider());

        [Fact]
        public void DrawableRatioProvider_UsesDrawableToWindowRatio()
        {
            var provider = new DrawableRatioDisplayScaleProvider();

            Assert.Equal(2f, provider.GetScale(new NativeWindowInfo(0, new Size(800, 600), new Size(1600, 1200))));
            Assert.Equal(1f, provider.GetScale(new NativeWindowInfo(0, Size.Empty, new Size(1600, 1200))));
        }

        [Fact]
        public void WindowsProvider_WithoutHandle_FallsBackToSystemDpi()
        {
            if (!OperatingSystem.IsWindows())
                return;

            float scale = new WindowsDisplayScaleProvider().GetScale(default);

            Assert.True(scale >= 1f, $"System DPI scale was {scale}");
        }
    }
}
