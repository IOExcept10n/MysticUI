using Stride.Core.Mathematics;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a panel with elements aligned to stack in given orientation.
    /// </summary>
    public class StackPanel : Panel
    {
        private int spacing;
        private Orientation orientation;
        private bool isBoundless;
        private bool overrideChildrenAlignment = true;

        /// <summary>
        /// Gets or sets the spacing between panel elements.
        /// </summary>
        [Category("Layout")]
        public int Spacing
        {
            get => spacing;
            set
            {
                if (value == spacing) return;
                spacing = value;
                InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets the orientation of the panel.
        /// </summary>
        [Category("Layout")]
        public Orientation Orientation
        {
            get => orientation;
            set
            {
                if (value == orientation) return;
                orientation = value;
                InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets the value determines whether the panel is "boundless".
        /// </summary>
        /// <value>
        /// If set to <see langword="true"/> - the panel doesn't count available size to measure, every control will get all panel available size when measure.
        /// This can be helpful when the panel is used as the main element for the <see cref="ScrollViewer"/>.
        /// </value>
        [Category("Behavior")]
        public bool IsBoundless
        {
            get => isBoundless;
            set
            {
                if (value == isBoundless) return;
                isBoundless = value;
                InvalidateArrange();
            }
        }

        /// <summary>
        /// Gets or sets the value that determines whether the panel does override the alignment properties of its children.
        /// </summary>
        /// <value>
        /// If set to <see langword="true"/> - the panel overrides children alignment according to orientation. This is needed to align them one-by-one.
        /// Set it to <see langword="false"/> only when the panel is a working part of any other controls and you want to handle the alignment by yourself.
        /// </value>
        [Category("Behavior")]
        [DefaultValue(true)]
        public bool OverrideChildrenAlignment
        {
            get => overrideChildrenAlignment;
            set
            {
                if (value == overrideChildrenAlignment) return;
                overrideChildrenAlignment = value;
                InvalidateArrange();
            }
        }

        /// <inheritdoc/>
        protected internal override void OnChildrenChanged(object sender, NotifyCollectionChangedEventArgs args)
        {
            base.OnChildrenChanged(sender, args);
            InvalidateArrange();
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            int width = 0, height = 0;
            if (Orientation == Orientation.Horizontal)
            {
                foreach (var child in Children)
                {
                    var measure = child.Measure(availableSize);
                    width += measure.X + Spacing;
                    height = Math.Max(height, measure.Y);
                }
            }
            else
            {
                foreach (var child in Children)
                {
                    var measure = child.Measure(availableSize);
                    height += measure.Y + Spacing;
                    width = Math.Max(width, measure.X);
                }
            }
            return new(width, height);
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            var bounds = ActualBounds;
            if (Orientation == Orientation.Horizontal)
            {
                int availableWidth = bounds.Width;
                for (int i = 0; i < Children.Count; i++)
                {
                    var control = Children[i];
                    control.HorizontalAlignment = HorizontalAlignment.Left;
                    control.Arrange(bounds);
                    int offset = control.Width + Spacing;
                    if (!isBoundless) availableWidth -= offset;
                    bounds.X += offset;
                    bounds.Width = availableWidth;
                }
            }
            else
            {
                int availableHeight = bounds.Height;
                for (int i = 0; i < Children.Count; i++)
                {
                    var control = Children[i];
                    control.VerticalAlignment = VerticalAlignment.Top;
                    control.Arrange(bounds);
                    int offset = control.Height + Spacing;
                    if (!isBoundless) availableHeight -= offset;
                    bounds.Y += offset;
                    bounds.Height = availableHeight;
                }
            }
        }
    }
}