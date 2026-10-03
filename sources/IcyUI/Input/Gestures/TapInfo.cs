// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Drawing;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Describes a tap.
    /// </summary>
    /// <param name="Kind">The pointer that tapped.</param>
    /// <param name="Position">The tap position, in physical pixels.</param>
    /// <param name="Count">
    /// The number of consecutive taps: <c>2</c> for a double tap. A tap continues the sequence when it lands within
    /// <see cref="GestureSettings.MultiTapDelay"/> and <see cref="GestureSettings.Slop"/> of the previous one.
    /// </param>
    public readonly record struct TapInfo(PointerKind Kind, Point Position, int Count);
}
