// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Collections.Generic;
using System.Linq;
using System.Collections.Specialized;
using Icy.Data;
using Xunit;

namespace Icy.Tests.Data
{
    public class RangeObservableCollectionTests
    {
        [Fact]
        public void InsertRange_RaisesOneAddForTheWholeRange()
        {
            var collection = new RangeObservableCollection<int> { 1, 4 };
            var events = Record(collection);

            collection.InsertRange(1, [2, 3]);

            Assert.Equal([1, 2, 3, 4], collection);
            NotifyCollectionChangedEventArgs e = Assert.Single(events);
            Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
            Assert.Equal(1, e.NewStartingIndex);
            Assert.Equal([2, 3], e.NewItems!.Cast<int>());
        }

        [Fact]
        public void RemoveRange_RaisesOneRemove_AndReturnsTheRemovedItems()
        {
            var collection = new RangeObservableCollection<int> { 1, 2, 3, 4 };
            var events = Record(collection);

            List<int> removed = collection.RemoveRange(1, 2);

            Assert.Equal([2, 3], removed);
            Assert.Equal([1, 4], collection);
            NotifyCollectionChangedEventArgs e = Assert.Single(events);
            Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
            Assert.Equal(1, e.OldStartingIndex);
            Assert.Equal([2, 3], e.OldItems!.Cast<int>());
        }

        [Fact]
        public void EmptyRanges_RaiseNothing()
        {
            var collection = new RangeObservableCollection<int> { 1 };
            var events = Record(collection);

            collection.InsertRange(0, []);
            collection.RemoveRange(0, 0);

            Assert.Empty(events);
        }

        [Fact]
        public void ResetTo_ReplacesEverything_WithOneReset()
        {
            var collection = new RangeObservableCollection<int> { 1, 2 };
            var events = Record(collection);

            collection.ResetTo([7, 8, 9]);

            Assert.Equal([7, 8, 9], collection);
            Assert.Equal(NotifyCollectionChangedAction.Reset, Assert.Single(events).Action);
        }

        private static List<NotifyCollectionChangedEventArgs> Record(INotifyCollectionChanged collection)
        {
            var events = new List<NotifyCollectionChangedEventArgs>();
            collection.CollectionChanged += (_, e) => events.Add(e);
            return events;
        }
    }
}
