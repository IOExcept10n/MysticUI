using System.Drawing;
using Icy.Rendering.Brushes;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class CheckBoxTests
    {
        [Fact]
        public void CheckBrush_DefaultsToTransparent()
        {
            var checkBox = new CheckBox();

            var brush = Assert.IsType<SolidColorBrush>(checkBox.CheckBrush);
            Assert.Equal(Color.Transparent.ToArgb(), brush.Color.ToArgb());
        }

        [Fact]
        public void CheckBrush_IsSettable()
        {
            var checkBox = new CheckBox();
            var brush = new SolidColorBrush(Color.White);

            checkBox.CheckBrush = brush;

            Assert.Same(brush, checkBox.CheckBrush);
        }
    }
}
