using MysticUI.Extensions;
using Stride.Core.Mathematics;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a panel that is used to precise controls layout using rows and columns.
    /// </summary>
    public class Grid : Panel
    {
        private int columnSpacing;
        private int rowSpacing;
        private readonly ObservableCollection<ColumnDefinition> columnDefinitions = new();
        private readonly ObservableCollection<RowDefinition> rowDefinitions = new();
        private readonly List<int> cellXLocations = new();
        private readonly List<int> cellYLocations = new();
        private readonly List<int> gridLinesX = new();
        private readonly List<int> gridLinesY = new();

        private Point actualSize;
        private readonly List<int> measureColumnWidths = new();
        private readonly List<int> measureRowHeights = new();
        private readonly List<Control> visibleControls = new();
        private readonly List<int> columnWidths = new();
        private readonly List<int> rowHeights = new();
        private int hoverRowIndex = -1;
        private int hoverColumnIndex = -1;
        private int selectedRowIndex = -1;
        private int selectedColumnIndex = -1;
        private List<Control>[,]? controlGroups;
        private bool handleRestSize = true;

        /// <summary>
        /// Determines whether to show debug layout grid lines between rows and columns.
        /// </summary>
        [Category("Miscellaneous")]
        public bool ShowGridLines { get; set; }

        /// <summary>
        /// Color of grid lines to draw.
        /// </summary>
        [Category("Miscellaneous")]
        public Color GridLinesColor { get; set; }

        /// <summary>
        /// Spacing between columns in grid.
        /// </summary>
        [Category("Layout")]
        public int ColumnSpacing
        {
            get => columnSpacing;
            set
            {
                if (value == columnSpacing) return;
                columnSpacing = value;
                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Spacing between rows in grid.
        /// </summary>
        [Category("Layout")]
        public int RowSpacing
        {
            get => rowSpacing;
            set
            {
                if (value == rowSpacing) return;
                rowSpacing = value;
                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Determines whether to handle the rest of size if the <see cref="Grid"/> has more available size than its content.
        /// </summary>
        [Category("Layout")]
        [DefaultValue(true)]
        public bool HandleRestSize
        {
            get => handleRestSize;
            set
            {
                if (value == handleRestSize) return;
                handleRestSize = value;
                InvalidateMeasure();
            }
        }

        /// <summary>
        /// Default column size to use.
        /// </summary>
        [Browsable(false)]
        public ColumnDefinition DefaultColumnDefinition { get; set; } = ColumnDefinition.Default;

        /// <summary>
        /// Default row size to use.
        /// </summary>
        [Browsable(false)]
        public RowDefinition DefaultRowDefinition { get; set; } = RowDefinition.Default;

        /// <summary>
        /// Collection of column definitions presented in the grid.
        /// </summary>
        [Browsable(false)]
        public ObservableCollection<ColumnDefinition> ColumnDefinitions => columnDefinitions;

        /// <summary>
        /// Collection of row definitions presented in grid.
        /// </summary>
        [Browsable(false)]
        public ObservableCollection<RowDefinition> RowDefinitions => rowDefinitions;

        /// <summary>
        /// Background brush for selected rows and columns.
        /// </summary>
        [Category("Appearance")]
        public IBrush? SelectionBackground { get; set; }

        /// <summary>
        /// Background brush for hovered rows and columns.
        /// </summary>
        [Category("Appearance")]
        public IBrush? SelectionHoverBackground { get; set; }

        /// <summary>
        /// Gets or sets a value that indicates how to select grid cells.
        /// </summary>
        [Category("Behavior")]
        public GridSelectionMode SelectionMode { get; set; }

        /// <summary>
        /// Gets or sets the index of the hovered row.
        /// </summary>
        /// <value>
        /// Index of the hovered row or <see langword="-1"/> if there are no hovered rows.
        /// </value>
        [Browsable(false), XmlIgnore, JsonIgnore]
        public int HoverRowIndex
        {
            get => hoverRowIndex;
            set
            {
                if (value == hoverRowIndex) return;
                hoverRowIndex = value;
                HoverIndexChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(HoverRowIndex));
            }
        }

        /// <summary>
        /// Gets or sets the index of the hovered column.
        /// </summary>
        /// <value>
        /// Index of the hovered column or <see langword="-1"/> if there are no hovered columns.
        /// </value>
        [Browsable(false), XmlIgnore, JsonIgnore]
        public int HoverColumnIndex
        {
            get => hoverColumnIndex;
            set
            {
                if (value == hoverColumnIndex) return;
                hoverColumnIndex = value;
                HoverIndexChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(HoverColumnIndex));
            }
        }

        /// <summary>
        /// Gets or sets the index of the selected row.
        /// </summary>
        /// <value>
        /// Index of the selected row or <see langword="-1"/> if there are no selected rows.
        /// </value>
        [Category("Behavior")]
        public int SelectedRowIndex
        {
            get => selectedRowIndex;
            set
            {
                if (value == selectedRowIndex) return;
                selectedRowIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(SelectedRowIndex));
            }
        }

        /// <summary>
        /// Gets or sets the index of the selected column.
        /// </summary>
        /// <value>
        /// Index of the selected column or <see langword="-1"/> if there are no selected columns.
        /// </value>
        [Category("Behavior")]
        public int SelectedColumnIndex
        {
            get => selectedColumnIndex;
            set
            {
                if (value == selectedColumnIndex) return;
                selectedColumnIndex = value;
                SelectedIndexChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(SelectedColumnIndex));
            }
        }

        /// <summary>
        /// Occurs when the value of <see cref="HoverColumnIndex"/> or <see cref="HoverRowIndex"/> properties changes.
        /// </summary>
        public event EventHandler? HoverIndexChanged;

        /// <summary>
        /// Occurs when the value of <see cref="SelectedColumnIndex"/> or <see cref="SelectedRowIndex"/> properties changes.
        /// </summary>
        public event EventHandler? SelectedIndexChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="Grid"/> class.
        /// </summary>
        public Grid()
        {
            columnDefinitions.CollectionChanged += OnProportionsChanged!;
            rowDefinitions.CollectionChanged += OnProportionsChanged!;

            ShowGridLines = false;
            GridLinesColor = Color.White;
        }

        /// <summary>
        /// Gets calculated width of the column with provided index.
        /// </summary>
        public int GetColumnWidth(int index)
        {
            if (columnWidths == null || index < 0 || index >= columnWidths.Count)
            {
                return -1;
            }

            return columnWidths[index];
        }

        /// <summary>
        /// Gets calculated width of the column with provided index.
        /// </summary>
        public int GetRowHeight(int index)
        {
            if (rowHeights == null || index < 0 || index >= rowHeights.Count)
            {
                return -1;
            }

            return rowHeights[index];
        }

        /// <summary>
        /// Gets calculated X position of the column with provided index.
        /// </summary>
        public int GetCellLocationX(int column)
        {
            if (column < 0 || column >= cellXLocations.Count)
            {
                return -1;
            }

            return cellXLocations[column];
        }

        /// <summary>
        /// Gets calculated Y position of the row with provided index.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public int GetCellLocationY(int row)
        {
            if (row < 0 || row >= cellYLocations.Count)
            {
                return -1;
            }

            return cellYLocations[row];
        }

        /// <summary>
        /// Gets the rectangle with bounds of the cell with provided position.
        /// </summary>
        /// <param name="column">Column index of the grid cell.</param>
        /// <param name="row">Row index of the grid cell.</param>
        /// <returns>Bounds of the provided cell.</returns>
        public Rectangle GetCellRectangle(int column, int row)
        {
            if (column < 0 || column >= cellXLocations.Count ||
                row < 0 || row >= cellYLocations.Count)
            {
                return Rectangle.Empty;
            }

            return new Rectangle(cellXLocations[column], cellYLocations[row],
                columnWidths[column], rowHeights[row]);
        }

        /// <summary>
        /// Gets the column definition at given index.
        /// </summary>
        public ColumnDefinition GetColumnDefinition(int index)
        {
            if (index < 0 || index >= ColumnDefinitions.Count)
            {
                return DefaultColumnDefinition;
            }

            return ColumnDefinitions[index];
        }

        /// <summary>
        /// Gets the row definition at given index.
        /// </summary>
        public RowDefinition GetRowDefinition(int index)
        {
            if (index < 0 || index >= RowDefinitions.Count)
            {
                return DefaultRowDefinition;
            }

            return RowDefinitions[index];
        }

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            RenderSelection(context);

            base.RenderInternal(context);

            if (!ShowGridLines)
                return;

            for (int i = 0; i < gridLinesX.Count; i++)
            {
                context.FillRectangle(new(gridLinesX[i] + ActualBounds.Left, ActualBounds.Top, 1, ActualBounds.Height), GridLinesColor);
            }

            for (int i = 0; i < gridLinesY.Count; i++)
            {
                context.FillRectangle(new(ActualBounds.Left, gridLinesY[i] + ActualBounds.Top, ActualBounds.Width, 1), GridLinesColor);
            }
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            return ProcessLayoutFixed(availableSize);
        }

        /// <inheritdoc/>
        protected internal override void OnMouseLeft()
        {
            base.OnMouseLeft();
            UpdateHoverPosition(null);
        }

        /// <inheritdoc/>
        protected internal override void OnMouseEntered()
        {
            base.OnMouseEntered();
            UpdateHoverPosition(Desktop?.MousePosition);
        }

        /// <inheritdoc/>
        protected internal override void OnMouseMove()
        {
            base.OnMouseMove();
            if (Desktop?.MousePosition != Desktop?.PreviousMousePosition)
            {
                UpdateHoverPosition(Desktop?.MousePosition);
            }
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchDown()
        {
            base.OnTouchDown();

            if (Desktop == null)
                return false;

            if (HoverRowIndex != -1)
            {
                if (SelectedRowIndex != HoverRowIndex)
                {
                    SelectedRowIndex = HoverRowIndex;
                }
                else
                {
                    SelectedRowIndex = -1;
                }
            }

            if (HoverColumnIndex != -1)
            {
                if (SelectedColumnIndex != HoverColumnIndex)
                {
                    SelectedColumnIndex = HoverColumnIndex;
                }
                else
                {
                    SelectedColumnIndex = -1;
                }
            }

            return SelectedColumnIndex != -1 && SelectedRowIndex != -1;
        }

        private void ProcessLayoutFixedPart()
        {
            // Provide as much size for the starred columns as we can.
            int size = 0;
            for (int i = 0; i < measureColumnWidths.Count; i++)
            {
                var proportion = GetColumnDefinition(i);
                if (proportion.Width.GridUnitType != GridUnitType.Star)
                    continue;
                size = Math.Max(measureColumnWidths[i], size);
            }
            for (int i = 0; i < measureColumnWidths.Count; i++)
            {
                var proportion = GetColumnDefinition(i);
                if (proportion.Width.GridUnitType != GridUnitType.Star)
                    continue;
                measureColumnWidths[i] = ((int)(size * proportion.Width.Value)).OptionalClamp(proportion.MinWidth, proportion.MaxWidth);
            }
            size = 0;
            for (int i = 0; i < measureRowHeights.Count; i++)
            {
                var proportion = GetRowDefinition(i);
                if (proportion.Height.GridUnitType != GridUnitType.Star)
                    continue;
                size = Math.Max(measureRowHeights[i], size);
            }
            for (int i = 0; i < measureRowHeights.Count; i++)
            {
                var proportion = GetRowDefinition(i);
                if (proportion.Height.GridUnitType != GridUnitType.Star)
                    continue;
                measureRowHeights[i] = ((int)(size * proportion.Height.Value)).OptionalClamp(proportion.MinHeight, proportion.MaxHeight);
            }
        }

        private Point ProcessLayoutFixed(Point availableSize)
        {
            int rows = 0, columns = 0;
            // Get the full list of all visible controls, also count required amount of rows and columns.
            visibleControls.Clear();
            foreach (var child in Children)
            {
                if (child.Visible)
                {
                    visibleControls.Add(child);
                    Point gridPosition = GetActualGridPosition(child);
                    int column = gridPosition.X + Math.Max(child.GridColumnSpan, 1);
                    columns = Math.Max(columns, column);
                    int row = gridPosition.Y + Math.Max(child.GridRowSpan, 1);
                    rows = Math.Max(rows, row);
                }
            }
            columns = Math.Max(columns, ColumnDefinitions.Count);
            rows = Math.Max(rows, RowDefinitions.Count);
            measureColumnWidths.Clear();
            measureRowHeights.Clear();
            for (int i = 0; i < columns; i++)
                measureColumnWidths.Add(0);
            for (int i = 0; i < rows; i++)
                measureRowHeights.Add(0);

            // Group controls by their rows and columns.
            if (controlGroups == null ||
                controlGroups.GetLength(0) < columns ||
                controlGroups.GetLength(1) < rows)
            {
                controlGroups = new List<Control>[columns, rows];
            }

            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    (controlGroups[column, row] ??= new()).Clear();
                }
            }

            foreach (var control in visibleControls)
            {
                controlGroups[control.GridColumn, control.GridRow].Add(control);
            }

            availableSize.X -= (measureColumnWidths.Count - 1) * columnSpacing;
            availableSize.Y -= (measureRowHeights.Count - 1) * rowSpacing;

            // Get the size of all defined hardcoded (pixel) columns and rows.
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    var rowProportion = GetRowDefinition(row);
                    var columnProportion = GetColumnDefinition(column);

                    if (columnProportion.Width.IsAbsolute)
                        measureColumnWidths[column] = (int)columnProportion.Width.Value;
                    if (rowProportion.Height.IsAbsolute)
                        measureRowHeights[row] = (int)rowProportion.Height.Value;

                    foreach (var control in controlGroups[column, row])
                    {
                        Point gridPosition = GetActualGridPosition(control);
                        Point measuredSize = Point.Zero;
                        if (!rowProportion.Height.IsAbsolute || !columnProportion.Width.IsAbsolute)
                        {
                            measuredSize = control.Measure(availableSize);
                        }

                        if (control.GridColumnSpan != 1)
                            measuredSize.X = 0;
                        if (control.GridRowSpan != 1)
                            measuredSize.Y = 0;

                        if (measuredSize.X > measureColumnWidths[column] && !columnProportion.Width.IsAbsolute)
                            measureColumnWidths[column] = measuredSize.X;
                        if (measuredSize.Y > measureRowHeights[row] && !rowProportion.Height.IsAbsolute)
                            measureRowHeights[row] = measuredSize.Y;
                    }
                }
            }

            ProcessLayoutFixedPart();

            // Return the total size of all rows and columns.
            int width = measureColumnWidths.Sum() + columnSpacing * (measureColumnWidths.Count - 1);
            int height = measureRowHeights.Sum() + rowSpacing * (measureRowHeights.Count - 1);
            return new(width, height);
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            var bounds = ActualBounds;
            ProcessLayoutFixed(new(bounds.Width, bounds.Height));

            columnWidths.Clear();
            columnWidths.AddRange(measureColumnWidths);
            rowHeights.Clear();
            rowHeights.AddRange(measureRowHeights);

            // Count for starred columns, compute their total part and partial sizes.
            float availableWidth = bounds.Width - (columnSpacing * (columnWidths.Count - 1));
            float totalPart = 0;
            for (int column = 0; column < columnWidths.Count; column++)
            {
                var proportion = GetColumnDefinition(column);
                if (proportion.Width.IsStar)
                {
                    totalPart += proportion.Width.Value;
                }
                else
                {
                    availableWidth -= columnWidths[column];
                }
            }

            if (totalPart != 0)
            {
                float space = 0;
                for (int column = 0; column < columnWidths.Count; column++)
                {
                    var proportion = GetColumnDefinition(column);
                    if (proportion.Width.IsStar)
                    {
                        space += columnWidths[column] = ((int)(proportion.Width.Value * (availableWidth / totalPart))).OptionalClamp(proportion.MinWidth, proportion.MaxWidth);
                    }
                }
                availableWidth -= space;
            }

            // Give all the available size for the last column that is not defined as fixed.
            if (availableWidth > 0)
            {
                int lastIndex = columnWidths.Count - 1;
                while (lastIndex >= 0 && availableWidth > 0)
                {
                    var definition = GetColumnDefinition(lastIndex);
                    if (!definition.Width.IsAbsolute)
                    {
                        if (definition.MaxWidth == 0)
                        {
                            columnWidths[lastIndex] += (int)availableWidth;
                            break;
                        }
                        else
                        {
                            int width = columnWidths[lastIndex];
                            int remain = definition.MaxWidth - width;
                            if (remain > availableWidth)
                            {
                                columnWidths[lastIndex] += (int)availableWidth;
                                break;
                            }
                            else
                            {
                                columnWidths[lastIndex] += remain;
                                availableWidth -= remain;
                            }
                        }
                    }
                    lastIndex--;
                }
            }

            // Count and compute stars for the rows.
            float availableHeight = bounds.Height - (rowSpacing * (rowHeights.Count - 1));

            totalPart = 0;
            for (int row = 0; row < rowHeights.Count; row++)
            {
                var proportion = GetRowDefinition(row);
                if (proportion.Height.IsStar)
                {
                    totalPart += proportion.Height.Value;
                }
                else
                {
                    availableHeight -= rowHeights[row];
                }
            }

            if (totalPart != 0)
            {
                float space = 0;
                for (int row = 0; row < rowHeights.Count; row++)
                {
                    var proportion = GetRowDefinition(row);
                    if (proportion.Height.IsStar)
                    {
                        space += rowHeights[row] = ((int)(proportion.Height.Value * (availableHeight / totalPart))).OptionalClamp(proportion.MinHeight, proportion.MaxHeight);
                    }
                }
                availableHeight -= space;
            }

            // Give all remaining height for the last row with non-fixed value.
            if (availableHeight > 0 && HandleRestSize)
            {
                int lastIndex = rowHeights.Count - 1;
                while (lastIndex >= 0 && availableHeight > 0)
                {
                    var definition = GetRowDefinition(lastIndex);
                    if (!definition.Height.IsAbsolute)
                    {
                        if (definition.MaxHeight == 0)
                        {
                            rowHeights[lastIndex] += (int)availableHeight;
                            break;
                        }
                        else
                        {
                            int height = rowHeights[lastIndex];
                            int remain = definition.MaxHeight - height;
                            if (remain > availableHeight)
                            {
                                rowHeights[lastIndex] += (int)availableHeight;
                                break;
                            }
                            else
                            {
                                rowHeights[lastIndex] += remain;
                                availableHeight -= remain;
                            }
                        }
                    }
                    lastIndex--;
                }
            }

            // Add debug lines to display.
            actualSize = Point.Zero;
            gridLinesX.Clear();
            cellXLocations.Clear();
            Point p = Point.Zero;
            for (int i = 0; i < columnWidths.Count; i++)
            {
                cellXLocations.Add(p.X);
                int width = columnWidths[i];
                p.X += width;
                if (i < columnWidths.Count - 1)
                {
                    gridLinesX.Add(p.X + columnSpacing / 2);
                }
                p.X += columnSpacing;
                actualSize.X += width;
            }

            gridLinesY.Clear();
            cellYLocations.Clear();
            for (int i = 0; i < rowHeights.Count; i++)
            {
                cellYLocations.Add(p.Y);
                int height = rowHeights[i];
                p.Y += height;

                if (i < rowHeights.Count - 1)
                {
                    gridLinesY.Add(p.Y + rowSpacing / 2);
                }
                p.Y += rowSpacing;
                actualSize.Y += height;
            }

            // Align all controls according to their cells.
            foreach (var control in visibleControls)
            {
                LayoutControl(control);
            }
        }

        private void LayoutControl(Control control)
        {
            Point gridPosition = GetActualGridPosition(control);
            Point cellSize = Point.Zero;
            cellSize.X = columnWidths.Skip(gridPosition.X).Take(control.GridColumnSpan).Sum() + (columnSpacing * control.GridColumnSpan - 1);
            cellSize.Y = rowHeights.Skip(gridPosition.Y).Take(control.GridRowSpan).Sum() + (rowSpacing * control.GridRowSpan - 1);

            Rectangle bounds = ActualBounds;
            Rectangle targetBounds = new(bounds.Left + cellXLocations[gridPosition.X], bounds.Top + cellYLocations[gridPosition.Y], cellSize.X, cellSize.Y);
            if (targetBounds.Right > bounds.Right)
            {
                targetBounds.Width = bounds.Right - targetBounds.X;
            }
            targetBounds.Width = Math.Max(targetBounds.Width, 0);
            if (targetBounds.Bottom > bounds.Bottom)
            {
                targetBounds.Height = bounds.Bottom - targetBounds.Y;
            }
            targetBounds.Height = Math.Max(targetBounds.Height, 0);
            control.Arrange(targetBounds);
        }

        private void RenderSelection(RenderContext context)
        {
            var bounds = ActualBounds;
            switch (SelectionMode)
            {
                case GridSelectionMode.None:
                    break;

                case GridSelectionMode.Row:
                    {
                        if (HoverRowIndex != -1 && HoverRowIndex != SelectedRowIndex)
                        {
                            Rectangle selection = new(bounds.Left,
                                                      cellYLocations[HoverRowIndex] + bounds.Top - rowSpacing / 2,
                                                      bounds.Width,
                                                      rowHeights[HoverRowIndex] + rowSpacing);
                            SelectionHoverBackground?.Draw(context, selection);
                        }
                        if (SelectedRowIndex != -1)
                        {
                            Rectangle selection = new(bounds.Left,
                                                      cellYLocations[SelectedRowIndex] + bounds.Top - rowSpacing / 2,
                                                      bounds.Width,
                                                      rowHeights[SelectedRowIndex] + rowSpacing);
                            SelectionBackground?.Draw(context, selection);
                        }
                        break;
                    }
                case GridSelectionMode.Column:
                    {
                        if (HoverColumnIndex != -1 && HoverColumnIndex != SelectedColumnIndex)
                        {
                            Rectangle selection = new(cellXLocations[HoverColumnIndex] + bounds.Left + columnSpacing / 2,
                                                      bounds.Top,
                                                      columnWidths[HoverColumnIndex] + columnSpacing,
                                                      bounds.Height);
                            SelectionHoverBackground?.Draw(context, selection);
                        }
                        if (SelectedColumnIndex != -1)
                        {
                            Rectangle selection = new(cellXLocations[SelectedColumnIndex] + bounds.Left + columnSpacing / 2,
                                                      bounds.Top,
                                                      columnWidths[SelectedColumnIndex] + columnSpacing,
                                                      bounds.Height);
                            SelectionBackground?.Draw(context, selection);
                        }
                        break;
                    }
                case GridSelectionMode.Cell:
                    {
                        if (HoverRowIndex != -1 && HoverColumnIndex != -1 && (HoverColumnIndex != SelectedColumnIndex || HoverRowIndex != SelectedRowIndex))
                        {
                            Rectangle selection = new(cellXLocations[HoverColumnIndex] + bounds.Left + columnSpacing / 2,
                                                      cellYLocations[HoverRowIndex] + bounds.Top - rowSpacing / 2,
                                                      columnWidths[HoverColumnIndex] + columnSpacing,
                                                      rowHeights[HoverRowIndex] + rowSpacing);
                            SelectionHoverBackground?.Draw(context, selection);
                        }
                        if (SelectedRowIndex != -1 && SelectedColumnIndex != -1)
                        {
                            Rectangle selection = new(cellXLocations[SelectedColumnIndex] + bounds.Left + columnSpacing / 2,
                                                      cellYLocations[SelectedRowIndex] + bounds.Top - rowSpacing / 2,
                                                      columnWidths[SelectedColumnIndex] + columnSpacing,
                                                      rowHeights[SelectedRowIndex] + rowSpacing);
                            SelectionBackground?.Draw(context, selection);
                        }
                        break;
                    }
            }
        }

        private void UpdateHoverPosition(Point? position)
        {
            if (SelectionMode == GridSelectionMode.None)
                return;

            if (position == null)
            {
                HoverRowIndex = -1;
                HoverColumnIndex = -1;
                return;
            }

            Point pos = ToLocal(position.Value);
            if (SelectionMode == GridSelectionMode.Column || SelectionMode == GridSelectionMode.Cell)
            {
                for (int i = 0; i < cellXLocations.Count; i++)
                {
                    int cx = cellXLocations[i] + ActualBounds.Left - ColumnSpacing / 2;
                    if (pos.X >= cx && pos.X < cx + columnWidths[i] + ColumnSpacing / 2)
                    {
                        HoverColumnIndex = i;
                        break;
                    }
                }
            }

            if (SelectionMode == GridSelectionMode.Row || SelectionMode == GridSelectionMode.Cell)
            {
                for (int i = 0; i < cellYLocations.Count; i++)
                {
                    int cy = cellYLocations[i] + ActualBounds.Top - RowSpacing / 2;
                    if (pos.Y >= cy && pos.Y < cy + rowHeights[i] + RowSpacing / 2)
                    {
                        HoverRowIndex = i;
                        break;
                    }
                }
            }
        }

        private static Point GetActualGridPosition(Control child) => new(child.GridColumn, child.GridRow);

        private void OnProportionsChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            if (args.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (var i in args.NewItems!)
                {
                    ((ProportionDefinition)i).Changed += OnProportionChanged!;
                }
            }
            else if (args.Action == NotifyCollectionChangedAction.Remove)
            {
                foreach (var i in args.OldItems!)
                {
                    ((ProportionDefinition)i).Changed -= OnProportionChanged!;
                }
            }

            HoverRowIndex = HoverColumnIndex = SelectedRowIndex = SelectedColumnIndex = -1;

            InvalidateMeasure();
        }

        private void OnProportionChanged(object sender, EventArgs args)
        {
            InvalidateMeasure();
        }

        /// <summary>
        /// Represents an enum that describes how to select grid cells.
        /// </summary>
        public enum GridSelectionMode
        {
            /// <summary>
            /// Don't select grid cells at all.
            /// </summary>
            None,

            /// <summary>
            /// Select the whole row.
            /// </summary>
            Row,

            /// <summary>
            /// Select the whole column.
            /// </summary>
            Column,

            /// <summary>
            /// Select a single cell.
            /// </summary>
            Cell
        }
    }
}