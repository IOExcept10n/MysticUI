using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Input.Events;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ToggleButtonTests
    {
        private static (Canvas Canvas, FakeInputSystem Input) CreateCanvas()
        {
            var input = new FakeInputSystem();
            var renderContext = new FakeRenderContext();
            var assets = new AssetConfiguration(AssetContext.ApplicationContext);
            var config = new IcyConfiguration(input, assets, renderContext, new ReflectionConfiguration());
            return (new Canvas(config), input);
        }

        [Fact]
        public void Tap_TogglesIsChecked()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var toggle = new ToggleButton
            {
                Width = 40,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            canvas.Add(toggle);
            canvas.Render();

            Assert.False(toggle.IsChecked);

            input.Events.Touch.RaiseTap(new TouchInfo(new Point(20, 20), 1));
            Assert.True(toggle.IsChecked);

            input.Events.Touch.RaiseTap(new TouchInfo(new Point(20, 20), 1));
            Assert.False(toggle.IsChecked);
        }

        [Fact]
        public void IsChecked_SetsCheckedControlStateFlag()
        {
            var toggle = new ToggleButton { IsChecked = true };

            Assert.Equal(ControlState.Checked, toggle.ControlState & ControlState.Checked);

            toggle.IsChecked = false;

            Assert.Equal(ControlState.Normal, toggle.ControlState & ControlState.Checked);
        }

        [Fact]
        public void IsCheckedChanged_Fires()
        {
            var toggle = new ToggleButton();
            int raisedCount = 0;
            toggle.IsCheckedChanged += (_, _) => raisedCount++;

            toggle.IsChecked = true;

            Assert.Equal(1, raisedCount);
        }

        [Fact]
        public void CheckBox_BehavesLikeToggleButton()
        {
            var checkBox = new CheckBox();

            checkBox.IsChecked = true;

            Assert.True(checkBox.IsChecked);
        }
    }
}
