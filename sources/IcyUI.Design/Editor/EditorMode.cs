// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor
{
    /// <summary>
    /// What pointer and keyboard input does while an <see cref="EditorSession"/> is attached.
    /// </summary>
    public enum EditorMode
    {
        /// <summary>
        /// The editor owns the input: clicks select, drags move and resize, and the editor's key bindings are active.
        /// The page's controls see no input.
        /// </summary>
        Edit,

        /// <summary>
        /// The page and the game behave normally, so the developer can open popups, switch tabs and so on; the
        /// selection stays visible, and only the mode toggle is bound.
        /// </summary>
        Interact,
    }
}
