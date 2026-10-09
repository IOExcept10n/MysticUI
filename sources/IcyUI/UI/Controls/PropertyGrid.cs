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

        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<TextBox, Action> expressionCommits = new();

        private object? target;
        private PropertyGridValueAdapter valueAdapter = PropertyGridValueAdapter.Default;
        private PropertyGridEntry? writingEntry;
        private PropertyRow? activeRow;
        private string? reportedMessage;
        private PropertyGridEntry? reportedEntry;

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyGrid"/> class.
        /// </summary>
        public PropertyGrid()
        {
            // See the class remarks for why pooling is disabled here.
            PoolingEnabled = false;
        }

        /// <summary>
        /// Occurs when <see cref="ActiveEntry"/> or <see cref="ActiveMessage"/> changed.
        /// </summary>
        public event EventHandler? ActiveMessageChanged;

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
                UpdateActive();
            }
        }

        /// <summary>
        /// Gets or sets how rows read and write <see cref="Target"/>'s properties. Defaults to
        /// <see cref="PropertyGridValueAdapter.Default"/>, which reads and writes them directly.
        /// </summary>
        /// <remarks>Setting it rebuilds every row.</remarks>
        /// <exception cref="ArgumentNullException">The value is <see langword="null"/>.</exception>
        public PropertyGridValueAdapter ValueAdapter
        {
            get => valueAdapter;
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                if (ReferenceEquals(valueAdapter, value))
                    return;
                valueAdapter = value;
                ItemsSource = BuildRows(target);
            }
        }

        /// <summary>
        /// Gets the property of the row holding the keyboard focus, or <see langword="null"/>.
        /// </summary>
        public PropertyGridEntry? ActiveEntry => activeRow?.Entry;

        /// <summary>
        /// Gets the help text for <see cref="ActiveEntry"/>: why its typed value is rejected while it is, otherwise its
        /// <see cref="PropertyGridEntry.Description"/>.
        /// </summary>
        /// <remarks>
        /// Core has no tooltips, so hosts show this themselves, for example in a status line under the grid. It updates on
        /// focus changes and keystrokes only.
        /// </remarks>
        public string? ActiveMessage => activeRow == null ? null : activeRow.InvalidReason ?? activeRow.Entry.Description;

        /// <summary>
        /// Re-reads every row's value, expression and Reset state from <see cref="Target"/> through
        /// <see cref="ValueAdapter"/>, keeping the rows themselves.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Call it after something other than this grid changed the target, such as an undo. Only rows whose value,
        /// expression or Reset state changed get a new editor, so a refresh after a one-property edit is cheap.
        /// </para>
        /// <para>
        /// Two rows keep their editor regardless: the one being written right now, and the one holding the keyboard
        /// focus, so typing is never interrupted.
        /// </para>
        /// </remarks>
        public void Refresh()
        {
            if (target == null)
                return;

            UIElement? focused = Canvas?.FocusedElement;
            foreach (ItemContainer container in RealizedContainers.Values)
            {
                if (container.Content is not PropertyRow row || ReferenceEquals(row.Entry, writingEntry))
                    continue;
                if (focused != null && row.Children.Count > 1 && IsSelfOrAncestor(row.Children[1], focused))
                    continue;
                if (!row.Shows(ReadState(row.Entry, target)))
                    FillRow(row, target);
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
            if (HorizontalOffset == newHorizontalOffset && VerticalOffset == newVerticalOffset &&
                ViewportWidth == newViewportWidth && ViewportHeight == newViewportHeight)
            {
                return;
            }

            HorizontalOffset = newHorizontalOffset;
            VerticalOffset = newVerticalOffset;
            ViewportWidth = newViewportWidth;
            ViewportHeight = newViewportHeight;

            InvalidateArrange();
        }

        /// <summary>
        /// Commits an expression row's text the way losing focus does. For tests.
        /// </summary>
        /// <param name="box">An expression row's text box.</param>
        /// <exception cref="InvalidOperationException"><paramref name="box"/> isn't an expression row of a grid.</exception>
        internal static void CommitExpressionForTest(TextBox box)
        {
            for (UIElement? element = box; element != null; element = element.Parent)
            {
                if (element is PropertyGrid grid && grid.expressionCommits.TryGetValue(box, out Action? commit))
                {
                    commit();
                    return;
                }
            }

            throw new InvalidOperationException("The box isn't an expression row of a PropertyGrid.");
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
            int x = ContentBounds.X - (int)HorizontalOffset;
            int y = ContentBounds.Y - (int)VerticalOffset;
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

            var row = new PropertyRow(entry);
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // A Grid with no RowDefinitions falls back to a single implicit Star track (Grid.ResolveTracks), which
            // measures to 0 height with no available-space constraint - exactly the case at this row's own
            // natural-size Measure time (PropertyGrid.MeasureRowStack calls row.Measure() directly, no ancestor
            // ever hands this Grid a known height). An explicit Auto row instead sizes to its children's natural
            // height, the same way every other hand-built Grid in this codebase declares its rows.
            row.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock { Text = entry.DisplayName, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(label, 0);
            row.Children.Add(label);
            FillRow(row, currentTarget);

            return new ItemContainer { Content = row };
        }

        /// <summary>
        /// Determines whether <paramref name="ancestor"/> is <paramref name="element"/> or one of its ancestors.
        /// </summary>
        /// <param name="ancestor">The candidate ancestor.</param>
        /// <param name="element">The element to walk up from.</param>
        private static bool IsSelfOrAncestor(UIElement ancestor, UIElement element)
        {
            for (UIElement? current = element; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                    return true;
            }

            return false;
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
        /// Parses and validates text typed into a numeric row.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="text">The typed text.</param>
        /// <param name="value">The value to write, when this method returns <see langword="true"/>.</param>
        /// <param name="reason">Why the text is rejected, when this method returns <see langword="false"/>.</param>
        /// <returns>
        /// <see langword="true"/> for a finite number that passes <see cref="PropertyGridEntry.Validate"/>, or for empty
        /// text when the property's default is <see cref="float.NaN"/>/<see cref="double.NaN"/> ("unset").
        /// </returns>
        private static bool TryParseInput(PropertyGridEntry entry, string text, out object? value, [System.Diagnostics.CodeAnalysis.NotNullWhen(false)] out string? reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                if (entry.HasDefaultValue && entry.DefaultValue is float.NaN or double.NaN)
                {
                    value = entry.DefaultValue;
                    return true;
                }

                value = null;
                reason = "Enter a number.";
                return false;
            }

            if (!TryConvertNumeric(text, entry.PropertyType, out value))
            {
                reason = "Enter a number.";
                return false;
            }

            if ((value is float f && float.IsInfinity(f)) || (value is double d && double.IsInfinity(d)))
            {
                reason = "Must be a finite number.";
                return false;
            }

            return entry.Validate(value, out reason);
        }

        /// <summary>
        /// Formats a numeric value for a row's text box, in the invariant culture.
        /// </summary>
        /// <param name="value">The value, or <see langword="null"/>.</param>
        /// <returns>The text; empty for <see langword="null"/>.</returns>
        private static string FormatNumber(object? value) =>
            Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

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
        /// Replaces <paramref name="row"/>'s editor and Reset button with fresh ones reading the current value.
        /// </summary>
        /// <param name="row">The row to fill; its label stays.</param>
        /// <param name="target">The object the row's entry belongs to.</param>
        private void FillRow(PropertyRow row, object target)
        {
            while (row.Children.Count > 1)
                row.Children.RemoveAt(row.Children.Count - 1);

            row.InvalidReason = null;
            row.Shown = ReadState(row.Entry, target);
            UIElement editor = BuildEditor(row.Entry, target);
            Grid.SetColumn(editor, 1);
            row.Children.Add(editor);
            foreach (UIElement focusable in editor.EnumerateVisualSubtree().Where(x => x.IsFocusable))
                focusable.FocusChanged += (_, _) => UpdateActive();

            if (valueAdapter.CanReset(row.Entry, target))
            {
                PropertyGridEntry entry = row.Entry;
                PropertyGridValueAdapter adapter = valueAdapter;
                var reset = new Button
                {
                    Content = new TextBlock { Text = "Reset" },
                    Padding = new Thickness(6, 2),
                    Margin = new Thickness(4, 0, 0, 0),
                    Command = new ActionCommand(() =>
                    {
                        adapter.Reset(entry, target);
                        Refresh();
                    }),
                };
                Grid.SetColumn(reset, 2);
                row.Children.Add(reset);
            }
        }

        /// <summary>
        /// Reads what a row for <paramref name="entry"/> shows, so <see cref="Refresh"/> can tell whether it changed.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the row's entry belongs to.</param>
        /// <returns>The state, or <see langword="null"/> when reading it failed (such a row is always rebuilt).</returns>
        private RowState? ReadState(PropertyGridEntry entry, object target)
        {
            try
            {
                return new RowState(valueAdapter.GetValue(entry, target), valueAdapter.GetExpression(entry, target), valueAdapter.CanReset(entry, target));
            }
            catch (Exception)
            {
                // The editor shows the failure itself (see BuildEditor); null makes the row rebuild on every refresh.
                return null;
            }
        }

        /// <summary>
        /// Re-reads which row holds the focus and raises <see cref="ActiveMessageChanged"/> when the active entry or its
        /// message changed.
        /// </summary>
        private void UpdateActive()
        {
            activeRow = null;

            // Canvas.Focus raises FocusChanged on the element losing focus before it moves FocusedElement on, so an element
            // that no longer reports IsFocused is already on its way out.
            UIElement? focused = Canvas?.FocusedElement is { IsFocused: true } current ? current : null;
            for (UIElement? element = focused; element != null; element = element.Parent)
            {
                if (element is PropertyRow row && IsSelfOrAncestor(this, row))
                {
                    activeRow = row;
                    break;
                }
            }

            if (ReferenceEquals(reportedEntry, ActiveEntry) && reportedMessage == ActiveMessage)
                return;

            reportedEntry = ActiveEntry;
            reportedMessage = ActiveMessage;
            ActiveMessageChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Marks <paramref name="box"/> <see cref="ControlState.Invalid"/> with <paramref name="reason"/>, or clears the mark
        /// when <paramref name="reason"/> is <see langword="null"/>, and records the reason on the box's row.
        /// </summary>
        /// <param name="box">A row editor's text box.</param>
        /// <param name="reason">Why its text is rejected, or <see langword="null"/>.</param>
        private void SetInvalid(TextBox box, string? reason)
        {
            box.ControlState = reason != null ? box.ControlState | ControlState.Invalid : box.ControlState & ~ControlState.Invalid;
            for (UIElement? element = box; element != null; element = element.Parent)
            {
                if (element is PropertyRow row)
                {
                    row.InvalidReason = reason;
                    break;
                }
            }

            UpdateActive();
        }

        /// <summary>
        /// Reads a row's current value through <see cref="ValueAdapter"/>, for reverting rejected text.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the row's entry belongs to.</param>
        /// <returns>The value, or <see langword="null"/> when the getter throws.</returns>
        private object? SafeGetValue(PropertyGridEntry entry, object target)
        {
            try
            {
                return valueAdapter.GetValue(entry, target);
            }
            catch (Exception)
            {
                // A getter that throws is shown by BuildEditor already; reverting to empty text is the best left to do.
                return null;
            }
        }

        /// <summary>
        /// Writes <paramref name="value"/> through <see cref="ValueAdapter"/>, marking <paramref name="entry"/> as the
        /// row being written so a synchronous <see cref="Refresh"/> keeps its editor.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the row's entry belongs to.</param>
        /// <param name="value">The new value.</param>
        /// <returns><see langword="true"/> when the adapter accepted the value.</returns>
        private bool Write(PropertyGridEntry entry, object target, object? value)
        {
            // A write may refresh this grid synchronously (an adapter whose document raises Changed); the row being
            // written keeps its editor so an open popup or a slider drag survives.
            PropertyGridEntry? previous = writingEntry;
            writingEntry = entry;
            try
            {
                return valueAdapter.TrySetValue(entry, target, value);
            }
            finally
            {
                writingEntry = previous;
            }
        }

        /// <summary>
        /// Builds a <see cref="TextBox"/> showing an attribute expression, committed through
        /// <see cref="PropertyGridValueAdapter.TrySetExpression"/> on Enter or when it loses focus.
        /// </summary>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the row's entry belongs to.</param>
        /// <param name="expression">The expression text to show.</param>
        /// <returns>The freshly built text box.</returns>
        private TextBox BuildExpressionEditor(PropertyGridEntry entry, object target, string expression)
        {
            var box = new TextBox { Text = expression, IsEnabled = !entry.IsReadOnly };

            // The box listens for Enter only while it has the focus, so idle rows cost nothing.
            Input.Devices.IKeyboardInput? keyboard = null;
            void OnKeyDown(object? sender, GenericEventArgs<Input.Devices.Keys> e)
            {
                if (e.Data == Input.Devices.Keys.Enter && box.IsFocused)
                    CommitExpression(box, entry, target);
            }

            void StopListening()
            {
                if (keyboard != null)
                    keyboard.KeyDown -= OnKeyDown;
                keyboard = null;
            }

            box.FocusChanged += (_, _) =>
            {
                if (box.IsFocused)
                {
                    StopListening();
                    keyboard = box.Canvas?.Configuration.Input.Keyboard;
                    if (keyboard != null)
                        keyboard.KeyDown += OnKeyDown;
                    return;
                }

                StopListening();
                CommitExpression(box, entry, target);
            };
            box.Detached += (_, _) => StopListening();
            expressionCommits.AddOrUpdate(box, () => CommitExpression(box, entry, target));
            return box;
        }

        /// <summary>
        /// Commits <paramref name="box"/>'s text through the adapter and marks it
        /// <see cref="ControlState.Invalid"/> when the adapter rejects it.
        /// </summary>
        /// <param name="box">The expression row's text box.</param>
        /// <param name="entry">The row's property.</param>
        /// <param name="target">The object the row's entry belongs to.</param>
        private void CommitExpression(TextBox box, PropertyGridEntry entry, object target)
        {
            bool accepted = valueAdapter.TrySetExpression(entry, target, box.Text);
            box.ControlState = accepted ? box.ControlState & ~ControlState.Invalid : box.ControlState | ControlState.Invalid;
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
                value = valueAdapter.GetValue(entry, target);
            }
            catch (Exception ex)
            {
                // A getter that throws (e.g. a lazily-computed property) must not crash the whole eager-realize
                // pass over every row (see MeasureContent/RealizeAllRows) - just this one row's own display.
                // Falls back to the same read-only-TextBlock treatment as an unmatched type, below, showing the
                // failure instead of a value.
                return new TextBlock { Text = $"(error: {ex.Message})", VerticalAlignment = VerticalAlignment.Center };
            }

            if (valueAdapter.GetExpression(entry, target) is { } expression)
                return BuildExpressionEditor(entry, target, expression);

            if (entry.PropertyType == typeof(string))
            {
                var textBox = new TextBox { Text = (string?)value ?? string.Empty, IsEnabled = !entry.IsReadOnly };
                textBox.TextChanged += (_, _) => Write(entry, target, textBox.Text);
                return textBox;
            }

            if (entry.PropertyType == typeof(bool))
            {
                var checkBox = new CheckBox { IsChecked = value is true, IsEnabled = !entry.IsReadOnly };
                checkBox.IsCheckedChanged += (_, _) => Write(entry, target, checkBox.IsChecked);
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
        /// Builds a numeric editor for <paramref name="entry"/> - a plain, validated <see cref="TextBox"/> when
        /// <see cref="PropertyGridEntry.Range"/> is absent or open-ended, or a <see cref="Slider"/>+<see cref="TextBox"/>
        /// pair (see <see cref="BuildRangedNumericEditor"/>) when it's bounded.
        /// </summary>
        /// <remarks>
        /// Typed text is parsed and checked with <see cref="PropertyGridEntry.Validate"/> before anything is written. Rejected
        /// text stays in the box, marked <see cref="ControlState.Invalid"/>, and reverts to the current value when the box
        /// loses focus. Empty text means "unset" for a property whose default is <see cref="float.NaN"/>.
        /// </remarks>
        /// <param name="entry">The numeric property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">The property's current value.</param>
        /// <returns>The freshly built editor widget.</returns>
        private UIElement BuildNumericEditor(PropertyGridEntry entry, object target, object? value)
        {
            if (entry.Range is { IsBounded: true } range)
                return BuildRangedNumericEditor(entry, target, value, range);

            var textBox = new TextBox { Text = FormatNumber(value), IsEnabled = !entry.IsReadOnly };
            bool reverting = false;
            textBox.TextChanged += (_, _) =>
            {
                if (reverting)
                    return;
                if (TryParseInput(entry, textBox.Text, out object? parsed, out string? reason))
                {
                    SetInvalid(textBox, null);
                    Write(entry, target, parsed);
                }
                else
                {
                    SetInvalid(textBox, reason);
                }
            };
            textBox.FocusChanged += (_, _) =>
            {
                // Leaving the box with rejected text shows the current value again (nothing was written).
                if (textBox.IsFocused || !textBox.ControlState.HasFlag(ControlState.Invalid))
                    return;
                reverting = true;
                try
                {
                    textBox.Text = FormatNumber(SafeGetValue(entry, target));
                }
                finally
                {
                    reverting = false;
                }

                SetInvalid(textBox, null);
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
        /// Typed text is validated against <paramref name="range"/> (including exclusive ends) instead of being clamped.
        /// Text outside it - including every prefix on the way to a valid value, such as the <c>2</c> of <c>2500</c> in
        /// <c>[Range(1000, 5000)]</c> - stays in the box as typed, is marked <see cref="ControlState.Invalid"/>, and writes
        /// nothing. The text is never rewritten while the box is focused; when it loses focus with rejected text, it shows
        /// the current value again.
        /// </para>
        /// <para>
        /// The slider can only produce values inside its own bounds; a slider value that the range's exclusive ends reject
        /// isn't written.
        /// </para>
        /// </remarks>
        /// <param name="entry">The numeric property this row edits.</param>
        /// <param name="target">The object <paramref name="entry"/> belongs to.</param>
        /// <param name="value">The property's current value.</param>
        /// <param name="range">The bounded range <see cref="PropertyGridEntry.Range"/> declares.</param>
        /// <returns>A <see cref="StackPanel"/> containing the freshly built <see cref="Slider"/> and <see cref="TextBox"/>.</returns>
        private UIElement BuildRangedNumericEditor(PropertyGridEntry entry, object target, object? value, ValueRange range)
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
                    if (TryConvertNumeric(slider.Value, entry.PropertyType, out object? converted) && entry.Validate(converted, out _))
                    {
                        textBox.Text = FormatNumber(converted);
                        SetInvalid(textBox, null);
                        Write(entry, target, converted);
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
                if (!TryParseInput(entry, textBox.Text, out object? typed, out string? reason))
                {
                    // Mid-edit text ("2" on the way to "2500" in [1000, 5000]) stays as typed and writes nothing.
                    SetInvalid(textBox, reason);
                    return;
                }

                SetInvalid(textBox, null);
                isSyncing = true;
                try
                {
                    slider.Value = (float)Convert.ToDouble(typed, System.Globalization.CultureInfo.InvariantCulture);
                    Write(entry, target, typed);
                }
                finally
                {
                    isSyncing = false;
                }
            };

            textBox.FocusChanged += (_, _) =>
            {
                if (textBox.IsFocused || isSyncing || !textBox.ControlState.HasFlag(ControlState.Invalid))
                    return;
                isSyncing = true;
                try
                {
                    if (TryConvertNumeric(slider.Value, entry.PropertyType, out object? current))
                        textBox.Text = FormatNumber(current);
                }
                finally
                {
                    isSyncing = false;
                }

                SetInvalid(textBox, null);
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
            comboBox.SelectionChanged += (_, _) => Write(entry, target, comboBox.SelectedItem);
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
            button.ColorChanged += (_, _) => Write(entry, target, button.SelectedColor);
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

                    Write(entry, target, compose(values));
                };
                panel.Children.Add(label);
                panel.Children.Add(box);
            }

            return panel;
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
        /// protected <see cref="ItemsControl.RealizedContainers"/> map <see cref="RealizeAllRows"/> guarantees is populated for every
        /// index in <c>[0, <see cref="ItemsControl.ItemCount"/>)</c> before this is ever called.
        /// </summary>
        /// <param name="index">The realized row's index.</param>
        private ItemContainer GetRealizedContainer(int index) => RealizedContainers[index];

        /// <summary>
        /// What a row's editor shows: the value, the expression text, and whether Reset is offered.
        /// </summary>
        /// <param name="Value">The value the adapter read.</param>
        /// <param name="Expression">The expression text, or <see langword="null"/> for a typed editor.</param>
        /// <param name="CanReset">Whether the row has a Reset button.</param>
        private readonly record struct RowState(object? Value, string? Expression, bool CanReset);

        /// <summary>
        /// A non-interactive divider row inserted before each new <see cref="PropertyGridEntry.Category"/> group.
        /// </summary>
        /// <param name="Name">The category name to display.</param>
        private sealed record CategoryHeader(string Name);

        /// <summary>
        /// A property row: label, editor and an optional Reset button, remembering the entry it shows so
        /// <see cref="Refresh"/> can refill it in place.
        /// </summary>
        /// <param name="entry">The property the row shows.</param>
        private sealed class PropertyRow(PropertyGridEntry entry) : Grid
        {
            /// <summary>
            /// Gets the property the row shows.
            /// </summary>
            public PropertyGridEntry Entry { get; } = entry;

            /// <summary>
            /// Gets or sets what the row's editor was built from, or <see langword="null"/> when that couldn't be read.
            /// </summary>
            public RowState? Shown { get; set; }

            /// <summary>
            /// Gets or sets why the text in the row's editor is rejected, or <see langword="null"/> while it's valid.
            /// </summary>
            public string? InvalidReason { get; set; }

            /// <summary>
            /// Determines whether the row's editor already shows <paramref name="state"/>.
            /// </summary>
            /// <param name="state">The freshly read state.</param>
            /// <returns><see langword="true"/> when both states were read and are equal.</returns>
            public bool Shows(RowState? state) => Shown is { } shown && state is { } current && shown.Equals(current);
        }

        /// <summary>
        /// A command that runs an action and can always execute; used by the Reset buttons.
        /// </summary>
        /// <param name="execute">The action to run.</param>
        private sealed class ActionCommand(Action execute) : System.Windows.Input.ICommand
        {
            /// <inheritdoc/>
            public event EventHandler? CanExecuteChanged
            {
                add { }
                remove { }
            }

            /// <inheritdoc/>
            public bool CanExecute(object? parameter) => true;

            /// <inheritdoc/>
            public void Execute(object? parameter) => execute();
        }
    }
}
