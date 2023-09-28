using MysticUI.Extensions;
using Stride.Core.Mathematics;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents the dropdown menu.
    /// </summary>
    public class DropdownMenu : ContentControl
    {
        /// <summary>
        /// The name of the expand button style.
        /// </summary>
        public const string ExpandButtonStyleName = "ExpandButtonStyle";

        private readonly ToggleButton button;
        private readonly StackPanel panel;
        private Control? child;
        private int minimalExpandedHeight;

        /// <summary>
        /// Determines whether the menu is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => button.IsChecked == true;
            set
            {
                if (value == button.IsChecked) return;
                button.IsChecked = value;
                InvalidateHierarchy();
                NotifyPropertyChanged(nameof(IsExpanded));
            }
        }

        /// <summary>
        /// Gets or sets the height of the button in the dropdown menu.
        /// </summary>
        public int ButtonHeight
        {
            get => button.Height;
            set
            {
                if (value == button.Height) return;
                button.Height = value;
                InvalidateHierarchy();
                NotifyPropertyChanged(nameof(ButtonHeight));
            }
        }

        /// <summary>
        /// Gets or sets the minimal height of the expanded dropdown menu.
        /// </summary>
        public int MinimalExpandedHeight
        {
            get => minimalExpandedHeight;
            set
            {
                if (value == minimalExpandedHeight) return;
                minimalExpandedHeight = value;
                InvalidateHierarchy();
                NotifyPropertyChanged(nameof(MinimalExpandedHeight));
            }
        }

        /// <summary>
        /// Gets or sets the text of the expand button.
        /// </summary>
        public string? Text
        {
            get => button.Content?.ToString();
            set => button.Content = value;
        }

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
            button = new ToggleButton()
            {
                StyleName = ExpandButtonStyleName
            };
            button.CheckedChanged += Button_CheckedChanged;
            panel = new()
            {
                Parent = this
            };
            
            
            Orientation = default;
            ResetChildInternal();

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
                Child = panel;
                InvalidateHierarchy();
            }
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            var result = base.MeasureInternal(availableSize);
            var test = panel.TestMeasure();
            if (test.X > result.X) 
                result.X = test.X;
            if (test.Y > result.Y)
                result.Y = test.Y;
            if (IsExpanded && MinimalExpandedHeight > result.Y)
                result.Y = MinimalExpandedHeight;
            return result;
        }

        /// <inheritdoc/>
        public override void InvalidateMeasure()
        {
            base.InvalidateMeasure();
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
            InvalidateHierarchy();
        }

        private void InvalidateHierarchy()
        {
            InvalidateMeasure();
            //Desktop?.Root?.InvalidateArrange();
            //Desktop?.Root?.InvalidateMeasure();
        }
    }
}