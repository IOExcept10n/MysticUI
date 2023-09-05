using MysticUI.Brushes.TextureBrushes;
using MysticUI.Extensions;
using Stride.Graphics;
using Stride.Input;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a base class for all item storage-oriented controls. It provides basic functionality for items managing, loading etc.
    /// </summary>
    public class ItemsControl : SingleItemControlBase<ScrollViewer>
    {
        private readonly HashSet<int> selectedIndices = new();
        private readonly HashSet<ButtonBase> buttons = new();
        private bool itemsDirty = true;
        private int selectedIndex = -1;
        private IEnumerable? itemsSource;
        private ControlTemplate? itemTemplate;
        private Panel itemsPanel = null!;
        private SelectionMode selectionMode;
        private bool updatingFromSource;

        /// <summary>
        /// Gets the current selected item.
        /// </summary>
        /// <value>
        /// <see cref="ItemsSource"/> item for the <see cref="SelectedIndex"/> or <see cref="Items"/> control for the index or <see langword="null"/> if there are no selected items.
        /// </value>
        public object? SelectedItem => GetItem(SelectedIndex);

        /// <summary>
        /// Gets or sets the ordinal index of the current selected item.
        /// </summary>
        [Category("Behavior")]
        public int SelectedIndex
        {
            get => selectedIndex;
            set
            {
                if (selectedIndex == value) return;
                selectedIndex = value;
                selectedIndices.Clear();
                if (value != -1) selectedIndices.Add(value);
                OnSelectedItemChanged();
                NotifyPropertyChanged(nameof(ItemsSource));
            }
        }

        /// <summary>
        /// Gets all selected indices.
        /// </summary>
        public IEnumerable<int> SelectedIndices => selectedIndices;

        /// <summary>
        /// Gets or sets the collection that is used to create contents of the <see cref="ItemsControl"/>.
        /// </summary>
        [Category("Behavior")]
        [BindingOverride(BindingMode.OneWay)]
        public IEnumerable? ItemsSource
        {
            get => itemsSource;
            set
            {
                if (itemsSource == value) return;
                if (itemsSource is INotifyCollectionChanged notify)
                {
                    notify.CollectionChanged -= ItemsSourceUpdate;
                }
                itemsSource = value;
                OnItemsSourceChanged();
                NotifyPropertyChanged(nameof(ItemsSource));
            }
        }

        /// <summary>
        /// Gets the collection with all content controls.
        /// </summary>
        [Content]
        public ObservableCollection<Control> Items { get; } = new();

        /// <summary>
        /// Gets or sets the template that is applied to the items in the <see cref="ItemsControl"/> to get controls from them.
        /// </summary>
        /// <remarks>
        /// If not set, all items will be transformed using common control wrapping algorithm.
        /// </remarks>
        [Category("Behavior")]
        public ControlTemplate? ItemTemplate
        {
            get => itemTemplate;
            set
            {
                if (itemTemplate == value) return;
                itemTemplate = value;
                InvalidateArrange();
                ItemTemplateChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(ItemTemplate));
            }
        }

        /// <summary>
        /// Gets or sets the panel to keep all content of the <see cref="ItemsControl"/>.
        /// </summary>
        /// <remarks>
        /// If not set, new <see cref="StackPanel"/> will be generated.
        /// </remarks>
        [Category("Layout")]
        public Panel ItemsPanel
        {
            get => itemsPanel;
            set
            {
                if (itemsPanel == value) return;
                itemsPanel = value;
                OnPanelChanged();
                NotifyPropertyChanged(nameof(ItemsPanel));
            }
        }

        /// <summary>
        /// Gets the value that indicates if the control doesn't have any items.
        /// </summary>
        public bool HasItems => Items.Any();

        /// <summary>
        /// Gets or sets the value that defines how to select items in the control.
        /// </summary>
        [Category("Behavior")]
        public SelectionMode SelectionMode
        {
            get => selectionMode;
            set
            {
                selectionMode = value;
                SelectedIndex = -1;
            }
        }

        /// <inheritdoc/>
        public override Desktop? Desktop
        {
            get => base.Desktop;
            set
            {
                Child.Desktop = base.Desktop = value;
            }
        }

        /// <summary>
        /// Gets or sets the internal <see cref="ScrollViewer"/> vertical scrollbar visibility.
        /// </summary>
        [Category("Behavior")]
        public ScrollbarVisibility ContentVerticalScrollBarVisibility
        {
            get => Child.VerticalScrollbarVisibility;
            set => Child.VerticalScrollbarVisibility = value;
        }

        /// <summary>
        /// Gets or sets the internal <see cref="ScrollViewer"/> horizontal scrollbar visibility.
        /// </summary>
        [Category("Behavior")]
        public ScrollbarVisibility ContentHorizontalScrollBarVisibility
        {
            get => Child.HorizontalScrollbarVisibility;
            set => Child.HorizontalScrollbarVisibility = value;
        }

        /// <summary>
        /// Occurs when the value of the <see cref="ItemsSource"/> property changes.
        /// </summary>
        public event EventHandler? ItemsSourceChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="SelectedItem"/> property changes.
        /// </summary>
        public event EventHandler? SelectedItemChanged;

        /// <summary>
        /// Occurs when any of multiple selected items changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Occurs when the value of the <see cref="ItemTemplate"/> property changes.
        /// </summary>
        public event EventHandler? ItemTemplateChanged;

        /// <summary>
        /// This event is used the handle all items in the <see cref="ItemsSource"/> to filter out before creating controls for them.
        /// </summary>
        public event EventHandler<CancellableEventArgs<FilteredItem>>? ItemsSourceFilter;

        /// <summary>
        /// Occurs when the button inside the <see cref="ItemsControl"/> is clicked.
        /// </summary>
        public event EventHandler<GenericEventArgs<ButtonClickContext>>? ButtonClick;

        /// <summary>
        /// Creates a new instance of the <see cref="ItemsControl"/> class.
        /// </summary>
        public ItemsControl()
        {
            ResetChildInternal();
            Items.CollectionChanged += ItemsChanged;
        }

        /// <summary>
        /// Selects the item at the given index.
        /// </summary>
        /// <param name="index">An index to select the item.</param>
        /// <param name="value">The value that determines whether to set or unset the item at the index.</param>
        public void SelectIndex(int index, bool value = true)
        {
            if (Desktop == null) return;
            if (SelectionMode == SelectionMode.None) SelectedIndex = -1;
            if (value)
            {
                if (SelectionMode == SelectionMode.Multiple || (SelectionMode == SelectionMode.Extended && Desktop.IsCtrlPressed))
                {
                    if (!selectedIndices.Any()) SelectedIndex = index;
                    else selectedIndices.Add(index);
                }
                else SelectedIndex = index;
            }
            else
            {
                if (SelectionMode == SelectionMode.Multiple || (SelectionMode == SelectionMode.Extended && Desktop.IsCtrlPressed))
                {
                    selectedIndices.Remove(index);
                    if (!selectedIndices.Any()) SelectedIndex = -1;
                }
                else if (SelectedIndex == index) SelectedIndex = -1;
            }
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Gets the item at given index.
        /// </summary>
        /// <param name="index">Index to get item at.</param>
        /// <returns>An object at the index or <see langword="null"/> if unavailable.</returns>
        public virtual object? GetItem(int index)
        {
            if (index < 0 || index >= Items.Count) return null;
            if (ItemsSource != null)
            {
                return ItemsSource.Cast<object>().ElementAt(index);
            }
            else return Items[index];
        }

        /// <summary>
        /// Invalidate list of items in the control and makes the <see cref="ItemsControl"/> to reset all controls inside it according to new <see cref="ItemsSource"/> values.
        /// </summary>
        public void InvalidateItems()
        {
            itemsDirty = true;
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            if (itemsDirty)
            {
                ResetItems();
            }
            ResetPanel();
            Child.Arrange(ActualBounds);
            base.ArrangeInternal();
        }

        /// <summary>
        /// Creates a panel if any values for the <see cref="ItemsPanel"/> property are set.
        /// </summary>
        /// <returns></returns>
        protected virtual Panel CreateDefaultPanel() => new StackPanel() { IsBoundless = true };

        /// <summary>
        /// Places the control at the internal panel.
        /// </summary>
        /// <param name="item">Control to set.</param>
        protected virtual void PlaceItemControl(Control item)
        {
            ItemsPanel.Children.Add(item);
        }

        /// <summary>
        /// Handles the change of the <see cref="ItemsSource"/> property value.
        /// </summary>
        protected internal virtual void OnItemsSourceChanged()
        {
            if (ItemsSource is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += ItemsSourceUpdate;
            }
            InvalidateItems();
            ItemsSourceChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles the <see cref="SelectedIndex"/> property update.
        /// </summary>
        protected internal virtual void OnSelectedItemChanged()
        {
            SelectedItemChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles the <see cref="SelectedIndices"/> property update.
        /// </summary>
        protected internal virtual void OnSelectionChanged()
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Handles the <see cref="ItemsPanel"/> property update.
        /// </summary>
        protected internal virtual void OnPanelChanged()
        {
            Child.Content = ItemsPanel;
            InvalidateArrange();
        }

        /// <summary>
        /// Handles the <see cref="Items"/> collection update.
        /// </summary>
        /// <param name="sender"><see cref="Items"/> property.</param>
        /// <param name="e">Event arguments for collection update.</param>
        /// <exception cref="NotSupportedException">Occurs when the collection can't be changed.</exception>
        protected internal virtual void ItemsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!updatingFromSource && ItemsSource != null) throw new NotSupportedException("Items property of the ItemsControl can't be modified when the ItemsSource is set.");
            if (e.OldItems != null)
            {
                foreach (Control control in e.OldItems)
                {
                    control.TouchDown -= SelectItem;
                    control.ProcessControls(x =>
                    {
                        if (buttons.Contains(x))
                        {
                            var button = (ButtonBase)x;
                            button.Click -= ButtonClickHandler!;
                            buttons.Remove(button);
                        }
                        return true;
                    });
                }
            }
            InvalidateArrange();
        }

        /// <summary>
        /// Sets the <see cref="Items"/> contents.
        /// </summary>
        protected internal virtual void ResetItems()
        {
            buttons.Clear();
            if (!HasItems && ItemsSource != null)
            {
                CancellableEventArgs<FilteredItem> itemFilter = new();
                int i = 0;
                updatingFromSource = true;
                foreach (var item in ItemsSource)
                {
                    itemFilter.Data = new(item, i++);
                    itemFilter.Cancel = false;
                    ItemsSourceFilter?.Invoke(this, itemFilter);
                    if (itemFilter.Cancel) continue;
                    Control control = WrapControl(item);
                    control.TouchDown += SelectItem;
                    Items.Add(control);
                }
                updatingFromSource = false;
            }
        }

        private void SelectItem(object? sender, EventArgs e)
        {
            SelectIndex(Items.IndexOf((Control)sender!));
        }

        /// <summary>
        /// Resets the panel contents.
        /// </summary>
        protected internal virtual void ResetPanel()
        {
            ItemsPanel ??= CreateDefaultPanel();
            Child.Content = ItemsPanel;
            ItemsPanel.Children.Clear();
            foreach (var control in Items)
            {
                PlaceItemControl(control);
            }
        }

        /// <inheritdoc/>
        protected internal override void ResetChildInternal()
        {
            Child = new()
            {
                Parent = this,
            };
        }

        private void ItemsSourceUpdate(object? sender, NotifyCollectionChangedEventArgs e)
        {
            updatingFromSource = true;
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    {
                        CancellableEventArgs<FilteredItem> itemFilter = new();
                        int i = e.NewStartingIndex, j = e.NewStartingIndex;
                        foreach (var item in e.NewItems!)
                        {
                            itemFilter.Data = new(item, i++);
                            itemFilter.Cancel = false;
                            ItemsSourceFilter?.Invoke(this, itemFilter);
                            if (itemFilter.Cancel) continue;
                            Control control = WrapControl(item);
                            control.TouchDown += SelectItem;
                            Items.Insert(j++, control);
                        }
                        break;
                    }
                case NotifyCollectionChangedAction.Remove:
                    {
                        int i = e.OldStartingIndex;
                        foreach (var item in e.OldItems!)
                        {
                            Items.RemoveAt(i++);
                        }
                        break;
                    }
                default:
                    {
                        Items.Clear();
                        InvalidateItems();
                        break;
                    }
            }
            updatingFromSource = false;
        }

        private void ButtonClickHandler(object sender, GenericEventArgs<MouseInfo> e)
        {
            ButtonClick?.Invoke(this, new ButtonClickContext((ButtonBase)sender, e.Data));
        }

        private Control WrapControl(object content)
        {
            Control child;
            if (content == null)
            {
                child = new();
            }
            else if (content is Control control)
            {
                child = control;
            }
            else if (ItemTemplate != null)
            {
                child = (Control)ItemTemplate.Instantiate(content);
            }
            else if (content is Texture texture)
            {
                var image = new ImageBrush(texture);
                child = new Image(image);
            }
            else if (content is IImage image)
            {
                child = new Image(image);
            }
            else
            {
                child = new TextBlock(content.ToString(), Font);
            }
            child.ProcessControls(x =>
            {
                if (x is ButtonBase button)
                {
                    button.Click += ButtonClickHandler!;
                    buttons.Add(button);
                }
                return true;
            });
            return child;
        }

        /// <summary>
        /// Represents a structure to filter out items of the <see cref="ItemsControl"/>.
        /// </summary>
        public readonly struct FilteredItem
        {
            /// <summary>
            /// An item to filter.
            /// </summary>
            public readonly object Item;

            /// <summary>
            /// An index of the item.
            /// </summary>
            public readonly int Index;

            /// <summary>
            /// Create a new <see cref="FilteredItem"/>.
            /// </summary>
            /// <param name="item">Item to filter.</param>
            /// <param name="index">Index in the source collection.</param>
            public FilteredItem(object item, int index)
            {
                Item = item;
                Index = index;
            }
        }

        /// <summary>
        /// Represents a structure for the buttons clicking handling.
        /// </summary>
        public readonly struct ButtonClickContext
        {
            /// <summary>
            /// Original button that was clicked to raise an event.
            /// </summary>
            public readonly ButtonBase OriginalSource;

            /// <summary>
            /// Info about mouse when clicked.
            /// </summary>
            public readonly MouseInfo ClickInfo;

            /// <summary>
            /// Creates a new <see cref="ButtonClickContext"/>.
            /// </summary>
            /// <param name="originalSource">Clicked button.</param>
            /// <param name="clickInfo">Info about mouse.</param>
            public ButtonClickContext(ButtonBase originalSource, MouseInfo clickInfo)
            {
                OriginalSource = originalSource;
                ClickInfo = clickInfo;
            }
        }
    }

    /// <summary>
    /// Defines a mode of items selection in the <see cref="ItemsControl"/>.
    /// </summary>
    public enum SelectionMode
    {
        /// <summary>
        /// Don't select items at all.
        /// </summary>
        None,

        /// <summary>
        /// Select only one item.
        /// </summary>
        Single,

        /// <summary>
        /// Select multiple items. Use <see cref="ItemsControl.SelectIndex(int, bool)"/> to select more than one item.
        /// </summary>
        Multiple,

        /// <summary>
        /// Select one item as default, but if press <see cref="Keys.LeftCtrl"/> or <see cref="Keys.RightCtrl"/>, multiple selection is allowed.
        /// </summary>
        Extended
    }
}