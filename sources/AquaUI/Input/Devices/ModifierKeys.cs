// Copyright (c) IOExcept10n (https://github.com/IOExcept10n)
// Distributed under MIT license. See LICENSE.md file in the project root for more information
namespace AquaUI.Input.Devices
{
    /// <summary>
    /// Defines an enumeration for the keyboard modifier keys.
    /// </summary>
    public enum ModifierKeys
    {
        /// <summary>
        /// No modifier keys.
        /// </summary>
        None = 0,

        /// <summary>
        /// The left or right <see langword="Shift"/> modifier key.
        /// </summary>
        Shift = 1 << 0,

        /// <summary>
        /// The left or right <see langword="Ctrl"/> (Control) modifier key.
        /// </summary>
        Ctrl = 1 << 1,

        /// <summary>
        /// The left or right <see langword="Alt"/> modifier key.
        /// </summary>
        Alt = 1 << 2,

        /// <summary>
        /// The left or right <see langword="Win"/> (Windows) modifier key.
        /// </summary>
        Win = 1 << 3,
    }
}