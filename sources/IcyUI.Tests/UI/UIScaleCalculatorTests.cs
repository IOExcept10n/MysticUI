using System.Drawing;
using Icy.UI;
using Xunit;

namespace Icy.Tests.UI
{
    public class UIScaleCalculatorTests
    {
        private static readonly Size Reference = new(1920, 1080);

        [Theory]
        [InlineData(UIScaleMode.None, 2f, 1f)]
        [InlineData(UIScaleMode.Dpi, 2f, 2f)]
        [InlineData(UIScaleMode.Dpi, 1.25f, 1.25f)]
        public void Compute_NoneAndDpi_UseDisplayScale(UIScaleMode mode, float display, float expected) =>
            Assert.Equal(expected, UIScaleCalculator.Compute(mode, display, new Size(2880, 1920), Reference, ReferenceFit.Fit, 1f), 3);

        [Theory]
        [InlineData(ReferenceFit.Fit, 1.5f)]
        [InlineData(ReferenceFit.Fill, 1.7778f)]
        [InlineData(ReferenceFit.MatchWidth, 1.5f)]
        [InlineData(ReferenceFit.MatchHeight, 1.7778f)]
        public void Compute_ReferenceResolution_AppliesFitPolicy(ReferenceFit fit, float expected) =>
            Assert.Equal(expected, UIScaleCalculator.Compute(UIScaleMode.ReferenceResolution, 2f, new Size(2880, 1920), Reference, fit, 1f), 3);

        [Fact]
        public void Compute_MultipliesUserScaleOnTopOfBase() =>
            Assert.Equal(3f, UIScaleCalculator.Compute(UIScaleMode.Dpi, 2f, new Size(800, 600), Reference, ReferenceFit.Fit, 1.5f), 3);

        [Fact]
        public void Compute_ZeroViewport_FallsBackToOne() =>
            Assert.Equal(1f, UIScaleCalculator.Compute(UIScaleMode.ReferenceResolution, 2f, Size.Empty, Reference, ReferenceFit.Fit, 1f));

        [Theory]
        [InlineData(float.NaN)]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.PositiveInfinity)]
        public void Compute_InvalidDisplayScale_FallsBackToOne(float display) =>
            Assert.Equal(1f, UIScaleCalculator.Compute(UIScaleMode.Dpi, display, new Size(800, 600), Reference, ReferenceFit.Fit, 1f));
    }
}
