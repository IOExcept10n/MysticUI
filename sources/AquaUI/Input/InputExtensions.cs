using System.Runtime.CompilerServices;
using AquaUI.Input.Devices;

namespace AquaUI.Input
{
    /// <summary>
    /// Provides utility extensions for the input system.
    /// </summary>
    internal static class InputExtensions
    {
        /// <summary>
        /// Determines whether the key is modifier.
        /// </summary>
        /// <param name="key">The key to test.</param>
        /// <returns><see langword="true"/> if the key is the modifier key; otherwise <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsModifier(this Keys key) => key switch
        {
            Keys.LeftAlt or Keys.RightAlt or Keys.LeftCtrl or Keys.RightCtrl or Keys.LeftShift or Keys.RightShift or Keys.LeftWin or Keys.RightWin => true,
            _ => false,
        };

        /// <summary>
        /// Gets the corresponding <see cref="ModifierKeys"/> value for this key.
        /// </summary>
        /// <param name="key">Keys to get the value for.</param>
        /// <returns>The corresponding <see cref="ModifierKeys"/> value or <see cref="ModifierKeys.None"/> if the key is not modifier.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ModifierKeys ToModifier(this Keys key) => key switch
        {
            Keys.LeftAlt or Keys.RightAlt => ModifierKeys.Alt,
            Keys.LeftCtrl or Keys.RightCtrl => ModifierKeys.Ctrl,
            Keys.LeftShift or Keys.RightShift => ModifierKeys.Shift,
            Keys.LeftWin or Keys.RightWin => ModifierKeys.Win,
            _ => ModifierKeys.None,
        };

        /// <summary>
        /// Determines whether the key cannot be combined with the <see cref="ModifierKeys.Alt"/> for navigation.
        /// </summary>
        /// <param name="key">The key to test.</param>
        /// <returns><see langword="true"/> if the key cannot be combined with <see cref="ModifierKeys.Alt"/>; otherwise <see langword="false"/>.</returns>
        public static bool IsWrongAltKey(this Keys key) => key switch
        {
            Keys.Apps or Keys.CapsLock or Keys.Delete or Keys.Escape or Keys.Pause or Keys.PrintScreen or Keys.Scroll or Keys.Tab => true,
            _ => false,
        };
    }
}
