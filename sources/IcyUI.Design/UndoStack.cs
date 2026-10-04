// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editing;

namespace Icy.Design
{
    /// <summary>
    /// The undo and redo history of one <see cref="DesignDocument"/>'s edits.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Undoing restores the text byte for byte and mirrors the change onto the live pages, like any edit. An element
    /// that comes back after its removal is undone is built fresh; a moved element that is moved back keeps its
    /// identity.
    /// </para>
    /// <para>
    /// Group several edits into one step with <see cref="MarkupEditor.BeginTransaction"/>.
    /// </para>
    /// </remarks>
    public sealed class UndoStack
    {
        private readonly DesignDocument document;
        private readonly List<UndoEntry> undo = [];
        private readonly List<UndoEntry> redo = [];
        private UndoEntry? transaction;
        private int transactionDepth;

        internal UndoStack(DesignDocument document)
        {
            this.document = document;
        }

        /// <summary>
        /// Occurs when the history changed: an edit was recorded, or a step was undone or redone.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets a value indicating whether there is a step to undo.
        /// </summary>
        public bool CanUndo => undo.Count > 0;

        /// <summary>
        /// Gets a value indicating whether there is a step to redo.
        /// </summary>
        public bool CanRedo => redo.Count > 0;

        /// <summary>
        /// Gets a description of the step <see cref="Undo"/> would undo, or <see langword="null"/>.
        /// </summary>
        public string? UndoDescription => undo.Count > 0 ? undo[^1].Description : null;

        /// <summary>
        /// Gets a description of the step <see cref="Redo"/> would redo, or <see langword="null"/>.
        /// </summary>
        public string? RedoDescription => redo.Count > 0 ? redo[^1].Description : null;

        /// <summary>
        /// Undoes the latest step.
        /// </summary>
        /// <returns>The outcome; a failure when there is nothing to undo.</returns>
        /// <exception cref="InvalidOperationException">A transaction is open.</exception>
        public EditResult Undo() => Replay(undo, redo);

        /// <summary>
        /// Redoes the latest undone step.
        /// </summary>
        /// <returns>The outcome; a failure when there is nothing to redo.</returns>
        /// <exception cref="InvalidOperationException">A transaction is open.</exception>
        public EditResult Redo() => Replay(redo, undo);

        /// <summary>
        /// Forgets the whole history.
        /// </summary>
        public void Clear()
        {
            undo.Clear();
            redo.Clear();
            Changed?.Invoke(this, EventArgs.Empty);
        }

        internal IDisposable BeginTransaction(string description)
        {
            if (transactionDepth++ == 0)
                transaction = new UndoEntry(description);
            return new TransactionScope(this);
        }

        internal void Record(EditStep inverse, string description)
        {
            redo.Clear();
            if (transaction != null)
            {
                transaction.Items.Add(UndoItem.ForStep(inverse));
                return;
            }

            var entry = new UndoEntry(description);
            entry.Items.Add(UndoItem.ForStep(inverse));
            undo.Add(entry);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        private EditResult Replay(List<UndoEntry> from, List<UndoEntry> to)
        {
            if (transactionDepth > 0)
                throw new InvalidOperationException("Undo and redo aren't possible while a transaction is open.");
            if (from.Count == 0)
                return EditResult.Failure(default, "There is nothing to replay.");

            UndoEntry entry = from[^1];
            from.RemoveAt(from.Count - 1);

            var replayed = new UndoEntry(entry.Description);
            for (int i = entry.Items.Count - 1; i >= 0; i--)
            {
                EditResult result = document.Apply(entry.Items[i].CreateStep(document), out EditStep? inverse);
                if (!result.Succeeded)
                {
                    // The history no longer matches the text; replaying any of it later would corrupt the document.
                    undo.Clear();
                    redo.Clear();
                    Changed?.Invoke(this, EventArgs.Empty);
                    return result;
                }

                replayed.Items.Add(UndoItem.ForStep(inverse!));
            }

            to.Add(replayed);
            Changed?.Invoke(this, EventArgs.Empty);
            return EditResult.Success();
        }

        private void EndTransaction()
        {
            if (--transactionDepth > 0)
                return;

            UndoEntry entry = transaction!;
            transaction = null;
            if (entry.Items.Count > 0)
            {
                undo.Add(entry);
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }

        private sealed class TransactionScope(UndoStack owner) : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                owner.EndTransaction();
            }
        }
    }
}
