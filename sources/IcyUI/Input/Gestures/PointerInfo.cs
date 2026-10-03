// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Describes a pointer press, release or hold.
    /// </summary>
    /// <param name="Kind">The pointer that produced the gesture.</param>
    /// <param name="Position">The pointer position, in physical pixels.</param>
    public readonly record struct PointerInfo(PointerKind Kind, Point Position);
}
