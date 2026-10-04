// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editing
{
    /// <summary>
    /// Tells the re-parse that the element <paramref name="Node"/> now starts at <paramref name="NewStart"/>, so it
    /// keeps its id even though its old text was deleted.
    /// </summary>
    internal readonly record struct MoveHint(NodeId Node, int NewStart);
}
