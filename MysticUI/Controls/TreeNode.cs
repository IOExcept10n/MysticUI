using Stride.Core.Mathematics;

namespace MysticUI.Controls
{
    /// <summary>
    /// Represents a single node of the <see cref="TreeView"/> element.
    /// </summary>
    public class TreeNode : SingleItemControlBase<Grid>
    {
        /// <summary>
        /// Gets the name of the mark button style.
        /// </summary>
        public const string TreeMarkStyleName = "TreeMarkStyle";

        private readonly TreeView? top;
        internal bool rowVisible;

        /// <summary>
        /// Gets or sets the value that determines whether the element is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => Mark.IsChecked == true;
            set => Mark.IsChecked = value;
        }

        /// <summary>
        /// Gets the text control of the <see cref="TreeNode"/>.
        /// </summary>
        public TextBlock Label { get; }

        /// <summary>
        /// Gets the check mark of the tree node.
        /// </summary>
        public Checkmark Mark { get; }

        /// <summary>
        /// Gets the grid for all child nodes of the <see cref="TreeNode"/>.
        /// </summary>
        public Grid ChildNodes { get; }

        /// <summary>
        /// Gets or sets the text of the <see cref="TreeNode"/>.
        /// </summary>
        public string? Text
        {
            get => Label.Text;
            set => Label.Text = value;
        }

        /// <inheritdoc/>
        public override Color ForegroundColor
        {
            get => base.ForegroundColor;
            set
            {
                base.ForegroundColor = value;
                Label.ForegroundColor = value;
            }
        }

        /// <summary>
        /// Gets the amount of elements inside the node.
        /// </summary>
        public int Count => ChildNodes.Count;

        /// <summary>
        /// Gets the parent node.
        /// </summary>
        public TreeNode? ParentNode { get; internal set; }

        /// <summary>
        /// Gets the brush for the selected elements.
        /// </summary>
        public IBrush? SelectionBrush { get; set; }

        /// <summary>
        /// Gets the brush for the hovered elements.
        /// </summary>
        public IBrush? SelectionHoverBrush { get; set; }

        /// <summary>
        /// Gets the tree node at the given index.
        /// </summary>
        /// <param name="index">An index for the node.</param>
        public TreeNode this[int index]
        {
            get => (TreeNode)ChildNodes[index];
        }

        /// <summary>
        /// Creates a new instance of the <see cref="TreeNode"/> class.
        /// </summary>
        /// <param name="topTree">A top of the tree view.</param>
        public TreeNode(TreeView? topTree = null)
        {
            ResetChild();
            top = topTree;
            top?.allNodes.Add(this);
            Mark = new()
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                StyleName = TreeMarkStyleName
            };
            Mark.CheckedChanged += (_, __) => ChildNodes!.Visible = IsExpanded;
            Child.Add(Mark);
            Label = new()
            {
                GridColumn = 1
            };
            Child.Add(Label);
            Child.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            Child.ColumnDefinitions.Add(new() { Width = GridLength.OneStar });
            Child.RowDefinitions.Add(new() { Height = GridLength.Auto });
            Child.RowDefinitions.Add(new() { Height = GridLength.Auto });
            ChildNodes = new()
            {
                Visible = false,
                GridColumn = 1,
                GridRow = 1,
            };
            Child.Add(ChildNodes);
            UpdateMark();
        }

        /// <inheritdoc/>
        public virtual void Clear()
        {
            ChildNodes.Clear();
            ChildNodes.RowDefinitions.Clear();
            UpdateMark();
        }

        /// <summary>
        /// Adds a node to the current node.
        /// </summary>
        /// <param name="text">Text for the node.</param>
        /// <returns>A new node instance to edit.</returns>
        public TreeNode Add(string text)
        {
            var result = new TreeNode(top ?? this as TreeView)
            {
                Text = text,
                GridRow = Count,
                ParentNode = this
            };
            ChildNodes.Add(result);
            ChildNodes.RowDefinitions.Add(new() { Height = GridLength.Auto });
            UpdateMark();
            return result;
        }

        /// <summary>
        /// Removes a node from the current node.
        /// </summary>
        /// <param name="item">An item to remove.</param>
        public void Remove(TreeNode item)
        {
            ChildNodes.Remove(item);
            if (top?.SelectedItem == item)
            {
                top.SelectedItem = null;
            }
        }

        /// <summary>
        /// Removes a node at given index from the current node.
        /// </summary>
        /// <param name="index">An index at which to remove children.</param>
        public void RemoveAt(int index)
        {
            var item = (TreeNode)ChildNodes[index];
            Remove(item);
        }

        /// <inheritdoc/>
        protected override void OnFontChanged()
        {
            base.OnFontChanged();
            Label.Font = Font;
        }

        /// <inheritdoc/>
        protected internal override void ResetChildInternal()
        {
            Child = new()
            {
                ColumnSpacing = 2,
                RowSpacing = 2
            };
        }

        /// <summary>
        /// Updates the checkmark visibility according to expansion state.
        /// </summary>
        protected virtual void UpdateMark()
        {
            Mark.Visible = ChildNodes.Any();
        }

        private void OnMarkUp(object sender, EventArgs e)
        {
            ChildNodes.Visible = false;
        }
    }
}