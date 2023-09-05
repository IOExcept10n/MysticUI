using MysticUI.Extensions;
using Stride.Core.Mathematics;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a container with the ability to store resizable parts which are split with movable thumbs.
    /// </summary>
    public class SplitPane : SingleItemControlBase<Grid>, IContainerControl
    {
        private readonly List<Button> thumbs = new();
        private int? mousePosition;
        private int thumbsSize;
        private Button? downThumb;
        private Orientation orientation;
        private int thumbSize = 6;

        /// <summary>
        /// Gets or sets the orientation of the control.
        /// </summary>
        [Category("Layout")]
        public Orientation Orientation
        {
            get => orientation;
            set
            {
                if (orientation == value) return;
                orientation = value;
                NotifyPropertyChanged(nameof(Orientation));
                Reset();
            }
        }

        /// <summary>
        /// Gets the collection with container children.
        /// </summary>
        [Content]
        public ObservableCollection<Control> Children { get; }

        IReadOnlyCollection<Control> IContainerControl.Children => Children;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets or sets the size of a single splitter thumb.
        /// </summary>
        [DefaultValue(6)]
        [Category("Appearance")]
        public int ThumbSize
        {
            get => thumbSize;
            set
            {
                if (thumbSize == value) return;
                thumbSize = value;
                NotifyPropertyChanged(nameof(ThumbSize));
                Reset();
            }
        }

        /// <summary>
        /// Occurs when one of internal proportions is changed.
        /// </summary>
        public event EventHandler? ProportionsChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SplitPane"/> class.
        /// </summary>
        public SplitPane()
        {
            Children = new ObservableCollection<Control>();
            Children.CollectionChanged += OnChildrenChanged;
            ResetChild();
        }

        /// <inheritdoc/>
        public void Add(Control control)
        {
            Children.Add(control);
        }

        /// <inheritdoc/>
        public IEnumerator<Control> GetEnumerator()
        {
            return Children.GetEnumerator();
        }

        /// <summary>
        /// Gets the proportion value of the control with given index.
        /// </summary>
        /// <param name="index">Control index to get actual proportion for.</param>
        /// <returns></returns>
        public float GetProportion(int index)
        {
            if (index < 0 || index > Children.Count) return 0;
            return Orientation == Orientation.Horizontal ?
                Child!.ColumnDefinitions[index].Width.Value :
                Child!.RowDefinitions[index].Height.Value;
        }

        /// <summary>
        /// Removes the
        /// </summary>
        /// <param name="control"></param>
        /// <returns></returns>
        public bool Remove(Control control)
        {
            return Children.Remove(control);
        }

        /// <summary>
        /// Resets the control proportions.
        /// </summary>
        public void Reset()
        {
            // Clear children
            Child.Clear();
            thumbs.Clear();
            thumbsSize = 0;
            Child.ColumnDefinitions.Clear();
            Child.RowDefinitions.Clear();

            int i = 0;
            foreach (var child in Children)
            {
                if (i > 0)
                {
                    // Add thumb
                    var thumb = new Button()
                    {
                        releaseOnTouchLeft = false,
                        StyleName = "ThumbStyle"
                    };
                    thumb.TouchDown += OnThumbTouchDown;
                    thumbsSize += ThumbSize;
                    if (Orientation == Orientation.Horizontal)
                    {
                        thumb.GridColumn = i * 2 - 1;
                        Child.ColumnDefinitions.Add(new() { Width = new(thumbSize) });
                    }
                    else
                    {
                        thumb.GridRow = i * 2 - 1;
                        Child.RowDefinitions.Add(new() { Height = new(thumbSize) });
                    }
                    Child.Add(thumb);
                    thumbs.Add(thumb);
                }
                // Give it 100 stars to allow precise part computations.
                GridLength length = i < Children.Count - 1 ?
                    new(1, GridUnitType.Star) :
                    GridLength.Auto;
                // Add a control with its grid position.
                if (Orientation == Orientation.Horizontal)
                {
                    child.GridColumn = i * 2;
                    Child.ColumnDefinitions.Add(new() { Width = length });
                }
                else
                {
                    child.GridRow = i * 2;
                    Child.RowDefinitions.Add(new() { Height = length });
                }
                Child.Add(child);
                i++;
            }

            foreach (var thumb in thumbs)
            {
                if (Orientation == Orientation.Horizontal)
                {
                    thumb.Width = ThumbSize;
                    thumb.Height = 0;
                }
                else
                {
                    thumb.Width = 0;
                    thumb.Height = ThumbSize;
                }
            }
            ProportionsChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the position of the splitter with given index.
        /// </summary>
        /// <param name="index">Index of the control thumb after which is needed.</param>
        /// <returns>Proportion value for the splitter with the index.</returns>
        public float GetSplitterPosition(int index)
        {
            GetProportions(index, out var left, out var right);
            return left.GetValueAmount() / (left.GetValueAmount() + right.GetValueAmount());
        }

        /// <summary>
        /// Sets the position of the splitter with given index.
        /// </summary>
        /// <param name="index">Index of the control thumb after which is needed.</param>
        /// <param name="proportion">New proportion value.</param>
        public void SetSplitterPosition(int index, float proportion)
        {
            GetProportions(index, out var left, out var right);
            float total = left.GetValueAmount() + right.GetValueAmount();
            float v1 = proportion * total, v2 = total - v1;
            left.SetValue(new(v1, GridUnitType.Star));
            right.SetValue(new(v2, GridUnitType.Star));
        }

        /// <inheritdoc/>
        protected internal override void OnTouchMoved()
        {
            base.OnTouchMoved();
            if (mousePosition == null || downThumb == null) return;
            if (Bounds.Width == 0) return;
            var handleIndex = Child.Children.IndexOf(downThumb);
            ProportionDefinition first, second;
            float firstValue;
            Point position = ToLocal(Desktop!.TouchPosition);
            if (Orientation == Orientation.Vertical)
            {
                int firstHeight = position.Y - mousePosition.Value;
                for (int i = 0; i < handleIndex - 1; i++)
                {
                    firstHeight -= Child.GetRowHeight(i);
                }
                firstValue = (float)firstHeight / (Bounds.Height - thumbsSize);
                first = Child.GetRowDefinition(handleIndex - 1);
                second = Child.GetRowDefinition(handleIndex + 1);
            }
            else
            {
                int firstWidth = position.X - mousePosition.Value;
                for (int i = 0; i < handleIndex - 1; i++)
                {
                    firstWidth -= Child.GetColumnWidth(i);
                }
                firstValue = (float)firstWidth / (Bounds.Width - thumbsSize);
                first = Child.GetColumnDefinition(handleIndex - 1);
                second = Child.GetColumnDefinition(handleIndex + 1);
            }

            if (firstValue >= 0 && firstValue <= 2)
            {
                float secondValue = first.GetValueAmount() + second.GetValueAmount() - firstValue;
                first.SetValue(new(firstValue, GridUnitType.Star));
                second.SetValue(new(secondValue, GridUnitType.Star));
                ProportionsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <inheritdoc/>
        protected internal override void ResetChildInternal()
        {
            Child = new();
        }

        /// <inheritdoc/>
        protected internal override void OnMouseLeft()
        {
            downThumb = null;
            mousePosition = null;
            base.OnMouseLeft();
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchUp()
        {
            downThumb = null;
            mousePosition = null;
            return base.OnTouchUp();
        }

        private void OnThumbTouchDown(object? sender, EventArgs e)
        {
            var target = (Button)sender!;
            downThumb = target;
            Point position = ToGlobal(target.Location);
            mousePosition = Orientation == Orientation.Horizontal ?
                Desktop!.TouchPosition.X - position.X :
                Desktop!.TouchPosition.Y - position.Y;
        }

        private void OnChildrenChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Reset();
        }

        private void GetProportions(int index, out ProportionDefinition left, out ProportionDefinition right)
        {
            int trueIndex = index * 2;
            if (Orientation == Orientation.Horizontal)
            {
                left = Child.GetColumnDefinition(trueIndex);
                right = Child.GetColumnDefinition(trueIndex + 2);
            }
            else
            {
                left = Child.GetRowDefinition(trueIndex);
                right = Child.GetRowDefinition(trueIndex + 2);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Children).GetEnumerator();
        }
    }
}