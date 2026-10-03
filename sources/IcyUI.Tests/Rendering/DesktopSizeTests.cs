using System.Drawing;
using Icy.Rendering.Display;
using Xunit;

namespace Icy.Tests.Rendering
{
    public class DesktopSizeTests
    {
        [Fact]
        public void FixedProvider_ReportsItsSize()
        {
            Assert.True(new FixedDesktopSizeProvider(new Size(2880, 1920)).TryGetDesktopSize(default, out Size size));
            Assert.Equal(new Size(2880, 1920), size);
        }

        [Fact]
        public void FixedProvider_WithoutSize_ReportsUnknown() =>
            Assert.False(new FixedDesktopSizeProvider().TryGetDesktopSize(default, out _));

        [Fact]
        public void GetProvider_NeverReturnsNull() => Assert.NotNull(DesktopSizes.GetProvider());

        [Fact]
        public void WindowsProvider_WithoutHandle_ReportsThePrimaryDesktop()
        {
            if (!OperatingSystem.IsWindows())
                return;

            Assert.True(new WindowsDesktopSizeProvider().TryGetDesktopSize(default, out Size size));
            Assert.True(size.Width > 0 && size.Height > 0, $"Desktop size was {size}");
        }
    }
}
