// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Icy.Data
{
    /// <summary>
    /// An <see cref="ObservableCollection{T}"/> that inserts and removes whole ranges with a single
    /// <see cref="INotifyCollectionChanged.CollectionChanged"/> notification.
    /// </summary>
    /// <remarks>
    /// <see cref="UI.Controls.ItemsControl"/> handles a multi-item <see cref="NotifyCollectionChangedAction.Add"/> or
    /// <see cref="NotifyCollectionChangedAction.Remove"/> in one pass, so a range costs one de-realization instead of one
    /// per item. <see cref="UI.Controls.TreeView"/> uses it for the rows an expand or collapse adds or removes.
    /// </remarks>
    /// <typeparam name="T">The item type.</typeparam>
    internal sealed class RangeObservableCollection<T> : ObservableCollection<T>
    {
        /// <summary>
        /// Inserts <paramref name="range"/> at <paramref name="index"/> and raises one <see cref="NotifyCollectionChangedAction.Add"/>.
        /// </summary>
        /// <param name="index">Where the first item goes.</param>
        /// <param name="range">The items, in order. An empty range changes nothing and raises nothing.</param>
        public void InsertRange(int index, IReadOnlyList<T> range)
        {
            if (range.Count == 0)
                return;

            CheckReentrancy();
            var added = new List<T>(range);
            if (Items is List<T> list)
            {
                list.InsertRange(index, added);
            }
            else
            {
                for (int i = 0; i < added.Count; i++)
                    Items.Insert(index + i, added[i]);
            }

            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, (IList)added, index));
        }

        /// <summary>
        /// Removes <paramref name="count"/> items from <paramref name="index"/> and raises one <see cref="NotifyCollectionChangedAction.Remove"/>.
        /// </summary>
        /// <param name="index">The first item to remove.</param>
        /// <param name="count">How many to remove. Zero changes nothing and raises nothing.</param>
        /// <returns>The removed items, in order.</returns>
        public List<T> RemoveRange(int index, int count)
        {
            if (count == 0)
                return [];

            CheckReentrancy();
            List<T> removed;
            if (Items is List<T> list)
            {
                removed = list.GetRange(index, count);
                list.RemoveRange(index, count);
            }
            else
            {
                removed = new List<T>(count);
                for (int i = 0; i < count; i++)
                {
                    removed.Add(Items[index]);
                    Items.RemoveAt(index);
                }
            }

            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, (IList)removed, index));
            return removed;
        }

        /// <summary>
        /// Replaces the whole content with <paramref name="items"/> and raises one <see cref="NotifyCollectionChangedAction.Reset"/>.
        /// </summary>
        /// <param name="items">The new content.</param>
        public void ResetTo(IEnumerable<T> items)
        {
            CheckReentrancy();
            Items.Clear();
            foreach (T item in items)
                Items.Add(item);

            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }

        private void Raise(NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(e);
        }
    }
}
