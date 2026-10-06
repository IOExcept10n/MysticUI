// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using Icy.UI;

namespace Icy.Design.Editor
{
    /// <summary>
    /// The element an <see cref="EditorSession"/> has selected.
    /// </summary>
    /// <param name="Document">The design document the element's markup belongs to.</param>
    /// <param name="Node">The element's node in <paramref name="Document"/>.</param>
    /// <param name="Instance">
    /// The live copy that was picked. When the same file is loaded more than once, edits still apply to every copy;
    /// this is the one adorners follow.
    /// </param>
    public sealed record EditorSelection(DesignDocument Document, NodeId Node, UIElement Instance);
}
