using FontStashSharp.RichText;
using MysticUI.Extensions;
using Stride.Core.Mathematics;
using Stride.Input;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents an item for the menu.
    /// </summary>
    public interface IMenuItem : INameIdentifiable
    {
        /// <summary>
        /// Item owner menu.
        /// </summary>
        public Menu Menu { get; set; }

        /// <summary>
        /// Character to call item quickly.
        /// </summary>
        public char UnderscoreChar { get; }

        /// <summary>
        /// Index of the item.
        /// </summary>
        public int Index { get; set; }
    }

    /// <summary>
    /// Represents the menu control.
    /// </summary>
    public class Menu : SingleItemControlBase<Grid>
    {
        private const string MenuLabelStyleName = "MenuLabelStyle";
        private const string MenuIconStyleName = "MenuIconStyle";
        private const string MenuShortcutStyleName = "MenuShortcutStyle";
        private const string MenuSeparatorStyleName = "MenuSeparatorStyle";

        private readonly ColumnDefinition imageProportion = ColumnDefinition.Default;
        private readonly ColumnDefinition shortcutDefinition = ColumnDefinition.Default;

        private bool internalSetSelectedIndex;
        private bool dirty;

        internal MenuItem? OpenedItem { get; private set; }

        private bool HasImage
        {
            get
            {
                if (Orientation == Orientation.Horizontal) return false;
                return Child.ColumnDefinitions[0] == imageProportion;
            }
            set
            {
                if (Orientation == Orientation.Horizontal) return;
                bool oldValue = HasImage;
                if (oldValue == value) return;
                if (oldValue && !value)
                {
                    Child.ColumnDefinitions.RemoveAt(0);
                }
                else if (!oldValue && value)
                {
                    Child.ColumnDefinitions.Insert(0, imageProportion);
                }
                dirty = true;
            }
        }

        private bool HasShortcut
        {
            get
            {
                if (Orientation == Orientation.Horizontal) return false;
                return Child.ColumnDefinitions[^1] == shortcutDefinition;
            }
            set
            {
                if (Orientation == Orientation.Horizontal) return;
                bool oldValue = HasShortcut;
                if (oldValue == value) return;
                if (oldValue && !value) Child.ColumnDefinitions.RemoveAt(Child.ColumnDefinitions.Count - 1);
                else Child.ColumnDefinitions.Add(shortcutDefinition);
                dirty = true;
            }
        }

        /// <summary>
        /// Gets or sets the orientation of the menu.
        /// </summary>
        public Orientation Orientation { get; set; }

        /// <summary>
        /// Determines whether the menu is open.
        /// </summary>
        [Browsable(false)]
        [XmlIgnore]
        public bool IsOpen => OpenedItem != null;

        /// <summary>
        /// Gets the items of the menu.
        /// </summary>
        [Browsable(false)]
        [Content]
        public ObservableCollection<IMenuItem> Items { get; } = new();

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

        /// <summary>
        /// Gets the index of the hovered item.
        /// </summary>
        public int HoverIndex
        {
            get => Orientation == Orientation.Vertical ? Child.HoverRowIndex : Child.HoverColumnIndex;
            set
            {
                if (Orientation == Orientation.Vertical)
                {
                    Child.HoverRowIndex = value;
                }
                else
                {
                    Child.HoverColumnIndex = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the index of the selected item.
        /// </summary>
        public int SelectedIndex
        {
            get => Orientation == Orientation.Vertical ? Child.SelectedRowIndex : Child.SelectedColumnIndex;
            set
            {
                if (Orientation == Orientation.Vertical)
                {
                    Child.SelectedRowIndex = value;
                }
                else
                {
                    Child.SelectedColumnIndex = value;
                }
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="Menu"/> class.
        /// </summary>
        public Menu()
        {
            Items.CollectionChanged += Items_CollectionChanged;
            AcceptFocus = true;
            ResetChild();
            OnOrientationSet();
            Child.HoverIndexChanged += Child_HoverIndexChanged;
            Child.SelectedIndexChanged += Child_SelectedIndexChanged;
            Child.TouchUp += Child_TouchUp;
            OpenedItem = null;
        }

        /// <summary>
        /// Finds the menu item by its name.
        /// </summary>
        /// <param name="name">Name of the menu item.</param>
        public MenuItem? FindMenuItemByName(string name)
        {
            foreach (var item in Items)
            {
                MenuItem? result = (item as MenuItem)?.FindMenuItemByName(name);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Closes the menu.
        /// </summary>
        public void Close()
        {
            if (Desktop != null && Desktop.ContextMenu == this) Desktop.HideContextMenu();
            HoverIndex = SelectedIndex = -1;
        }

        /// <summary>
        /// Handles the mouse hover.
        /// </summary>
        /// <param name="delta"></param>
        public void MouseHover(int delta)
        {
            if (Items.Count == 0) return;
            // First step - get the index of the currently selected item.
            int selectedIndex = SelectedIndex;
            if (selectedIndex == -1)
            {
                selectedIndex = HoverIndex;
            }
            int hoverIndex = selectedIndex;
            for (int i = 0; i <= Items.Count; i++)
            {
                if (i > Items.Count) return;
                hoverIndex = (Items.Count + hoverIndex + delta) % Items.Count;
                if (Items[hoverIndex] is MenuItem item)
                {
                    HoverIndex = item.Index;
                    return;
                }
            }
        }

        /// <inheritdoc/>
        protected internal override void ResetChildInternal()
        {
            Child = new();
        }

        /// <inheritdoc/>
        protected internal override void OnKeyDown(Keys key)
        {
            base.OnKeyDown(key);
            if (key == Keys.Enter || key == Keys.Space)
            {
                int selectedIndex = HoverIndex;
                var item = GetMenuItem(selectedIndex);
                if (item != null && !item.CanOpen)
                {
                    Click(selectedIndex);
                    return;
                }
            }

            // Simple keys transformation into their char representation:
            string keyName = key.ToString();
            if (keyName.StartsWith('D') && keyName.Length == 2) keyName = keyName[1].ToString();
            if (keyName.Length == 1)
            {
                char c = char.ToLower(keyName[0]);
                foreach (var item in Items)
                {
                    if (item is MenuItem menuItem && menuItem.UnderscoreChar == c)
                    {
                        Click(menuItem.Index);
                        return;
                    }
                }
            }

            OpenedItem?.Submenu.OnKeyDown(key);
        }

        /// <inheritdoc/>
        protected internal override Point MeasureInternal(Point availableSize)
        {
            UpdateGrid();
            return base.MeasureInternal(availableSize);
        }

        private void UpdateGrid()
        {
            if (!dirty) return;
            int index = 0;
            bool hasImage = this.HasImage,
                hasShortcut = this.HasShortcut;
            int separatorSpan = 1;
            if (hasImage)
            {
                separatorSpan = 2;
            }
            if (hasShortcut)
            {
                separatorSpan++;
            }
            foreach (var item in Items)
            {
                if (item is MenuItem menuItem)
                {
                    if (Orientation == Orientation.Horizontal)
                    {
                        menuItem.Label.GridColumn = index;
                        menuItem.Label.GridRow = index;
                    }
                    else
                    {
                        int columnIndex = 0;
                        if (hasImage)
                        {
                            menuItem.ImageControl.GridColumn = columnIndex++;
                            menuItem.ImageControl.GridRow = index;
                        }
                        menuItem.Label.GridColumn = columnIndex++;
                        menuItem.Label.GridRow = index;
                        if (hasShortcut)
                        {
                            menuItem.Shortcut.GridColumn = columnIndex++;
                            menuItem.Shortcut.GridRow = index;
                        }
                    }
                }
                else if (item is MenuSeparator menuSeparator)
                {
                    if (Orientation == Orientation.Horizontal)
                    {
                        menuSeparator.Separator.GridColumn = index;
                        menuSeparator.Separator.GridRow = 0;
                    }
                    else
                    {
                        menuSeparator.Separator.GridColumn = 0;
                        menuSeparator.Separator.GridColumnSpan = separatorSpan;
                        menuSeparator.Separator.GridRow = 0;
                    }
                }
                item.Index = index++;
            }
            dirty = false;
        }

        private Rectangle GetItemBounds(int index)
        {
            var bounds = Child.Bounds;
            if (Orientation == Orientation.Horizontal)
            {
                return new(bounds.X + Child.GetCellLocationX(index), bounds.Y, Child.GetColumnWidth(index), bounds.Height);
            }
            else
            {
                return new(bounds.X, bounds.Y + Child.GetCellLocationY(index), bounds.Width, Child.GetRowHeight(index));
            }
        }

        private void UpdateControls()
        {
            bool hasImage = false, hasShortcut = false;
            foreach (var item in Items.OfType<MenuItem>())
            {
                if (item.Image != null) hasImage = true;
                if (!string.IsNullOrEmpty(item.ShortcutText)) hasShortcut = true;
            }
            this.HasImage = hasImage;
            this.HasShortcut = hasShortcut;
        }

        private void SetMenuItem(MenuItem item)
        {
            item.ImageControl.Foreground = item.Image;
            if (item.Image != null && !Child.Children.Contains(item.ImageControl))
            {
                Child.Children.Add(item.ImageControl);
            }
            else if (item.Image == null && Child.Children.Contains(item.ImageControl))
            {
                Child.Children.Remove(item.ImageControl);
            }
            item.Shortcut.Text = item.ShortcutText;
            if (item.ShortcutColor != default)
            {
                item.Shortcut.ForegroundColor = item.ShortcutColor;
            }
            else if (ForegroundColor != default)
            {
                item.Shortcut.ForegroundColor = ForegroundColor;
            }

            if (!string.IsNullOrEmpty(item.ShortcutText) && !Child.Children.Contains(item.Shortcut))
            {
                Child.Children.Add(item.Shortcut);
            }
            else if (string.IsNullOrEmpty(item.ShortcutText) && Child.Children.Contains(item.Shortcut))
            {
                Child.Children.Remove(item.Shortcut);
            }

            item.Label.Text = item.DisplayText;
            if (item.Color != default)
            {
                item.Label.ForegroundColor = item.Color;
            }
            else if (ForegroundColor != default)
            {
                item.Label.ForegroundColor = ForegroundColor;
            }
            UpdateControls();
        }

        private void InsertItem(IMenuItem item)
        {
            item.Menu = this;
            if (item is MenuItem menuItem)
            {
                menuItem.Updated += MenuItem_Updated!;
                menuItem.Label.SetStyle(MenuLabelStyleName);
                if (Orientation == Orientation.Vertical)
                {
                    menuItem.ImageControl.SetStyle(MenuIconStyleName);
                    menuItem.Shortcut.SetStyle(MenuShortcutStyleName);
                }
                // Add only label as other controls may be optionally added by SetMenuItem
                Child.Children.Add(menuItem.Label);
                SetMenuItem(menuItem);
            }
            else if (item is MenuSeparator menuSeparator)
            {
                Thumb separator = new() { Orientation = Orientation };
                Child.Children.Add(separator);
                separator.SetStyle(MenuSeparatorStyleName);
                menuSeparator.Separator = separator;
            }
        }

        private void RemoveItem(IMenuItem item)
        {
            if (item is MenuItem menuItem)
            {
                menuItem.Updated -= MenuItem_Updated!;
                Child.Children.Remove(menuItem.Label);
                Child.Children.Remove(menuItem.ImageControl);
                Child.Children.Remove(menuItem.Shortcut);
            }
            else if (item is MenuSeparator separator)
            {
                Child.Children.Remove(separator.Separator);
            }
        }

        private void OnOrientationSet()
        {
            if (Orientation == Orientation.Vertical)
            {
                Child.SelectionMode = Grid.GridSelectionMode.Row;
            }
            else
            {
                Child.SelectionMode = Grid.GridSelectionMode.Column;
            }
        }

        private void Items_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            switch (e.Action)
            {
                case System.Collections.Specialized.NotifyCollectionChangedAction.Add:
                    foreach (IMenuItem item in e.NewItems!)
                    {
                        InsertItem(item);
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Remove:
                    foreach (IMenuItem item in e.OldItems!)
                    {
                        RemoveItem(item);
                    }
                    break;
                case System.Collections.Specialized.NotifyCollectionChangedAction.Reset:
                    Child.Clear();
                    break;
            }
            dirty = true;
        }

        private void MenuItem_Updated(object? sender, EventArgs e)
        {
            SetMenuItem((MenuItem)sender!);
        }

        private void Desktop_ContextMenuClosed(object? sender, Extensions.GenericEventArgs<Control> e)
        {
            OpenedItem = null;
            if (!internalSetSelectedIndex) SelectedIndex = HoverIndex = -1;
        }

        private void Child_TouchUp(object? sender, EventArgs e)
        {
            if (Items[SelectedIndex] is MenuItem menuItem && !menuItem.CanOpen)
            {
                Close();
                menuItem.OnSelected();
            }
        }

        private MenuItem? GetMenuItem(int index)
        {
            if (index == -1) return null;
            return Items[index] as MenuItem;
        }

        private void Click(int index)
        {
            var item = GetMenuItem(index);
            if (item != null)
            {
                if (item.CanOpen)
                {
                    SelectedIndex = HoverIndex = index;
                }
                else
                {
                    Close();
                    item.OnSelected();
                }
            }
        }

        private void Child_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (OpenedItem != null)
            {
                try
                {
                    internalSetSelectedIndex = true;
                    if (Desktop!.ContextMenu != this)
                    {
                        Desktop.HideContextMenu();
                    }
                }
                finally
                {
                    internalSetSelectedIndex = false;
                }
            }
            if (Items[SelectedIndex] is MenuItem menuItem && menuItem.CanOpen)
            {
                ShowSubMenu(menuItem);
            }
        }

        private void Child_HoverIndexChanged(object? sender, EventArgs e)
        {
            var menuItem = GetMenuItem(HoverIndex);
            if (menuItem == null)
            {
                HoverIndex = -1;
                return;
            }
            if (!IsOpen || Desktop == null) return;
            if (Desktop.ContextMenu != this && menuItem.CanOpen && OpenedItem != menuItem)
            {
                SelectedIndex = HoverIndex;
            }
        }

        private void ShowSubMenu(MenuItem item)
        {
            var bounds = GetItemBounds(item.Index);
            Point position = Orientation == Orientation.Horizontal ? new(bounds.X, bounds.Bottom) : new(bounds.Right, bounds.Y);
            position = ToGlobal(position);
            Desktop!.ShowContextMenu(item.Submenu, position);
            OpenedItem = item;
        }
    }

    /// <summary>
    /// Represents the item of the menu.
    /// </summary>
    public class MenuItem : UIElement, IMenuItem
    {
        internal readonly Image ImageControl = new()
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        internal readonly TextBlock Label = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
        };

        internal readonly TextBlock Shortcut = new()
        {
            VerticalAlignment = VerticalAlignment.Center,
        };

        internal readonly Menu Submenu = new();

        private string? text;
        private bool displayTextDirty;
        private string? displayText;
        private string? displayTextDisabled;
        private Color color;
        private IImage? image;
        private string? shortcutText;
        private Color shortcutColor;

        /// <summary>
        /// Text of the item.
        /// </summary>
        public string? Text
        {
            get => text;
            set
            {
                if (value == text) return;
                text = value;
                displayTextDirty = true;
                UnderscoreChar = default;
                if (value != null)
                {
                    int index = value.IndexOf('&');
                    if (index >= 0 && index + 1 < value.Length)
                    {
                        UnderscoreChar = char.ToLower(value[index + 1]);
                    }
                }
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }

        internal string? DisplayText
        {
            get
            {
                UpdateDisplayText();
                return displayText;
            }
        }

        internal string? DisplayTextDisabled
        {
            get
            {
                UpdateDisplayText();
                return displayTextDisabled;
            }
        }

        /// <summary>
        /// Color of the item text.
        /// </summary>
        public Color Color
        {
            get => Color;
            set
            {
                if (value == color) return;
                color = value;
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Tag of the item.
        /// </summary>
        public object? Tag { get; set; }

        /// <summary>
        /// Image of the item.
        /// </summary>
        public IImage? Image
        {
            get => image;
            set
            {
                if (value == image) return;
                image = value;
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Text for the shortcut.
        /// </summary>
        public string? ShortcutText
        {
            get => shortcutText;
            set
            {
                if (value == shortcutText) return;
                shortcutText = value;
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Color for the shortcut text.
        /// </summary>
        public Color ShortcutColor
        {
            get => shortcutColor;
            set
            {
                if (value == shortcutColor) return;
                shortcutColor = value;
                Updated?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Owner menu for the item.
        /// </summary>
        public Menu Menu { get; set; } = null!;

        /// <summary>
        /// Other items stored in the item.
        /// </summary>
        public ObservableCollection<IMenuItem> Items => Submenu.Items;

        /// <summary>
        /// Determines whether the item is enabled.
        /// </summary>
        public bool Enabled
        {
            get => ImageControl.Enabled;
            set => ImageControl.Enabled = value;
        }

        /// <inheritdoc/>
        public char UnderscoreChar { get; private set; }

        /// <summary>
        /// Determines whether the item can be opened.
        /// </summary>
        public bool CanOpen => Items.Count > 0;

        /// <inheritdoc/>
        public int Index { get; set; }

        /// <summary>
        /// Occurs when the item is selected.
        /// </summary>
        public event EventHandler? Selected;

        /// <summary>
        /// Occurs when the item was updated.
        /// </summary>
        public event EventHandler? Updated;

        private void UpdateDisplayText()
        {
            if (!displayTextDirty) return;
            if (UnderscoreChar == default || string.IsNullOrEmpty(Text))
            {
                displayTextDisabled = displayText = text;
            }
            else
            {
                Color originalColor = Menu.ForegroundColor;
                if (Color != default)
                {
                    originalColor = Color;
                }
                Color specialCharColor = Menu.ActiveForegroundColor;
                int underscoreIndex = Text.IndexOf('&');
                char underscoreChar = Text[underscoreIndex + 1];
                displayTextDisabled = Text[0..underscoreIndex] + Text[(underscoreIndex + 1)..];
                if (specialCharColor != default)
                {
                    displayText = Text[..underscoreIndex] +
                        @$"/c[{specialCharColor.ToHexString()}]" +
                        underscoreChar +
                        $@"/c[{originalColor.ToHexString()}]" +
                        Text[(underscoreIndex + 2)..];
                }
                else
                {
                    displayText = displayTextDisabled;
                }
            }
            displayTextDirty = false;
        }

        internal MenuItem? FindMenuItemByName(string name)
        {
            if (name == Name) return this;
            foreach (var item in Items.OfType<MenuItem>())
            {
                var result = item.FindMenuItemByName(name);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// Handles the item selection.
        /// </summary>
        public void OnSelected() => Selected?.Invoke(this, EventArgs.Empty);

        /// <inheritdoc/>
        protected internal override void OnNameChanged()
        {
            base.OnNameChanged();
            Updated?.Invoke(this, EventArgs.Empty);
        }
    }
}