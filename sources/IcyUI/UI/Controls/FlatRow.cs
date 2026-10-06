// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// One visible row of a <see cref="TreeView"/>: a data item at a depth, under its parent row.
    /// </summary>
    /// <param name="item">The data item.</param>
    /// <param name="depth">The depth: <c>0</c> for a root.</param>
    /// <param name="parent">The parent row, or <see langword="null"/> for a root.</param>
    internal sealed class FlatRow(object item, int depth, FlatRow? parent)
    {
        /// <summary>Gets the data item.</summary>
        public object Item { get; } = item;

        /// <summary>Gets the depth: <c>0</c> for a root.</summary>
        public int Depth { get; } = depth;

        /// <summary>Gets the parent row, or <see langword="null"/> for a root.</summary>
        public FlatRow? Parent { get; } = parent;

        /// <inheritdoc/>
        public override string ToString() => Item.ToString() ?? string.Empty;
    }
}
