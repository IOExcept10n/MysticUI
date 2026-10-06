// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// Carries the data item of a <see cref="TreeView.ItemExpanded"/> or <see cref="TreeView.ItemCollapsed"/> event.
    /// </summary>
    /// <param name="item">The item that was expanded or collapsed.</param>
    public sealed class TreeViewItemEventArgs(object item) : EventArgs
    {
        /// <summary>Gets the item that was expanded or collapsed.</summary>
        public object Item { get; } = item;
    }
}
