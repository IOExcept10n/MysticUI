// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Diagnostics;
using Icy.Data;
using Icy.Data.Markup.Attributes;
using Icy.Markup;
using Icy.UI.Styles;

namespace Icy.UI.Controls
{
    /// <summary>
    /// Shows hierarchical data as an indented, expandable list. Only visible rows are realized, however large the tree.
    /// </summary>
    /// <remarks>
    /// <para>Where the roots come from: <see cref="ItemsSource"/>, or the inline <see cref="Items"/> (not both).</para>
    /// <para>Where an item's children come from (the first configured source wins):</para>
    /// <list type="number">
    /// <item><description><see cref="ChildrenSelector"/>, when set;</description></item>
    /// <item><description>otherwise the <see cref="HierarchicalDataTemplate.ChildrenPath"/> of the item's template;</description></item>
    /// <item><description>otherwise <see cref="TreeViewNode.Children"/>, for a <see cref="TreeViewNode"/>;</description></item>
    /// <item><description>otherwise none.</description></item>
    /// </list>
    /// <para>
    /// Expansion state belongs to the tree, keyed by item reference. For a <see cref="TreeViewNode"/> it's the node's own
    /// <see cref="TreeViewNode.IsExpanded"/>. An item that appears under two parents is expanded in both places.
    /// </para>
    /// <para>
    /// Internally the tree keeps the visible rows as a flat list. Expanding or collapsing an item inserts or removes its
    /// visible subtree as one change, and only the rows on screen have containers (see <see cref="ItemsControl"/>).
    /// <see langword="null"/> items aren't supported.
    /// </para>
    /// </remarks>
    [ContentProperty(nameof(Items))]
    public class TreeView : Control
    {
        private readonly TreeViewList list;
        private readonly ScrollViewer scrollViewer;
        private readonly RangeObservableCollection<FlatRow> rows = [];
        private readonly HashSet<object> expanded = new(ReferenceEqualityComparer.Instance);
        private IEnumerable? itemsSource;
        private Func<object, IEnumerable?>? childrenSelector;
        private Func<object, bool>? isItemSelectable;
        private DataTemplate? itemTemplate;
        private Func<object, DataTemplate>? itemTemplateSelector;
        private float indent = 16;
        private bool settingNodeState;
        private object? selectedItem;
        private FlatRow? currentRow;

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeView"/> class.
        /// </summary>
        public TreeView()
        {
            IsFocusable = true;
            // Assigned before ItemsSource: setting it calls back into OnRowsChanged, which uses the list.
            list = new TreeViewList(this);
            list.ItemsSource = rows;
            scrollViewer = new ScrollViewer { Content = list };
            ((Border)Chrome).Child = scrollViewer;
            Items.CollectionChanged += Items_CollectionChanged;

            // Starts following the (empty) inline Items right away, so markup-only trees populate without an attach.
            Rebuild();
        }

        /// <summary>Occurs when an item is expanded, through the API, the UI, or a visible <see cref="TreeViewNode"/>'s <see cref="TreeViewNode.IsExpanded"/>.</summary>
        public event EventHandler<TreeViewItemEventArgs>? ItemExpanded;

        /// <summary>Occurs when an item is collapsed, through the API, the UI, or a visible <see cref="TreeViewNode"/>'s <see cref="TreeViewNode.IsExpanded"/>.</summary>
        public event EventHandler<TreeViewItemEventArgs>? ItemCollapsed;

        /// <summary>Occurs when <see cref="SelectedItem"/> changes.</summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Gets the inline root items, the markup content of the tree. Used when <see cref="ItemsSource"/> isn't set.
        /// </summary>
        /// <exception cref="InvalidOperationException">An item is added while <see cref="ItemsSource"/> is set.</exception>
        public ObservableCollection<object> Items { get; } = [];

        /// <summary>
        /// Gets or sets the root items. An <see cref="INotifyCollectionChanged"/> source is followed live.
        /// </summary>
        /// <exception cref="InvalidOperationException">A source is set while <see cref="Items"/> isn't empty.</exception>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public IEnumerable? ItemsSource
        {
            get => itemsSource;
            set
            {
                if (value != null && Items.Count > 0)
                    throw new InvalidOperationException($"A {nameof(TreeView)} takes its roots from either '{nameof(ItemsSource)}' or inline '{nameof(Items)}', not both.");
                if (SetProperty(ref itemsSource, value))
                    Rebuild();
            }
        }

