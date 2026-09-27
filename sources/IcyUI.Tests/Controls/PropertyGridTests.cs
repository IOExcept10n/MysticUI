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

            public DayOfWeek FavoriteDay { get; set; } = DayOfWeek.Monday;

            public System.Drawing.Color TintColor { get; set; } = System.Drawing.Color.Red;

            public System.Numerics.Vector3 Position { get; set; } = new(1f, 2f, 3f);

            public object Unsupported { get; set; } = new();
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

        /// <summary>
        /// A target whose <see cref="Range"/> minimum needs more than one digit - typing a value into its editor
        /// one keystroke at a time used to be impossible, since the first digit was clamped up to the minimum and
        /// written back over the text being typed.
        /// </summary>
        private sealed class HighRangedTarget
        {
            [Range(1000, 5000)]
            public int Bitrate { get; set; } = 2000;
        }

        /// <summary>
        /// A target with a wholly negative <see cref="Range"/> - the other direction in which building the
        /// editor's <see cref="Slider"/> used to cross its own default bounds.
        /// </summary>
        private sealed class NegativeRangedTarget
        {
            [Range(-10, -1)]
            public int Offset { get; set; } = -5;
        }

        /// <summary>
        /// A target with a <see cref="System.Numerics.Matrix4x4"/> property - sixteen indistinguishable component
        /// boxes without per-component labels.
        /// </summary>
        private sealed class MatrixTarget
        {
            public System.Numerics.Matrix4x4 Transform { get; set; } = System.Numerics.Matrix4x4.Identity;
        }

        /// <summary>
        /// A target with enough properties to overflow a small <see cref="ScrollViewer"/> viewport, so the grid's
        /// full row stack can't fit on screen at once.
        /// </summary>
        private sealed class ManyRowsTarget
        {
            public string Row01 { get; set; } = "1";

            public string Row02 { get; set; } = "2";

            public string Row03 { get; set; } = "3";

            public string Row04 { get; set; } = "4";

            public string Row05 { get; set; } = "5";

            public string Row06 { get; set; } = "6";

            public string Row07 { get; set; } = "7";

            public string Row08 { get; set; } = "8";

            public string Row09 { get; set; } = "9";

            public string Row10 { get; set; } = "10";

            public string Row11 { get; set; } = "11";

            public string Row12 { get; set; } = "12";

            public string Row13 { get; set; } = "13";

            public string Row14 { get; set; } = "14";

            public string Row15 { get; set; } = "15";

            public string Row16 { get; set; } = "16";
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

        [Fact]
        public void Target_EnumProperty_RealizesComboBoxWithAllEnumValues()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var comboBox = Assert.IsType<ComboBox>(FindEditor(grid, nameof(SampleTarget.FavoriteDay)));
            Assert.Equal(DayOfWeek.Monday, comboBox.SelectedItem);

            comboBox.SelectedItem = DayOfWeek.Friday;
            Assert.Equal(DayOfWeek.Friday, target.FavoriteDay);
        }

        [Fact]
        public void Target_ColorProperty_RealizesColorPickerButtonBoundToValue()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var button = Assert.IsType<ColorPickerButton>(FindEditor(grid, nameof(SampleTarget.TintColor)));
            Assert.Equal(System.Drawing.Color.Red, button.SelectedColor);

            button.SelectedColor = System.Drawing.Color.Blue;
            Assert.Equal(System.Drawing.Color.Blue, target.TintColor);
        }

        [Fact]
        public void Target_Vector3Property_RealizesThreeComponentTextBoxesBoundToValue()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var editor = Assert.IsType<StackPanel>(FindEditor(grid, nameof(SampleTarget.Position)));
            var boxes = editor.Children.OfType<TextBox>().ToList();
            Assert.Equal(3, boxes.Count);
            Assert.Equal("1", boxes[0].Text);
            Assert.Equal("2", boxes[1].Text);
            Assert.Equal("3", boxes[2].Text);

            boxes[0].Text = "9";
            Assert.Equal(9f, target.Position.X);
        }

        [Fact]
        public void HostedInScrollViewer_ViewportSmallerThanTheRowStack_KeepsEveryRowRealizedAndDoesNotThrow()
        {
            // Regression: PropertyGrid inherits IVirtualizingScrollInfo from ItemsControl, so a hosting
            // ScrollViewer calls OnViewportChanged on it unconditionally. The base implementation's realize walk
            // de-realized every row outside the viewport, while this control's own ArrangeContent walks
            // [0, ItemCount) unconditionally - so the very next arrange pass threw KeyNotFoundException looking up
            // a row that had just been dropped. Any PropertyGrid with more rows than fit (i.e. the whole point of
            // hosting one in a ScrollViewer, as PropertyGridDemo does) hit this.
            //
            // No font is configured here, so every row measures to zero height and the ScrollViewer's own extent
            // stays 0 - hence the offset is driven straight through the IVirtualizingScrollInfo contract (exactly
            // what ScrollViewer.UpdateContentOffset does) rather than through VerticalOffset, whose setter would
            // clamp it back to 0.
            var grid = new PropertyGrid { Target = new ManyRowsTarget() };
            var scrollViewer = new ScrollViewer { Width = 300, Height = 100, Content = grid };
            scrollViewer.Arrange(new System.Drawing.Rectangle(0, 0, 300, 100));

            // 16 properties + the single "Misc" category header they all fall under.
            int rowCount = GetRealizedContainers(grid).Count;
            Assert.Equal(17, rowCount);
            int unscrolledY = GetRealizedContainers(grid)[0].ActualBounds.Y;

            grid.OnViewportChanged(0f, 60f, scrollViewer.ViewportWidth, scrollViewer.ViewportHeight);

            // Nothing may be de-realized for merely sitting outside the viewport.
            Assert.Equal(rowCount, GetRealizedContainers(grid).Count);

            // The arrange pass that used to throw KeyNotFoundException.
            grid.InvalidateArrange();
            grid.Arrange(new System.Drawing.Rectangle(0, 0, 300, 100));

            Assert.Equal(rowCount, GetRealizedContainers(grid).Count);
            Assert.Equal(unscrolledY - 60, GetRealizedContainers(grid)[0].ActualBounds.Y);
        }

        [Fact]
        public void ExtentHeight_ReportsTheFullMeasuredRowStackNotTheBaseEstimate()
        {
            // Regression: ItemsControl.ExtentHeight answers from a running average over partially realized items
            // (ItemCount * DefaultEstimatedItemHeight before anything is realized), which describes nothing this
            // control ever lays out - a hosting ScrollViewer clamps VerticalOffset against that number, so an
            // underestimate puts the last rows permanently out of reach.
            var grid = new PropertyGrid { Target = new ManyRowsTarget() };

            float extent = grid.ExtentHeight;

            var realized = GetRealizedContainers(grid);
            Assert.Equal(17, realized.Count);
            Assert.Equal((float)realized.Values.Sum(row => row.Measure().Height), extent);
            Assert.NotEqual(realized.Count * grid.DefaultEstimatedItemHeight, extent);
        }

        [Fact]
        public void Target_RangeMinimumWiderThanOneDigit_DoesNotRewriteTheTextBoxWhileItIsFocused()
        {
            // Regression: the TextChanged handler clamped and rewrote textBox.Text on every keystroke, so typing
            // "2500" into a [Range(1000, 5000)] property replaced the "2" with "1000" before a second digit could
            // be typed - the field was untypable. The clamped value must still reach the target immediately; only
            // the displayed text waits for focus loss.
            var target = new HighRangedTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var panel = Assert.IsType<StackPanel>(FindEditor(grid, nameof(HighRangedTarget.Bitrate)));
            var slider = Assert.IsType<Slider>(panel.Children.ElementAtOrDefault(0));
            var textBox = Assert.IsType<TextBox>(panel.Children.ElementAtOrDefault(1));

            textBox.SetFocused(true);
            textBox.Text = "2";

            Assert.Equal("2", textBox.Text);
            Assert.Equal(1000, target.Bitrate);
            Assert.Equal(1000f, slider.Value);

            // Mid-edit progress must survive too: "25" is still below the minimum, still not rewritten.
            textBox.Text = "25";
            Assert.Equal("25", textBox.Text);

            // ... and typing on to a valid value lands exactly there.
            textBox.Text = "2500";
            Assert.Equal("2500", textBox.Text);
            Assert.Equal(2500, target.Bitrate);

            // Focus loss settles the display on the clamped value.
            textBox.Text = "10";
            textBox.SetFocused(false);

            Assert.Equal("1000", textBox.Text);
            Assert.Equal(1000, target.Bitrate);
        }

        [Fact]
        public void Target_RangeNotOverlappingTheSlidersDefaultBounds_BuildsTheEditorWithoutThrowing()
        {
            // Regression found while covering the [Range(1000, 5000)] case above: Slider.Minimum/Maximum each
            // re-clamp Value through float.Clamp, which throws on min > max - so assigning Minimum = 1000 while
            // Maximum was still Slider's default 100 crashed row construction outright. Any range sitting wholly
            // above 100, or wholly below 0, was unusable.
            var high = new PropertyGrid { Target = new HighRangedTarget() };
            high.Measure();

            var highPanel = Assert.IsType<StackPanel>(FindEditor(high, nameof(HighRangedTarget.Bitrate)));
            var highSlider = Assert.IsType<Slider>(highPanel.Children.ElementAtOrDefault(0));
            Assert.Equal(1000f, highSlider.Minimum);
            Assert.Equal(5000f, highSlider.Maximum);
            Assert.Equal(2000f, highSlider.Value);

            var negative = new PropertyGrid { Target = new NegativeRangedTarget() };
            negative.Measure();

            var negativePanel = Assert.IsType<StackPanel>(FindEditor(negative, nameof(NegativeRangedTarget.Offset)));
            var negativeSlider = Assert.IsType<Slider>(negativePanel.Children.ElementAtOrDefault(0));
            Assert.Equal(-10f, negativeSlider.Minimum);
            Assert.Equal(-1f, negativeSlider.Maximum);
            Assert.Equal(-5f, negativeSlider.Value);
        }

        [Fact]
        public void Target_Matrix4x4Property_RealizesOneLabelPerComponentAlongsideItsTextBox()
        {
            // Regression: BuildVectorEditor took a componentNames array but only ever read its Length, so a
            // Matrix4x4 rendered as sixteen indistinguishable boxes.
            var target = new MatrixTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var editor = Assert.IsType<StackPanel>(FindEditor(grid, nameof(MatrixTarget.Transform)));
            var labels = editor.Children.OfType<TextBlock>().Select(label => label.Text).ToList();
            var boxes = editor.Children.OfType<TextBox>().ToList();

            Assert.Equal(
                new[]
                {
                    "M11", "M12", "M13", "M14",
                    "M21", "M22", "M23", "M24",
                    "M31", "M32", "M33", "M34",
                    "M41", "M42", "M43", "M44",
                },
                labels);
            Assert.Equal(labels.Count, boxes.Count);
            Assert.All(boxes, box => Assert.False(float.IsNaN(box.Width)));
        }

        [Fact]
        public void ItemsSource_SetDirectlyToAnUnrelatedItem_FallsBackToAReadOnlyDisplayInsteadOfThrowing()
        {
            // ItemsSource is inherited and publicly settable, and is meant to be owned by Target (see its
            // remarks) - but an item assigned straight to it must not crash row construction mid-layout, which a
            // hard cast to PropertyGridEntry did.
            var grid = new PropertyGrid { ItemsSource = new List<object> { new object() } };

            grid.Measure();

            ItemContainer row = Assert.Single(GetRealizedContainers(grid).Values);
            Assert.IsType<TextBlock>(row.Content);
        }

        [Fact]
        public void Target_UnrecognizedType_RealizesReadOnlyTextBlockAndDoesNotThrow()
        {
            var target = new SampleTarget();
            var grid = new PropertyGrid { Target = target };
            grid.Measure();

            var display = Assert.IsType<TextBlock>(FindEditor(grid, nameof(SampleTarget.Unsupported)));
            Assert.False(string.IsNullOrEmpty(display.Text));
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
