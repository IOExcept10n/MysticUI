using Stride.Core.Mathematics;
using Stride.Input;
using System.ComponentModel;
using System.Xml.Serialization;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a control to present elements hierarchy in the tree view.
    /// </summary>
    public class TreeView : TreeNode
    {
        internal readonly List<TreeNode> allNodes = new();
        private TreeNode? hoverItem;
        private TreeNode? selectedItem;
        private bool showRoot = true;
        private bool areRowsDirty = true;

        /// <summary>
        /// Gets all nodes collection of the tree.
        /// </summary>
        public IReadOnlyList<TreeNode> AllNodes => allNodes;

        /// <summary>
        /// Gets or sets the selected item of the tree.
        /// </summary>
        [XmlIgnore]
        [Browsable(false)]
        public TreeNode? SelectedItem
        {
            get => selectedItem;
            set
            {
                if (value == selectedItem) return;
                selectedItem = value;
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                NotifyPropertyChanged(nameof(SelectedItem));
            }
        }

        /// <summary>
        /// Gets or sets the value that determines whether to show root checkmark of the tree.
        /// </summary>
        /// <value>
        /// <see langword="true"/> to show the checkmark block of the root,
        /// <see langword="false"/> to not show it and make the tree root always expanded.
        /// </value>
        [DefaultValue(true)]
        public bool ShowRoot
        {
            get => showRoot;
            set
            {
                if (value == showRoot) return;
                showRoot = value;
                UpdateShowRoot();
                NotifyPropertyChanged(nameof(ShowRoot));
            }
        }

        /// <summary>
        /// Occurs when the selected item of the <see cref="TreeView"/> changes.
        /// </summary>
        public event EventHandler? SelectionChanged;

        /// <summary>
        /// Creates a new instance of the <see cref="TreeView"/> class.
        /// </summary>
        public TreeView()
        {
            AcceptFocus = true;
        }

        /// <inheritdoc/>
        public override void Clear()
        {
            base.Clear();
            allNodes.Clear();
            allNodes.Add(this);
            selectedItem = this;
            hoverItem = this;
        }

        /// <summary>
        /// Processes an action to all nodes of the tree. If any node returns <see langword="false"/>, processing ends.
        /// </summary>
        /// <param name="predicate">Function to process all nodes.</param>
        /// <returns><see langword="true"/> if the function returned <see langword="true"/> on all elements, <see langword="false"/> otherwise.</returns>
        public bool ForAll(Func<TreeNode, bool> predicate) => Iterate(this, predicate);

        /// <summary>
        /// Expands the <see cref="TreeView"/> to the given element.
        /// </summary>
        /// <param name="node">An element to expand to.</param>
        public void ExpandPath(TreeNode node)
        {
            var path = new Stack<TreeNode>();
            path.Push(this);
            if (!FindPath(path, node)) return;
            while (path.Count > 0)
            {
                var p = path.Pop();
                p.IsExpanded = true;
            }
        }

        /// <inheritdoc/>
        protected internal override void ArrangeInternal()
        {
            base.ArrangeInternal();
            areRowsDirty = true;
        }

        /// <inheritdoc/>
        protected internal override void RenderInternal(RenderContext context)
        {
            if (areRowsDirty)
            {
                UpdateRowInfo();
                areRowsDirty = false;
            }

            if (IsActive && hoverItem != null && hoverItem != SelectedItem && SelectionHoverBrush != null)
            {
                var area = GetItemRect(hoverItem);
                SelectionHoverBrush.Draw(context, area);
            }

            if (SelectedItem != null && SelectedItem.rowVisible && SelectionBrush != null)
            {
                var area = GetItemRect(SelectedItem);
                SelectionBrush.Draw(context, area);
            }

            base.RenderInternal(context);
        }

        /// <inheritdoc/>
        protected internal override void OnKeyDown(Keys key)
        {
            base.OnKeyDown(key);
            if (SelectedItem == null) return;
            int index = 0;
            IList<Control>? parentControls = null;
            if (SelectedItem.ParentNode != null)
            {
                parentControls = SelectedItem.ParentNode.ChildNodes.Children;
                index = parentControls.IndexOf(SelectedItem);
                if (index == -1)
                {
                    return;
                }
            }

            switch (key)
            {
                case Keys.Enter:
                    SelectedItem.IsExpanded = !SelectedItem.IsExpanded;
                    break;

                case Keys.Up:
                    if (parentControls != null)
                    {
                        if (index == 0 && SelectedItem.ParentNode != this)
                        {
                            SelectedItem = SelectedItem.ParentNode;
                        }
                        else
                        {
                            var previousItem = (TreeNode)parentControls[index - 1];
                            if (!previousItem.IsExpanded || previousItem.Count == 0)
                            {
                                SelectedItem = previousItem;
                            }
                            else
                            {
                                SelectedItem = previousItem[^1];
                            }
                        }
                    }
                    break;

                case Keys.Down:
                    if (SelectedItem.IsExpanded && SelectedItem.Count > 0)
                    {
                        SelectedItem = (TreeNode)SelectedItem.ChildNodes[0];
                    }
                    else if (parentControls != null)
                    {
                        if (index + 1 < parentControls.Count)
                        {
                            SelectedItem = (TreeNode)parentControls[index + 1];
                        }
                        else if (SelectedItem.ParentNode != null)
                        {
                            var parent2 = SelectedItem.ParentNode.ParentNode;
                            if (parent2 != null)
                            {
                                var parentIndex = parent2.ChildNodes.Children.IndexOf(SelectedItem.ParentNode);
                                if (parentIndex + 1 < parent2.Count)
                                {
                                    SelectedItem = parent2[parentIndex + 1];
                                }
                            }
                        }
                    }
                    break;
            }
        }

        /// <inheritdoc/>
        protected internal override bool OnTouchDown()
        {
            base.OnTouchDown();
            if (Desktop == null) return false;
            SetHoverItem(Desktop.TouchPosition);
            if (hoverItem != null)
            {
                if (!hoverItem.rowVisible)
                {
                    return false;
                }
                SelectedItem = hoverItem;
            }
            return true;
        }

        /// <inheritdoc/>
        protected internal override void OnDoubleClick()
        {
            base.OnDoubleClick();
            if (hoverItem != null)
            {
                if (!hoverItem.rowVisible) return;
                if (hoverItem.Mark.Visible && !hoverItem.Mark.ContainsTouch)
                {
                    hoverItem.Mark.IsChecked = !hoverItem.Mark.IsChecked;
                }
            }
        }

        /// <inheritdoc/>
        protected internal override void OnMouseMove()
        {
            base.OnMouseMove();
            if (Desktop == null) return;
            hoverItem = null;
            SetHoverItem(Desktop.MousePosition);
        }

        /// <inheritdoc/>
        protected internal override void OnMouseLeft()
        {
            base.OnMouseLeft();
            hoverItem = null;
        }

        /// <inheritdoc/>
        protected override void UpdateMark()
        {
            if (!ShowRoot) return;
            base.UpdateMark();
        }

        private void UpdateShowRoot()
        {
            if (showRoot)
            {
                Mark.Visible = true;
                Label.Visible = true;
                ChildNodes.Visible = Mark.IsChecked == true;
            }
            else
            {
                Mark.Visible = false;
                Label.Visible = false;
                Mark.IsChecked = true;
                ChildNodes.Visible = true;
            }
        }

        private Rectangle GetItemRect(TreeNode row)
        {
            var position = ToLocal(row.ToGlobal(row.ActualBounds.Location));
            return new(ActualBounds.Left, position.Y, ActualBounds.Width, row.Child.GetRowHeight(0));
        }

        private void SetHoverItem(Point position)
        {
            if (!ContainsPoint(position)) return;
            position = ToLocal(position);
            foreach (var rowInfo in allNodes)
            {
                if (rowInfo.rowVisible)
                {
                    var rect = GetItemRect(rowInfo);
                    if (rect.Contains(position))
                    {
                        hoverItem = rowInfo;
                        return;
                    }
                }
            }
        }

        private bool Iterate(TreeNode node, Func<TreeNode, bool> predicate)
        {
            if (!predicate(node)) return false;
            foreach (var child in node.ChildNodes.Children)
            {
                var subNode = (TreeNode)child;
                if (!Iterate(subNode, predicate)) return false;
            }
            return true;
        }

        private void UpdateRowInfo()
        {
            foreach (var info in allNodes)
            {
                info.rowVisible = false;
            }
            UpdateVisibilityRecursive(this);
        }

        private static void UpdateVisibilityRecursive(TreeNode tree)
        {
            tree.rowVisible = false;
            if (!tree.IsExpanded)
            {
                foreach (var item in tree.ChildNodes.Children.OfType<TreeNode>())
                {
                    UpdateVisibilityRecursive(item);
                }
            }
        }

        private static bool FindPath(Stack<TreeNode> path, TreeNode node)
        {
            var top = path.Peek();
            for (int i = 0; i < top.Count; i++)
            {
                var child = top[i];
                if (child == node) return true;
                path.Push(child);
                if (FindPath(path, node)) return true;
                path.Pop();
            }
            return false;
        }
    }
}