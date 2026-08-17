using System.Drawing;
using System.Linq;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ProgressBarTests
    {
        [Fact]
        public void Value_ClampsToMinMaxRange()
        {
            var progressBar = new ProgressBar { Minimum = 0, Maximum = 100 };

            progressBar.Value = 150;
            Assert.Equal(100, progressBar.Value);

            progressBar.Value = -10;
            Assert.Equal(0, progressBar.Value);
        }

        [Fact]
        public void Maximum_LoweredBelowCurrentValue_ClampsValue()
        {
            var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 80 };

            progressBar.Maximum = 50;

            Assert.Equal(50, progressBar.Value);
        }

        [Fact]
        public void MeasureContent_ReturnsFixedDefaultSize()
        {
            var progressBar = new ProgressBar { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };

            Size measured = progressBar.Measure();

            Assert.Equal(new Size(120, 16), measured);
        }

        [Fact]
        public void ArrangeContent_SetsFillWidthProportionalToValue()
        {
            var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 50 };

            progressBar.Arrange(new Rectangle(0, 0, 200, 20));

            var fill = (Border)progressBar.EnumerateVisualSubtree().Last();
            Assert.Equal(100, fill.ActualBounds.Width);
        }

        [Fact]
        public void ArrangeContent_ZeroValue_ProducesEmptyFill()
        {
            var progressBar = new ProgressBar { Minimum = 0, Maximum = 100, Value = 0 };

            progressBar.Arrange(new Rectangle(0, 0, 200, 20));

            var fill = (Border)progressBar.EnumerateVisualSubtree().Last();
            Assert.Equal(0, fill.ActualBounds.Width);
        }
    }
}