        /// <summary>
        /// Gets or sets a function that returns an item's children, or <see langword="null"/> for none. When set, it's the
        /// only source of children.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public Func<object, IEnumerable?>? ChildrenSelector
        {
            get => childrenSelector;
            set
            {
                if (SetProperty(ref childrenSelector, value))
                    Rebuild();
            }
        }

        /// <summary>
        /// Gets or sets a function that decides whether an item can be selected. When unset, a <see cref="TreeViewNode"/>
        /// uses its <see cref="TreeViewNode.IsSelectable"/> and every other item is selectable.
        /// </summary>
        /// <remarks>Tapping a non-selectable item with children expands or collapses it instead.</remarks>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public Func<object, bool>? IsItemSelectable
        {
            get => isItemSelectable;
            set
            {
                if (SetProperty(ref isItemSelectable, value))
                {
                    list.RestampAll();
                    if (selectedItem != null && !IsSelectable(selectedItem))
                        Select(null, reveal: false);
                }
            }
        }

        /// <summary>
        /// Gets or sets the template for each row's content. A <see cref="HierarchicalDataTemplate"/> also supplies children.
        /// Without one, a row shows its item's text.
        /// </summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public DataTemplate? ItemTemplate
        {
            get => itemTemplate;
            set
            {
                if (SetProperty(ref itemTemplate, value))
                    Rebuild();
            }
        }

        /// <summary>Gets or sets a function that picks a template per item, overriding <see cref="ItemTemplate"/>.</summary>
        [Category("Content")]
        [DefaultValue(null)]
        [RegisterReference]
        public Func<object, DataTemplate>? ItemTemplateSelector
        {
            get => itemTemplateSelector;
            set
            {
                if (SetProperty(ref itemTemplateSelector, value))
                    Rebuild();
            }
        }

        /// <summary>Gets or sets how far each level is indented, in layout units. Defaults to <c>16</c>.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
        [Category("Layout")]
        [DefaultValue(16f)]
        [RegisterReference]
        public float Indent
        {
            get => indent;
            set
            {
                Guard.IsGreaterThanOrEqualTo(value, 0f);
                if (SetProperty(ref indent, value))
                    list.RestampAll();
            }
        }

        /// <summary>
        /// Gets or sets the selected data item, or <see langword="null"/> for none.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item><description>Setting a selectable item reveals it (see <see cref="Reveal(object)"/>) and selects it.</description></item>
        /// <item><description>Setting an item that isn't in the tree, or can't be selected, clears the selection.</description></item>
        /// <item><description>Collapsing an ancestor keeps the selection; removing the item from the data clears it.</description></item>
        /// </list>
        /// </remarks>
        [Category("Behavior")]
        [DefaultValue(null)]
        [RegisterReference]
        public object? SelectedItem
        {
            get => selectedItem;
            set => Select(value, reveal: true);
        }

        /// <summary>Gets the visible rows, top to bottom.</summary>
        internal IReadOnlyList<FlatRow> Rows => rows;

        /// <summary>Gets the inner row list.</summary>
        internal TreeViewList List => list;

        /// <summary>Gets the inner scroll viewer.</summary>
        internal ScrollViewer ScrollViewer => scrollViewer;

        private IEnumerable Roots => itemsSource ?? Items;

        /// <summary>
        /// Gets a value indicating whether <paramref name="item"/> is expanded.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><see langword="true"/> when the item shows its children (or would, once visible).</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public bool IsExpanded(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            return item is TreeViewNode node ? node.IsExpanded : expanded.Contains(item);
        }

        /// <summary>
        /// Expands <paramref name="item"/>. Every visible row of it shows its children. An item that isn't visible keeps the
        /// state for when it becomes visible.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public void Expand(object item) => SetExpanded(item, true);

        /// <summary>
        /// Collapses <paramref name="item"/>. Its descendants keep their own expansion, so expanding it again restores the
        /// subtree as it was.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public void Collapse(object item) => SetExpanded(item, false);

        /// <summary>
        /// Makes <paramref name="item"/> visible: expands every ancestor on its path from a root, then scrolls its row into
        /// view.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <returns><see langword="false"/> when the item isn't anywhere in the tree.</returns>
        /// <remarks>
        /// An item that is already visible only scrolls. Otherwise the tree searches its data depth-first through child
        /// resolution, which is O(n) in the number of items; the search never revisits an item, so cyclic data terminates.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public bool Reveal(object item)
        {
            ArgumentNullException.ThrowIfNull(item);
            int index = IndexOfVisible(item);
            if (index < 0)
            {
                if (FindPath(item) is not { } path)
                    return false;

                for (int i = 0; i < path.Count - 1; i++)
                    Expand(path[i]);
                index = IndexOfVisible(item);
                if (index < 0)
                    return false;
            }

            list.ScrollIntoView(index);
            return true;
        }

