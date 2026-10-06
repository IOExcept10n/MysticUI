// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Design.Editor.Placement
{
    /// <summary>
    /// The result of resizing one axis.
    /// </summary>
    /// <param name="Size">The new size, or <see langword="null"/> when the size isn't written.</param>
    /// <param name="MarginStart">The new start margin (left or top).</param>
    /// <param name="MarginEnd">The new end margin (right or bottom).</param>
    public readonly record struct AxisResize(int? Size, int MarginStart, int MarginEnd);
}
