using Stride.Core.Mathematics;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a presentation of the <see cref="ListBox"/> with the ability to use the <see cref="Grid"/> as the main panel.
    /// </summary>
    public class GridView : ListBox
    {
        private Orientation orientation;
        private int cellsAmount;
        private ColumnDefinition defaultColumnDefinition = ColumnDefinition.Default;
        private RowDefinition defaultRowDefinition = RowDefinition.Default;
        private bool handleRestSize = true;

        /// <summary>
        /// Amount of cells in a single row or column of the internal <see cref="Grid"/>.
        /// </summary>
        [Category("Layout")]
        public int CellsAmount
        {
            get => cellsAmount;
            set
            {
                if (cellsAmount == value) return;
                cellsAmount = value;
                InvalidateArrange();
                NotifyPropertyChanged(nameof(CellsAmount));
            }
        }

        /// <summary>
        /// Orientation of the cells.
        /// </summary>
        [Category("Layout")]
        public Orientation Orientation
        {
            get => orientation;
            set
            {
                if (orientation == value) return;
                orientation = value;
                InvalidateArrange();
                NotifyPropertyChanged(nameof(Orientation));
            }
        }

        /// <summary>
        /// Defines the custom default column definition for the internal grid.
        /// </summary>
        /// <remarks>
        /// This property overrides the <see cref="Grid"/> style so use it to customize the internal <see cref="Grid"/>.
        /// </remarks>
        [Category("Behavior")]
        public ColumnDefinition DefaultColumnDefinition
        {
            get => defaultColumnDefinition;
            set
            {
                if (defaultColumnDefinition == value) return;
                defaultColumnDefinition = value;
                InvalidateArrange();
                NotifyPropertyChanged(nameof(DefaultColumnDefinition));
            }
        }

        /// <summary>
        /// Defines the custom default row definition for the internal grid.
        /// </summary>
        /// <remarks>
        /// This property overrides the <see cref="Grid"/> style so use it to customize the internal <see cref="Grid"/>.
        /// </remarks>
        [Category("Behavior")]
        public RowDefinition DefaultRowDefinition
        {
            get => defaultRowDefinition;
            set
            {
                if (defaultRowDefinition == value) return;
                defaultRowDefinition = value;
                InvalidateArrange();
                NotifyPropertyChanged(nameof(DefaultRowDefinition));
            }
        }

        /// <summary>
        /// Defines the internal grid behavior to handle the rest of size.
        /// </summary>
        /// <remarks>
        /// This property overrides the <see cref="Grid"/> style so use it to customize the internal <see cref="Grid"/>.
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool HandleRestSize
        {
            get => handleRestSize;
            set
            {
                if (handleRestSize == value) return;
                handleRestSize = value;
                InvalidateArrange();
                NotifyPropertyChanged(nameof(HandleRestSize));
            }
        }

        /// <inheritdoc/>
        protected override Panel CreateDefaultPanel() => new Grid() { DefaultColumnDefinition = defaultColumnDefinition, DefaultRowDefinition = defaultRowDefinition, HandleRestSize = handleRestSize };

        /// <inheritdoc/>
        protected override void PlaceItemControl(Control item)
        {
            if (ItemsPanel is Grid)
            {
                int position = ItemsPanel.Count;
                Point boundsSize = new(ActualBounds.Width, ActualBounds.Height);
                if (boundsSize.X == 0) boundsSize.X = Parent?.ActualBounds.Width ?? 0;
                if (boundsSize.Y == 0) boundsSize.Y = Parent?.ActualBounds.Height ?? 0;
                Point itemSize = item.Measure(boundsSize);
                if (Orientation == Orientation.Vertical)
                {
                    int amount = CellsAmount;
                    if (amount == 0)
                    {
                        if (itemSize.X != 0) amount = boundsSize.X / itemSize.X;
                        else amount = 1;
                    }
                    item.GridRow = position / amount;
                    item.GridColumn = position % amount;
                }
                else
                {
                    int amount = CellsAmount;
                    if (amount == 0)
                    {
                        if (itemSize.Y != 0) amount = boundsSize.Y / itemSize.Y;
                        else amount = 1;
                    }
                    item.GridColumn = position / amount;
                    item.GridRow = position % amount;
                }
            }
            base.PlaceItemControl(item);
        }
    }
}