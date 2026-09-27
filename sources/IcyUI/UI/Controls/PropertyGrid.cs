// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using Icy.Data;
using Icy.Data.Markup.Attributes;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A Unity-Inspector-style control: lists <see cref="Target"/>'s properties as rows (a label and a
    /// type-appropriate editor), letting a user edit them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="ItemsControl.ItemsSource"/> holds a flat mix of <see cref="PropertyGridEntry"/> (a real row)
    /// and an internal category-header marker, rebuilt whenever <see cref="Target"/> changes.
    /// <see cref="CreateContainer"/> is overridden to build each row's <see cref="Grid"/> (label | editor)
    /// directly in code - see <see cref="BuildEditor"/> for the per-<see cref="Type"/> editor dispatch - rather
    /// than through <see cref="ItemsControl.ItemTemplate"/>/markup, mirroring <see cref="TabControl"/>'s own
    /// precedent for un-templated, code-built items.
    /// </para>
    /// <para>
    /// <see cref="ItemsControl.PoolingEnabled"/> is disabled at construction for the same reason
    /// <see cref="TabControl"/> disables it: a row built directly in code has no <c>{Binding}</c> for
    /// <see cref="ItemsControl"/>'s pooled-reuse path (which only reassigns the pooled container's
    /// <see cref="ContentControl.Content"/>'s <see cref="UIElement.DataContext"/>) to refresh - without this, a
    /// recycled row could keep showing a previous <see cref="Target"/>'s editor widget/value.
    /// </para>
    /// <para>
    /// Like <see cref="TabControl"/>, this control never participates in <see cref="IVirtualizingScrollInfo"/>'s
    /// viewport-driven realize pipeline - a property list is expected to be short enough that eagerly realizing
    /// every row (see <see cref="MeasureContent"/>/<see cref="ArrangeContent"/>, which lay rows out as a simple
    /// vertical stack) is preferable to the complexity of virtualizing it. Host this inside a
    /// <see cref="ScrollViewer"/> for scrolling when the target has more properties than fit on screen.
    /// </para>
    /// </remarks>
    public class PropertyGrid : ItemsControl
    {
        private object? target;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyGrid"/> class.
        /// </summary>
        public PropertyGrid()
        {
            // Never built - CreateContainer below never calls DataTemplate.Build - only assigned so
            // ItemsControl.EnsureRealized's unconditional ResolveTemplate() call doesn't throw for want of an
            // ItemTemplate. See the class remarks for why pooling is also disabled here.
            ItemTemplate = new DataTemplate();
            PoolingEnabled = false;
        }

        /// <summary>
        /// Gets or sets the object whose properties this grid displays and edits. Setting this re-enumerates
        /// every browsable property via <see cref="PropertyGridEntry.EnumerateFor(object)"/> and rebuilds every
        /// row; assigning <see langword="null"/> clears the grid.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? Target
        {
            get => target;
            set
            {
                if (!SetProperty(ref target, value))
                    return;
                ItemsSource = BuildRows(value);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Eagerly realizes every row (this control never virtualizes - see the class remarks), then measures
        /// the resulting vertical stack's own natural size directly instead of going through
        /// <see cref="ItemsControl.ExtentWidth"/>/<see cref="ItemsControl.ExtentHeight"/> (which, with nothing
        /// ever calling <see cref="ItemsControl.OnViewportChanged(float, float, float, float)"/> for this
        /// control, never get populated) - mirrors <see cref="TabControl.MeasureContent"/>'s identical situation.
        /// </remarks>
        protected override Size MeasureContent()
        {
            RealizeAllRows();

            int width = 0;
            int height = 0;
            for (int i = 0; i < ItemCount; i++)
            {
                Size rowSize = GetRealizedContainer(i).Measure();
                width = Math.Max(width, rowSize.Width);
                height += rowSize.Height;
            }

            return new Size(width, height);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Arranges every realized row into a simple top-to-bottom vertical stack spanning
        /// <see cref="Control.ContentBounds"/>'s full width, each as tall as its own natural size - the
        /// vertical-stack counterpart of <see cref="TabControl.ArrangeContent"/>'s horizontal header row.
        /// </remarks>
        protected override void ArrangeContent()
        {
            int y = ContentBounds.Y;
            for (int i = 0; i < ItemCount; i++)
            {
                ItemContainer row = GetRealizedContainer(i);
                Size rowSize = row.Measure();
                row.InvalidateArrange();
                row.Arrange(new Rectangle(ContentBounds.X, y, ContentBounds.Width, rowSize.Height));
                y += rowSize.Height;
            }

            Chrome.InvalidateArrange();
            Chrome.Arrange(ActualBounds);
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Dispatches to a category-header row for a <see cref="CategoryHeader"/> marker item, or a
        /// label+editor <see cref="Grid"/> row for a <see cref="PropertyGridEntry"/> - see
        /// <see cref="BuildEditor"/> for the editor-widget dispatch by <see cref="PropertyGridEntry.PropertyType"/>.
        /// </remarks>
        /// <param name="template">Unused - see the class remarks.</param>
        /// <param name="item">Either a <see cref="CategoryHeader"/> or a <see cref="PropertyGridEntry"/>.</param>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
        {
            if (item is CategoryHeader header)
                return new ItemContainer { Content = new TextBlock { Text = header.Name } };

            var entry = (PropertyGridEntry)item;
            object currentTarget = target ?? throw new InvalidOperationException(
                $"'{nameof(PropertyGrid)}' realized a row with no '{nameof(Target)}' set.");

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            var label = new TextBlock { Text = entry.DisplayName, VerticalAlignment = VerticalAlignment.Center };
            UIElement editor = BuildEditor(entry, currentTarget);

            Grid.SetColumn(label, 0);
            Grid.SetColumn(editor, 1);
            row.Children.Add(label);
            row.Children.Add(editor);

            return new ItemContainer { Content = row };
        }

        /// <summary>
        /// Builds the editor widget for <paramref name="entry"/>, wired to read <paramref name="target"/>'s
        /// current value and write edits back to it. Falls back to a read-only <see cref="TextBlock"/> showing
        /// <see cref="object.ToString"/> for any type without a dedicated editor.
        /// </summary>
        /// <param name="entry">The property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <returns>The freshly built editor widget.</returns>
        private UIElement BuildEditor(PropertyGridEntry entry, object target)
        {
            object? value;
            try
            {
                value = entry.GetValue(target);
            }
            catch (Exception ex)
            {
                // A getter that throws (e.g. a lazily-computed property) must not crash the whole eager-realize
                // pass over every row (see MeasureContent/RealizeAllRows) - just this one row's own display.
                // Falls back to the same read-only-TextBlock treatment as an unmatched type, below, showing the
                // failure instead of a value.
                return new TextBlock { Text = $"(error: {ex.Message})", VerticalAlignment = VerticalAlignment.Center };
            }

            if (entry.PropertyType == typeof(string))
            {
                var textBox = new TextBox { Text = (string?)value ?? string.Empty, IsEnabled = !entry.IsReadOnly };
                textBox.TextChanged += (_, _) => entry.TrySetValue(target, textBox.Text);
                return textBox;
            }

            if (entry.PropertyType == typeof(bool))
            {
                var checkBox = new CheckBox { IsChecked = value is true, IsEnabled = !entry.IsReadOnly };
                checkBox.IsCheckedChanged += (_, _) => entry.TrySetValue(target, checkBox.IsChecked);
                return checkBox;
            }

            if (IsNumericType(entry.PropertyType))
                return BuildNumericEditor(entry, target, value);

            // TODO: nested/complex-object rows, and dedicated DateOnly/TimeOnly/DateTime/Uri editors, are out of
            // scope for v1 - see docs/superpowers/specs/2026-09-27-propertygrid-design.md. Every such type (and
            // any other type without a dedicated editor above) falls through to this read-only display.
            return new TextBlock { Text = value?.ToString() ?? string.Empty, VerticalAlignment = VerticalAlignment.Center };
        }

        /// <summary>
        /// Builds a numeric editor for <paramref name="entry"/> - a plain <see cref="TextBox"/> when no
        /// <see cref="PropertyGridEntry.Range"/> is present, or a <see cref="Slider"/>+<see cref="TextBox"/>
        /// pair (see <see cref="BuildRangedNumericEditor"/>) when one is.
        /// </summary>
        /// <param name="entry">The numeric property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">The property's current value.</param>
        /// <returns>The freshly built editor widget.</returns>
        private UIElement BuildNumericEditor(PropertyGridEntry entry, object target, object? value)
        {
            if (entry.Range is { } range)
                return BuildRangedNumericEditor(entry, target, value, range);

            var textBox = new TextBox
            {
                Text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0",
                IsEnabled = !entry.IsReadOnly,
            };
            textBox.TextChanged += (_, _) =>
            {
                if (TryConvertNumeric(textBox.Text, entry.PropertyType, out object? converted))
                    entry.TrySetValue(target, converted);
            };
            return textBox;
        }

        /// <summary>
        /// Builds the <see cref="Slider"/>+<see cref="TextBox"/> pair used for a numeric property that declares
        /// <paramref name="range"/> - mirrors <see cref="ColorPicker"/>'s hue-slider+hex-box pairing, including
        /// its re-entrancy guard: a local <c>isSyncing</c> flag each widget's own changed handler checks before
        /// writing back to the other widget/<paramref name="target"/>, so the <see cref="Slider"/> pushing its
        /// new value into the <see cref="TextBox"/> (or vice versa) doesn't bounce back through the other
        /// widget's own changed handler and loop - see <see cref="ColorPicker.SelectedColor"/>'s own <c>isSyncing</c>
        /// field for the identical pattern this one is modeled on.
        /// </summary>
        /// <param name="entry">The numeric property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">The property's current value.</param>
        /// <param name="range">The inclusive range <see cref="PropertyGridEntry.Range"/> declares.</param>
        /// <returns>A <see cref="StackPanel"/> containing the freshly built <see cref="Slider"/> and <see cref="TextBox"/>.</returns>
        private UIElement BuildRangedNumericEditor(PropertyGridEntry entry, object target, object? value, (double Min, double Max) range)
        {
            var slider = new Slider
            {
                Minimum = (float)range.Min,
                Maximum = (float)range.Max,
                IsEnabled = !entry.IsReadOnly,
            };
            slider.Value = (float)Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

            var textBox = new TextBox
            {
                Text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "0",
                IsEnabled = !entry.IsReadOnly,
            };

            bool isSyncing = false;

            slider.ValueChanged += (_, _) =>
            {
                if (isSyncing)
                    return;
                isSyncing = true;
                try
                {
                    if (TryConvertNumeric(slider.Value, entry.PropertyType, out object? converted))
                    {
                        textBox.Text = Convert.ToString(converted, System.Globalization.CultureInfo.InvariantCulture) ?? textBox.Text;
                        entry.TrySetValue(target, converted);
                    }
                }
                finally
                {
                    isSyncing = false;
                }
            };

            textBox.TextChanged += (_, _) =>
            {
                if (isSyncing)
                    return;
                isSyncing = true;
                try
                {
                    if (TryConvertNumeric(textBox.Text, entry.PropertyType, out object? converted))
                    {
                        slider.Value = (float)Convert.ToDouble(converted, System.Globalization.CultureInfo.InvariantCulture);
                        entry.TrySetValue(target, converted);
                    }
                }
                finally
                {
                    isSyncing = false;
                }
            };

            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            panel.Children.Add(slider);
            panel.Children.Add(textBox);
            return panel;
        }

        /// <summary>
        /// Determines whether <paramref name="type"/> is one of the numeric primitive types this control
        /// recognizes for its numeric editor branch.
        /// </summary>
        /// <param name="type">The type to check.</param>
        private static bool IsNumericType(Type type) =>
            type == typeof(sbyte) || type == typeof(byte) ||
            type == typeof(short) || type == typeof(ushort) ||
            type == typeof(int) || type == typeof(uint) ||
            type == typeof(long) || type == typeof(ulong) ||
            type == typeof(float) || type == typeof(double) || type == typeof(decimal);

        /// <summary>
        /// Attempts to parse <paramref name="text"/> into <paramref name="targetType"/>, one of the numeric
        /// types <see cref="IsNumericType"/> recognizes.
        /// </summary>
        /// <param name="text">The text to parse.</param>
        /// <param name="targetType">The numeric type to parse into.</param>
        /// <param name="value">The parsed value, when this method returns <see langword="true"/>.</param>
        private static bool TryConvertNumeric(string text, Type targetType, out object? value)
        {
            value = null;
            return double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsed)
                && TryConvertNumeric(parsed, targetType, out value);
        }

        /// <summary>
        /// Converts <paramref name="parsed"/> into <paramref name="targetType"/>, one of the numeric types
        /// <see cref="IsNumericType"/> recognizes - the shared conversion step both <see cref="TryConvertNumeric(string, Type, out object?)"/>
        /// (parsing a <see cref="TextBox"/> edit) and <see cref="BuildRangedNumericEditor"/> (converting a
        /// <see cref="Slider.Value"/> change) funnel through.
        /// </summary>
        /// <param name="parsed">The numeric value to convert.</param>
        /// <param name="targetType">The numeric type to convert into.</param>
        /// <param name="value">The converted value, when this method returns <see langword="true"/>.</param>
        private static bool TryConvertNumeric(double parsed, Type targetType, out object? value)
        {
            value = null;
            try
            {
                value = Convert.ChangeType(parsed, targetType, System.Globalization.CultureInfo.InvariantCulture);
                return true;
            }
            catch (Exception ex) when (ex is InvalidCastException or OverflowException)
            {
                return false;
            }
        }

        /// <summary>
        /// Builds the flat, category-grouped row sequence <see cref="ItemsSource"/> is assigned from - a
        /// <see cref="CategoryHeader"/> before the first row of each newly encountered
        /// <see cref="PropertyGridEntry.Category"/>, in the order <see cref="PropertyGridEntry.EnumerateFor(object)"/>
        /// already groups them in.
        /// </summary>
        /// <param name="target">The object to build rows for, or <see langword="null"/> to produce no rows.</param>
        private static IEnumerable<object> BuildRows(object? target)
        {
            if (target == null)
                yield break;

            string? currentCategory = null;
            foreach (PropertyGridEntry entry in PropertyGridEntry.EnumerateFor(target))
            {
                if (entry.Category != currentCategory)
                {
                    currentCategory = entry.Category;
                    yield return new CategoryHeader(currentCategory);
                }

                yield return entry;
            }
        }

        /// <summary>
        /// Realizes every row (see the class remarks for why this control eagerly realizes everything, unlike
        /// the base <see cref="ItemsControl"/>'s viewport-driven virtualization), and de-realizes anything
        /// left over from a previous, larger <see cref="Target"/>.
        /// </summary>
        private void RealizeAllRows()
        {
            for (int i = 0; i < ItemCount; i++)
                EnsureRealized(i);
            DerealizeOutOfRange(index => index < ItemCount);
        }

        /// <summary>
        /// Gets the container realized for the row at <paramref name="index"/> - a small wrapper over the
        /// protected <c>realizedContainers</c> map <see cref="RealizeAllRows"/> guarantees is populated for every
        /// index in <c>[0, <see cref="ItemsControl.ItemCount"/>)</c> before this is ever called.
        /// </summary>
        /// <param name="index">The realized row's index.</param>
        private ItemContainer GetRealizedContainer(int index) => realizedContainers[index];

        /// <summary>
        /// A non-interactive divider row inserted before each new <see cref="PropertyGridEntry.Category"/> group.
        /// </summary>
        /// <param name="Name">The category name to display.</param>
        private sealed record CategoryHeader(string Name);
    }
}
