// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.UI.Controls
{
    /// <summary>
    /// Specifies along which axes a <see cref="ScrollViewer"/> pans with touch and the middle mouse button.
    /// </summary>
    public enum PanningMode
    {
        /// <summary>
        /// Pans along the axes whose content extent exceeds the viewport.
        /// </summary>
        Auto,

        /// <summary>
        /// Never pans; drags go to the next scroll area out.
        /// </summary>
        None,

        /// <summary>
        /// Pans vertically only.
        /// </summary>
        Vertical,

        /// <summary>
        /// Pans horizontally only.
        /// </summary>
        Horizontal,

        /// <summary>
        /// Pans along both axes.
        /// </summary>
        Both,
    }
}
