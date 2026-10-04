// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Tells the re-parse that the element starting at <paramref name="Start"/> is a removed element coming back, so it
    /// and its descendants get their old ids (<paramref name="Ids"/>, in document order) instead of new ones, and
    /// older history entries that name them keep working.
    /// </summary>
    internal readonly record struct RestoreHint(int Start, IReadOnlyList<NodeId> Ids);
}
