// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// One thing to replay when an <see cref="UndoEntry"/> is undone or redone: a recorded step, or the value an
    /// attribute had before a run of coalesced edits.
    /// </summary>
    internal sealed class UndoItem
    {
        private readonly EditStep? step;
        private readonly NodeId node;
        private readonly string? name;
        private readonly string? originalRaw;

        private UndoItem(EditStep? step, NodeId node, string? name, string? originalRaw)
        {
            this.step = step;
            this.node = node;
            this.name = name;
            this.originalRaw = originalRaw;
        }

        public (NodeId Node, string Name)? CoalescedKey => name != null ? (node, name) : null;

        public static UndoItem ForStep(EditStep step) => new(step, default, null, null);

        public static UndoItem ForCoalesced(NodeId node, string name, string originalRaw) => new(null, node, name, originalRaw);

        public EditStep CreateStep(DesignDocument document)
        {
            if (step != null)
                return step;

            // GetNode flushes pending values, so the span is the attribute's current one.
            var attribute = document.GetNode(node)?.FindAttribute(name!)
                ?? throw new InvalidOperationException($"The attribute '{name}' of element {node} no longer exists.");
            return new EditStep(
                new TextChangeSet([new TextChange(attribute.ValueSpan, originalRaw!)]),
                [new AttributeChangedAction(node, name!)],
                $"Set {name}");
        }
    }
}
