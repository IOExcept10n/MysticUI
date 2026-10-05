// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Syntax;

namespace Icy.Design.Editing
{
    /// <summary>
    /// The structural difference between two markup trees, element by element.
    /// </summary>
    internal sealed class TreeDiff
    {
        /// <summary>
        /// Gets or sets why the root can't be followed in place (its type or <c>x:Class</c> changed), or
        /// <see langword="null"/>. When set, nothing else is filled in.
        /// </summary>
        public string? RootProblem { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether anything under a property element changed. Those parts are opaque:
        /// they can only be shown by reloading the page.
        /// </summary>
        public bool OpaqueChanged { get; set; }

        /// <summary>
        /// Gets every new element that corresponds to an old one (matched, moved, replaced, or paired structurally
        /// inside a property element), mapped to that old element. New elements missing here are new.
        /// </summary>
        public Dictionary<ElementSyntax, ElementSyntax> Pairs { get; } = [];

        /// <summary>
        /// Gets the old elements that are gone, topmost only: their descendants go with them.
        /// </summary>
        public List<ElementSyntax> Removed { get; } = [];

        /// <summary>
        /// Gets the elements, matched by <c>x:Name</c>, that sit somewhere else now.
        /// </summary>
        public List<(ElementSyntax Old, ElementSyntax New)> Moved { get; } = [];

        /// <summary>
        /// Gets the elements that correspond but can't be updated in place (type, own text or namespace declarations
        /// changed), so they're rebuilt with their id kept.
        /// </summary>
        public List<(ElementSyntax Old, ElementSyntax New)> Replaced { get; } = [];

        /// <summary>
        /// Gets the new elements with no old counterpart, topmost only: their descendants come with them.
        /// </summary>
        public List<ElementSyntax> Inserted { get; } = [];

        /// <summary>
        /// Gets each attribute added, removed or changed on a corresponding element, named as written in the new tree
        /// (or the old one, for a removal), with the new element it belongs to.
        /// </summary>
        public List<(ElementSyntax Element, string Name)> ChangedAttributes { get; } = [];

        /// <summary>
        /// Gets a value indicating whether nothing live has to change.
        /// </summary>
        public bool IsEmpty =>
            RootProblem == null && !OpaqueChanged && Removed.Count == 0 && Moved.Count == 0
            && Replaced.Count == 0 && Inserted.Count == 0 && ChangedAttributes.Count == 0;
    }
}
