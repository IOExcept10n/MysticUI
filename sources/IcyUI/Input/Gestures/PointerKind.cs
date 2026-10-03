// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Gestures
{
    /// <summary>
    /// Specifies which pointer produced a gesture.
    /// </summary>
    public enum PointerKind
    {
        /// <summary>
        /// A touch contact (finger).
        /// </summary>
        Touch,

        /// <summary>
        /// The left mouse button. Taps, holds and drags like a finger.
        /// </summary>
        MouseLeft,

        /// <summary>
        /// The right mouse button. Its release raises <see cref="IGestureEvents.Held"/> (a context click). It never taps or drags.
        /// </summary>
        MouseRight,

        /// <summary>
        /// The middle mouse button. It only drags, and those drags are meant for panning content rather than for controls.
        /// </summary>
        MouseMiddle,
    }
}
