// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.ObjectModel;
using System.ComponentModel;
using Icy.Data;

namespace Icy.UI
{
    /// <summary>
    /// Represents a collection of <see cref="UIElement"/> objects that can be observed for changes.
    /// </summary>
    public class UIElementCollection : ObservableCollection<UIElement>
    {
        /// <summary>
        /// Occurs when a new <see cref="UIElement"/> is added to the collection.
        /// </summary>
        public event DataEventHandler<UIElement>? ItemAdded;

        /// <summary>
        /// Occurs when a <see cref="UIElement"/> is removed from the collection.
        /// </summary>
        public event DataEventHandler<UIElement>? ItemRemoved;

        /// <summary>
        /// Occurs when a new <see cref="UIElement"/> is about to be added to the collection.
        /// This event can be used to cancel the addition.
        /// </summary>
        public event CancellableEventHandler<UIElement>? ItemAdding;

        /// <summary>
        /// Occurs when a <see cref="UIElement"/> is about to be removed from the collection.
        /// This event can be used to cancel the removal.
        /// </summary>
        public event CancellableEventHandler<UIElement>? ItemRemoving;

        /// <summary>
        /// Occurs when the collection is about to be cleared.
        /// This event can be used to cancel the clearing operation.
        /// </summary>
        public event CancelEventHandler? Resetting;

        /// <summary>
        /// Occurs when the collection has been cleared.
        /// </summary>
        public event EventHandler? Reset;

        /// <summary>
        /// Inserts a new <see cref="UIElement"/> into the collection at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index at which the new item should be inserted.</param>
        /// <param name="item">The <see cref="UIElement"/> to insert.</param>
        protected override void InsertItem(int index, UIElement item)
        {
            CancellableEventArgs<UIElement>? args = new(item);
            ItemAdding?.Invoke(this, args);
            if (args.Cancel)
                return;
            base.InsertItem(index, item);
            ItemAdded?.Invoke(this, item);
        }

        /// <summary>
        /// Removes all items from the collection.
        /// </summary>
        protected override void ClearItems()
        {
            CancelEventArgs args = new();
            Resetting?.Invoke(this, args);
            if (args.Cancel)
                return;
            base.ClearItems();
            Reset?.Invoke(this, args);
        }

        /// <summary>
        /// Removes the <see cref="UIElement"/> at the specified index from the collection.
        /// </summary>
        /// <param name="index">The zero-based index of the element to remove.</param>
        protected override void RemoveItem(int index)
        {
            var item = this[index];
            CancellableEventArgs<UIElement>? args = new(item);
            ItemRemoving?.Invoke(this, args);
            if (args.Cancel)
                return;
            base.RemoveItem(index);
            ItemRemoved?.Invoke(this, item);
        }
    }
}