using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Tests.Input;
using Icy.Tests.Rendering;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class SliderTests
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
        public void Value_ClampsToMinMaxRange()
        {
            var slider = new Slider { Minimum = 0, Maximum = 100 };

            slider.Value = 150;
            Assert.Equal(100, slider.Value);

            slider.Value = -10;
            Assert.Equal(0, slider.Value);
        }

        [Fact]
        public void Maximum_LoweredBelowCurrentValue_ClampsValue()
        {
            var slider = new Slider { Minimum = 0, Maximum = 100, Value = 80 };

            slider.Maximum = 50;

            Assert.Equal(50, slider.Value);
        }

        [Fact]
        public void ArrangeContent_PositionsThumbProportionally()
        {
            var slider = new Slider { Width = 200, Height = 20, Minimum = 0, Maximum = 100, Value = 50 };

            slider.Arrange(new Rectangle(0, 0, 200, 20));

            // roaming = ContentBounds.Width(200) - BorderThickness.Width(0) - ThumbSize(16) = 184; at 50%, margin = 92.
            var thumb = (Border)slider.EnumerateVisualSubtree().Last();
            Assert.Equal(92, thumb.Margin.Left);
        }

        [Fact]
        public void Drag_UpdatesValueBasedOnPointerPosition()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var slider = new Slider
            {
                Width = 200,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Minimum = 0,
                Maximum = 100,
            };
            canvas.Add(slider);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(100, 10));

            Assert.Equal(50, slider.Value);
        }

        [Fact]
        public void Drag_BeyondTrackEnd_ClampsToMaximum()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var slider = new Slider
            {
                Width = 200,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Minimum = 0,
                Maximum = 100,
            };
            canvas.Add(slider);
            canvas.Render();

            // The drag must start on the slider to be captured by it (hit-testing determines thumb-grab); once
            // captured, subsequent positions can go anywhere, including far past the track's end.
            input.Events.Drag.RaiseDragStarted(new Point(100, 10));
            input.Events.Drag.RaiseDragPerforming(new Point(1000, 10));

            Assert.Equal(100, slider.Value);
        }

        [Fact]
        public void Drag_ContinuesAfterCursorLeavesBounds()
        {
            var (canvas, input) = CreateCanvas();
            canvas.IsInputEnabled = true;
            canvas.IsVisible = true;
            var slider = new Slider
            {
                Width = 200,
                Height = 20,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Minimum = 0,
                Maximum = 100,
            };
            canvas.Add(slider);
            canvas.Render();

            input.Events.Drag.RaiseDragStarted(new Point(100, 10));
            Assert.Equal(50, slider.Value);

            input.Events.Drag.RaiseDragPerforming(new Point(-500, -500));
            Assert.Equal(0, slider.Value);

            input.Events.Drag.RaiseDragEnded(new Point(-500, -500));
        }
    }
}
