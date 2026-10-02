// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.ComponentModel;
using System.Drawing;
using System.Numerics;
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
    /// This control never virtualizes: a property list is expected to be short enough that eagerly realizing
    /// every row (see <see cref="MeasureContent"/>/<see cref="ArrangeContent"/>, which lay rows out as a simple
    /// vertical stack) is preferable to the complexity of virtualizing it. Rows come and go in exactly one place -
    /// <see cref="RealizeAllRows"/>, driven by a <see cref="Target"/> reassignment - and never because of where the
    /// viewport happens to sit.
    /// </para>
    /// <para>
    /// It does still take part in <see cref="IVirtualizingScrollInfo"/>, because <see cref="ItemsControl"/>
    /// implements that interface and a hosting <see cref="ScrollViewer"/> therefore delegates to it
    /// unconditionally - but only for the two halves of the contract that make sense here, both overridden to
    /// replace the base's estimating/virtualizing behavior:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// extent reporting - <see cref="ComputeExtentHeight"/> returns the eagerly measured stack's real, exact
    /// height, not <see cref="ItemsControl"/>'s running-average estimate over partially realized items;
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// offset-driven repositioning - <see cref="OnViewportChanged(float, float, float, float)"/> records the new
    /// offsets/viewport and re-arranges, which <see cref="ArrangeContent"/> honors by shifting every row by the
    /// current scroll offset. It deliberately does not call the base implementation, whose realize walk would
    /// de-realize every row outside the viewport and leave <see cref="ArrangeContent"/>'s own loop over
    /// <c>[0, <see cref="ItemsControl.ItemCount"/>)</c> looking up rows that no longer exist.
    /// </description>
    /// </item>
    /// </list>
    /// Host this inside a <see cref="ScrollViewer"/> for scrolling when the target has more properties than fit on
    /// screen.
    /// </para>
    /// </remarks>
    public class PropertyGrid : ItemsControl
    {
        /// <summary>
        /// The fixed width, in pixels, of each per-component <see cref="TextBox"/> a
        /// <see cref="BuildVectorEditor"/> row builds - see its remarks for why these don't size to content.
        /// </summary>
        private const float ComponentEditorWidth = 48f;

        /// <summary>
        /// The gap, in pixels, kept around each per-component label <see cref="BuildVectorEditor"/> builds, so the
        /// components of a multi-component row stay visually separated.
        /// </summary>
        private const int ComponentLabelSpacing = 4;

        private object? target;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyGrid"/> class.
        /// </summary>
        public PropertyGrid()
        {
            // See the class remarks for why pooling is disabled here.
            PoolingEnabled = false;
        }

        /// <summary>
        /// Gets or sets the object whose properties this grid displays and edits. Setting this re-enumerates
        /// every browsable property via <see cref="PropertyGridEntry.EnumerateFor(object)"/> and rebuilds every
        /// row; assigning <see langword="null"/> clears the grid.
        /// </summary>
        /// <remarks>
        /// This property owns <see cref="ItemsControl.ItemsSource"/> outright - every assignment here overwrites it
        /// wholesale - so the inherited <see cref="ItemsControl.ItemsSource"/> should not be set directly on a
        /// <see cref="PropertyGrid"/>. <see cref="CreateContainer"/> still falls back to a read-only display for an
        /// item it doesn't recognize rather than throwing, but that path exists purely so a stray direct assignment
        /// can't crash a layout pass - it is not a supported way to populate this control.
        /// </remarks>
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
        /// <para>
        /// Records the offsets/viewport the host <see cref="ScrollViewer"/> reports and re-arranges (see
        /// <see cref="ArrangeContent"/>, which offsets every row by them) - deliberately without calling
        /// <see cref="ItemsControl.OnViewportChanged(float, float, float, float)"/>, and without any
        /// realize/de-realize work of its own.
        /// </para>
        /// <para>
        /// The base implementation's realize walk keeps only the rows intersecting the viewport (plus
        /// <see cref="ItemsControl.ScrollAheadBuffer"/>) realized and de-realizes the rest, which directly
        /// contradicts this control's eager, keep-everything-realized design: <see cref="ArrangeContent"/> walks
        /// <c>[0, <see cref="ItemsControl.ItemCount"/>)</c> unconditionally, so any row dropped for sitting off
        /// screen would leave it looking up a container that no longer exists. Rows are realized and de-realized in
        /// exactly one place, <see cref="RealizeAllRows"/>, driven by <see cref="Target"/> - never by scrolling.
        /// </para>
        /// </remarks>
        public override void OnViewportChanged(float newHorizontalOffset, float newVerticalOffset, float newViewportWidth, float newViewportHeight)
        {
            if (horizontalOffset == newHorizontalOffset && verticalOffset == newVerticalOffset &&
                viewportWidth == newViewportWidth && viewportHeight == newViewportHeight)
            {
                return;
            }

            horizontalOffset = newHorizontalOffset;
            verticalOffset = newVerticalOffset;
            viewportWidth = newViewportWidth;
            viewportHeight = newViewportHeight;

            InvalidateArrange();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Eagerly realizes every row (this control never virtualizes - see the class remarks), then measures
        /// the resulting vertical stack's own natural size directly (see <see cref="MeasureRowStack"/>) instead of
        /// going through <see cref="ItemsControl.MeasureContent"/>'s
        /// <see cref="ItemsControl.ExtentWidth"/>/<see cref="ItemsControl.ExtentHeight"/> pair, whose base
        /// implementations describe a partially realized, estimated single-column stack rather than this fully
        /// realized one - mirrors <see cref="TabControl.MeasureContent"/>'s identical situation.
        /// </remarks>
        protected override Size MeasureContent() => MeasureRowStack();

        /// <inheritdoc/>
        /// <remarks>
        /// Returns the eagerly realized row stack's real, exact total height (see <see cref="MeasureRowStack"/>) -
        /// <see cref="ItemsControl"/>'s own running-average estimate over partially realized items would report a
        /// height this control's layout never produces, which a hosting <see cref="ScrollViewer"/> would then clamp
        /// <see cref="ScrollViewer.VerticalOffset"/> against (typically an underestimate, cutting the last rows out
        /// of reach entirely). Realizes every row first, since this can be read - via
        /// <see cref="ItemsControl.ExtentHeight"/>, e.g. by <see cref="ScrollViewer.ExtentHeight"/> - before this
        /// control has ever been measured.
        /// </remarks>
        protected override float ComputeExtentHeight() => MeasureRowStack().Height;

        /// <inheritdoc/>
        /// <remarks>
        /// Arranges every realized row into a simple top-to-bottom vertical stack spanning
        /// <see cref="Control.ContentBounds"/>'s full width, each as tall as its own natural size - the
        /// vertical-stack counterpart of <see cref="TabControl.ArrangeContent"/>'s horizontal header row - shifted
        /// by the scroll offsets a hosting <see cref="ScrollViewer"/> last reported through
        /// <see cref="OnViewportChanged(float, float, float, float)"/>, the same way
        /// <see cref="ItemsControl.RealizeRange(int, float)"/> and <see cref="WrapGrid.RealizeRange(int, float)"/>
        /// offset the containers they position. Both offsets are zero when this control isn't hosted in a
        /// <see cref="ScrollViewer"/>, making this the plain unscrolled stack in that case.
        /// </remarks>
        protected override void ArrangeContent()
        {
            int x = ContentBounds.X - (int)horizontalOffset;
            int y = ContentBounds.Y - (int)verticalOffset;
            for (int i = 0; i < ItemCount; i++)
            {
                ItemContainer row = GetRealizedContainer(i);
                Size rowSize = row.Measure();
                row.InvalidateArrange();
                row.Arrange(new Rectangle(x, y, ContentBounds.Width, rowSize.Height));
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
        /// Anything else falls back to the same read-only <see cref="TextBlock"/> display <see cref="BuildEditor"/>
        /// uses for a type it has no editor for, rather than throwing: <see cref="ItemsControl.ItemsSource"/> is
        /// inherited and publicly settable, and a mismatched item assigned straight to it (see
        /// <see cref="Target"/>'s remarks for why that isn't a supported way to populate this control) must not
        /// crash row construction from inside a layout pass.
        /// </remarks>
        /// <param name="template">Unused - see the class remarks.</param>
        /// <param name="item">Ideally a <see cref="CategoryHeader"/> or a <see cref="PropertyGridEntry"/>.</param>
        protected override ItemContainer CreateContainer(DataTemplate template, object item)
        {
            if (item is CategoryHeader header)
                return new ItemContainer { Content = new TextBlock { Text = header.Name } };

            if (item is not PropertyGridEntry entry)
                return new ItemContainer { Content = new TextBlock { Text = item?.ToString() ?? string.Empty, VerticalAlignment = VerticalAlignment.Center } };

            object currentTarget = target ?? throw new InvalidOperationException(
                $"'{nameof(PropertyGrid)}' realized a row with no '{nameof(Target)}' set.");

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

            // A Grid with no RowDefinitions falls back to a single implicit Star track (Grid.ResolveTracks), which
            // measures to 0 height with no available-space constraint - exactly the case at this row's own
            // natural-size Measure time (PropertyGrid.MeasureRowStack calls row.Measure() directly, no ancestor
            // ever hands this Grid a known height). An explicit Auto row instead sizes to its children's natural
            // height, the same way every other hand-built Grid in this codebase declares its rows.
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = entry.DisplayName, VerticalAlignment = VerticalAlignment.Center };
            UIElement editor = BuildEditor(entry, currentTarget);

            Grid.SetColumn(label, 0);
            Grid.SetColumn(editor, 1);
            row.Children.Add(label);
            row.Children.Add(editor);

            return new ItemContainer { Content = row };
        }

        /// <summary>
        /// Realizes every row and measures the vertical stack they form - the widest row's width by the sum of
        /// every row's height.
        /// </summary>
        /// <remarks>
        /// The single measurement pass <see cref="MeasureContent"/> and <see cref="ComputeExtentHeight"/> share, so
        /// this control's own desired size and the extent it reports to a hosting <see cref="ScrollViewer"/> can
        /// never disagree about the same stack.
        /// </remarks>
        /// <returns>The realized row stack's total size.</returns>
        private Size MeasureRowStack()
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

            if (entry.PropertyType.IsEnum)
                return BuildEnumEditor(entry, target, value);

            if (entry.PropertyType == typeof(Color))
                return BuildColorEditor(entry, target, value);

            if (entry.PropertyType == typeof(Vector2))
                return BuildVectorEditor(entry, target, value, ["X", "Y"], v => new Vector2((float)v[0], (float)v[1]), v => [((Vector2)v!).X, ((Vector2)v).Y]);

            if (entry.PropertyType == typeof(Vector3))
                return BuildVectorEditor(entry, target, value, ["X", "Y", "Z"], v => new Vector3((float)v[0], (float)v[1], (float)v[2]), v => [((Vector3)v!).X, ((Vector3)v).Y, ((Vector3)v).Z]);

            if (entry.PropertyType == typeof(Vector4))
                return BuildVectorEditor(entry, target, value, ["X", "Y", "Z", "W"], v => new Vector4((float)v[0], (float)v[1], (float)v[2], (float)v[3]), v => [((Vector4)v!).X, ((Vector4)v).Y, ((Vector4)v).Z, ((Vector4)v).W]);

            if (entry.PropertyType == typeof(Quaternion))
                return BuildVectorEditor(entry, target, value, ["X", "Y", "Z", "W"], v => new Quaternion((float)v[0], (float)v[1], (float)v[2], (float)v[3]), v => [((Quaternion)v!).X, ((Quaternion)v).Y, ((Quaternion)v).Z, ((Quaternion)v).W]);

            if (entry.PropertyType == typeof(Matrix3x2))
                return BuildVectorEditor(entry, target, value, ["M11", "M12", "M21", "M22", "M31", "M32"], v => BuildMatrix3x2(v), DecomposeMatrix3x2);

            if (entry.PropertyType == typeof(Matrix4x4))
                return BuildVectorEditor(entry, target, value, ["M11", "M12", "M13", "M14", "M21", "M22", "M23", "M24", "M31", "M32", "M33", "M34", "M41", "M42", "M43", "M44"], v => BuildMatrix4x4(v), DecomposeMatrix4x4);

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
        /// <remarks>
        /// <para>
        /// <see cref="Slider.Value"/>'s setter clamps to <see cref="Slider.Minimum"/>/<see cref="Slider.Maximum"/>
        /// internally, but a value typed into <see cref="TextBox.Text"/> is not otherwise bounded by
        /// <paramref name="range"/> at all. Both the initial <see cref="TextBox.Text"/> assignment and the
        /// <see cref="TextBox.TextChanged"/> handler below therefore always route the value through
        /// <c>slider.Value</c> first and re-read it back out afterwards, using that already-clamped result - never
        /// the raw incoming value - as the single source of truth for what actually reaches
        /// <paramref name="target"/> (via <see cref="PropertyGridEntry.TrySetValue"/>) and what's redisplayed in
        /// <see cref="TextBox.Text"/>. Without this, typing an out-of-range value (e.g. <c>150</c> on a
        /// <c>[Range(0, 100)]</c> property) would write the unclamped value straight through
        /// <see cref="PropertyGridEntry.TrySetValue"/> while the <see cref="Slider"/> silently clamped its own
        /// display to <c>100</c> - the pair would permanently disagree, and <paramref name="range"/> would never
        /// actually be enforced through this path.
        /// </para>
        /// <para>
        /// The one thing that clamping must not do is rewrite <see cref="TextBox.Text"/> out from under someone
        /// mid-edit. Every keystroke raises <see cref="TextBox.TextChanged"/> against the partial text typed so far,
        /// so writing the clamped result back on each one makes any range whose minimum needs more than one digit
        /// untypable: on a <c>[Range(1000, 5000)]</c> property, the <c>2</c> of an intended <c>2500</c> clamps to
        /// <c>1000</c> and replaces the text before a second digit can be typed (and a negative range like
        /// <c>[Range(-10, -1)]</c> breaks the same way on the leading <c>-</c>). The clamped value is therefore still
        /// pushed to <c>slider.Value</c> and <paramref name="target"/> on every keystroke, but
        /// <see cref="TextBox.Text"/> itself is only rewritten while the box does not have focus - a
        /// <see cref="UIElement.FocusChanged"/> handler performs that one rewrite on focus loss, settling the display
        /// on the clamped value once the user is done typing.
        /// </para>
        /// <para>
        /// That focus-loss rewrite goes through the same <c>isSyncing</c> guard as everything else here: assigning
        /// <see cref="TextBox.Text"/> re-raises <see cref="TextBox.TextChanged"/>, which would otherwise re-run the
        /// whole parse/clamp/write path a second time for a value that just came out of it.
        /// </para>
        /// </remarks>
        /// <param name="entry">The numeric property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">The property's current value.</param>
        /// <param name="range">The inclusive range <see cref="PropertyGridEntry.Range"/> declares.</param>
        /// <returns>A <see cref="StackPanel"/> containing the freshly built <see cref="Slider"/> and <see cref="TextBox"/>.</returns>
        private UIElement BuildRangedNumericEditor(PropertyGridEntry entry, object target, object? value, (double Min, double Max) range)
        {
            var slider = new Slider { IsEnabled = !entry.IsReadOnly };

            // Both ends have to be assigned one at a time, and each setter immediately re-clamps Slider.Value
            // through float.Clamp - which throws outright when handed min > max. The *transient* pair each
            // assignment forms with the end that hasn't been assigned yet (Slider's own defaults, 0 and 100) must
            // therefore stay ordered. Assigning the end furthest from those defaults first guarantees that: a
            // Maximum >= 0 can never fall below the default Minimum of 0, and a wholly negative range's Minimum
            // (<= its own negative Maximum) can never exceed the default Maximum of 100. An inverted range never
            // reaches here at all - PropertyGridEntry.Range rejects one.
            if (range.Max >= 0)
            {
                slider.Maximum = (float)range.Max;
                slider.Minimum = (float)range.Min;
            }
            else
            {
                slider.Minimum = (float)range.Min;
                slider.Maximum = (float)range.Max;
            }

            slider.Value = (float)Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);

            // Re-read slider.Value (not the raw incoming `value`) for the initial text: the assignment above
            // already clamped it to range, so this starts the TextBox in agreement with the Slider even when
            // the target's current value sits outside its declared Range before the user ever touches either
            // widget - see the class remarks.
            object? initialValue = TryConvertNumeric(slider.Value, entry.PropertyType, out object? convertedInitial) ? convertedInitial : value;
            var textBox = new TextBox
            {
                Text = Convert.ToString(initialValue, System.Globalization.CultureInfo.InvariantCulture) ?? "0",
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
                    if (TryConvertNumeric(textBox.Text, entry.PropertyType, out object? typed))
                    {
                        // slider.Value's setter clamps to [Minimum, Maximum] - re-read it afterwards as the
                        // clamped result, rather than trusting `typed` (the raw, possibly out-of-range parse),
                        // for both what gets written to the target and what's redisplayed in the TextBox - see
                        // the class remarks.
                        slider.Value = (float)Convert.ToDouble(typed, System.Globalization.CultureInfo.InvariantCulture);
                        if (TryConvertNumeric(slider.Value, entry.PropertyType, out object? clamped))
                        {
                            // Never rewrite the text the user is still typing into - that's what made a range
                            // like [Range(1000, 5000)] untypable. The clamped value still reaches the target
                            // immediately; the display catches up on focus loss, below. See this method's remarks.
                            if (!textBox.IsFocused)
                                textBox.Text = Convert.ToString(clamped, System.Globalization.CultureInfo.InvariantCulture) ?? textBox.Text;
                            entry.TrySetValue(target, clamped);
                        }
                    }
                }
                finally
                {
                    isSyncing = false;
                }
            };

            textBox.FocusChanged += (_, _) =>
            {
                // Only on focus LOSS, and only the display: slider.Value already holds the clamped value every
                // keystroke wrote, so this just settles the text on it. Goes through isSyncing because assigning
                // Text re-raises TextChanged - see this method's remarks.
                if (textBox.IsFocused || isSyncing)
                    return;
                isSyncing = true;
                try
                {
                    if (TryConvertNumeric(slider.Value, entry.PropertyType, out object? clamped))
                        textBox.Text = Convert.ToString(clamped, System.Globalization.CultureInfo.InvariantCulture) ?? textBox.Text;
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
        /// Builds a <see cref="ComboBox"/> editor over every value of <paramref name="entry"/>'s enum type.
        /// </summary>
        /// <param name="entry">The enum-typed property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">The property's current value.</param>
        /// <returns>The freshly built editor widget.</returns>
        private UIElement BuildEnumEditor(PropertyGridEntry entry, object target, object? value)
        {
            // No ItemTemplate: ItemsControl's built-in default shows each enum member's name.
            var comboBox = new ComboBox
            {
                ItemsSource = EnumValueCache.GetValues(entry.PropertyType),
                SelectedItem = value,
                IsEnabled = !entry.IsReadOnly,
            };
            comboBox.SelectionChanged += (_, _) => entry.TrySetValue(target, comboBox.SelectedItem);
            return comboBox;
        }

        /// <summary>
        /// Builds a <see cref="ColorPickerButton"/> editor for a <see cref="Color"/>-typed property.
        /// </summary>
        /// <param name="entry">The color-typed property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">The property's current value.</param>
        /// <returns>The freshly built editor widget.</returns>
        private UIElement BuildColorEditor(PropertyGridEntry entry, object target, object? value)
        {
            var button = new ColorPickerButton
            {
                SelectedColor = value is Color color ? color : Color.White,
                IsEnabled = !entry.IsReadOnly,
            };
            button.ColorChanged += (_, _) => entry.TrySetValue(target, button.SelectedColor);
            return button;
        }

        /// <summary>
        /// Builds a composite row of one labeled numeric <see cref="TextBox"/> per component for a
        /// <c>System.Numerics</c> vector/quaternion/matrix-typed property - <paramref name="componentNames"/> in
        /// display order, <paramref name="compose"/> rebuilding the struct from every field's parsed value on any
        /// edit, <paramref name="decompose"/> reading the current per-component values back out.
        /// </summary>
        /// <param name="entry">The property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">
        /// The property's current value, already fetched by <see cref="BuildEditor"/>'s own guarded
        /// <see cref="PropertyGridEntry.GetValue(object)"/> call - reused here instead of calling it a second,
        /// unguarded time, so a getter that throws (succeeding once inside that guard, then throwing again were
        /// this to re-fetch it) can't crash this row's build the way <see cref="BuildEditor"/>'s try/catch exists
        /// to prevent.
        /// </param>
        /// <param name="componentNames">The component labels, in display order.</param>
        /// <param name="compose">Builds the struct value from the parsed component values, in the same order.</param>
        /// <param name="decompose">Reads the struct's current component values back out, in the same order.</param>
        /// <returns>
        /// The freshly built <see cref="StackPanel"/> of per-component <see cref="TextBlock"/> label +
        /// <see cref="TextBox"/> pairs.
        /// </returns>
        /// <remarks>
        /// Each component gets its own <see cref="TextBlock"/> label immediately before its <see cref="TextBox"/>:
        /// <c>X</c>/<c>Y</c>/<c>Z</c> is guessable from position alone, but a <see cref="Matrix4x4"/>'s sixteen
        /// boxes are not. The boxes take a fixed <see cref="UIElement.Width"/>
        /// (<see cref="ComponentEditorWidth"/>) rather than sizing to content, so that same sixteen-component row
        /// stays a predictable width instead of growing with whatever digits its values happen to have; the labels
        /// size to their own (short, known) text.
        /// </remarks>
        private UIElement BuildVectorEditor(
            PropertyGridEntry entry,
            object target,
            object? value,
            string[] componentNames,
            Func<double[], object> compose,
            Func<object?, double[]> decompose)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            double[] current = decompose(value);
            var boxes = new TextBox[componentNames.Length];

            for (int i = 0; i < componentNames.Length; i++)
            {
                var label = new TextBlock
                {
                    Text = componentNames[i],
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(i == 0 ? 0 : ComponentLabelSpacing, 0, ComponentLabelSpacing, 0),
                };
                var box = new TextBox
                {
                    Text = current[i].ToString(System.Globalization.CultureInfo.InvariantCulture),
                    IsEnabled = !entry.IsReadOnly,
                    Width = ComponentEditorWidth,
                };
                boxes[i] = box;
                box.TextChanged += (_, _) =>
                {
                    var values = new double[componentNames.Length];
                    for (int j = 0; j < componentNames.Length; j++)
                    {
                        if (!double.TryParse(boxes[j].Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out values[j]))
                            return;
                    }

                    entry.TrySetValue(target, compose(values));
                };
                panel.Children.Add(label);
                panel.Children.Add(box);
            }

            return panel;
        }

        /// <summary>
        /// Builds a <see cref="Matrix3x2"/> from <paramref name="v"/>'s six components, in
        /// <see cref="Matrix3x2.M11"/>/<see cref="Matrix3x2.M12"/>/<see cref="Matrix3x2.M21"/>/<see cref="Matrix3x2.M22"/>/
        /// <see cref="Matrix3x2.M31"/>/<see cref="Matrix3x2.M32"/> order - the <c>compose</c> delegate
        /// <see cref="BuildVectorEditor"/> uses for a <see cref="Matrix3x2"/>-typed property.
        /// </summary>
        /// <param name="v">The parsed component values, in the order above.</param>
        /// <returns>The composed <see cref="Matrix3x2"/>.</returns>
        private static Matrix3x2 BuildMatrix3x2(double[] v) => new((float)v[0], (float)v[1], (float)v[2], (float)v[3], (float)v[4], (float)v[5]);

        /// <summary>
        /// Reads a <see cref="Matrix3x2"/>'s six components back out, in the same order
        /// <see cref="BuildMatrix3x2"/> expects - the <c>decompose</c> delegate <see cref="BuildVectorEditor"/>
        /// uses for a <see cref="Matrix3x2"/>-typed property. Falls back to <see cref="Matrix3x2.Identity"/> when
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="value">The property's current value.</param>
        /// <returns>The six component values, in the order above.</returns>
        private static double[] DecomposeMatrix3x2(object? value)
        {
            var m = (Matrix3x2)(value ?? Matrix3x2.Identity);
            return [m.M11, m.M12, m.M21, m.M22, m.M31, m.M32];
        }

        /// <summary>
        /// Builds a <see cref="Matrix4x4"/> from <paramref name="v"/>'s sixteen components, in row-major
        /// <see cref="Matrix4x4.M11"/>.. <see cref="Matrix4x4.M44"/> order - the <c>compose</c> delegate
        /// <see cref="BuildVectorEditor"/> uses for a <see cref="Matrix4x4"/>-typed property.
        /// </summary>
        /// <param name="v">The parsed component values, in the order above.</param>
        /// <returns>The composed <see cref="Matrix4x4"/>.</returns>
        private static Matrix4x4 BuildMatrix4x4(double[] v) => new(
            (float)v[0], (float)v[1], (float)v[2], (float)v[3],
            (float)v[4], (float)v[5], (float)v[6], (float)v[7],
            (float)v[8], (float)v[9], (float)v[10], (float)v[11],
            (float)v[12], (float)v[13], (float)v[14], (float)v[15]);

        /// <summary>
        /// Reads a <see cref="Matrix4x4"/>'s sixteen components back out, in the same order
        /// <see cref="BuildMatrix4x4"/> expects - the <c>decompose</c> delegate <see cref="BuildVectorEditor"/>
        /// uses for a <see cref="Matrix4x4"/>-typed property. Falls back to <see cref="Matrix4x4.Identity"/> when
        /// <paramref name="value"/> is <see langword="null"/>.
        /// </summary>
        /// <param name="value">The property's current value.</param>
        /// <returns>The sixteen component values, in the order above.</returns>
        private static double[] DecomposeMatrix4x4(object? value)
        {
            var m = (Matrix4x4)(value ?? Matrix4x4.Identity);
            return [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
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
        /// Builds the flat, category-grouped row sequence <see cref="ItemsControl.ItemsSource"/> is assigned from - a
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
