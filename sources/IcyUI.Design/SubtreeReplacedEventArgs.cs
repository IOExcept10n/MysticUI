// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design
{
    /// <summary>
    /// Describes a live subtree an edit had to rebuild instead of updating in place.
    /// </summary>
    /// <remarks>
    /// Code that holds references to elements inside <see cref="OldElement"/> should drop them: they no longer belong
    /// to the page.
    /// </remarks>
    /// <param name="node">The markup element the subtree was built from.</param>
    /// <param name="oldElement">The subtree that was removed from the page.</param>
    /// <param name="newElement">The subtree that took its place.</param>
    public sealed class SubtreeReplacedEventArgs(NodeId node, UIElement oldElement, UIElement newElement) : EventArgs
    {
        /// <summary>
        /// Gets the markup element the subtree was built from.
        /// </summary>
        public NodeId Node { get; } = node;

        /// <summary>
        /// Gets the subtree that was removed from the page.
        /// </summary>
        public UIElement OldElement { get; } = oldElement;

        /// <summary>
        /// Gets the subtree that took its place.
        /// </summary>
        public UIElement NewElement { get; } = newElement;
    }
}
