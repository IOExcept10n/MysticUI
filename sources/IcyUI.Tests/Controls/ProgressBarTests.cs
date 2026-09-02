using System.Drawing;
using System.Linq;
using Icy.Assets;
using Icy.Configuration;
using Icy.Markup;
using Icy.UI;
using Icy.UI.Controls;
using Icy.UI.Styles;
using Xunit;

namespace Icy.Tests.Controls
{
    public class ProgressBarTests
    {
        private static ControlTemplate LoadTemplate(string markup)
        {
            var configuration = new IcyConfiguration(new Icy.Tests.Input.FakeInputSystem(), new AssetConfiguration(AssetContext.ApplicationContext), new Icy.Tests.Rendering.FakeRenderContext(), new ReflectionConfiguration());
            var loader = new MarkupLoader(configuration);
            return (ControlTemplate)loader.LoadObject(markup);
        }

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

        [Fact]
        public void Template_WithPartFill_ArrangeSizesTheTemplatesOwnFill()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="ProgressBar">
                  <Border>
                    <Border x:Name="PART_Fill" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
                  </Border>
                </ControlTemplate>
                """);
            var progressBar = new ProgressBar { Template = template, Minimum = 0, Maximum = 100, Value = 50 };

            progressBar.Arrange(new Rectangle(0, 0, 200, 20));

            var templatedFill = progressBar.EnumerateVisualSubtree().OfType<Border>().Single(b => b.Name == "PART_Fill");
            Assert.Equal(100, templatedFill.Width);
        }

        [Fact]
        public void Template_WithPartFill_ValueChangeUpdatesTheTemplatesOwnFill()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="ProgressBar">
                  <Border>
                    <Border x:Name="PART_Fill" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
                  </Border>
                </ControlTemplate>
                """);
            var progressBar = new ProgressBar { Template = template, Minimum = 0, Maximum = 100, Value = 50 };
            progressBar.Arrange(new Rectangle(0, 0, 200, 20));

            progressBar.Value = 25;
            progressBar.Arrange(new Rectangle(0, 0, 200, 20));

            var templatedFill = progressBar.EnumerateVisualSubtree().OfType<Border>().Single(b => b.Name == "PART_Fill");
            Assert.Equal(50, templatedFill.Width);
        }

        [Fact]
        public void Template_WithoutPartFill_StillArrangesWithoutThrowing()
        {
            var template = LoadTemplate("""<ControlTemplate TargetType="ProgressBar"><Border/></ControlTemplate>""");
            var progressBar = new ProgressBar { Template = template, Minimum = 0, Maximum = 100, Value = 50 };

            progressBar.Arrange(new Rectangle(0, 0, 200, 20));
        }

        [Fact]
        public void Template_ClearedAfterBeingSet_RestoresDefaultFillBehavior()
        {
            var template = LoadTemplate(
                """
                <ControlTemplate TargetType="ProgressBar">
                  <Border>
                    <Border x:Name="PART_Fill" HorizontalAlignment="Left" VerticalAlignment="Stretch"/>
                  </Border>
                </ControlTemplate>
                """);
            var progressBar = new ProgressBar { Template = template, Minimum = 0, Maximum = 100, Value = 50 };
            progressBar.Arrange(new Rectangle(0, 0, 200, 20));

            progressBar.Template = null;
            progressBar.Arrange(new Rectangle(0, 0, 200, 20));

            var fill = (Border)progressBar.EnumerateVisualSubtree().Last();
            Assert.Equal(100, fill.ActualBounds.Width);
        }
    }
}
