using MysticUI.Extensions;
using Stride.Core.Mathematics;
using Stride.Input;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a control with the ability to select an item from the set of values.
    /// </summary>
    public class Selector : SingleItemControlBase<ToggleButton>, IContainerControl
    {
        private readonly ListBox listBox = new();

        /// <summary>
        /// Gets or sets the maximal height of the dropdown menu.
        /// </summary>
        [DefaultValue(300)]
        [Category("Layout")]
        public int MaxDropdownHeight
        {
            get => listBox.MaxHeight;
            set => listBox.MaxHeight = value;
        }

        /// <summary>
        /// Gets the value that determines whether the menu is expanded.
        /// </summary>
        public bool IsExpanded => Child.IsChecked == true;

        /// <summary>
        /// Gets or sets the default text for the selection window if no items are selected.
        /// </summary>
        [DefaultValue("")]
        public string DefaultText { get; set; } = string.Empty;

        /// <inheritdoc/>
        public override Desktop? Desktop
        {
            get => base.Desktop;
            set
            {
                if (base.Desktop != null)
                {
                    base.Desktop.ContextMenuClosed -= Desktop_ContextMenuClosed;
                }
                base.Desktop = value;
                if (base.Desktop != null)
                {
                    base.Desktop.ContextMenuClosed += Desktop_ContextMenuClosed;
                }
            }
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<Control> Children => listBox.Items;

        /// <summary>
        /// Gets the items of the <see cref="Selector"/>.
        /// </summary>
        public ObservableCollection<Control> Items => listBox.Items;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <summary>
        /// Gets the <see cref="Controls.ListBox"/> with the items for the <see cref="Selector"/> instance.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        public ListBox ListBox => listBox;

        /// <summary>
        /// Gets the selected element of the <see cref="ListBox"/>.
        /// </summary>
        public Control SelectedItem => listBox.Items[listBox.SelectedIndex];

        /// <summary>
        /// Gets or sets the selected index.
        /// </summary>
        public int SelectedIndex
        {
            get => listBox.SelectedIndex;
            set => listBox.SelectedIndex = value;
        }

        /// <summary>
        /// Gets or sets the selection mode.
        /// </summary>
        public SelectionMode SelectionMode
        {
            get => listBox.SelectionMode;
            set => listBox.SelectionMode = value;
        }

        /// <summary>
        /// Occurs when the selection of the control is changed.
        /// </summary>
        public event EventHandler? SelectedIndexChanged
        {
            add => listBox.SelectedItemChanged += value;
            remove => listBox.SelectedItemChanged -= value;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Selector"/> class.
        /// </summary>
        public Selector()
        {
            AcceptFocus = true;
            ResetChild();
            listBox.SelectionChanged += ListBox_SelectionChanged;
            listBox.Items.CollectionChanged += ListBoxItems_CollectionChanged;
            listBox.ButtonClick += ListBox_ButtonClick;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            MaxDropdownHeight = 300;
        }

        /// <inheritdoc/>
        public void Add(Control control)
        {
            Items.Add(control);
        }

        /// <inheritdoc/>
        public IEnumerator<Control> GetEnumerator()
        {
            return Items.GetEnumerator();
        }

        /// <inheritdoc/>
        public bool Remove(Control control)
        {
            return Items.Remove(control);
        }

        /// <inheritdoc/>
        protected internal override void ResetChildInternal()
        {
            Child = new ToggleButton().WithDefaultStyle();
            Child.CheckedChanged += Child_CheckedChanged;
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            Point result = base.MeasureInternal(availableSize);

            int oldWidth = listBox.Width;
            listBox.Width = 0;
            bool oldVisibility = listBox.Visible;

            Point listResult = listBox.TestMeasure();
            if (listResult.X > result.X)
            {
                result.X = listResult.X;
            }
            listBox.Width = oldWidth;
            listBox.Visible = oldVisibility;
            result.X += 32;
            return result;
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            base.ArrangeInternal();
            listBox.Width = BorderBounds.Width;
        }

        /// <inheritdoc/>
        protected internal override void OnKeyDown(Keys key)
        {
            base.OnKeyDown(key);
            listBox.OnKeyDown(key);
        }

        internal void HideDropdown()
        {
            if (Desktop?.ContextMenu == listBox)
            {
                Desktop.HideContextMenu();
            }
        }

        internal void UpdateSelectedItem()
        {
            string? text = null;
            Color color = ForegroundColor;
            if (SelectedItem is ContentControl boxItem)
            {
                text = boxItem.Content?.ToString();
                if (boxItem.ForegroundColor != default)
                    color = boxItem.ForegroundColor;
                if (SelectedItem is ToggleButton toggle) toggle.IsPressed = true;
                else if (SelectedItem is ListBoxItem listBoxItem) listBoxItem.IsSelected = true;
            }
            else if (SelectedItem is TextBlock textBlock)
            {
                text = textBlock.Text;
                if (textBlock.ForegroundColor != default)
                    color = textBlock.ForegroundColor;
            }
            Child.Content = text ?? DefaultText;
            Child.ForegroundColor = color;
        }

        private void Child_CheckedChanged(object? sender, EventArgs e)
        {
            if (listBox.Items.Count == 0 || Desktop == null) return;
            if (IsExpanded)
            {
                if (listBox.SelectedIndex == -1) listBox.SelectedIndex = 0;
                listBox.Width = BorderBounds.Width;
                var position = ToGlobal(new(0, Height));
                Desktop.ShowContextMenu(listBox, position);
            }
        }

        private void ListBox_ButtonClick(object? sender, Extensions.GenericEventArgs<ItemsControl.ButtonClickContext> e)
        {
            HideDropdown();
        }

        private void ListBoxItems_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    {
                        foreach (Control item in e.NewItems!)
                        {
                            item.PropertyChanged += Item_PropertyChanged;
                        }
                        break;
                    }
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    {
                        foreach (Control item in e.OldItems!)
                        {
                            item.PropertyChanged -= Item_PropertyChanged;
                        }
                        UpdateSelectedItem();
                        break;
                    }
                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    {
                        UpdateSelectedItem();
                        break;
                    }
            }
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender == SelectedItem)
            {
                UpdateSelectedItem();
            }
            InvalidateMeasure();
        }

        private void ListBox_SelectionChanged(object? sender, EventArgs e)
        {
            UpdateSelectedItem();
        }

        private void Desktop_ContextMenuClosed(object? sender, Extensions.GenericEventArgs<Control> e)
        {
            Child.IsChecked = false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Items).GetEnumerator();
        }
    }
}