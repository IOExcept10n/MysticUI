// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design
{
    /// <summary>
    /// Identifies one element of a <see cref="DesignDocument"/>, stably across edits.
    /// </summary>
    /// <remarks>
    /// Every edit re-parses the document into new syntax nodes; a <see cref="NodeId"/> is what stays the same for an
    /// element that survived the edit, including one that was moved. An element an edit inserted gets a new id.
    /// </remarks>
    /// <param name="Value">The id's raw value, unique within its document.</param>
    public readonly record struct NodeId(int Value)
    {
        /// <inheritdoc/>
        public override string ToString() => $"#{Value}";
    }
}
