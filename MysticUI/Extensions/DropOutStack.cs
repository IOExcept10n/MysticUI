using CommunityToolkit.Diagnostics;
using System.Collections;

namespace MysticUI.Extensions
{
    /// <summary>
    /// represents the stack with the ability to limit the stack by given capacity.
    /// </summary>
    /// <remarks>
    /// When you add more elements than the capacity allows, the first added element will be deleted from the stack.
    /// </remarks>
    /// <typeparam name="T">Type of elements in the stack.</typeparam>
    public class DropOutStack<T> : IReadOnlyCollection<T>
    {
        private readonly int capacity;
        private readonly T[] items;
        private int top = 0;
        private int count;

        /// <inheritdoc/>
        public int Count => count;

        /// <summary>
        /// Creates a new instance of the <see cref="DropOutStack{T}"/>.
        /// </summary>
        /// <param name="capacity">Capacity of the stack.</param>
        public DropOutStack(int capacity)
        {
            this.capacity = capacity;
            items = new T[capacity];
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DropOutStack{T}"/> based on existing collection.
        /// </summary>
        /// <param name="values">A collection instance to get values from.</param>
        public DropOutStack(IReadOnlyCollection<T> values)
        {
            Guard.IsNotNull(values);
            capacity = values.Count;
            items = new T[capacity];
            foreach (var element in values)
            {
                Push(element);
            }
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DropOutStack{T}"/> based on existing collection with overriding its size.
        /// </summary>
        /// <param name="values">A collection instance to get values from.</param>
        /// <param name="newCapacity">New capacity for the stack.</param>
        public DropOutStack(IReadOnlyCollection<T> values, int newCapacity)
        {
            Guard.IsNotNull(values);
            capacity = newCapacity;
            items = new T[newCapacity];
            foreach (var element in values)
            {
                Push(element);
            }
        }

        /// <summary>
        /// Adds an element to the stack.
        /// </summary>
        /// <param name="item">Item to add.</param>
        public void Push(T item)
        {
            items[top] = item;
            top = (top + 1) % items.Length;
            count++;
            if (count > capacity) count = capacity;
        }

        /// <summary>
        /// Removes an element from the stack and returns it.
        /// </summary>
        /// <returns>The last element added to the stack.</returns>
        public T Pop()
        {
            items[top] = default!;
            top = (items.Length + top - 1) % items.Length;
            Guard.IsGreaterThanOrEqualTo(count--, 0, "length");
            return items[top]!;
        }

        /// <summary>
        /// Gets the element last added to the stack without removing.
        /// </summary>
        public T Peek()
        {
            int index = (items.Length + top - 1) % items.Length;
            return items[index]!;
        }

        /// <summary>
        /// Clears the stack.
        /// </summary>
        public void Clear()
        {
            Array.Clear(items);
            top = count = 0;
        }

        /// <summary>
        /// Inline Enumerator used directly by foreach.
        /// </summary>
        /// <returns>An enumerator of this collection</returns>
        public Enumerator GetEnumerator()
        {
            return new(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        /// <summary>
        /// Represents an enumerator for the <see cref="DropOutStack{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator<T>
        {
            private readonly int length;
            private readonly int startIndex;
            private DropOutStack<T> stack;
            private int position;
            private int index;

            /// <inheritdoc/>
            public readonly T Current => stack.items[index];

            /// <inheritdoc/>
            readonly object IEnumerator.Current => stack.items[index]!;

            /// <summary>
            /// Creates a new enumerator for the <see cref="DropOutStack{T}"/>.
            /// </summary>
            /// <param name="values">A <see cref="DropOutStack{T}"/> to create enumerator for.</param>
            public Enumerator(DropOutStack<T> values)
            {
                position = 0;
                startIndex = index = values.top;
                length = values.count;
                stack = values;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                stack = null!;
            }

            /// <inheritdoc/>
            public bool MoveNext()
            {
                if (++position == length) return false;
                // Check that the collection wasn't changed since last iteration.
                Guard.IsEqualTo(length, stack.count);
                index = (index + 1) % length;
                return true;
            }

            /// <inheritdoc/>
            public void Reset()
            {
                index = startIndex;
                position = 0;
            }
        }
    }
}