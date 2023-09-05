using System.Collections;

namespace MysticUI.Extensions.Text
{
    internal class UndoRedoStack : IReadOnlyCollection<UndoRedoRecord>
    {
        public DropOutStack<UndoRedoRecord> Stack { get; }

        public int Count => Stack.Count;

        public UndoRedoStack(int capacity)
        {
            Stack = new(capacity);
        }

        internal UndoRedoStack(IReadOnlyCollection<UndoRedoRecord> stack, int capacity)
        {
            Stack = new(stack, capacity);
        }

        public void Clear() => Stack.Clear();

        public void Insert(int position, int length)
        {
            if (length <= 0) return;
            Stack.Push(new()
            {
                OperationType = TextEditOperationType.Insertion,
                Position = position,
                Length = length
            });
        }

        public void Delete(string text, int position, int length)
        {
            if (length <= 0) return;
            Stack.Push(new(TextEditOperationType.Deletion, text[position..(position + length)], position, length));
        }

        public void Replace(string text, int position, int length, int newLength)
        {
            if (length <= 0)
            {
                Insert(position, length);
                return;
            }
            if (newLength <= 0)
            {
                Delete(text, position, length);
                return;
            }
            Stack.Push(new(TextEditOperationType.Replacement, text[position..(position + length)], position, newLength));
        }

        public IEnumerator<UndoRedoRecord> GetEnumerator()
        {
            return Stack.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)Stack).GetEnumerator();
        }
    }
}