        /// <summary>Gets <paramref name="item"/>'s children, following the resolution order in the class remarks.</summary>
        /// <param name="item">The item.</param>
        /// <returns>The children, or <see langword="null"/> for none. Never a <see cref="string"/>.</returns>
        internal IEnumerable? GetChildren(object item)
        {
            IEnumerable? children = childrenSelector != null
                ? childrenSelector(item)
                : ResolveTemplate(item) is HierarchicalDataTemplate hierarchical
                    ? hierarchical.GetChildren(item)
                    : (item as TreeViewNode)?.Children;
            return children is string ? null : children;
        }

        /// <summary>Gets a value indicating whether <paramref name="item"/> has at least one child.</summary>
        /// <param name="item">The item.</param>
        /// <returns><see langword="true"/> when the item has children.</returns>
        internal bool HasChildren(object item) => GetChildren(item) switch
        {
            null => false,
            ICollection collection => collection.Count > 0,
            IEnumerable enumerable => enumerable.Cast<object>().Any(),
        };

        /// <summary>Gets a value indicating whether <paramref name="item"/> can be selected.</summary>
        /// <param name="item">The item.</param>
        /// <returns><see langword="true"/> when selectable.</returns>
        internal bool IsSelectable(object item) => isItemSelectable?.Invoke(item) ?? (item as TreeViewNode)?.IsSelectable ?? true;

        /// <summary>Resolves the content template for <paramref name="item"/>.</summary>
        /// <param name="item">The item.</param>
        /// <returns>The selector's template, else <see cref="ItemTemplate"/>, else the built-in text template.</returns>
        internal DataTemplate ResolveTemplate(object item) => itemTemplateSelector?.Invoke(item) ?? itemTemplate ?? DataTemplate.Default;

        /// <summary>Stamps <paramref name="container"/> with <paramref name="row"/>'s state.</summary>
        /// <param name="container">The realized container.</param>
        /// <param name="row">The row it shows.</param>
        internal void Stamp(TreeViewItem container, FlatRow row) =>
            container.Update(row.Depth, row.Depth * indent, HasChildren(row.Item), IsExpanded(row.Item), IsSelectable(row.Item));

        /// <summary>Handles a tap on a row (outside its chevron): selects it, or toggles a non-selectable branch.</summary>
        /// <param name="row">The tapped row.</param>
        internal void OnRowTapped(FlatRow row)
        {
            currentRow = row;
            if (IsSelectable(row.Item))
                Select(row.Item, reveal: false);
            else if (HasChildren(row.Item))
                Toggle(row.Item);
            SyncList();
        }

        /// <summary>Handles a tap on a row's chevron: toggles it without selecting.</summary>
        /// <param name="row">The row.</param>
        internal void OnExpanderTapped(FlatRow row)
        {
            currentRow = row;
            Toggle(row.Item);
            SyncList();
        }

        /// <summary>
        /// Called by the list after every change to the rows: moves a current row that disappeared to its nearest visible
        /// ancestor, and maps the selection and current row back to row indices.
        /// </summary>
        internal void OnRowsChanged()
        {
            if (currentRow != null && rows.IndexOf(currentRow) < 0)
            {
                FlatRow? ancestor = currentRow.Parent;
                while (ancestor != null && rows.IndexOf(ancestor) < 0)
                    ancestor = ancestor.Parent;
                currentRow = ancestor;
            }

            SyncList();
        }

        /// <inheritdoc/>
        protected override void OnAttached()
        {
            base.OnAttached();
            Rebuild();
        }

