// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;
using Icy.Design.Text;

namespace Icy.Design
{
    /// <summary>
    /// The outcome of an edit.
    /// </summary>
    /// <remarks>
    /// An edit is all-or-nothing: when it fails, neither the text nor any live page changed.
    /// </remarks>
    public sealed class EditResult
    {
        private EditResult(bool succeeded, NodeId? node, Diagnostic? error)
        {
            Succeeded = succeeded;
            Node = node;
            Error = error;
        }

        /// <summary>
        /// Gets a value indicating whether the edit was made.
        /// </summary>
        public bool Succeeded { get; }

        /// <summary>
        /// Gets the element the edit created, such as the one <c>MarkupEditor.InsertElement</c> inserted;
        /// otherwise <see langword="null"/>.
        /// </summary>
        public NodeId? Node { get; }

        /// <summary>
        /// Gets why the edit failed, or <see langword="null"/> when it succeeded.
        /// </summary>
        public Diagnostic? Error { get; }

        /// <inheritdoc/>
        public override string ToString() => Succeeded ? "Succeeded" : $"Failed: {Error}";

        internal static EditResult Success(NodeId? node = null) => new(true, node, null);

        internal static EditResult Failure(TextSpan span, string message) => new(false, null, new Diagnostic(span, message));
    }
}
