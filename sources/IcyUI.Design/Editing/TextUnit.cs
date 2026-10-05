// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.Design.Text;

namespace Icy.Design.Editing
{
    /// <summary>
    /// One independently applied part of a text change: the action, the element it leaves out of sync if it fails
    /// (<see langword="null"/> when there's nothing to rebuild later, as for a removal), and where to report a failure.
    /// </summary>
    internal readonly record struct TextUnit(MirrorAction Action, NodeId? Node, TextSpan Span);
}
