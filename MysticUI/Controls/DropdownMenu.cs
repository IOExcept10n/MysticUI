using MysticUI.Extensions;
using Stride.Core.Mathematics;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the dropdown menu.
    /// </summary>
    public class DropdownMenu : ContentControl
    {
        private readonly ToggleButton button;
        private readonly StackPanel panel;
        private Control? child;

        /// <summary>
        /// Determines whether the menu is expanded.
        /// </summary>
        public bool IsExpanded => button.IsChecked == true;

        /// <summary>
        /// Gets or sets the menu orientation.
        /// </summary>
        public Orientation Orientation
        {
            get => panel.Orientation;
            set
            {
                panel.Orientation = value;
                if (value == Orientation.Horizontal)
                {
                    button.VerticalAlignment = VerticalAlignment.Stretch;
                    button.HorizontalAlignment = HorizontalAlignment.Left;
                }
                else
                {
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.VerticalAlignment = VerticalAlignment.Top;
                }
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DropdownMenu"/> class.
        /// </summary>
        public DropdownMenu()
        {
            button = new ToggleButton().WithDefaultStyle();
            button.CheckedChanged += Button_CheckedChanged;
            panel = new();
            Orientation = default;
        }

        /// <inheritdoc/>
        protected internal override void ResetChildInternal()
        {
            base.ResetChildInternal();
            if (Child != null)
            {
                child = Child;
                panel.Clear();
                panel.Add(button);
                SetContent();
                //Desktop?.InvalidateLayout();
            }
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            var result = base.MeasureInternal(availableSize);
            var test = panel.TestMeasure();
            return new(Math.Max(result.X, test.X), Math.Max(result.Y, test.Y));
        }

        private void Button_CheckedChanged(object? sender, EventArgs e)
        {
            SetContent();
        }

        private void SetContent()
        {
            if (child != null)
            {
                panel.Remove(child);
                if (IsExpanded)
                {
                    panel.Add(child);
                }
            }
            InvalidateMeasure();
        }
    }
}