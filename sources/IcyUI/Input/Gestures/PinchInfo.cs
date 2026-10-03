// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
using System.Numerics;

namespace Icy.Input.Gestures
{
    /// <summary>
    /// Describes one step of a two-finger pinch.
    /// </summary>
    /// <param name="Center">The midpoint between the two fingers, in physical pixels.</param>
    /// <param name="Scale">The finger distance relative to the distance when the pinch started (<c>2</c> = twice as far apart).</param>
    /// <param name="ScaleDelta">The scale change since the previous pinch event, as a factor.</param>
    /// <param name="CenterDelta">The movement of <paramref name="Center"/> since the previous pinch event, in physical pixels.</param>
    public readonly record struct PinchInfo(Vector2 Center, float Scale, float ScaleDelta, Vector2 CenterDelta);
}
