// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design
{
    /// <summary>
    /// Describes a change to a <see cref="DesignDocument"/>'s text.
    /// </summary>
    /// <param name="changes">The changes, expressed against the text as it was right before this change.</param>
    /// <param name="version">The document's version after the change.</param>
    public sealed class DocumentChangedEventArgs(TextChangeSet changes, int version) : EventArgs
    {
        /// <summary>
        /// Gets the changes, expressed against the text as it was right before this change. Applying them to a copy
        /// of the previous text gives the new <see cref="DesignDocument.Text"/>.
        /// </summary>
        public TextChangeSet Changes { get; } = changes;

        /// <summary>
        /// Gets the document's version after the change.
        /// </summary>
        public int Version { get; } = version;
    }
}
