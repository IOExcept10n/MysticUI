using System.ComponentModel.DataAnnotations;
using Icy.UI;
using Icy.UI.Controls;
using Xunit;

namespace Icy.Tests.Controls
{
    public class PropertyGridTests
    {
        private sealed class SampleTarget
        {
            public string Nickname { get; set; } = "Alpha";

            public bool Active { get; set; } = true;

            public int Score { get; set; } = 10;
        }

        private sealed class OtherTarget
        {
            public bool Ready { get; set; } = true;

            public string Note { get; set; } = "hi";
        }

        private sealed class ThrowingTarget
        {
            public string Broken => throw new InvalidOperationException("boom");
        }

        private sealed class RangedTarget
        {
            [Range(0, 100)]
            public int Volume { get; set; } = 50;
        }

        [Fact]
        public void PoolingEnabled_IsFalseAfterConstruction()
        {
            var grid = new PropertyGrid();
            Assert.False(grid.PoolingEnabled);
        }

        [Fact]
        public void Target_Null_ProducesNoItems()
        {
            var grid = new PropertyGrid { Target = null };
            grid.Measure();
            Assert.Equal(0, GetRealizedContainers(grid).Count);
        }

        [Fact]
        public void Target_StringProperty_RealizesTextBoxEditorBoundToValue()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var textBox = Assert.IsType<TextBox>(FindEditor(grid, nameof(SampleTarget.Nickname)));
            Assert.Equal("Alpha", textBox.Text);

            textBox.Text = "Beta";
            Assert.Equal("Beta", target.Nickname);
        }

        [Fact]
        public void Target_BoolProperty_RealizesCheckBoxEditorBoundToValue()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var checkBox = Assert.IsType<CheckBox>(FindEditor(grid, nameof(SampleTarget.Active)));
            Assert.True(checkBox.IsChecked);

            checkBox.IsChecked = false;
            Assert.False(target.Active);
        }

        [Fact]
        public void Target_NumericPropertyWithoutRange_RealizesPlainTextBoxEditor()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var textBox = Assert.IsType<TextBox>(FindEditor(grid, nameof(SampleTarget.Score)));
            Assert.Equal("10", textBox.Text);

            textBox.Text = "42";
            Assert.Equal(42, target.Score);
        }

        [Fact]
        public void Target_ReassignedToDifferentlyShapedObject_RowsReflectNewTargetWithNoStaleWidgets()
        {
            var grid = new PropertyGrid { Target = new SampleTarget() };
            grid.Measure();

            var other = new OtherTarget();
            grid.Target = other;
            grid.Measure();

            var checkBox = Assert.IsType<CheckBox>(FindEditor(grid, nameof(OtherTarget.Ready)));
            Assert.True(checkBox.IsChecked);

            var textBox = Assert.IsType<TextBox>(FindEditor(grid, nameof(OtherTarget.Note)));
            Assert.Equal("hi", textBox.Text);

            Assert.Null(FindEditor(grid, nameof(SampleTarget.Nickname)));
        }

        [Fact]
        public void Target_PropertyGetterThrows_RealizesReadOnlyTextBlockFallbackInsteadOfThrowing()
        {
            var target = new ThrowingTarget();
            var grid = new PropertyGrid { Target = target };

            // Must not throw - a throwing getter (e.g. a lazily-computed property) is a per-row display
            // failure, not a reason to crash the whole eager-realize pass over every row.
            grid.Measure();

            Assert.IsType<TextBlock>(FindEditor(grid, nameof(ThrowingTarget.Broken)));
        }

        [Fact]
        public void Target_NumericPropertyWithRange_RealizesSliderAndTextBoxPairBoundToValueBothWays()
        {
            var target = new RangedTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var editor = FindEditor(grid, nameof(RangedTarget.Volume));
            var panel = Assert.IsType<StackPanel>(editor);
            var slider = Assert.IsType<Slider>(panel.Children.ElementAtOrDefault(0));
            var textBox = Assert.IsType<TextBox>(panel.Children.ElementAtOrDefault(1));

            Assert.Equal(0f, slider.Minimum);
            Assert.Equal(100f, slider.Maximum);
            Assert.Equal(50f, slider.Value);
            Assert.Equal("50", textBox.Text);

            slider.Value = 80f;
            Assert.Equal(80, target.Volume);
            Assert.Equal("80", textBox.Text);

            textBox.Text = "20";
            Assert.Equal(20, target.Volume);
            Assert.Equal(20f, slider.Value);
        }

        [Fact]
        public void Target_OutOfRangeTextBoxEdit_ClampsTargetSliderAndTextBoxToRangeConsistently()
        {
            var target = new RangedTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var editor = FindEditor(grid, nameof(RangedTarget.Volume));
            var panel = Assert.IsType<StackPanel>(editor);
            var slider = Assert.IsType<Slider>(panel.Children.ElementAtOrDefault(0));
            var textBox = Assert.IsType<TextBox>(panel.Children.ElementAtOrDefault(1));

            textBox.Text = "150";

            // All three must agree on the clamped value (100, the declared Maximum) - not 150, and not a
            // Slider/TextBox/target that each disagree with each other.
            Assert.Equal(100, target.Volume);
            Assert.Equal(100f, slider.Value);
            Assert.Equal("100", textBox.Text);
        }

        /// <summary>
        /// Finds the realized row's editor widget (the row <see cref="Grid"/>'s second child) whose label
        /// (first child, a <see cref="TextBlock"/>) matches <paramref name="propertyDisplayName"/> - none of
        /// this file's sample types use <c>[DisplayName]</c>, so the label always equals the property name.
        /// </summary>
        private static UIElement? FindEditor(PropertyGrid grid, string propertyDisplayName)
        {
            foreach (ItemContainer container in GetRealizedContainers(grid).Values)
            {
                if (container.Content is Grid row &&
                    row.Children.ElementAtOrDefault(0) is TextBlock label &&
                    label.Text == propertyDisplayName)
                {
                    return row.Children.ElementAtOrDefault(1);
                }
            }

            return null;
        }

        /// <summary>
        /// Reaches <see cref="ItemsControl"/>'s private realized-container map - the exact reflection helper
        /// <c>ListBoxTests.cs</c>/<c>TabControlTests.cs</c> already use for this, reused verbatim rather than
        /// adding a new test-only accessor to the control itself.
        /// </summary>
        private static Dictionary<int, ItemContainer> GetRealizedContainers(ItemsControl control) =>
            (Dictionary<int, ItemContainer>)typeof(ItemsControl)
                .GetField("realizedContainers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(control)!;
    }
}
