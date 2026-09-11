using System.Numerics;
using Icy.Data;
using Icy.Input.Devices;
using Icy.Input.Events;
using Xunit;

namespace Icy.Tests.Input
{
    /// <summary>
    /// Covers <see cref="NavigationEvents"/>, the default <see cref="INavigationEvents"/> implementation - the
    /// real raw-key-to-<see cref="Vector2"/> translation that <see cref="Icy.Tests.Input.FakeInputSystem"/>'s own
    /// <see cref="FakeNavigationEvents"/> deliberately bypasses (it lets tests synthesize
    /// <see cref="INavigationEvents.FocusChanging"/> payloads directly), so every existing
    /// <see cref="Icy.UI.Controls.Selector"/>/<see cref="Icy.UI.Controls.ComboBox"/> test that exercises arrow-key
    /// navigation goes through <c>InvokeOnNavigationFocusChanging</c> (reflection) instead of a real key press -
    /// this class is what actually covers the translation layer those tests skip.
    /// </summary>
    public class NavigationEventsTests
    {
        [Theory]
        [InlineData(Keys.Up, 0, -1)]
        [InlineData(Keys.Down, 0, 1)]
        [InlineData(Keys.Left, -1, 0)]
        [InlineData(Keys.Right, 1, 0)]
        public void ArrowKey_RaisesFocusChanging_WithTheExpectedDirection(Keys key, float expectedX, float expectedY)
        {
            var input = new FakeInputSystem();
            var navigation = new NavigationEvents(input);
            navigation.Initialize();

            Vector2? direction = null;
            navigation.FocusChanging += (_, e) => direction = e.Data;

            input.Keyboard.RaiseKeyDown(key);

            Assert.Equal(new Vector2(expectedX, expectedY), direction);
        }
    }
}
