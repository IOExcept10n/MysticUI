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
        /// Gets or sets how close together repeated sets of the same attribute must be to merge into one undo step,
        /// as during a drag. 500 ms by default.
        /// </summary>
        public TimeSpan CoalesceWindow { get; set; } = TimeSpan.FromMilliseconds(500);

        internal TimeProvider Clock { get; set; } = TimeProvider.System;

        /// <summary>
        /// Undoes the latest step.
        /// </summary>
        /// <returns>The outcome; a failure when there is nothing to undo.</returns>
        /// <exception cref="InvalidOperationException">A transaction is open.</exception>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        public EditResult Undo()
        {
            document.ThrowIfDisposed();
            return Replay(undo, redo);
        }

        /// <summary>
        /// Redoes the latest undone step.
        /// </summary>
        /// <returns>The outcome; a failure when there is nothing to redo.</returns>
        /// <exception cref="InvalidOperationException">A transaction is open.</exception>
        /// <exception cref="ObjectDisposedException">The document's session was disposed.</exception>
        public EditResult Redo()
        {
            document.ThrowIfDisposed();
            return Replay(redo, undo);
        }

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

        internal void RecordText(string previousText, string description)
        {
            redo.Clear();
            if (transaction != null)
            {
                transaction.Items.Add(UndoItem.ForText(previousText));
                return;
            }

            var entry = new UndoEntry(description);
            entry.Items.Add(UndoItem.ForText(previousText));
            undo.Add(entry);
            Changed?.Invoke(this, EventArgs.Empty);
        }

        internal void RecordCoalesced(NodeId node, string name, string originalRaw, string description)
        {
            (NodeId, string) key = (node, name);
            DateTimeOffset now = Clock.GetUtcNow();
            redo.Clear();

            UndoEntry? target = transaction;
            if (target == null && undo.Count > 0 && undo[^1].CoalesceKey == key && now - undo[^1].LastEdit <= CoalesceWindow)
                target = undo[^1];

            if (target == null)
            {
                target = new UndoEntry(description) { CoalesceKey = key };
                undo.Add(target);
            }

            target.LastEdit = now;

            // The first edit of a run knows the value to go back to; later ones in the same run add nothing. A run
            // ends at a recorded step: its inverse has fixed offsets that assume the value as it was then, so a later
            // edit of the same attribute needs its own item, undone before that step.
            int last = target.Items.FindLastIndex(x => x.CoalescedKey == key);
            bool stepSince = last >= 0 && target.Items.Skip(last + 1).Any(x => x.CoalescedKey == null);
            if (last < 0 || stepSince)
                target.Items.Add(UndoItem.ForCoalesced(node, name, originalRaw));

            Changed?.Invoke(this, EventArgs.Empty);
        }

        private EditResult Replay(List<UndoEntry> from, List<UndoEntry> to)
        {
            if (transactionDepth > 0)
                throw new InvalidOperationException("Undo and redo aren't possible while a transaction is open.");
            if (from.Count == 0)
                return EditResult.Failure(default, "There is nothing to replay.");

            document.VerifyAccess();
            UndoEntry entry = from[^1];
            from.RemoveAt(from.Count - 1);

            var replayed = new UndoEntry(entry.Description);
            for (int i = entry.Items.Count - 1; i >= 0; i--)
            {
                EditResult result = ReplayItem(entry.Items[i], out UndoItem? undone);
                if (!result.Succeeded)
                {
                    RollBack(entry, replayed, from);
                    return result;
                }

                replayed.Items.Add(undone!);
            }

            to.Add(replayed);
            Changed?.Invoke(this, EventArgs.Empty);
            return EditResult.Success();
        }

        /// <summary>
        /// Replays one item and gives back the item that would undo the replay.
        /// </summary>
        private EditResult ReplayItem(UndoItem item, out UndoItem? undone)
        {
            undone = null;
            if (item.Text is { } previousText)
            {
                // Applying text never fails: the text always wins, and live gaps show in LiveErrors.
                string current = document.Text;
                document.ApplyTextCore(previousText);
                undone = UndoItem.ForText(current);
                return EditResult.Success();
            }

            EditResult result;
            EditStep? inverse = null;
            try
            {
                result = document.Apply(item.CreateStep(document), out inverse);
            }
            catch (InvalidOperationException ex)
            {
                // Building the step can find its target gone; that's a failed replay, not a crash.
                result = EditResult.Failure(default, ex.Message);
            }

            if (result.Succeeded)
                undone = UndoItem.ForStep(inverse!);
            return result;
        }

        /// <summary>
        /// Puts back what a failed replay already did, so an entry is never left half-applied. When that works the entry
        /// stays where it was and the history is intact; when it doesn't, the history no longer matches the text and is
        /// cleared, since replaying any of it later would corrupt the document.
        /// </summary>
        private void RollBack(UndoEntry entry, UndoEntry replayed, List<UndoEntry> from)
        {
            bool restored = true;
            for (int j = replayed.Items.Count - 1; j >= 0 && restored; j--)
                restored = ReplayItem(replayed.Items[j], out _).Succeeded;

            if (restored)
            {
                from.Add(entry);
            }
            else
            {
                undo.Clear();
                redo.Clear();
            }

            Changed?.Invoke(this, EventArgs.Empty);
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
