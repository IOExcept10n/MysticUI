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
        private readonly Dictionary<string, string?> originals = new(StringComparer.Ordinal);
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

                if (!originals.ContainsKey(edit.Name))
                    originals[edit.Name] = document.GetNode(node)?.FindAttribute(edit.Name)?.Value;

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
        /// Applies <paramref name="edits"/> as the gesture's whole state: an attribute this gesture wrote earlier but
        /// <paramref name="edits"/> no longer mentions gets its value from before the gesture back.
        /// </summary>
        /// <returns>The first failure, or a success.</returns>
        public EditResult ApplyExactly(NodeId node, IReadOnlyList<AttributeEdit> edits)
        {
            EditResult result = Apply(node, edits);
            if (!result.Succeeded)
                return result;

            var restores = new List<AttributeEdit>();
            foreach (string name in applied.Keys)
            {
                if (!edits.Any(x => string.Equals(x.Name, name, StringComparison.Ordinal)))
                    restores.Add(new AttributeEdit(name, originals[name]));
            }

            return Apply(node, restores);
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
