using Icy.Diagnostics;
using Icy.UI;
using Xunit;

namespace Icy.Tests.Diagnostics
{
    public class DebugAttachedPropertyTests
    {
        [Fact]
        public void GetVisualization_Unset_ReturnsNull()
        {
            var element = new UIElement();

            Assert.Null(Debug.GetVisualization(element));
        }

        [Fact]
        public void SetVisualization_ThenGet_RoundTrips()
        {
            var element = new UIElement();

            Debug.SetVisualization(element, "Bounds");

            Assert.Equal("Bounds", Debug.GetVisualization(element));
        }

        [Fact]
        public void SetVisualization_EmptyString_RoundTrips()
        {
            var element = new UIElement();

            Debug.SetVisualization(element, string.Empty);

            Assert.Equal(string.Empty, Debug.GetVisualization(element));
        }

        [Fact]
        public void SetVisualization_IsIndependentPerElement()
        {
            var first = new UIElement();
            var second = new UIElement();

            Debug.SetVisualization(first, "Bounds");

            Assert.Equal("Bounds", Debug.GetVisualization(first));
            Assert.Null(Debug.GetVisualization(second));
        }

        [Fact]
        public void SetVisualization_Null_ClearsTheOverride()
        {
            var element = new UIElement();
            Debug.SetVisualization(element, "Bounds");

            Debug.SetVisualization(element, null);

            Assert.Null(Debug.GetVisualization(element));
        }
    }
}
