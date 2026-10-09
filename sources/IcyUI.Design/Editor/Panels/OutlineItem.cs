// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Icy.Design.Editor.Panels
{
    /// <summary>
    /// One row of an <see cref="OutlinePanel"/>: a markup element of the shown document.
    /// </summary>
    /// <remarks>
    /// Items are kept per <see cref="NodeId"/> while the panel shows the same document, so an edit updates existing items
    /// instead of replacing them, and the tree keeps its expansion and selection.
    /// </remarks>
    public sealed class OutlineItem : INotifyPropertyChanged
    {
        private string label;
        private bool isHealthy = true;

        /// <summary>
        /// Initializes a new instance of the <see cref="OutlineItem"/> class.
        /// </summary>
        /// <param name="node">The element's id.</param>
        /// <param name="label">The text the row shows.</param>
        internal OutlineItem(NodeId node, string label)
        {
            Node = node;
            this.label = label;
        }

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Gets the id of the markup element this row shows.
        /// </summary>
        public NodeId Node { get; }

        /// <summary>
        /// Gets the text the row shows: the element's type, followed by its quoted name when it has one.
        /// </summary>
        public string Label
        {
            get => label;
            internal set => Set(ref label, value);
        }

        /// <summary>
        /// Gets a value indicating whether the live pages follow the element's markup.
        /// </summary>
        /// <remarks>
        /// <see langword="false"/> while the document is out of sync (see <see cref="DesignDocument.IsInSync"/>). The MVP
        /// tracks this per document, so every item of an out-of-sync document reports it.
        /// </remarks>
        public bool IsHealthy
        {
            get => isHealthy;
            internal set => Set(ref isHealthy, value);
        }

        /// <summary>
        /// Gets the rows of the element's content elements, in markup order.
        /// </summary>
        public ObservableCollection<OutlineItem> Children { get; } = [];

        /// <inheritdoc/>
        public override string ToString() => isHealthy ? Label : $"{Label} (!)";

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
