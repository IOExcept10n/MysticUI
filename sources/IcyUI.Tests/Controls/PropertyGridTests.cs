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
