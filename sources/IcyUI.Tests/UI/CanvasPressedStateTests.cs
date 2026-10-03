using System.Drawing;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.UI
{
    public class CanvasPressedStateTests
    {
        private static UIElement Box(int x) => new()
        {
            Width = 100,
            Height = 100,
            Margin = new Thickness(x, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        [Fact]
        public void SecondPointerDown_ReleasesTheFirstPressedElement()
        {
            // Regression: Canvas tracks one pressed element. A second finger landing on another control used to overwrite it,
            // leaving the first control stuck in ControlState.Pressed after both fingers lifted.
            var input = new FakeInputSystem();
            var configuration = new IcyConfiguration(input, new AssetConfiguration(AssetContext.ApplicationContext), new FakeRenderContext(), new ReflectionConfiguration());
            var canvas = new Canvas(configuration) { IsInputEnabled = true, IsVisible = true };
            UIElement a = Box(0);
            UIElement b = Box(200);
            canvas.Add(a);
            canvas.Add(b);
            canvas.Render();

            input.Events.Touch.RaiseTouchDown(new Point(50, 50));
            input.Events.Touch.RaiseTouchDown(new Point(250, 50));
            input.Events.Touch.RaiseTouchUp(new Point(250, 50));
            input.Events.Touch.RaiseTouchUp(new Point(50, 50));

            Assert.False(a.ControlState.HasFlag(ControlState.Pressed));
            Assert.False(b.ControlState.HasFlag(ControlState.Pressed));
        }
    }
}
