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

        private static (NavigationEvents Navigation, FakeInputSystem Input, FakeGamepadInput Pad, List<Vector2> Raised) Create()
        {
            var pad = new FakeGamepadInput();
            var input = new FakeInputSystem { Gamepad = pad };
            var navigation = new NavigationEvents(input);
            navigation.Initialize();
            var raised = new List<Vector2>();
            navigation.FocusChanging += (_, e) => raised.Add(e.Data);
            return (navigation, input, pad, raised);
        }

        [Fact]
        public void Defaults_AreTunedForMenus()
        {
            (NavigationEvents navigation, _, _, _) = Create();

            Assert.Equal(0.5f, navigation.MinimalFocusChangeDistance);
            Assert.Equal(TimeSpan.FromSeconds(0.4), navigation.RepeatStartDelay);
            Assert.Equal(TimeSpan.FromSeconds(0.1), navigation.RepeatDelay);
        }

        [Fact]
        public void AHeldArrow_RepeatsAfterTheStartDelay_ThenAtTheInterval_AndStopsOnRelease()
        {
            (NavigationEvents navigation, FakeInputSystem input, _, List<Vector2> raised) = Create();

            input.Keyboard.RaiseKeyDown(Keys.Down);
            navigation.Update(TimeSpan.FromSeconds(0.3));
            Assert.Single(raised);

            navigation.Update(TimeSpan.FromSeconds(0.1));
            Assert.Equal(2, raised.Count);
            navigation.Update(TimeSpan.FromSeconds(0.1));
            Assert.Equal(3, raised.Count);

            input.Keyboard.RaiseKeyUp(Keys.Down);
            navigation.Update(TimeSpan.FromSeconds(1));
            Assert.Equal(3, raised.Count);
            Assert.All(raised, d => Assert.Equal(Vector2.UnitY, d));
        }

        [Fact]
        public void APressedDPadButton_RaisesOnce_ThenRepeats_UntilReleased()
        {
            (NavigationEvents navigation, _, FakeGamepadInput pad, List<Vector2> raised) = Create();

            pad.RaiseButtonPressed(GamePadButtons.PadLeft);
            Assert.Equal([-Vector2.UnitX], raised);
            navigation.Update(TimeSpan.FromSeconds(0.4));
            Assert.Equal(2, raised.Count);

            pad.RaiseButtonReleased(GamePadButtons.PadLeft);
            navigation.Update(TimeSpan.FromSeconds(1));
            Assert.Equal(2, raised.Count);
        }

        [Fact]
        public void ReleasingAnOlderSource_DoesNotStopTheNewerHeldDirection()
        {
            (NavigationEvents navigation, FakeInputSystem input, FakeGamepadInput pad, List<Vector2> raised) = Create();

            input.Keyboard.RaiseKeyDown(Keys.Down);
            pad.RaiseButtonPressed(GamePadButtons.PadRight);
            input.Keyboard.RaiseKeyUp(Keys.Down);
            navigation.Update(TimeSpan.FromSeconds(0.4));

            Assert.Equal([Vector2.UnitY, Vector2.UnitX, Vector2.UnitX], raised);
        }

        [Fact]
        public void TheStick_FiresOncePastTheDeadZone_FlipsY_AndIgnoresJitter()
        {
            (_, _, FakeGamepadInput pad, List<Vector2> raised) = Create();

            pad.RaiseLeftStick(new Vector2(0, 0.3f));
            Assert.Empty(raised);

            pad.RaiseLeftStick(new Vector2(0.1f, 0.8f));
            pad.RaiseLeftStick(new Vector2(0.12f, 0.85f));
            pad.RaiseLeftStick(new Vector2(0.05f, 0.9f));

            // Pushing up on a pad (+Y) is "up" in UI space (-Y).
            Assert.Equal([-Vector2.UnitY], raised);
        }

        [Fact]
        public void TheStick_ReleasesWithHysteresis_AndChangingAxisIsANewPress()
        {
            (NavigationEvents navigation, _, FakeGamepadInput pad, List<Vector2> raised) = Create();

            pad.RaiseLeftStick(new Vector2(0.9f, 0));
            pad.RaiseLeftStick(new Vector2(0.4f, 0));
            Assert.Single(raised);

            pad.RaiseLeftStick(new Vector2(0.1f, -0.9f));
            Assert.Equal([Vector2.UnitX, Vector2.UnitY], raised);

            pad.RaiseLeftStick(new Vector2(0, -0.3f));
            navigation.Update(TimeSpan.FromSeconds(1));
            Assert.Equal(2, raised.Count);
        }

        [Theory]
        [InlineData(ModifierKeys.Ctrl)]
        [InlineData(ModifierKeys.Shift)]
        public void ModifiedArrows_AreLeftForEditing(ModifierKeys modifiers)
        {
            (_, FakeInputSystem input, _, List<Vector2> raised) = Create();
            input.Keyboard.ModifierKeys = modifiers;

            input.Keyboard.RaiseKeyDown(Keys.Left);

            Assert.Empty(raised);
        }

        [Fact]
        public void APadDisconnectingMidHold_StopsTheRepeat()
        {
            (NavigationEvents navigation, FakeInputSystem input, FakeGamepadInput pad, List<Vector2> raised) = Create();

            pad.RaiseLeftStick(new Vector2(0.9f, 0));
            input.Events.Devices.RaiseDeviceDisconnected(pad);
            navigation.Update(TimeSpan.FromSeconds(1));

            Assert.Single(raised);
        }
    }
}
