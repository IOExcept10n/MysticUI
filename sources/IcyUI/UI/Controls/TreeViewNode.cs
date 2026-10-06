// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Icy.Markup;

namespace Icy.UI.Controls
{
    /// <summary>
    /// A ready-made data item for a <see cref="TreeView"/>: a header, child nodes, and per-node expansion and
    /// selectability. Lets a static tree be written directly in markup.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Children"/> is the content property, so nested <c>&lt;TreeViewNode&gt;</c> elements need no wrapper:
    /// </para>
    /// <code language="xml">
    /// &lt;TreeView&gt;
    ///   &lt;TreeViewNode Header="Basics" IsSelectable="False" IsExpanded="True"&gt;
    ///     &lt;TreeViewNode Header="Controls"/&gt;
    ///   &lt;/TreeViewNode&gt;
    /// &lt;/TreeView&gt;
    /// </code>
    /// <para>
    /// A <see cref="TreeView"/> reads <see cref="IsExpanded"/> as the node's expansion state and keeps it in step both
    /// ways, and treats <see cref="IsSelectable"/> as the node's selectability unless
    /// <see cref="TreeView.IsItemSelectable"/> is set. Without an item template, a row shows <see cref="ToString"/>.
    /// </para>
    /// </remarks>
    [ContentProperty(nameof(Children))]
    public class TreeViewNode : INotifyPropertyChanged
    {
        private object? header;
        private bool isExpanded;
        private bool isSelectable = true;
        private object? tag;

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeViewNode"/> class with no header.
        /// </summary>
        public TreeViewNode()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TreeViewNode"/> class.
        /// </summary>
        /// <param name="header">The header to show.</param>
        public TreeViewNode(object? header)
        {
            this.header = header;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Gets or sets what the node's row shows.</summary>
        public object? Header
        {
            get => header;
            set => Set(ref header, value);
        }

        /// <summary>Gets the child nodes. A <see cref="TreeView"/> follows changes to this collection while the node is visible.</summary>
        public ObservableCollection<TreeViewNode> Children { get; } = [];

        /// <summary>Gets or sets a value indicating whether the node shows its children. Defaults to <see langword="false"/>.</summary>
        public bool IsExpanded
        {
            get => isExpanded;
            set => Set(ref isExpanded, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the node can be selected. Defaults to <see langword="true"/>. Tapping a
        /// non-selectable node with children opens or closes it instead.
        /// </summary>
        public bool IsSelectable
        {
            get => isSelectable;
            set => Set(ref isSelectable, value);
        }

        /// <summary>Gets or sets any data the application wants to attach to the node.</summary>
        public object? Tag
        {
            get => tag;
            set => Set(ref tag, value);
        }

        /// <summary>Returns the text of <see cref="Header"/>.</summary>
        /// <returns><see cref="Header"/>'s text, or an empty string when there is none.</returns>
        public override string ToString() => header?.ToString() ?? string.Empty;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
