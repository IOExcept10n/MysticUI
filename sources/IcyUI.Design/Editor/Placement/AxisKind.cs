// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// How an element is positioned on one axis.
    /// </summary>
    public enum AxisKind
    {
        /// <summary>Aligned to the start (left or top).</summary>
        Start,

        /// <summary>Aligned to the end (right or bottom).</summary>
        End,

        /// <summary>Centered, or stretched with an explicit size.</summary>
        Centered,

        /// <summary>Stretched with no explicit size: it fills the slot between its margins.</summary>
        Stretched,
    }
}
