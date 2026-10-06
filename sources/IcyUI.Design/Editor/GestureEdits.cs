// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Editor.Placement;

namespace Icy.Design.Editor
{
    /// <summary>
    /// Runs one gesture's edits as one undo step: a transaction that is undone as a whole when any edit fails.
    /// </summary>
    internal sealed class GestureEdits : IDisposable
    {
        private readonly DesignDocument document;
        private readonly IDisposable transaction;
        private readonly Dictionary<string, string?> applied = new(StringComparer.Ordinal);
        private bool edited;
        private bool closed;

        public GestureEdits(DesignDocument document, string description)
        {
            this.document = document;
            transaction = document.Editor.BeginTransaction(description);
        }

        /// <summary>
        /// Applies each edit whose value differs from what this gesture last wrote for that attribute.
        /// </summary>
        /// <returns>The first failure, or a success.</returns>
        public EditResult Apply(NodeId node, IEnumerable<AttributeEdit> edits)
        {
            foreach (AttributeEdit edit in edits)
            {
                if (applied.TryGetValue(edit.Name, out string? last) && last == edit.Value)
                    continue;

                EditResult result = edit.Value is { } value
                    ? document.Editor.SetAttribute(node, edit.Name, value)
                    : document.Editor.ClearAttribute(node, edit.Name);
                if (!result.Succeeded)
                    return result;

                applied[edit.Name] = edit.Value;
                edited = true;
            }

            return EditResult.Success();
        }

        /// <summary>
        /// Runs a structural edit inside the gesture.
        /// </summary>
        public EditResult Run(Func<EditResult> edit)
        {
            EditResult result = edit();
            if (result.Succeeded)
                edited = true;
            return result;
        }

        /// <summary>
        /// Closes the transaction, keeping its edits as one undo step.
        /// </summary>
        public void Commit()
        {
            if (!closed)
            {
                closed = true;
                transaction.Dispose();
            }
        }

        /// <summary>
        /// Closes the transaction and undoes it, leaving no undo entry.
        /// </summary>
        public void Rollback()
        {
            if (closed)
                return;

            closed = true;
            transaction.Dispose();
            if (edited)
            {
                document.Editor.UndoStack.Undo();
                document.Editor.UndoStack.DropRedo();
            }
        }

        public void Dispose() => Commit();
    }
}
