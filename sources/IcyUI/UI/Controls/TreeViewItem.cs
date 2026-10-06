// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// A row of a <see cref="TreeView"/>: a <see cref="SelectorItem"/> that knows its depth, whether it has children and
    /// is expanded, and whether it can be selected.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tree sets every row property each time the row is realized, because a pooled row is reused for another item
    /// with only its content's <see cref="UIElement.DataContext"/> rebound.
    /// </para>
    /// <para>Template parts:</para>
    /// <list type="bullet">
    /// <item><description><see cref="IndentPartName"/>: any element; its <see cref="UIElement.Width"/> is set to the row's indent.</description></item>
    /// <item><description>
    /// <see cref="ExpanderPartName"/>: a <see cref="TreeViewExpander"/>. It shows <see cref="IconKind.ChevronDown"/>
    /// when expanded and <see cref="IconKind.ChevronRight"/> otherwise, and is transparent and not hit-testable when the
    /// row has no children, so leaves keep their alignment.
    /// </description></item>
    /// </list>
    /// </remarks>
    public class TreeViewItem : SelectorItem
    {
        /// <summary>The name of the template part that shows the chevron.</summary>
        public const string ExpanderPartName = "PART_Expander";

        /// <summary>The name of the template part whose width indents the row.</summary>
        public const string IndentPartName = "PART_Indent";

        private int depth;
        private bool hasChildren;
        private bool isExpanded;
        private bool isSelectable = true;
        private float indentWidth;
        private TreeViewExpander? expander;
        private UIElement? indentPart;
        private bool expanderTapped;

        /// <summary>
        /// Occurs when the row's chevron is tapped. The same tap doesn't raise <see cref="SelectorItem.Tapped"/>.
        /// </summary>
        public event EventHandler? ExpanderTapped;

        /// <summary>Gets the row's depth: <c>0</c> for a root.</summary>
        public int Depth
        {
            get => depth;
            private set => SetProperty(ref depth, value);
        }

        /// <summary>Gets a value indicating whether the row's item has children.</summary>
        public bool HasChildren
        {
            get => hasChildren;
            private set => SetProperty(ref hasChildren, value);
        }

        /// <summary>Gets a value indicating whether the row's item is expanded.</summary>
        public bool IsExpanded
        {
            get => isExpanded;
            private set => SetProperty(ref isExpanded, value);
        }

        /// <summary>Gets a value indicating whether the row's item can be selected.</summary>
        public bool IsSelectable
        {
            get => isSelectable;
            private set => SetProperty(ref isSelectable, value);
        }

        /// <summary>Gets the indent last stamped on the row, in layout units.</summary>
        internal float IndentWidth => indentWidth;

        /// <summary>Gets the template's chevron part, if any.</summary>
        internal TreeViewExpander? Expander => expander;

        /// <summary>Gets the template's indent part, if any.</summary>
        internal UIElement? IndentPart => indentPart;

        /// <summary>
        /// Stamps the row state for the item this row shows now.
        /// </summary>
        /// <param name="depth">The row's depth.</param>
        /// <param name="indentWidth">The indent, in layout units.</param>
        /// <param name="hasChildren">Whether the item has children.</param>
        /// <param name="isExpanded">Whether the item is expanded.</param>
        /// <param name="isSelectable">Whether the item can be selected.</param>
        internal void Update(int depth, float indentWidth, bool hasChildren, bool isExpanded, bool isSelectable)
        {
            Depth = depth;
            HasChildren = hasChildren;
            IsExpanded = isExpanded;
            IsSelectable = isSelectable;
            this.indentWidth = indentWidth;
            UpdateParts();
        }

        /// <inheritdoc/>
        protected override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (expander != null)
                expander.Tapped -= Expander_Tapped;

            expander = GetTemplateChild<TreeViewExpander>(ExpanderPartName);
            indentPart = GetTemplateChild<UIElement>(IndentPartName);
            if (expander != null)
                expander.Tapped += Expander_Tapped;
            UpdateParts();
        }

        /// <inheritdoc/>
        /// <remarks>Skipped once right after a tap on the chevron, which bubbles here as well.</remarks>
        protected internal override void OnTap()
        {
            if (expanderTapped)
            {
                expanderTapped = false;
                return;
            }

            base.OnTap();
        }

        private void Expander_Tapped(object? sender, EventArgs e)
        {
            expanderTapped = true;
            ExpanderTapped?.Invoke(this, EventArgs.Empty);
        }

        private void UpdateParts()
        {
            if (indentPart != null)
                indentPart.Width = indentWidth;
            if (expander != null)
            {
                expander.Kind = isExpanded ? IconKind.ChevronDown : IconKind.ChevronRight;
                expander.Opacity = hasChildren ? 1 : 0;
                expander.IsHitTestVisible = hasChildren;
            }
        }
    }
}
