// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// The edges a resize handle moves. Corner handles combine two edges.
    /// </summary>
    [Flags]
    public enum ResizeHandle
    {
        /// <summary>No edge.</summary>
        None = 0,

        /// <summary>The left edge.</summary>
        Left = 1,

        /// <summary>The top edge.</summary>
        Top = 2,

        /// <summary>The right edge.</summary>
        Right = 4,

        /// <summary>The bottom edge.</summary>
        Bottom = 8,

        /// <summary>The top-left corner.</summary>
        TopLeft = Top | Left,

        /// <summary>The top-right corner.</summary>
        TopRight = Top | Right,

        /// <summary>The bottom-left corner.</summary>
        BottomLeft = Bottom | Left,

        /// <summary>The bottom-right corner.</summary>
        BottomRight = Bottom | Right,
    }
}
