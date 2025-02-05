// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace Icy.Input.Devices
{
    /// <summary>
    /// Represents the flags set for the mouse buttons.
    /// </summary>
    [Flags]
    public enum MouseButtons : byte
    {
        /// <summary>
        /// No buttons.
        /// </summary>
        None = 0,

        /// <summary>
        /// Left button.
        /// </summary>
        LeftButton = 1,

        /// <summary>
        /// Middle button.
        /// </summary>
        MiddleButton = 2,

        /// <summary>
        /// Right button.
        /// </summary>
        RightButton = 4,

        /// <summary>
        /// First extended button.
        /// </summary>
        ExtendedButton1 = 8,

        /// <summary>
        /// Second extended button.
        /// </summary>
        ExtendedButton2 = 16,
    }
}