        private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (itemsSource != null)
                throw new InvalidOperationException($"A {nameof(TreeView)} takes its roots from either '{nameof(ItemsSource)}' or inline '{nameof(Items)}', not both.");
            Rebuild();
        }

        private void Rebuild()
        {
            var built = new List<FlatRow>();
            foreach (object item in Roots)
                AppendVisible(item, 0, null, built);
            rows.ResetTo(built);
            ClearSelectionIfGone();
        }

        /// <summary>Appends <paramref name="item"/>'s row and, while expanded, its visible descendants.</summary>
        private void AppendVisible(object item, int depth, FlatRow? parent, List<FlatRow> into)
        {
            var row = new FlatRow(item, depth, parent);
            into.Add(row);

            // An item that is its own ancestor (a cycle in the data) is shown, but never expanded again.
            if (IsExpanded(item) && !HasAncestor(parent, item) && GetChildren(item) is { } children)
            {
                foreach (object child in children)
                    AppendVisible(child, depth + 1, row, into);
            }
        }

        private static bool HasAncestor(FlatRow? row, object item)
        {
            for (; row != null; row = row.Parent)
            {
                if (ReferenceEquals(row.Item, item))
                    return true;
            }

            return false;
        }

        private void InsertRows(int index, List<FlatRow> range) => rows.InsertRange(index, range);

        private void RemoveRows(int index, int count, bool fromData) => rows.RemoveRange(index, count);

        /// <summary>Counts the rows below <paramref name="index"/> that are deeper than it: its visible subtree.</summary>
        private int DescendantCount(int index)
        {
            int depth = rows[index].Depth;
            int end = index + 1;
            while (end < rows.Count && rows[end].Depth > depth)
                end++;
            return end - index - 1;
        }

        private int IndexOfVisible(object item)
        {
            for (int i = 0; i < rows.Count; i++)
            {
                if (ReferenceEquals(rows[i].Item, item))
                    return i;
            }

            return -1;
        }

        private void SyncList()
        {
            int selected = selectedItem == null ? -1 : IndexOfVisible(selectedItem);
            if (list.SelectedIndex != selected)
                list.SelectedIndex = selected;
            list.CurrentIndex = IsFocused && currentRow != null ? rows.IndexOf(currentRow) : -1;
        }

        private void Select(object? item, bool reveal)
        {
            if (item != null && (!IsSelectable(item) || !(reveal ? Reveal(item) : IndexOfVisible(item) >= 0)))
                item = null;

            if (SetProperty(ref selectedItem, item, nameof(SelectedItem)))
            {
                SyncList();
                SelectionChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                SyncList();
            }
        }

        /// <summary>Finds the path from a root to <paramref name="target"/>, depth-first through child resolution.</summary>
        /// <returns>The items from the root to the target inclusive, or <see langword="null"/> when it isn't in the tree.</returns>
        private List<object>? FindPath(object target)
        {
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            var path = new List<object>();
            foreach (object root in Roots)
            {
                if (Search(root))
                    return path;
            }

            return null;

            bool Search(object item)
            {
                if (!visited.Add(item))
                    return false;

                path.Add(item);
                if (ReferenceEquals(item, target))
                    return true;
                if (GetChildren(item) is { } children)
                {
                    foreach (object child in children)
                    {
                        if (Search(child))
                            return true;
                    }
                }

                path.RemoveAt(path.Count - 1);
                return false;
            }
        }

        /// <summary>Clears the selection when its item is no longer anywhere in the tree's data.</summary>
        private void ClearSelectionIfGone()
        {
            if (selectedItem != null && IndexOfVisible(selectedItem) < 0 && FindPath(selectedItem) == null)
                Select(null, reveal: false);
        }

        private void Toggle(object item) => SetExpanded(item, !IsExpanded(item));

        private void SetExpanded(object item, bool value)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (IsExpanded(item) == value)
                return;

            if (item is TreeViewNode node)
            {
                // The node raises PropertyChanged; the tree's own node handler (Task 6) ignores changes it makes itself.
                settingNodeState = true;
                try
                {
                    node.IsExpanded = value;
                }
                finally
                {
                    settingNodeState = false;
                }
            }
            else if (value)
            {
                expanded.Add(item);
            }
            else
            {
                expanded.Remove(item);
            }

            ApplyExpansion(item, value);
        }

        /// <summary>Shows or hides the subtree of every visible row of <paramref name="item"/>, then raises the event.</summary>
        private void ApplyExpansion(object item, bool value)
        {
            foreach (FlatRow row in rows.Where(r => ReferenceEquals(r.Item, item)).ToList())
            {
                int index = rows.IndexOf(row);
                if (index < 0)
                    continue;

                if (value)
                {
                    var subtree = new List<FlatRow>();
                    if (!HasAncestor(row.Parent, item) && GetChildren(item) is { } children)
                    {
                        foreach (object child in children)
                            AppendVisible(child, row.Depth + 1, row, subtree);
                    }

                    InsertRows(index + 1, subtree);
                }
                else
                {
                    RemoveRows(index + 1, DescendantCount(index), fromData: false);
                }

                list.Restamp(row);
            }

            (value ? ItemExpanded : ItemCollapsed)?.Invoke(this, new TreeViewItemEventArgs(item));
        }
    }
}
