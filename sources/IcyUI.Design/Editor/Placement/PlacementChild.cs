// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// A container's content child as a placement sees it.
    /// </summary>
    /// <param name="Index">
    /// The child's position among the container's markup content children, counted without the element being placed:
    /// the index <see cref="MarkupEditor.MoveElement"/> takes.
    /// </param>
    /// <param name="Instance">The child's live copy in the container's load scope.</param>
    public readonly record struct PlacementChild(int Index, UIElement Instance);
}
