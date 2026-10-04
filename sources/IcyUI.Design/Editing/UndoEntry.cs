// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// One undo or redo step as the user sees it: an edit, a transaction, or a run of coalesced edits.
    /// </summary>
    internal sealed class UndoEntry(string description)
    {
        public string Description { get; } = description;

        /// <summary>
        /// Gets the items, in the order their edits were made. Replaying goes through them backwards.
        /// </summary>
        public List<UndoItem> Items { get; } = [];

        /// <summary>
        /// Gets the attribute a run of coalesced edits targets, or <see langword="null"/> for any other entry.
        /// </summary>
        public (NodeId Node, string Name)? CoalesceKey { get; init; }

        /// <summary>
        /// Gets or sets when the entry last grew.
        /// </summary>
        public DateTimeOffset LastEdit { get; set; }
    }
}
